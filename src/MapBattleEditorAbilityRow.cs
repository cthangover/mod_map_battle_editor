using System.Text.Json.Nodes;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Tools.Common;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Maps a local ability to an existing formula action and inclusive Manhattan range.</summary>
    public partial class MapBattleEditorAbilityRow : MapBattleEditorRow
    {
        private OptionButton action;
        private SpinBox minimum;
        private SpinBox maximum;

        /// <summary>Borrows one raw ability object, preserving provider fields outside this form.</summary>
        public MapBattleEditorAbilityRow(MapBattleEditorWindow owner, MapBattleEditorPanel panel, JsonObject data)
            : base(owner, panel, data)
        {
        }

        /// <summary>Builds reference/range controls; AP and formula remain owned by the selected action.</summary>
        protected override void Construct()
        {
            Name = "Ability_" + Data["Id"];
            BuildColumn();
            Identity();
            action = Choice("ActionId", "action", Entities.Collect<ActionCharacterInfo>(required: false).Keys);
            action.ItemSelected += OnAction;
            minimum = Number("MinRange", "min_range", 1, 1, 256);
            maximum = Number("MaxRange", "max_range", 1, 1, 256);
            minimum.ValueChanged += OnMinimum;
            maximum.ValueChanged += OnMaximum;
            Column.AddChild(ToolUi.Hint(ToolUi.Tr("map_battle_editor/action_hint")));
            var remove = ToolUi.Button("map_battle_editor/remove_ability", OnRemove);
            remove.Name = "Remove";
            Column.AddChild(remove);
            Loading = false;
        }

        private void OnAction(long index)
        {
            Change("ActionId", JsonValue.Create(action.GetItemText((int)index)));
        }

        private void OnMinimum(double value)
        {
            Change("MinRange", JsonValue.Create((int)value));
        }

        private void OnMaximum(double value)
        {
            Change("MaxRange", JsonValue.Create((int)value));
        }

        private void OnRemove()
        {
            Remove("Abilities");
        }
    }
}
