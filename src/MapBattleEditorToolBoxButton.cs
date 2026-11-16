using Cthangover.Core.UI.Tool;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Exposes the encounter editor through the ordinary in-game tools menu.</summary>
    public sealed class MapBattleEditorToolBoxButton : IToolBoxButton
    {
        /// <summary>Opens the encounter provider without requiring a particular logical map.</summary>
        public string ToolId => "map_battle_editor";

        /// <summary>The tools menu uses the translated label rather than a dedicated icon.</summary>
        public string IconPath => string.Empty;

        /// <summary>Label shared with the encounter window.</summary>
        public string LocaleKey => "map_battle_editor/title";

        /// <summary>Available whenever this optional authoring mod is loaded.</summary>
        public bool IsVisible()
        {
            return true;
        }
    }
}
