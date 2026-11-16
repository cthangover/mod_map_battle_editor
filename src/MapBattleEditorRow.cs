using System.Collections.Generic;
using System.Text.Json.Nodes;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Tools.Common;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Shared utility row helpers; a field operation edits the existing JSON object and preserves unknown properties.</summary>
    public abstract partial class MapBattleEditorRow : ModWidget
    {
        /// <summary>Borrowed document owner; all writes participate in its undo and foreign-patch transaction.</summary>
        protected readonly MapBattleEditorWindow Owner;

        /// <summary>Borrowed list owner, used only after an explicit structural operation.</summary>
        protected readonly MapBattleEditorPanel Panel;

        /// <summary>The document-owned object, never a replacement DTO that could discard extension fields.</summary>
        protected readonly JsonObject Data;

        /// <summary>Blocks control initialization signals from becoming edits.</summary>
        protected bool Loading = true;

        /// <summary>Form column owned by this widget.</summary>
        protected VBoxContainer Column;

        /// <summary>Receives document-owned references; construction remains lazy under ModWidget.</summary>
        protected MapBattleEditorRow(MapBattleEditorWindow owner, MapBattleEditorPanel panel, JsonObject data)
        {
            Owner = owner;
            Panel = panel;
            Data = data;
        }

        /// <summary>Creates one card whose minimum height comes from its actual form contents.</summary>
        protected void BuildColumn()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            Column = new VBoxContainer();
            AddChild(Column);
            Column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            Column.MinimumSizeChanged += FitHeight;
        }

        private void FitHeight()
        {
            CustomMinimumSize = new Vector2(0, Column.GetCombinedMinimumSize().Y + 12);
        }

        /// <summary>Writes one field after recording its previous JSON value.</summary>
        protected void Change(string key, JsonNode value)
        {
            if (Loading)
            {
                return;
            }

            Owner.BeginEdit();
            Data[key] = value;
            Owner.CommitEdit();
        }

        /// <summary>Creates a labeled bounded integer control without changing its original JSON on load.</summary>
        protected SpinBox Number(string key, string label, int fallback, double min = 0, double max = 1000000)
        {
            return ToolUi.Field(Column, "map_battle_editor/" + label, new SpinBox
            {
                Name = key,
                MinValue = min,
                MaxValue = max,
                Step = 1,
                Value = Data[key]?.GetValue<int>() ?? fallback
            });
        }

        /// <summary>Creates a reference picker retaining an unresolved value for validation rather than erasing it.</summary>
        protected OptionButton Choice(string key, string label, IEnumerable<string> options)
        {
            var picker = ToolUi.Field(Column, "map_battle_editor/" + label, new OptionButton { Name = key });
            var value = Data[key]?.ToString();
            var found = -1;
            foreach (var id in options)
            {
                picker.AddItem(id);
                if (id == value)
                {
                    found = picker.ItemCount - 1;
                }
            }

            if (found < 0 && value != null)
            {
                picker.AddItem(value);
                found = picker.ItemCount - 1;
            }

            picker.Select(found);
            return picker;
        }

        /// <summary>Shows stable local identity without allowing a rename to orphan existing encounter references.</summary>
        protected void Identity()
        {
            var identity = ToolUi.Field(Column, "tools/entity_editor/id", new LineEdit
            {
                Name = "LocalId",
                Text = Data["Id"]?.ToString() ?? string.Empty,
                Editable = false
            });
            identity.TooltipText = ToolUi.Tr("map_battle_editor/local_id_hint");
        }

        /// <summary>Removes this one array item as an undoable operation.</summary>
        protected void Remove(string key)
        {
            Owner.BeginEdit();
            ((JsonArray)Owner.Selected.Data[key]).Remove(Data);
            Owner.CommitEdit();
            Panel.ReloadRows();
        }
    }
}
