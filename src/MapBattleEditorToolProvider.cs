using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Registers the encounter authoring window independently of the map and character editor windows.</summary>
    public sealed class MapBattleEditorToolProvider : IToolProvider
    {
        /// <summary>Stable tool identity used by the toolbar and other editor integrations.</summary>
        public string Id => "map_battle_editor";

        /// <summary>Localized name shared with the toolbar entry.</summary>
        public string LocaleKey => "map_battle_editor/title";

        /// <summary>Creates a document owner; opening it never starts or changes an encounter.</summary>
        public Window CreateWindow()
        {
            return new MapBattleEditorWindow();
        }
    }
}
