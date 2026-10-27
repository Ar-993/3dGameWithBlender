using _3DLight.Items;
using Ars3D.Items;
using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Ui;

// Owns only the thumbnail textures. Models are borrowed from the item catalog.
internal sealed class InventoryIconCache : IDisposable
{
    private readonly GraphicsDevice graphics;
    private readonly Dictionary<CompiledModel, RenderTarget2D> icons = [];

    public InventoryIconCache(GraphicsDevice graphics)
    {
        this.graphics = graphics;
    }

    public void Prepare(WorldItem? item)
    {
        if (item is null || item.Renderer is null)
            return;

        CompiledModel model = item.Renderer.Model;
        if (icons.ContainsKey(model))
            return;

        icons.Add(model, CreateIcon(item));
    }

    public Texture2D? Get(WorldItem item)
    {
        if (item.Renderer is null)
            return null;

        icons.TryGetValue(item.Renderer.Model, out RenderTarget2D? icon);
        return icon;
    }

    private RenderTarget2D CreateIcon(WorldItem item)
    {
        var icon = new RenderTarget2D(graphics, 96, 96,
            false, SurfaceFormat.Color, DepthFormat.Depth24);
        var previousState = new SavedGraphicsState(graphics);

        try
        {
            DrawModel(icon, item);
            return icon;
        }
        catch
        {
            icon.Dispose();
            throw;
        }
        finally
        {
            previousState.Restore(graphics);
        }
    }

    private void DrawModel(RenderTarget2D icon, WorldItem item)
    {
        graphics.SetRenderTarget(icon);
        graphics.Clear(Color.Transparent);
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.RasterizerState = RasterizerState.CullNone;
        graphics.BlendState = BlendState.NonPremultiplied;

        float radius = ItemMath.ToGame(item.State.Definition.HalfExtents).Length();
        Vector3 camera = Vector3.Normalize(new Vector3(3f, 1.6f, 1.5f)) * radius * 4f;
        Matrix view = Matrix.CreateLookAt(camera, Vector3.Zero, Vector3.Up);
        Matrix projection = Matrix.CreateOrthographic(
            radius * 2.3f, radius * 2.3f, 0.001f, radius * 10f);

        var pose = new ItemPose(new System.Numerics.Vector3(0f, -item.Height * 0.5f, 0f),
            System.Numerics.Quaternion.Identity);
        var lighting = new SceneLighting { Enabled = false };
        item.Renderer!.Draw(pose, view, projection, lighting);
    }

    public void Dispose()
    {
        foreach (RenderTarget2D icon in icons.Values)
            icon.Dispose();
        icons.Clear();
    }

    // Rendering into a texture temporarily changes the device state.
    private sealed class SavedGraphicsState
    {
        private readonly RenderTargetBinding[] targets;
        private readonly Viewport viewport;
        private readonly BlendState blend;
        private readonly DepthStencilState depth;
        private readonly RasterizerState rasterizer;
        private readonly SamplerState sampler;

        public SavedGraphicsState(GraphicsDevice graphics)
        {
            targets = graphics.GetRenderTargets();
            viewport = graphics.Viewport;
            blend = graphics.BlendState;
            depth = graphics.DepthStencilState;
            rasterizer = graphics.RasterizerState;
            sampler = graphics.SamplerStates[0];
        }

        public void Restore(GraphicsDevice graphics)
        {
            graphics.SetRenderTargets(targets);
            graphics.Viewport = viewport;
            graphics.BlendState = blend;
            graphics.DepthStencilState = depth;
            graphics.RasterizerState = rasterizer;
            graphics.SamplerStates[0] = sampler;
        }
    }
}
