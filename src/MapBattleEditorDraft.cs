using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.MapBattle;
using Cthangover.Tools.RpgEditor;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Validates editable recipes before any file write or sandbox launch; errors are locale keys.</summary>
    public static class MapBattleEditorDraft
    {
        /// <summary>Returns the first actionable problem, or null; invalid drafts remain in the undo document.</summary>
        public static string Check(JsonObject data, RpgEditorScene scene)
        {
            MapBattleDefinition recipe;
            try
            {
                recipe = data.Deserialize<MapBattleDefinition>();
            }
            catch (JsonException)
            {
                return "map_battle_editor/error/data";
            }

            if (recipe == null || string.IsNullOrWhiteSpace(recipe.ID) || recipe.RewardExp < 0)
            {
                return "map_battle_editor/error/data";
            }

            if (scene == null || scene.Map.Width < 1 || scene.Map.Height < 1
                || scene.Map.Width > 128 || scene.Map.Height > 128)
            {
                return "map_battle_editor/error/map";
            }

            var size = scene.Map.Width * scene.Map.Height;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<int>();
            var roster = new HashSet<string>(StringComparer.Ordinal);
            var playerCount = 0;
            var enemyCount = 0;
            var abilities = new Dictionary<string, MapBattleAbility>(StringComparer.Ordinal);
            foreach (var ability in recipe.Abilities ?? new List<MapBattleAbility>())
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.Id) || !abilities.TryAdd(ability.Id, ability)
                    || ability.MinRange < 1 || ability.MaxRange < ability.MinRange)
                {
                    return "map_battle_editor/error/abilities";
                }

                if (ability.ActionId == null || !Entities.Collect<ActionCharacterInfo>(required: false).ContainsKey(ability.ActionId))
                {
                    return "map_battle_editor/error/action";
                }

                var action = ActionCharacterFactory.Instance.Get(ability.ActionId);
                if (action == null || action.Type != Cthangover.Core.Characters.ActionCharacterType.ToEnemy
                    || string.IsNullOrWhiteSpace(action.GetStr("Formula")) || action.GetInt("RequiredPoint") < 1
                    || !string.IsNullOrEmpty(action.GetStr("Cost")) || !string.IsNullOrEmpty(action.GetStr("State"))
                    || action.GetInt("Pierce") != 0 || action.GetStr("Effect", "damage") != "damage")
                {
                    return "map_battle_editor/error/action";
                }
            }

            foreach (var unit in recipe.Units ?? new List<MapBattleUnit>())
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.Id) || !ids.Add(unit.Id)
                    || unit.Template == null || !Entities.Collect<CharacterInfo>(required: false).ContainsKey(unit.Template)
                    || unit.ActionAllowance < 1
                    || unit.MoveAllowance < 0 || unit.Abilities == null || unit.Abilities.Count == 0)
                {
                    return "map_battle_editor/error/units";
                }

                if (unit.Cell < 0 || unit.Cell >= size || !cells.Add(unit.Cell)
                    || !scene.Walk.IsWalkable(unit.Cell % scene.Map.Width, unit.Cell / scene.Map.Width))
                {
                    return "map_battle_editor/error/cell";
                }

                var template = Entities.Collect<CharacterInfo>(required: false)[unit.Template];
                foreach (var id in unit.Abilities)
                {
                    if (!abilities.TryGetValue(id, out var ability))
                    {
                        return "map_battle_editor/error/abilities";
                    }

                    var assigned = false;
                    foreach (var action in template.Actions ?? new List<string>())
                    {
                        assigned |= action == ability.ActionId;
                    }

                    if (unit.RosterId == null && !assigned)
                    {
                        return "map_battle_editor/error/assigned";
                    }
                }

                if (unit.RosterId != null && (!unit.Player || !roster.Add(unit.RosterId)))
                {
                    return "map_battle_editor/error/roster";
                }

                playerCount += unit.Player ? 1 : 0;
                enemyCount += unit.Player ? 0 : 1;
            }

            if (playerCount < 1 || playerCount > 2 || enemyCount < 1 || enemyCount > 20)
            {
                return "map_battle_editor/error/sides";
            }

            foreach (var cell in recipe.Opaque ?? new List<int>())
            {
                if (cell < 0 || cell >= size)
                {
                    return "map_battle_editor/error/metadata";
                }
            }

            foreach (var cell in recipe.Cover ?? new List<int>())
            {
                if (cell < 0 || cell >= size)
                {
                    return "map_battle_editor/error/metadata";
                }
            }

            try
            {
                MapBattleOutcome.ValidateTargets(new MapBattleState
                {
                    Units = recipe.Units,
                    VictoryTarget = recipe.VictoryTarget,
                    DefeatTarget = recipe.DefeatTarget
                });
            }
            catch (ArgumentException)
            {
                return "map_battle_editor/error/targets";
            }

            return null;
        }
    }
}
