using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Ars3D.Rendering;

/// <summary>Owns the frame targets and executes geometry, fog, transparency and post passes.</summary>
public sealed class SceneRenderer : IDisposable
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly Effect depthEffect;
    private readonly Effect fogEffect;
    private readonly Effect postEffect;
    private readonly SpriteBatch spriteBatch;
    private readonly BlendState fogBlend = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One
    };
    private RenderTarget2D depthTarget = null!;
    private RenderTarget2D sceneTarget = null!;
    private RenderTarget2D postTarget = null!;
    private int requestedSamples = -1;
    private bool disposed;

    public SceneRenderQueue Queue { get; }
    public PostProcessSettings PostProcessing { get; } = new();

    public SceneRenderer(GraphicsDevice graphicsDevice, ContentManager content)
    {
        this.graphicsDevice = graphicsDevice;
        // Effects belong to the caller's ContentManager, unlike our targets and SpriteBatch.
        depthEffect = content.Load<Effect>("DepthShader");
        fogEffect = content.Load<Effect>("FogShader");
        postEffect = content.Load<Effect>("PostProcess");
        Queue = new SceneRenderQueue(graphicsDevice);
        spriteBatch = new SpriteBatch(graphicsDevice);
        try
        {
            EnsureTargets(fogEnabled: true);
        }
        catch
        {
            spriteBatch.Dispose();
            fogBlend.Dispose();
            throw;
        }
    }

    /// <summary>Draws queued objects to the back buffer. Draw the HUD after this returns.</summary>
    public void Draw(Matrix view, Matrix projection, SceneLighting lighting)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(lighting);
        if (graphicsDevice.GetRenderTargets().Length != 0)
            throw new InvalidOperationException("SceneRenderer.Draw must start on the back buffer.");

        EnsureTargets(lighting.Fog.Enabled);
        graphicsDevice.Textures[0] = null;
        try
        {
            // The distance texture is sampled by fog. Its hardware depth buffer only
            // chooses the nearest opaque surface; it is not itself sampled as a texture.
            if (lighting.Fog.Enabled)
            {
                graphicsDevice.SetRenderTarget(depthTarget);
                graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer,
                    Color.White, 1f, 0);
                Queue.DrawDepth(view, projection, lighting, depthEffect,
                    lighting.Fog.EffectiveEndDistance);
            }

            graphicsDevice.SetRenderTarget(sceneTarget);
            graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer,
                lighting.Fog.Enabled ? lighting.Fog.Color : new Color(25, 30, 40), 1f, 0);
            Queue.DrawOpaque(view, projection, lighting);

            if (lighting.Fog.Enabled)
            {
                lighting.Fog.Apply(fogEffect.Parameters);
                fogEffect.Parameters["DepthFar"].SetValue(lighting.Fog.EffectiveEndDistance);
                // Fog emits (fogColor, fogAmount) and alpha blends over sceneTarget.
                // It reads only depthTarget, so sceneTarget can remain bound throughout
                // opaque -> fog -> transparent, preserving the opaque depth buffer.
                DrawFullscreen(depthTarget, fogEffect, fogBlend,
                    SamplerState.PointClamp);
            }

            Queue.DrawTransparent(view, projection, lighting);

            // Never sample the target currently being written. Further image effects
            // can alternate sceneTarget and postTarget before the final presentation.
            graphicsDevice.Textures[0] = null;
            graphicsDevice.SetRenderTarget(postTarget);
            PostProcessing.Apply(postEffect, (float)postTarget.Width / postTarget.Height);
            DrawFullscreen(sceneTarget, postEffect, BlendState.Opaque, SamplerState.LinearClamp);

            graphicsDevice.Textures[0] = null;
            graphicsDevice.SetRenderTarget(null);
            DrawFullscreen(postTarget, null, BlendState.Opaque, SamplerState.LinearClamp);
        }
        finally
        {
            graphicsDevice.Textures[0] = null;
            graphicsDevice.SetRenderTarget(null);
        }
    }

    private void DrawFullscreen(Texture2D source, Effect? effect, BlendState blend,
        SamplerState sampler)
    {
        spriteBatch.Begin(SpriteSortMode.Deferred, blend, sampler,
            DepthStencilState.None, RasterizerState.CullNone, effect);
        spriteBatch.Draw(source, graphicsDevice.Viewport.Bounds, Color.White);
        spriteBatch.End();
    }

    private void EnsureTargets(bool fogEnabled)
    {
        // Read the back-buffer viewport, never the size of one of our intermediate targets.
        int width = Math.Max(1, graphicsDevice.Viewport.Width);
        int height = Math.Max(1, graphicsDevice.Viewport.Height);
        // Packed single-sample distance and opaque color must describe the same
        // coverage. MSAA color with single-sample depth would erase silhouette edges
        // in the fog overlay. Keep hardware MSAA for frames that do not need fog.
        int samples = fogEnabled ? 0 : graphicsDevice.PresentationParameters.MultiSampleCount;
        if (sceneTarget is not null && !sceneTarget.IsDisposed &&
            sceneTarget.Width == width && sceneTarget.Height == height &&
            requestedSamples == samples)
            return;

        RenderTarget2D? newDepth = null;
        RenderTarget2D? newScene = null;
        RenderTarget2D? newPost = null;
        try
        {
            // Packed distances must not be averaged by MSAA resolves or linear filtering.
            newDepth = new RenderTarget2D(graphicsDevice, width, height, false,
                SurfaceFormat.Color, DepthFormat.Depth24);
            newScene = new RenderTarget2D(graphicsDevice, width, height, false,
                SurfaceFormat.Color, DepthFormat.Depth24, samples, RenderTargetUsage.DiscardContents);
            newPost = new RenderTarget2D(graphicsDevice, width, height, false,
                SurfaceFormat.Color, DepthFormat.None);
        }
        catch
        {
            newPost?.Dispose();
            newScene?.Dispose();
            newDepth?.Dispose();
            throw;
        }

        graphicsDevice.Textures[0] = null;
        depthTarget?.Dispose();
        sceneTarget?.Dispose();
        postTarget?.Dispose();
        depthTarget = newDepth;
        sceneTarget = newScene;
        postTarget = newPost;
        requestedSamples = samples;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Queue.Clear();
        depthTarget.Dispose();
        sceneTarget.Dispose();
        postTarget.Dispose();
        spriteBatch.Dispose();
        fogBlend.Dispose();
    }
}