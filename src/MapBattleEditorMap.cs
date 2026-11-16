using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.Rpg.Model;
using Cthangover.Tools.RpgEditor;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Builds a detached effective map with the existing RPG editor resolver, including placement collision.</summary>
    public static class MapBattleEditorMap
    {
        /// <summary>Resolves current authored content without advancing the live world or editing the map's files.</summary>
        public static RpgEditorScene Read(string id)
        {
            var maps = Entities.Collect<RpgMap>(required: false);
            if (id == null || !maps.TryGetValue(id, out var map)
                || map.Width < 1 || map.Height < 1 || map.Width > 128 || map.Height > 128)
            {
                return null;
            }

            var placements = new JsonArray();
            foreach (var placed in Entities.Collect<RpgPlacement>(required: false).Values)
            {
                if (placed.MapId == id)
                {
                    placements.Add(JsonSerializer.SerializeToNode(placed));
                }
            }

            var files = new List<(string Path, string Text)>
            {
                ("maps/preview.json", new JsonObject
                {
                    ["Kind"] = "rpg_map",
                    ["Items"] = new JsonArray(JsonSerializer.SerializeToNode(map))
                }.ToJsonString()),
                ("placements/preview.json", new JsonObject
                {
                    ["Kind"] = "rpg_placement",
                    ["Items"] = placements
                }.ToJsonString())
            };
            var document = RpgMapDocument.Open(id, "map_battle_editor", files,
                Array.Empty<(string, string, string, JsonObject)>(),
                Array.Empty<(string, string, string, JsonObject)>(), BaseOf, FormOf);
            return RpgEditorScene.From(document, ObjectOf);
        }

        private static JsonObject BaseOf(string kind, string id)
        {
            return null;
        }

        private static RpgObjectForm? FormOf(string id)
        {
            return ObjectOf(id)?.Form;
        }

        private static RpgObject ObjectOf(string id)
        {
            var objects = Entities.Collect<RpgObject>(required: false);
            return id != null && objects.TryGetValue(id, out var value) ? value : null;
        }
    }
}
