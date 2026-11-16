using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Games;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Mods.MapBattle;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Runs the real 2D fight under a full save snapshot; Stop and terminal Continue restore all original owners.</summary>
    public partial class MapBattleEditorTrial : Node
    {
        private readonly MapBattleEditorWindow owner;
        private readonly MapBattleDefinition recipe;
        private string snapshot;
        private string previous;
        private MapBattleState active;
        private double waiting;
        private bool running;
        private bool restoring;
        private MapBattleController previousController;

        /// <summary>Copies the authored draft; test changes cannot mutate its document or settlement identity.</summary>
        public MapBattleEditorTrial(MapBattleEditorWindow owner, JsonObject data)
        {
            this.owner = owner;
            recipe = data.Deserialize<MapBattleDefinition>();
            recipe.ID = "__map_battle_editor/" + Guid.NewGuid().ToString("N");
            ProcessMode = ProcessModeEnum.Always;
        }

        /// <summary>Snapshots an idle running game, suppresses normal entry scenarios and enters the selected map.</summary>
        public bool Start()
        {
            var manager = GetNode<SceneManager>("/root/SceneManager");
            if (!ActiveGame.IsActive || Cthangover.Mods.Rpg.RpgWorldHost.Current?.Session == null
                || SceneManager.IsTestPlayActive || MapBattleStorage.Memory.Active != null
                || !manager.HasScene(recipe.Map))
            {
                owner.Status("map_battle_editor/error/test_context");
                return false;
            }

            snapshot = JsonSerializer.Serialize(SaveService.BuildPackage("__map_battle_editor"));
            previous = SceneManager.GetCurrentSceneName();
            var layer = new CanvasLayer { Layer = 200 };
            AddChild(layer);
            var bar = TscnScenes.LoadAndBuild("scenes/ui/map_battle_editor_play_bar.tscn") as Control;
            if (bar == null)
            {
                owner.Status("map_battle_editor/error/test_context");
                return false;
            }

            layer.AddChild(bar);
            bar.GetNode<Button>("Row/Stop").Pressed += Stop;
            SceneManager.TestScenarioText = "scene: " + recipe.Map + "\npriority: 100\n---\nend\n";
            SceneManager.IsTestPlayActive = true;
            running = true;
            previousController = MapBattleController.Current;
            manager.PendingSceneName = recipe.Map;
            GetNode<GodotSceneService>("/root/GodotSceneService").LoadShell();
            return true;
        }

        /// <summary>Waits for the ordinary shell, then starts the validated recipe through the runtime controller.</summary>
        public override void _Process(double delta)
        {
            if (ApplicationExit.IsQuitting)
            {
                return;
            }

            if (restoring)
            {
                if (FreshShellReady() && SceneManager.GetCurrentSceneName() == previous)
                {
                    restoring = false;
                    if (GodotObject.IsInstanceValid(owner))
                    {
                        owner.TrialFinished();
                    }

                    QueueFree();
                }

                return;
            }

            if (!running)
            {
                return;
            }

            waiting += delta;
            if (active == null)
            {
                if (FreshShellReady() && MapBattleController.Current.TryStart(recipe))
                {
                    active = MapBattleStorage.Memory.Active;
                }
                else if (waiting > 30)
                {
                    Stop();
                    if (GodotObject.IsInstanceValid(owner))
                    {
                        owner.Status("map_battle_editor/error/test_start");
                    }
                }
            }
            else if (MapBattleStorage.Memory.Active == null)
            {
                Stop();
            }
        }

        private bool FreshShellReady()
        {
            var controller = MapBattleController.Current;
            var host = Cthangover.Mods.Rpg.RpgWorldHost.Current;
            return controller != null && !ReferenceEquals(controller, previousController) && controller.IsNodeReady()
                && host?.Session != null && host.View?.Presentation != null && !GodotSceneService.IsTransitioning;
        }

        /// <summary>F8 restores the same snapshot as the visible Stop button.</summary>
        public override void _Input(InputEvent input)
        {
            if (running && input is InputEventKey key && key.Pressed && key.PhysicalKeycode == Key.F8)
            {
                Stop();
                GetViewport().SetInputAsHandled();
            }
        }

        /// <summary>Restores once, including HP, XP, point budgets and reward receipts; editor document changes stay authored.</summary>
        public void Stop()
        {
            if (!running)
            {
                return;
            }

            running = false;
            Callable.From(Restore).CallDeferred();
        }

        private void Restore()
        {
            SceneManager.IsTestPlayActive = false;
            SceneManager.TestScenarioText = null;
            if (!ApplicationExit.IsQuitting)
            {
                SaveService.ApplyPackage(JsonSerializer.Deserialize<SavePackage>(snapshot), "map battle editor stop");
                var manager = GetNode<SceneManager>("/root/SceneManager");
                previousController = MapBattleController.Current;
                restoring = true;
                manager.PendingSceneName = previous;
                GetNode<GodotSceneService>("/root/GodotSceneService").LoadShell();
            }

            snapshot = null;
            active = null;
            if (!restoring)
            {
                QueueFree();
            }
        }
    }
}
