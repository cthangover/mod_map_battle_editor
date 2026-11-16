using System;
using System.Text.Json.Nodes;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Rpg;
using Cthangover.Mods.Rpg.Render;
using Cthangover.Tools.RpgEditor;
using Godot;

namespace Cthangover.Tools.MapBattleEditor
{
    /// <summary>Owns a detached ordinary map canvas and texture cache; clicks use the renderer's actual inverse projection.</summary>
    public partial class MapBattleEditorPreview : ModWidget
    {
        private RpgEditorCanvas canvas;
        private RpgTextures textures;
        private RpgRenderSceneBuilder builder;
        private string map;

        /// <summary>Detached collision snapshot used by numeric cell validation as well as the canvas.</summary>
        public RpgEditorScene Scene { get; private set; }

        /// <summary>Reports a left click in map coordinates; the encounter form chooses placement or mask painting.</summary>
        public event Action<Vector2I> Picked;

        /// <summary>Builds the borrowed map-editor widget under this utility root.</summary>
        protected override void Construct()
        {
            Name = "Preview";
            CustomMinimumSize = new Vector2(500, 280);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            textures = new RpgTextures();
            builder = new RpgRenderSceneBuilder(textures.Image, textures.Charset);
            canvas = new RpgEditorCanvas();
            AddChild(canvas);
            canvas.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            canvas.CellPressed += OnCell;
        }

        /// <summary>Rebuilds geometry only when the selected map changes; form edits update marks without reallocating scenery.</summary>
        public void ShowRecipe(JsonObject data, string selectedUnit = null)
        {
            var id = data["Map"]?.ToString();
            if (id != map)
            {
                map = id;
                Scene = MapBattleEditorMap.Read(id);
                canvas.SetScene(Scene == null ? null : builder.Build(Scene.Map, Scene.Cells, Scene.Statics));
                canvas.FitToView();
            }

            canvas.Marks.Clear();
            if (Scene == null)
            {
                return;
            }

            for (var y = 0; y < Scene.Map.Height; y++)
            {
                for (var x = 0; x < Scene.Map.Width; x++)
                {
                    if (!Scene.Walk.IsWalkable(x, y))
                    {
                        Mark(y * Scene.Map.Width + x, new Color(.5f, .52f, .56f, .5f));
                    }
                }
            }

            AddMask(data["Opaque"] as JsonArray, new Color(.8f, .65f, .1f, .4f));
            AddMask(data["Cover"] as JsonArray, new Color(.4f, .8f, .2f, .35f));
            foreach (var item in data["Units"] as JsonArray ?? new JsonArray())
            {
                var cell = item["Cell"]?.GetValue<int>() ?? -1;
                var player = item["Player"]?.GetValue<bool>() ?? false;
                Mark(cell, player ? new Color(.15f, .65f, 1, .8f) : new Color(1, .2f, .15f, .8f));
                if (cell >= 0 && cell < Scene.Map.Width * Scene.Map.Height)
                {
                    var color = item["Id"]?.ToString() == selectedUnit ? Colors.Gold : new Color(.75f, .8f, .9f);
                    canvas.Marks.Add(new CanvasRect(cell % Scene.Map.Width, cell / Scene.Map.Width, 1, 1, color, false));
                }
            }

            canvas.Refresh();
        }

        private void AddMask(JsonArray array, Color color)
        {
            foreach (var item in array ?? new JsonArray())
            {
                Mark(item.GetValue<int>(), color);
            }
        }

        private void Mark(int cell, Color color)
        {
            if (cell >= 0 && cell < Scene.Map.Width * Scene.Map.Height)
            {
                canvas.Marks.Add(new CanvasRect(cell % Scene.Map.Width, cell / Scene.Map.Width, 1, 1, color, true));
            }
        }

        /// <summary>Changes only the local view; cells and rules remain in map coordinates.</summary>
        public void SetProjection(string id)
        {
            canvas.SetProjection(id);
            canvas.FitToView();
        }

        private void OnCell(Vector2I cell, MouseButton button, bool doubleClick)
        {
            if (button == MouseButton.Left)
            {
                Picked?.Invoke(cell);
            }
        }

        /// <summary>Idempotently releases the canvas before its cache; safe during close and shutdown.</summary>
        public void Release()
        {
            if (canvas != null)
            {
                canvas.SetScene(null);
                canvas = null;
            }

            builder?.Dispose();
            builder = null;
            textures?.Dispose();
            textures = null;
        }

        /// <summary>Releases only this preview's resources when its owning tool leaves the tree.</summary>
        protected override void Destruct()
        {
            Release();
        }
    }
}
