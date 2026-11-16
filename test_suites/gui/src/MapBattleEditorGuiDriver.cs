using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Cthangover.Core.UI.Tool;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Mods.Interface.View;
using Cthangover.Mods.MapBattle;
using Cthangover.Mods.Rpg;
using Cthangover.Mods.Stats;
using Cthangover.Tools.RpgEditor;
using Cthangover.Tools.Services;
using Godot;

namespace Cthangover.Tools.MapBattleEditor.GuiTests
{
    /// <summary>Exercises actual form controls and viewport map clicks, persistence, inherited patches and real trial rollback.</summary>
    public partial class MapBattleEditorGuiDriver : Node
    {
        private MapBattleEditorGuiFiles files;
        private MapBattleEditorWindow window;
        private int step;
        private int passed;
        private double elapsed;
        private double delay;
        private string encounter;
        private string beforeInvalid;
        private string beforeForeign;
        private string memory;
        private int originalHealth;
        private int originalExp;
        private string projection;

        /// <summary>Runs bounded steps only inside Godot; every failure restores fixture files and exits through ApplicationExit.</summary>
        public override void _Process(double delta)
        {
            if (!GameEnvironment.IsRuntime || step < 0)
            {
                return;
            }

            elapsed += delta;
            delay += delta;
            if (delay < .12)
            {
                return;
            }

            delay = 0;
            try
            {
                if (elapsed > 180)
                {
                    throw new TimeoutException("GUI step " + step);
                }

                Run();
            }
            catch (Exception error)
            {
                GameLogger.Log("TEST", "map battle editor step " + step + ": " + error, LogLevel.Error);
                Finish(1);
            }
        }

