using Cthangover.Core.Mods;

namespace Cthangover.Tools.MapBattleEditor.GuiTests
{
    /// <summary>Mounts one bounded real-input authoring driver only through the explicitly activated test suite.</summary>
    public sealed class MapBattleEditorGuiInitializer : IModInitializer
    {
        private static bool queued;

        /// <summary>Individual mod load does not create windows or fixtures.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Queues the driver after runtime services and the game's normal content are available.</summary>
        public void OnModResourcesReady()
        {
            if (!queued)
            {
                queued = true;
                ModRoot.AddDeferred(new MapBattleEditorGuiDriver());
            }
        }
    }
}
