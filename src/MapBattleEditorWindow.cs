using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Tools.Common;
using Cthangover.Tools.EntityEditor;
using Cthangover.Tools.Services;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Encounter-specific inspector over the shared undo, foreign-patch and changed-file document model.</summary>
    public partial class MapBattleEditorWindow : ToolWindow
    {
        private string pendingMod;
        private MapBattleEditorPanel panel;
        private MapBattleEditorTrial trial;
        private readonly HashSet<string> savedIdentities = new(StringComparer.Ordinal);

        /// <summary>Writable destination; foreign encounters save as minimal patches here.</summary>
        public string ModId { get; private set; }

        /// <summary>Saved identities stay stable; new own records may receive a custom identity before their first save.</summary>
        public bool CanEditIdentity => Selected != null && !Selected.IsForeign && !savedIdentities.Contains(Selected.Id);

        /// <summary>Current raw document, retaining fields the encounter form does not own.</summary>
        public EntityDocument Document { get; private set; }

        /// <summary>Current encounter recipe; selection does not mutate it.</summary>
        public EntityRecord Selected { get; private set; }

        /// <summary>Builds utility controls under ModWidget and opens the first writable mod.</summary>
        public MapBattleEditorWindow() : base("map_battle_editor/title")
        {
            Size = new Vector2I(1200, 850);
            panel = new MapBattleEditorPanel(this);
            AddChild(panel);
            panel.EnsureConstructed();
            var mods = ModFileService.ListWritableMods();
            panel.FillMods(mods);
            if (mods.Count > 0)
            {
                OpenMod(mods[0]);
            }
        }

        /// <summary>Uses the common unsaved-changes prompt before replacing a destination document.</summary>
        public void OpenMod(string mod)
        {
            pendingMod = mod;
            RunDiscardingChanges(LoadPending);
        }

        private void LoadPending()
        {
            if (!ModFileService.IsWritable(pendingMod))
            {
                return;
            }

            ModId = pendingMod;
            var files = new List<(string Path, string Text)>();
            foreach (var file in ModFileService.ListFiles(ModId, string.Empty, ".json"))
            {
                files.Add((file, ModFileService.ReadText(ModId, file)));
            }

            Document = EntityDocument.Load("map_battle", "encounters/" + ModId + ".json", files, out var skipped);
            var patch = ForeignEntities.PatchFile("map_battle", ModId);
            Document.AttachForeign(patch, ModFileService.ReadText(ModId, patch), typeof(Cthangover.Mods.MapBattle.MapBattleDefinition),
                ForeignEntities.Scan("map_battle", ModId), ForeignBase);
            savedIdentities.Clear();
            foreach (var record in Document.Records)
            {
                savedIdentities.Add(record.Id);
            }

            MarkClean();
            panel.RefreshList();
            Select(Document.Records.Count > 0 ? Document.Records[0] : null);
            Status(skipped.Count > 0 ? "map_battle_editor/error/files" : "map_battle_editor/hint");
        }

        private JsonObject ForeignBase(string id)
        {
            return ForeignEntities.BaseWithout("map_battle", id, ModId, ForeignEntities.PatchFile("map_battle", ModId));
        }

        /// <summary>Shows an own or inherited encounter without creating a patch merely by opening it.</summary>
        public void Select(EntityRecord record)
        {
            Selected = record;
            panel.ShowRecord(record);
        }

        /// <summary>Starts one reversible form operation before a field or list changes.</summary>
        public void BeginEdit()
        {
            Document.BeginChange();
        }

        /// <summary>
        /// Commits the foreign difference and refreshes diagnostics without rebuilding focused text controls.
        /// </summary>
        public void CommitEdit()
        {
            if (Selected.IsForeign)
            {
                Document.CommitForeign(Selected);
            }

            SetDirty();
            panel.RefreshList();
            panel.RefreshPreview();
        }

        /// <summary>Creates an empty recipe; validation keeps it off disk until both sides and legal actions exist.</summary>
        public void NewEncounter()
        {
            if (Document == null)
            {
                return;
            }

            Document.BeginChange();
            var record = Document.Add(new JsonObject
            {
                ["Id"] = Document.FreeId(ModId + "/encounter"),
                ["RewardExp"] = 10,
                ["Units"] = new JsonArray(),
                ["Abilities"] = new JsonArray(),
                ["Opaque"] = new JsonArray(),
                ["Cover"] = new JsonArray()
            });
            Select(record);
            SetDirty();
            panel.RefreshList();
        }

        /// <summary>Copies even a foreign recipe to a new own identity, leaving its owner untouched.</summary>
        public void CopyEncounter()
        {
            if (Selected == null)
            {
                return;
            }

            var copy = (JsonObject)Selected.Data.DeepClone();
            copy["Id"] = Document.FreeId(ModId + "/encounter");
            Document.BeginChange();
            Select(Document.Add(copy));
            SetDirty();
            panel.RefreshList();
        }

        /// <summary>Confirms deletion; foreign records become ordinary deletion patches.</summary>
        public void DeleteEncounter()
        {
            if (Selected != null)
            {
                ShowConfirm("tools/entity_editor/delete", "map_battle_editor/delete_confirm", DeleteConfirmed);
            }
        }

        private void DeleteConfirmed()
        {
            Document.BeginChange();
            if (Selected.IsForeign)
            {
                Document.DeleteForeign(Selected);
            }
            else
            {
                Document.Remove(Selected);
            }

            Select(null);
            SetDirty();
            panel.RefreshList();
        }

        /// <summary>Restores the selected inherited recipe by removing its local patch.</summary>
        public void ResetInherited()
        {
            if (Selected?.IsForeign == true)
            {
                Document.BeginChange();
                Document.ResetForeign(Selected);
                Select(Selected);
                SetDirty();
            }
        }

        /// <summary>Restores one document snapshot, including participant arrays and foreign patches.</summary>
        public void Undo()
        {
            RestoreHistory(false);
        }

        /// <summary>Reapplies one undone operation without touching the live battle or map.</summary>
        public void Redo()
        {
            RestoreHistory(true);
        }

        private void RestoreHistory(bool redo)
        {
            var id = Selected?.Id;
            if (Document != null && (redo ? Document.Redo() : Document.Undo()))
            {
                Select(Document.FindAny(id));
                SetDirty();
                panel.RefreshList();
            }
        }

        /// <summary>Validates every own/edited recipe before writing any file; unrelated source files remain byte-identical.</summary>
        public bool Save()
        {
            if (Document == null || panel.InvalidInput || Document.Validate().Count > 0)
            {
                Status("map_battle_editor/error/data");
                return false;
            }

            foreach (var record in Document.Records)
            {
                if (!Validate(record))
                {
                    return false;
                }
            }

            foreach (var record in Document.Foreign)
            {
                if (Document.PatchOf(record.Id) != null && !Document.IsDeleted(record) && !Validate(record))
                {
                    return false;
                }
            }

            foreach (var file in Document.ChangedFiles())
            {
                if (!ModFileService.WriteText(ModId, file.Path, file.Text))
                {
                    Status("tools/common/readonly");
                    return false;
                }
            }

            var patch = Document.ChangedPatch();
            if (patch != null)
            {
                var value = patch.Value;
                var written = value.Text == null ? ModFileService.DeleteFile(ModId, value.Path)
                    : ModFileService.WriteText(ModId, value.Path, value.Text);
                if (!written)
                {
                    Status("tools/common/readonly");
                    return false;
                }
            }

            ModDependencies.Ensure(ModId, "map_battle");
            ModDependencies.EnsureReferenced(ModId, References());
            Document.MarkSaved();
            foreach (var record in Document.Records)
            {
                savedIdentities.Add(record.Id);
            }

            panel.RefreshIdentityLock();
            MarkClean();
            ModManager.Instance.GetResolver<PatchResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            ModEventBus.Publish(new EntitiesSavedEvent
            {
                Kind = "map_battle",
                ModId = ModId
            });
            Status("map_battle_editor/saved");
            return true;
        }

        private bool Validate(EntityRecord record)
        {
            var problem = MapBattleEditorDraft.Check(record.Data, MapBattleEditorMap.Read(record.Data["Map"]?.ToString()));
            if (problem != null)
            {
                Status(problem);
                return false;
            }

            return true;
        }

        private IEnumerable<(string Kind, string Id)> References()
        {
            var records = new List<EntityRecord>(Document.Records);
            foreach (var record in Document.Foreign)
            {
                if (Document.PatchOf(record.Id) != null && !Document.IsDeleted(record))
                {
                    records.Add(record);
                }
            }

            foreach (var record in records)
            {
                yield return ("rpg_map", record.Data["Map"]?.ToString());
                foreach (var unit in (JsonArray)record.Data["Units"])
                {
                    yield return ("character", unit["Template"]?.ToString());
                }

                foreach (var ability in (JsonArray)record.Data["Abilities"])
                {
                    yield return ("action", ability["ActionId"]?.ToString());
                }
            }
        }

        /// <summary>Starts a reversible real-game play session after the same validation used by Save.</summary>
        public void TestBattle()
        {
            if (trial != null || Selected == null || panel.InvalidInput || !Validate(Selected))
            {
                return;
            }

            trial = new MapBattleEditorTrial(this, Selected.Data);
            GetTree().Root.AddChild(trial);
            if (trial.Start())
            {
                Hide();
            }
            else
            {
                trial.QueueFree();
                trial = null;
            }
        }

        /// <summary>Releases the trial identity after restoration so another independent trial may start.</summary>
        public void TrialFinished()
        {
            trial = null;
            Show();
        }

        /// <summary>Shows localized validation and lifecycle feedback in the shared tool status bar.</summary>
        public void Status(string key)
        {
            SetStatus(ToolUi.Tr(key));
        }

        /// <summary>Ctrl+S uses the same validated writer as the toolbar.</summary>
        protected override void OnSaveShortcut()
        {
            Save();
        }

        /// <summary>Closing releases the detached map and stops any active trial before this window disappears.</summary>
        protected override void Cleanup()
        {
            trial?.Stop();
            panel.ReleasePreview();
        }
    }
}