        private void Run()
        {
            if (!Cthangover.Core.Bootstrap.Boot.IsReady || GodotSceneService.IsTransitioning)
            {
                return;
            }

            switch (step)
            {
                case 0:
                    if (RpgWorldHost.Current == null)
                    {
                        return;
                    }

                    projection = "topdown";
                    foreach (var arg in OS.GetCmdlineUserArgs())
                    {
                        if (arg == "--tile-renderer=isometric")
                        {
                            projection = "isometric";
                        }
                    }

                    GameData.Instance.Settings.Language = projection == "isometric" ? "en" : "ru";
                    LocaleLoader.LoadLocale(GameData.Instance.Settings.Language);
                    files = new MapBattleEditorGuiFiles();
                    GetNode<SceneManager>("/root/SceneManager").SwitchScene(MapBattleEditorGuiFiles.Map);
                    step++;
                    return;
                case 1:
                    if (RpgWorldHost.Current?.Session?.Map.ID != MapBattleEditorGuiFiles.Map
                        || StatsService.RosterEntry("__map_editor/guard") == null || RpgInput.IsWalkBlocked(this))
                    {
                        return;
                    }

                    window = (MapBattleEditorWindow)ToolFactory.Instance.Get("map_battle_editor").CreateWindow();
                    ToolWindow.ShowWindow(window);
                    window.OpenMod(MapBattleEditorGuiFiles.Mod);
                    Press("New");
                    encounter = window.Selected.Id;
                    Pick(Field<OptionButton>(window, "Map"), MapBattleEditorGuiFiles.Map);
                    AddAbilities();
                    AddUnits();
                    window.Selected.Data["ProviderNote"] = new JsonObject { ["token"] = "keep-root" };
                    window.Selected.Data["Units"][0]["CosmeticToken"] = "keep-unit";
                    window.Selected.Data["Abilities"][0]["ProviderToken"] = "keep-ability";
                    Pick(Field<OptionButton>(window, "Projection"), projection);
                    Check(Field<RpgEditorCanvas>(window, "RpgEditorCanvas").Projection == projection,
                        "preview uses the requested installed projection");
                    Check(window.Selected.Data["Units"].AsArray().Count == 4, "four GUI-created participants");
                    Check(window.Selected.Data["Abilities"].AsArray().Count == 2, "two GUI-created spatial actions");
                    step++;
                    return;
                case 2:
                    Shot("01-form");
                    ClickCell(2, 1);
                    step++;
                    return;
                case 3:
                    Check(window.Selected.Data["Units"][0]["Cell"].GetValue<int>() == 10,
                        "real projected click places the selected unit");
                    var row = Unit(0);
                    Check(Field<SpinBox>(row, "X").Value == 2 && Field<SpinBox>(row, "Y").Value == 1,
                        "numeric coordinates reflect the click");
                    Field<OptionButton>(window, "PaintMode").Select(1);
                    ClickCell(3, 2);
                    step = 30;
                    return;
                case 30:
                    Check(HasCell("Opaque", 19), "canvas paints sight blockers on the chosen map");
                    Field<OptionButton>(window, "PaintMode").Select(2);
                    ClickCell(4, 4);
                    step++;
                    return;
                case 31:
                    Check(HasCell("Cover", 36), "canvas paints cover independently of collision");
                    Field<OptionButton>(window, "PaintMode").Select(3);
                    ClickCell(4, 4);
                    step++;
                    return;
                case 32:
                    Check(!HasCell("Cover", 36) && HasCell("Opaque", 19), "erase clears only the clicked mask cell");
                    Field<OptionButton>(window, "PaintMode").Select(2);
                    ClickCell(4, 4);
                    step++;
                    return;
                case 33:
                    Check(HasCell("Cover", 36), "cover can be restored through the same canvas input");
                    Field<OptionButton>(window, "PaintMode").Select(0);
                    Pick(Field<OptionButton>(window, "PlaceUnit"), "unit_2");
                    ClickCell(2, 3);
                    step++;
                    return;
                case 34:
                    Check(window.Selected.Data["Units"][1]["Cell"].GetValue<int>() == 26,
                        "canvas placement respects the selected participant");
                    Pick(Field<OptionButton>(window, "PlaceUnit"), "unit_1");
                    row = Unit(0);
                    beforeInvalid = ModFileService.ReadText(MapBattleEditorGuiFiles.Mod, "encounters/map_battle_editor.json");
                    Field<SpinBox>(row, "X").Value = 128;
                    Press("Save");
                    Check(beforeInvalid == ModFileService.ReadText(MapBattleEditorGuiFiles.Mod,
                        "encounters/map_battle_editor.json"), "invalid numeric input cannot write stale valid data");
                    Field<SpinBox>(row, "X").Value = 2;
                    Pick(Field<OptionButton>(window, "Victory"), "unit_3");
                    Pick(Field<OptionButton>(window, "Defeat"), "unit_2");
                    Field<SpinBox>(window, "Reward").Value = 17;
                    Press("Save");
                    Check(Entities.Collect<MapBattleDefinition>()[encounter].VictoryTarget == "unit_3",
                        "saved runtime catalogue sees the selected victory condition");
                    Field<SpinBox>(window, "Reward").Value = 18;
                    Press("Undo");
                    Check(window.Selected.Data["RewardExp"].GetValue<int>() == 17, "undo restores the previous reward");
                    Press("Redo");
                    Check(window.Selected.Data["RewardExp"].GetValue<int>() == 18, "redo restores the new reward");
                    Press("Save");
                    Check(!Field<LineEdit>(window, "EncounterId").Editable, "saved encounter identity stays stable");
                    var written = JsonNode.Parse(ModFileService.ReadText(MapBattleEditorGuiFiles.Mod,
                        "encounters/map_battle_editor.json"))["Items"][0];
                    Check(written["ProviderNote"]["token"].ToString() == "keep-root"
                        && written["Units"][0]["CosmeticToken"].ToString() == "keep-unit"
                        && written["Abilities"][0]["ProviderToken"].ToString() == "keep-ability",
                        "root, participant and ability provider fields survive form save");
                    step = 4;
                    return;
                case 4:
                    Shot("02-saved");
                    CheckPreviewPixels();
                    Field<ScrollContainer>(window, "InspectorScroll").EnsureControlVisible(Unit(0));
                    step = 40;
                    return;
                case 40:
                    Shot("05-participant");
                    Field<ScrollContainer>(window, "InspectorScroll").EnsureControlVisible(
                        Field<Control>(Field<VBoxContainer>(window, "Abilities"), "Ability_ability_2"));
                    step++;
                    return;
                case 41:
                    var abilityRows = Field<VBoxContainer>(window, "Abilities");
                    var closeRow = Field<Control>(abilityRows, "Ability_ability_1");
                    var rangedRow = Field<Control>(abilityRows, "Ability_ability_2");
                    Check(closeRow.Size.Y > 0 && rangedRow.Size.Y > 0
                        && rangedRow.Position.Y >= closeRow.Position.Y + closeRow.Size.Y,
                        "ability cards occupy separate visible layout rows");
                    Shot("06-abilities");
                    Field<ScrollContainer>(window, "InspectorScroll").ScrollVertical = 0;
                    step++;
                    return;
                case 42:
                    var original = window.Selected.Data.ToJsonString();
                    Press("Copy");
                    Check(window.Selected.Id != encounter && window.Selected.Data["Units"].AsArray().Count == 4,
                        "copy owns a separate identity and participant array");
                    Field<SpinBox>(window, "Reward").Value = 999;
                    Check(window.Document.Find(encounter).Data.ToJsonString() == original, "copy edit leaves source unchanged");
                    Press("Delete");
                    foreach (var child in window.GetChildren())
                    {
                        if (child is ConfirmationDialog dialog)
                        {
                            dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
                            break;
                        }
                    }

                    Check(window.Document.Records.Count == 1, "confirmed deletion removes only the copy");
                    window.Select(window.Document.Find(encounter));
                    beforeForeign = ModFileService.ReadText("map_battle", "encounters/editor_gui_foreign.json");
                    window.Select(window.Document.FindForeign("map_battle/gui_foreign"));
                    Field<SpinBox>(window, "Reward").Value = 29;
                    Press("Save");
                    var patch = JsonNode.Parse(ModFileService.ReadText(MapBattleEditorGuiFiles.Mod,
                        "patches/map_battle/map_battle_editor.json"));
                    Check(patch["Items"][0].AsObject().Count == 2
                        && patch["Items"][0]["RewardExp"].GetValue<int>() == 29,
                        "foreign edit writes only Id and changed reward");
                    Check(beforeForeign == ModFileService.ReadText("map_battle", "encounters/editor_gui_foreign.json"),
                        "inherited source remains byte-identical");
                    Press("Reset");
                    Press("Save");
                    Check(ModFileService.ReadText(MapBattleEditorGuiFiles.Mod,
                        "patches/map_battle/map_battle_editor.json") == null, "reset removes the empty local patch");
                    window.Select(window.Document.Find(encounter));
                    originalHealth = StatsService.RosterEntry("__map_editor/guard").Attributes.Health.Value;
                    originalExp = StatsService.RosterEntry("__map_editor/guard").Exp;
                    memory = JsonSerializer.Serialize(MapBattleStorage.Memory);
                    Press("TestBattle");
                    step = 5;
                    return;
                case 5:
                    if (MapBattleController.Current?.Session == null)
                    {
                        return;
                    }

                    Check(!window.Visible, "trial releases modal editor input");
                    Check(MapBattleController.Current.Session.State.VictoryTarget == "unit_3"
                        && MapBattleController.Current.Session.State.DefeatTarget == "unit_2",
                        "real trial uses authored outcome rules");
                    Shot("03-trial");
                    step++;
                    return;
                case 6:
                    var controller = MapBattleController.Current;
                    if (controller?.Session?.State.Outcome != null)
                    {
                        GameLogger.Log("TEST", "settlement before: outcome=" + controller.Session.State.Outcome
                            + ", reward=" + controller.Session.State.RewardExp
                            + ", roster=" + controller.Session.State.Units[0].RosterId
                            + ", blocked=" + RpgInput.IsWalkBlocked(controller)
                            + ", expected=" + (originalExp + 18)
                            + ", xp=" + StatsService.RosterEntry("__map_editor/guard").Exp);
                        controller.GetNode<Button>("Footer/Content/Actions/Continue").EmitSignal(Button.SignalName.Pressed);
                        GameLogger.Log("TEST", "settlement after: xp=" + StatsService.RosterEntry("__map_editor/guard").Exp
                            + ", active=" + (MapBattleStorage.Memory.Active != null));
                        Check(StatsService.RosterEntry("__map_editor/guard").Exp == originalExp + 18,
                            "actual settlement changes XP before the trial restores it");
                        step++;
                        return;
                    }

                    if (controller?.Session?.Active.Player == true)
                    {
                        Check(controller.Issue(MapBattleAi.Choose(controller.Session)), "player command uses runtime authority");
                    }

                    return;
                case 7:
                    if (!window.Visible || SceneManager.IsTestPlayActive)
                    {
                        return;
                    }

                    Check(JsonSerializer.Serialize(MapBattleStorage.Memory) == memory, "trial restores encounter and reward ledger");
                    Check(StatsService.RosterEntry("__map_editor/guard").Attributes.Health.Value == originalHealth
                        && StatsService.RosterEntry("__map_editor/guard").Exp == originalExp,
                        "trial restores logical health and experience");
                    Check(window.Selected.Id == encounter, "authored recipe and selection survive real shell reconstruction");
                    Press("TestBattle");
                    step++;
                    return;
                case 8:
                {
                        if (MapBattleController.Current?.Session == null)
                        {
                            return;
                        }

                        using var stopKey = new InputEventKey
                        {
                            PhysicalKeycode = Key.F8,
                            Keycode = Key.F8,
                            Pressed = true
                        };
                        GetViewport().PushInput(stopKey);
                        step++;
                        return;
                }
                case 9:
                    if (!window.Visible || SceneManager.IsTestPlayActive)
                    {
                        return;
                    }

                    Check(JsonSerializer.Serialize(MapBattleStorage.Memory) == memory, "F8 also restores the original envelope");
                    var saved = JsonSerializer.Deserialize<MapBattleDefinition>(JsonSerializer.Serialize(
                        Entities.Collect<MapBattleDefinition>()[encounter]));
                    Check(saved.Units.Count == 4 && saved.Abilities.Count == 2 && saved.RewardExp == 18
                        && saved.VictoryTarget == "unit_3" && saved.DefeatTarget == "unit_2",
                        "complete JSON round trip retains GUI authoring and outcome fields");
                    Shot("04-restored");
                    Press("TestBattle");
                    step++;
                    return;
                case 10:
                    if (MapBattleController.Current?.Session == null)
                    {
                        return;
                    }

                    window.EmitSignal(Window.SignalName.CloseRequested);
                    step++;
                    return;
                case 11:
                    if (GodotObject.IsInstanceValid(window) || SceneManager.IsTestPlayActive
                        || MapBattleController.Current?.Session != null || RpgWorldHost.Current?.View?.Presentation == null)
                    {
                        return;
                    }

                    Check(SceneManager.GetCurrentSceneName() == MapBattleEditorGuiFiles.Map,
                        "closing the trial window restores the original logical map");
                    Check(JsonSerializer.Serialize(MapBattleStorage.Memory) == memory
                        && StatsService.RosterEntry("__map_editor/guard").Attributes.Health.Value == originalHealth
                        && StatsService.RosterEntry("__map_editor/guard").Exp == originalExp,
                        "closing the trial window restores the full original reward and health state");
                    Check(!RpgInput.IsWalkBlocked(this), "closing the editor releases its modal input lease");
                    Finish(0);
                    return;
            }
        }

