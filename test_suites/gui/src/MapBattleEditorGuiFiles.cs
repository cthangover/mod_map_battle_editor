using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Tools.Services;

namespace Cthangover.Tools.MapBattleEditor.GuiTests
{
    /// <summary>Owns every temporary authoring input and restores byte-identical source text through IDisposable.</summary>
    public sealed class MapBattleEditorGuiFiles : IDisposable
    {
        /// <summary>Temporary scene and logical map shared by the two projection launches.</summary>
        public const string Map = "map_battle_editor_gui";

        /// <summary>Destination remains a real writable mod, never an inaccessible virtual-suite provider.</summary>
        public const string Mod = "map_battle_editor";

        private readonly Dictionary<(string Mod, string Path), string> before = new();

        /// <summary>Creates isolated character/action/map/scenario inputs, plus an inherited recipe for minimal-patch checks.</summary>
        public MapBattleEditorGuiFiles()
        {
            Track(Mod, "manifest.json");
            Track(Mod, "encounters/" + Mod + ".json");
            Track(Mod, "patches/map_battle/" + Mod + ".json");
            Write(Mod, "characters/gui_actions.json", @"
{""Kind"":""action"",""Items"":[
  {""Id"":""__map_editor/strike"",""Name"":""map_battle_editor/gui_strike"",""Type"":""ToEnemy"",""Image"":""characters/village/hero"",
   ""Values"":[""Formula=a.attack"",""RequiredPoint=1"",""Effect=damage""]},
  {""Id"":""__map_editor/shot"",""Name"":""map_battle_editor/gui_shot"",""Type"":""ToEnemy"",""Image"":""characters/village/hero"",
   ""Values"":[""Formula=a.attack"",""RequiredPoint=2"",""Effect=damage""]}
]}");
            Write(Mod, "characters/gui_characters.json", @"
{""Kind"":""character"",""Items"":[
  {""Id"":""__map_editor/guard"",""Name"":""map_battle_editor/gui_guard"",""Health"":30,""Attack"":8,
   ""Point"":2,""Image"":""characters/village/hero"",""Charset"":""village/hero"",""Actions"":[""__map_editor/strike"",""__map_editor/shot""]},
  {""Id"":""__map_editor/archer"",""Name"":""map_battle_editor/gui_archer"",""Health"":24,""Attack"":7,
   ""Point"":2,""Image"":""characters/village/hero"",""Charset"":""village/villager"",""Actions"":[""__map_editor/strike"",""__map_editor/shot""]},
  {""Id"":""__map_editor/enemy"",""Name"":""map_battle_editor/gui_enemy"",""Health"":8,""Attack"":3,
   ""Point"":2,""Image"":""characters/village/slime"",""Actions"":[""__map_editor/strike"",""__map_editor/shot""]}
]}");
            Write(Mod, "maps/gui_map.json", @"
{""Kind"":""rpg_map"",""Items"":[{""Id"":""map_battle_editor_gui"",""Width"":8,""Height"":6,
 ""TileSize"":56,""Zoom"":1,""LightMode"":""day"",""Spawn"":{""X"":1,""Y"":1,""Dir"":""Right""},
 ""Legend"":{},""Collision"":[""........"",""........"",""...#...."",""...#...."",""........"",""........""]}]}");
            Write(Mod, "scenes/gui_map.json", "{\"name\":\"" + Map + "\"}");
            Write(Mod, "scenarios/gui_map.scenario", "scene: " + Map + "\npriority: 100\nis_one_run: true\n---\n"
                + "action party.add id=__map_editor/guard\naction party.add id=__map_editor/archer\nend\n");
            Write("map_battle", "encounters/editor_gui_foreign.json", @"
{""Kind"":""map_battle"",""Items"":[{""Id"":""map_battle/gui_foreign"",""Map"":""map_battle_editor_gui"",
 ""RewardExp"":3,""Units"":[
   {""Id"":""p"",""Template"":""__map_editor/guard"",""Player"":true,""Cell"":17,""Abilities"":[""hit""]},
   {""Id"":""e"",""Template"":""__map_editor/enemy"",""Cell"":22,""Abilities"":[""hit""]}],
 ""Abilities"":[{""Id"":""hit"",""ActionId"":""__map_editor/strike"",""MaxRange"":1}]}]}");
            Refresh();
        }

        private void Track(string mod, string path)
        {
            before.TryAdd((mod, path), ModFileService.ReadText(mod, path));
        }

        private void Write(string mod, string path, string text)
        {
            Track(mod, path);
            if (!ModFileService.WriteText(mod, path, text + "\n"))
            {
                throw new InvalidOperationException("Fixture is not writable: " + mod + "/" + path);
            }
        }

        /// <summary>Drops shared caches after fixture publication or restoration, matching ordinary editor saves.</summary>
        public static void Refresh()
        {
            ModManager.Instance.GetResolver<PatchResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            SceneManager.ReloadContent();
        }

        /// <summary>Restores exactly the files owned by this fixture, including a manifest affected by dependency inference.</summary>
        public void Dispose()
        {
            foreach (var entry in before)
            {
                if (entry.Value == null)
                {
                    ModFileService.DeleteFile(entry.Key.Mod, entry.Key.Path);
                }
                else
                {
                    ModFileService.WriteText(entry.Key.Mod, entry.Key.Path, entry.Value);
                }
            }

            Refresh();
        }
    }
}
