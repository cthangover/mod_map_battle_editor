using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Rpg.Model;
using Cthangover.Tools.Common;
using Cthangover.Tools.EntityEditor;
using Cthangover.Tools.RpgEditor;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Utility-only form skeleton; raw JSON stays in EntityDocument while named controls edit known fields.</summary>
    public partial class MapBattleEditorPanel : ModWidget
    {
        private readonly MapBattleEditorWindow owner;
        private readonly List<string> mods = new();
        private readonly List<string> ids = new();
        private OptionButton modPicker;
        private ItemList list;
        private VBoxContainer fields;
        private LineEdit identity;
        private OptionButton maps;
        private SpinBox reward;
        private OptionButton victory;
        private OptionButton defeat;
        private OptionButton unitPicker;
        private OptionButton paint;
        private OptionButton projection;
        private VBoxContainer units;
        private VBoxContainer abilities;
        private Label problem;
        private MapBattleEditorPreview preview;
        private bool suppress;

        /// <summary>Validation includes incomplete text fields, so Save cannot silently reuse older valid input.</summary>
        public bool InvalidInput
        {
            get
            {
                foreach (var child in units.GetChildren())
                {
                    if (child is MapBattleEditorUnitRow row && row.InvalidInput)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Selected map width for explicit row/column conversion; zero without a resolved map.</summary>
        public int MapWidth => preview.Scene?.Map.Width ?? 0;

        /// <summary>Selected map height for numeric bounds validation.</summary>
        public int MapHeight => preview.Scene?.Map.Height ?? 0;

        /// <summary>Receives the borrowed window/document owner; no native UI exists until normal widget construction.</summary>
        public MapBattleEditorPanel(MapBattleEditorWindow owner)
        {
            this.owner = owner;
        }

        /// <summary>Creates all utility controls beneath the themed ModWidget root.</summary>
        protected override void Construct()
        {
            Name = "MapBattleEditorPanel";
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var outer = new VBoxContainer();
            AddChild(outer);
            outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var tools = new HBoxContainer();
            outer.AddChild(tools);
            modPicker = new OptionButton
            {
                Name = "Destination",
                CustomMinimumSize = new Vector2(220, 0)
            };
            modPicker.ItemSelected += OnMod;
            tools.AddChild(modPicker);
            AddButton(tools, "Save", "tools/common/save", Save);
            AddButton(tools, "New", "tools/entity_editor/new", owner.NewEncounter);
            AddButton(tools, "Copy", "tools/entity_editor/duplicate", owner.CopyEncounter);
            AddButton(tools, "Delete", "tools/entity_editor/delete", owner.DeleteEncounter);
            AddButton(tools, "Undo", "tools/entity_editor/undo", owner.Undo);
            AddButton(tools, "Redo", "tools/entity_editor/redo", owner.Redo);
            AddButton(tools, "Reset", "tools/entity_editor/reset", owner.ResetInherited);
            AddButton(tools, "TestBattle", "map_battle_editor/test", owner.TestBattle);
            var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            outer.AddChild(split);
            list = new ItemList
            {
                Name = "Encounters",
                CustomMinimumSize = new Vector2(220, 0)
            };
            list.ItemSelected += OnSelected;
            split.AddChild(list);
            var scroll = new ScrollContainer
            {
                Name = "InspectorScroll",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            split.AddChild(scroll);
            fields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scroll.AddChild(fields);
            identity = ToolUi.Field(fields, "tools/entity_editor/id", new LineEdit { Name = "EncounterId" });
            identity.TextChanged += OnId;
            maps = ToolUi.Field(fields, "map_battle_editor/map", new OptionButton { Name = "Map" });
            maps.ItemSelected += OnMap;
            reward = ToolUi.Field(fields, "map_battle_editor/reward", new SpinBox
            {
                Name = "Reward",
                MinValue = 0,
                MaxValue = int.MaxValue,
                Step = 1
            });
            reward.ValueChanged += OnReward;
            victory = ToolUi.Field(fields, "map_battle_editor/victory", new OptionButton { Name = "Victory" });
            defeat = ToolUi.Field(fields, "map_battle_editor/defeat", new OptionButton { Name = "Defeat" });
            victory.ItemSelected += OnVictory;
            defeat.ItemSelected += OnDefeat;
            fields.AddChild(ToolUi.Hint(ToolUi.Tr("map_battle_editor/rules_hint")));
            var picking = new HBoxContainer();
            fields.AddChild(picking);
            unitPicker = new OptionButton
            {
                Name = "PlaceUnit",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            picking.AddChild(unitPicker);
            unitPicker.ItemSelected += OnUnitPick;
            paint = new OptionButton { Name = "PaintMode" };
            paint.AddItem(ToolUi.Tr("map_battle_editor/place"));
            paint.AddItem(ToolUi.Tr("map_battle_editor/opaque"));
            paint.AddItem(ToolUi.Tr("map_battle_editor/cover"));
            paint.AddItem(ToolUi.Tr("map_battle_editor/erase"));
            picking.AddChild(paint);
            projection = new OptionButton { Name = "Projection" };
            AddProjection("topdown");
            AddProjection("isometric");
            projection.ItemSelected += OnProjection;
            picking.AddChild(projection);
            preview = new MapBattleEditorPreview();
            fields.AddChild(preview);
            fields.AddChild(ToolUi.Hint(ToolUi.Tr("map_battle_editor/legend")));
            preview.EnsureConstructed();
            preview.Picked += OnCell;
            problem = new Label
            {
                Name = "Validation",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            fields.AddChild(problem);
            fields.AddChild(ToolUi.Section("map_battle_editor/units"));
            units = new VBoxContainer { Name = "Units" };
            fields.AddChild(units);
            AddButton(fields, "AddUnit", "map_battle_editor/add_unit", AddUnit);
            fields.AddChild(ToolUi.Section("map_battle_editor/abilities"));
            abilities = new VBoxContainer { Name = "Abilities" };
            fields.AddChild(abilities);
            AddButton(fields, "AddAbility", "map_battle_editor/add_ability", AddAbility);
            fields.AddChild(ToolUi.Hint(ToolUi.Tr("map_battle_editor/test_hint")));
        }

        private static void AddButton(Container parent, string name, string key, Action handler)
        {
            var button = ToolUi.Button(key, handler);
            button.Name = name;
            parent.AddChild(button);
        }

        /// <summary>Lists actual writable destinations; the selected owner remains the only write target.</summary>
        public void FillMods(List<string> values)
        {
            mods.AddRange(values);
            foreach (var mod in mods)
            {
                modPicker.AddItem(mod);
            }
        }

        /// <summary>Refreshes identities without rebuilding focused form controls.</summary>
        public void RefreshList()
        {
            ids.Clear();
            list.Clear();
            if (owner.Document == null)
            {
                return;
            }

            foreach (var record in owner.Document.Records)
            {
                AddRecord(record);
            }

            foreach (var record in owner.Document.Foreign)
            {
                if (!owner.Document.IsDeleted(record))
                {
                    AddRecord(record);
                }
            }
        }

        private void AddRecord(EntityRecord record)
        {
            ids.Add(record.Id);
            list.AddItem((record.IsForeign ? "↗ " : string.Empty) + record.Id);
            if (record.Id == owner.Selected?.Id)
            {
                list.Select(ids.Count - 1);
            }
        }

        /// <summary>Shows a recipe without invoking edit handlers or creating a foreign patch.</summary>
        public void ShowRecord(EntityRecord record)
        {
            suppress = true;
            fields.Visible = record != null;
            if (record != null)
            {
                modPicker.Select(mods.IndexOf(owner.ModId));
                identity.Text = record.Id;
                identity.Editable = owner.CanEditIdentity;
                maps.Clear();
                foreach (var map in Entities.Collect<RpgMap>(required: false).Keys)
                {
                    maps.AddItem(map);
                }

                SelectText(maps, record.Data["Map"]?.ToString());
                reward.Value = record.Data["RewardExp"]?.GetValue<int>() ?? 10;
                preview.ShowRecipe(record.Data);
                ReloadRows();
            }

            suppress = false;
            RefreshPreview();
        }

        /// <summary>Locks a newly saved stable identity without rebuilding focused participant controls.</summary>
        public void RefreshIdentityLock()
        {
            identity.Editable = owner.CanEditIdentity;
        }

        private static void SelectText(OptionButton control, string text)
        {
            control.Select(-1);
            for (var i = 0; i < control.ItemCount; i++)
            {
                if (control.GetItemText(i) == text)
                {
                    control.Select(i);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                control.AddItem(text);
                control.Select(control.ItemCount - 1);
            }
        }

        /// <summary>Rebuilds only structural lists after add/remove or an explicit map placement.</summary>
        public void ReloadRows()
        {
            Clear(units);
            Clear(abilities);
            if (owner.Selected == null)
            {
                return;
            }

            foreach (var item in Array("Units"))
            {
                if (item is JsonObject data)
                {
                    var row = new MapBattleEditorUnitRow(owner, this, data);
                    units.AddChild(row);
                    row.EnsureConstructed();
                }
            }

            foreach (var item in Array("Abilities"))
            {
                if (item is JsonObject data)
                {
                    var row = new MapBattleEditorAbilityRow(owner, this, data);
                    abilities.AddChild(row);
                    row.EnsureConstructed();
                }
            }

            var selected = unitPicker.Selected >= 0 ? unitPicker.GetItemText(unitPicker.Selected) : null;
            unitPicker.Clear();
            foreach (var item in Array("Units"))
            {
                unitPicker.AddItem(item["Id"]?.ToString() ?? string.Empty);
            }

            SelectText(unitPicker, selected);
            if (unitPicker.Selected < 0 && unitPicker.ItemCount > 0)
            {
                unitPicker.Select(0);
            }
        }

        private JsonArray Array(string key)
        {
            return owner.Selected.Data[key] as JsonArray ?? new JsonArray();
        }

        private static void Clear(Container container)
        {
            foreach (var child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }
        }

        /// <summary>Refreshes detached geometry, outcome choices and diagnostics after an operation.</summary>
        public void RefreshPreview()
        {
            if (owner.Selected == null)
            {
                return;
            }

            var selectedUnit = unitPicker.Selected >= 0 ? unitPicker.GetItemText(unitPicker.Selected) : null;
            preview.ShowRecipe(owner.Selected.Data, selectedUnit);
            var previous = suppress;
            suppress = true;
            FillTargets(victory, false, "VictoryTarget", "map_battle_editor/all_enemies");
            FillTargets(defeat, true, "DefeatTarget", "map_battle_editor/all_players");
            suppress = previous;
            var error = MapBattleEditorDraft.Check(owner.Selected.Data, preview.Scene);
            problem.Text = ToolUi.Tr(error ?? "map_battle_editor/valid");
            problem.TooltipText = problem.Text;
        }

        private void FillTargets(OptionButton picker, bool player, string key, string all)
        {
            picker.Clear();
            picker.AddItem(ToolUi.Tr(all));
            picker.SetItemMetadata(0, string.Empty);
            var selected = owner.Selected.Data[key]?.ToString();
            picker.Select(0);
            foreach (var item in Array("Units"))
            {
                if ((item["Player"]?.GetValue<bool>() ?? false) != player)
                {
                    continue;
                }

                var id = item["Id"]?.ToString() ?? string.Empty;
                picker.AddItem(id);
                picker.SetItemMetadata(picker.ItemCount - 1, id);
                if (id == selected)
                {
                    picker.Select(picker.ItemCount - 1);
                }
            }

            if (selected != null && picker.Selected == 0)
            {
                picker.AddItem(selected);
                picker.SetItemMetadata(picker.ItemCount - 1, selected);
                picker.Select(picker.ItemCount - 1);
            }
        }

        private void OnMod(long index)
        {
            if (!suppress && index >= 0)
            {
                owner.OpenMod(mods[(int)index]);
            }
        }

        private void OnSelected(long index)
        {
            owner.Select(owner.Document.FindAny(ids[(int)index]));
        }

        private void Change(string key, JsonNode value)
        {
            if (suppress || owner.Selected == null)
            {
                return;
            }

            owner.BeginEdit();
            owner.Selected.Data[key] = value;
            owner.CommitEdit();
        }

        private void OnId(string value)
        {
            Change("Id", JsonValue.Create(value.Trim()));
        }

        private void OnMap(long index)
        {
            Change("Map", JsonValue.Create(maps.GetItemText((int)index)));
            if (!suppress)
            {
                ReloadRows();
            }
        }

        private void OnReward(double value)
        {
            Change("RewardExp", JsonValue.Create((int)value));
        }

        private void OnVictory(long index)
        {
            var value = victory.GetItemMetadata((int)index).AsString();
            Change("VictoryTarget", string.IsNullOrEmpty(value) ? null : JsonValue.Create(value));
        }

        private void OnDefeat(long index)
        {
            var value = defeat.GetItemMetadata((int)index).AsString();
            Change("DefeatTarget", string.IsNullOrEmpty(value) ? null : JsonValue.Create(value));
        }

        private void OnUnitPick(long index)
        {
            if (!suppress)
            {
                RefreshPreview();
            }
        }

        private void AddProjection(string id)
        {
            if (RpgEditorCanvas.HasProjection(id))
            {
                projection.AddItem(ToolUi.Tr("map_battle_editor/projection_" + id));
                projection.SetItemMetadata(projection.ItemCount - 1, id);
            }
        }

        private void OnProjection(long index)
        {
            preview.SetProjection(projection.GetItemMetadata((int)index).AsString());
        }

        private void OnCell(Vector2I cell)
        {
            if (owner.Selected == null || preview.Scene == null)
            {
                return;
            }

            var value = cell.Y * preview.Scene.Map.Width + cell.X;
            if (cell.X < 0 || cell.Y < 0 || cell.X >= preview.Scene.Map.Width || cell.Y >= preview.Scene.Map.Height)
            {
                return;
            }

            owner.BeginEdit();
            if (paint.Selected == 0 && unitPicker.Selected >= 0)
            {
                var id = unitPicker.GetItemText(unitPicker.Selected);
                foreach (var item in Array("Units"))
                {
                    if (item["Id"]?.ToString() == id)
                    {
                        item["Cell"] = value;
                    }
                }
            }
            else
            {
                ToggleCell("Opaque", value, paint.Selected == 1);
                ToggleCell("Cover", value, paint.Selected == 2);
            }

            owner.CommitEdit();
            ReloadRows();
        }

        private void ToggleCell(string key, int cell, bool toggle)
        {
            if (!toggle && paint.Selected != 3)
            {
                return;
            }

            var values = owner.Selected.Data[key] as JsonArray;
            if (values == null)
            {
                values = new JsonArray();
                owner.Selected.Data[key] = values;
            }

            for (var i = values.Count - 1; i >= 0; i--)
            {
                if (values[i]?.GetValue<int>() == cell)
                {
                    values.RemoveAt(i);
                    return;
                }
            }

            if (toggle)
            {
                values.Add(cell);
            }
        }

        private void AddUnit()
        {
            if (owner.Selected == null)
            {
                return;
            }

            owner.BeginEdit();
            var array = owner.Selected.Data["Units"] as JsonArray;
            if (array == null)
            {
                array = new JsonArray();
                owner.Selected.Data["Units"] = array;
            }

            array.Add(new JsonObject
            {
                ["Id"] = FreeLocalId(array, "unit"),
                ["Player"] = array.Count == 0,
                ["Cell"] = array.Count,
                ["Speed"] = 10,
                ["ActionAllowance"] = 2,
                ["MoveAllowance"] = 4,
                ["Abilities"] = new JsonArray()
            });
            owner.CommitEdit();
            ReloadRows();
        }

        private void AddAbility()
        {
            if (owner.Selected == null)
            {
                return;
            }

            owner.BeginEdit();
            var array = owner.Selected.Data["Abilities"] as JsonArray;
            if (array == null)
            {
                array = new JsonArray();
                owner.Selected.Data["Abilities"] = array;
            }

            array.Add(new JsonObject
            {
                ["Id"] = FreeLocalId(array, "ability"),
                ["MinRange"] = 1,
                ["MaxRange"] = 1
            });
            owner.CommitEdit();
            ReloadRows();
        }

        private static string FreeLocalId(JsonArray array, string prefix)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in array)
            {
                ids.Add(item["Id"]?.ToString() ?? string.Empty);
            }

            var index = 1;
            while (ids.Contains(prefix + "_" + index))
            {
                index++;
            }

            return prefix + "_" + index;
        }

        private void Save()
        {
            owner.Save();
        }

        /// <summary>Detaches native scenery before releasing preview-owned textures.</summary>
        public void ReleasePreview()
        {
            preview?.Release();
        }
    }
}