        private bool HasCell(string key, int value)
        {
            foreach (var cell in window.Selected.Data[key] as JsonArray ?? new JsonArray())
            {
                if (cell.GetValue<int>() == value)
                {
                    return true;
                }
            }

            return false;
        }

        private void CheckPreviewPixels()
        {
            using var image = ViewportCapture.Take(window);
            var canvas = Field<RpgEditorCanvas>(window, "RpgEditorCanvas");
            var point = canvas.GlobalPosition + canvas.CanvasPointAt(new Vector2(2.5f, 1.5f));
            var player = image.GetPixel(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y));
            Check(player.B > player.R * 2 && player.G > player.R,
                "rendered player marker moves to the clicked cell without another mouse event");
            point = canvas.GlobalPosition + canvas.CanvasPointAt(new Vector2(4.5f, 4.5f));
            var cover = image.GetPixel(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y));
            Check(cover.G > cover.R && cover.R > cover.B,
                "rendered cover marker updates without moving the mouse over the preview");
        }

        private void AddAbilities()
        {
            for (var i = 0; i < 2; i++)
            {
                Press("AddAbility");
                var row = Field<Control>(window, "Ability_ability_" + (i + 1));
                Pick(Field<OptionButton>(row, "ActionId"), i == 0 ? "__map_editor/strike" : "__map_editor/shot");
                Field<SpinBox>(row, "MaxRange").Value = i == 0 ? 1 : 6;
            }
        }

        private void AddUnits()
        {
            for (var i = 0; i < 4; i++)
            {
                Press("AddUnit");
                var row = Unit(i);
                var template = i == 0 ? "__map_editor/guard" : i == 1 ? "__map_editor/archer" : "__map_editor/enemy";
                Pick(Field<OptionButton>(row, "Template"), template);
                Field<CheckBox>(row, "Player").ButtonPressed = i < 2;
                Field<SpinBox>(row, "X").Value = i < 2 ? 1 : 6;
                Field<SpinBox>(row, "Y").Value = i % 2 + 2;
                Field<SpinBox>(row, "Speed").Value = 40 - i * 10;
                if (i < 2)
                {
                    var roster = Field<LineEdit>(row, "RosterId");
                    roster.Text = template;
                    roster.EmitSignal(LineEdit.SignalName.TextChanged, template);
                }

                Field<CheckBox>(row, i % 2 == 0 ? "Ability_ability_1" : "Ability_ability_2").ButtonPressed = true;
            }
        }

        private Control Unit(int index)
        {
            return Field<Control>(window, "Unit_unit_" + (index + 1));
        }

        private static T Field<T>(Node root, string name) where T : Node
        {
            return root.FindChild(name, true, false) as T ?? throw new InvalidOperationException("Missing GUI " + name);
        }

        private void Press(string name)
        {
            Field<Button>(window, name).EmitSignal(Button.SignalName.Pressed);
        }

        private static void Pick(OptionButton picker, string value)
        {
            for (var i = 0; i < picker.ItemCount; i++)
            {
                if (picker.GetItemText(i) == value || picker.GetItemMetadata(i).AsString() == value)
                {
                    picker.Select(i);
                    picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)i);
                    return;
                }
            }

            throw new InvalidOperationException("Missing choice: " + value);
        }

        private void ClickCell(int x, int y)
        {
            var canvas = Field<RpgEditorCanvas>(window, "RpgEditorCanvas");
            var point = canvas.GlobalPosition + canvas.CanvasPointAt(new Vector2(x + .5f, y + .5f));
            using var motion = new InputEventMouseMotion { Position = point };
            canvas.GetViewport().PushInput(motion, true);
            using var press = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Position = point,
                Pressed = true
            };
            canvas.GetViewport().PushInput(press, true);
            using var release = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Position = point,
                Pressed = false
            };
            canvas.GetViewport().PushInput(release, true);
        }

        private void Shot(string name)
        {
            var path = OS.GetEnvironment("MAP_BATTLE_EDITOR_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = Path.Combine(path, projection);
            Directory.CreateDirectory(path);
            var viewport = GodotObject.IsInstanceValid(window) && window.Visible ? (Viewport)window : GetViewport();
            Check(ViewportCapture.SavePng(viewport, Path.Combine(path, name + ".png")), "capture " + name);
        }

        private void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }

            passed++;
            GameLogger.Log("TEST", "[PASS] " + message);
        }

        private void Finish(int failed)
        {
            step = -1;
            if (GodotObject.IsInstanceValid(window))
            {
                window.Free();
            }

            files?.Dispose();
            files = null;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=map_battle_editor passed={passed} failed={failed}");
            ApplicationExit.Quit(GetTree(), failed);
        }
    }
}
