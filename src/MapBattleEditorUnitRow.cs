using System.Text.Json.Nodes;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Tools.Common;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Edits one instance's side, character binding, initiative, budgets and map coordinates.</summary>
    public partial class MapBattleEditorUnitRow : MapBattleEditorRow
    {
        private OptionButton template;
        private CheckBox player;
        private LineEdit roster;
        private SpinBox x;
        private SpinBox y;
        private SpinBox speed;
        private SpinBox ap;
        private SpinBox movement;
        private VBoxContainer actions;

        /// <summary>True for unfinished out-of-map numeric input, even while the previous valid JSON cell is retained.</summary>
        public bool InvalidInput => Panel.MapWidth < 1 || x.Value < 0 || y.Value < 0
            || x.Value >= Panel.MapWidth || y.Value >= Panel.MapHeight;

        /// <summary>Borrows one existing item; mutations retain its unrelated provider fields.</summary>
        public MapBattleEditorUnitRow(MapBattleEditorWindow owner, MapBattleEditorPanel panel, JsonObject data)
            : base(owner, panel, data)
        {
        }

        /// <summary>Builds participant controls with initialized signals suppressed.</summary>
        protected override void Construct()
        {
            Name = "Unit_" + Data["Id"];
            BuildColumn();
            Identity();
            template = Choice("Template", "template", Entities.Collect<CharacterInfo>(required: false).Keys);
            template.ItemSelected += OnTemplate;
            player = ToolUi.Field(Column, "map_battle_editor/player", new CheckBox
            {
                Name = "Player",
                ButtonPressed = Data["Player"]?.GetValue<bool>() ?? false
            });
            player.Toggled += OnPlayer;
            roster = ToolUi.Field(Column, "map_battle_editor/roster", new LineEdit
            {
                Name = "RosterId",
                Text = Data["RosterId"]?.ToString() ?? string.Empty,
                PlaceholderText = ToolUi.Tr("map_battle_editor/roster_hint")
            });
            roster.TextChanged += OnRoster;
            var width = System.Math.Max(1, Panel.MapWidth);
            var cell = Data["Cell"]?.GetValue<int>() ?? 0;
            x = Number("X", "x", 0, -1, 128);
            y = Number("Y", "y", 0, -1, 128);
            x.Value = cell % width;
            y.Value = cell / width;
            x.ValueChanged += OnPosition;
            y.ValueChanged += OnPosition;
            speed = Number("Speed", "speed", 10, -1000000);
            ap = Number("ActionAllowance", "ap", 2, 1);
            movement = Number("MoveAllowance", "movement", 4);
            speed.ValueChanged += OnSpeed;
            ap.ValueChanged += OnAp;
            movement.ValueChanged += OnMovement;
            actions = new VBoxContainer { Name = "AssignedAbilities" };
            Column.AddChild(actions);
            foreach (var ability in Owner.Selected.Data["Abilities"] as JsonArray ?? new JsonArray())
            {
                var id = ability["Id"]?.ToString() ?? string.Empty;
                var check = new CheckBox
                {
                    Name = "Ability_" + id,
                    Text = id
                };
                foreach (var assigned in Data["Abilities"] as JsonArray ?? new JsonArray())
                {
                    check.ButtonPressed |= assigned.ToString() == id;
                }

                actions.AddChild(check);
                check.Toggled += OnAbilities;
            }

            var remove = ToolUi.Button("map_battle_editor/remove_unit", OnRemove);
            remove.Name = "Remove";
            Column.AddChild(remove);
            Loading = false;
        }

        private void OnTemplate(long index)
        {
            Change("Template", JsonValue.Create(template.GetItemText((int)index)));
        }

        private void OnPlayer(bool value)
        {
            Change("Player", JsonValue.Create(value));
        }

        private void OnRoster(string value)
        {
            Change("RosterId", string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value.Trim()));
        }

        private void OnPosition(double value)
        {
            if (InvalidInput)
            {
                Owner.Status("map_battle_editor/error/cell");
                return;
            }

            Change("Cell", JsonValue.Create((int)y.Value * Panel.MapWidth + (int)x.Value));
        }

        private void OnSpeed(double value)
        {
            Change("Speed", JsonValue.Create((int)value));
        }

        private void OnAp(double value)
        {
            Change("ActionAllowance", JsonValue.Create((int)value));
        }

        private void OnMovement(double value)
        {
            Change("MoveAllowance", JsonValue.Create((int)value));
        }

        private void OnAbilities(bool value)
        {
            var array = new JsonArray();
            foreach (var child in actions.GetChildren())
            {
                if (child is CheckBox check && check.ButtonPressed)
                {
                    array.Add(check.Text.ToString());
                }
            }

            Change("Abilities", array);
        }

        private void OnRemove()
        {
            Remove("Units");
        }
    }
}
