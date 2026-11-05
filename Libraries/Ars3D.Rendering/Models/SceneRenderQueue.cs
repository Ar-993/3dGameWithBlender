using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ars3D.Rendering;

/// <summary>
/// Собирает геометрию всего кадра. Ресурсы остаются у моделей; матрицы каждого
/// экземпляра копируются, чтобы общая модель могла встречаться в кадре много раз.
/// </summary>
public sealed class SceneRenderQueue
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly List<QueuedMesh> meshPool = [];
    private readonly List<QueuedMesh> transparentMeshes = [];

    public int Count { get; private set; }
    public int OpaqueCount { get; private set; }
    public int TransparentCount => Count - OpaqueCount;

    public SceneRenderQueue(GraphicsDevice graphicsDevice)
    {
        this.graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    // Память сохраняем для следующего кадра. В текущем кадре используются
    // только первые Count записей meshPool, а не весь список.
    public void Clear()
    {
        Count = 0;
        OpaqueCount = 0;
        transparentMeshes.Clear();
    }

    internal void Add(VertexBuffer vertices, IndexBuffer indices, Effect effect,
        Texture2D texture, Matrix world, Matrix[] bones, BoundingBox localBounds,
        bool skinned, bool transparent, float opacity, float alphaCutoff)
    {
        if (vertices.GraphicsDevice != graphicsDevice)
            throw new ArgumentException("Модель и очередь должны использовать одно GraphicsDevice.");

        if (Count == meshPool.Count)
            meshPool.Add(new QueuedMesh());

        QueuedMesh mesh = meshPool[Count];
        mesh.Vertices = vertices;
        mesh.Indices = indices;
        mesh.Effect = effect;
        mesh.Texture = texture;
        mesh.World = world;
        mesh.LocalBounds = localBounds;
        mesh.Skinned = skinned;
        mesh.Transparent = transparent;
        mesh.Opacity = opacity;
        mesh.AlphaCutoff = alphaCutoff;
        mesh.SubmissionIndex = Count;

        // У каждого экземпляра своя копия позы, даже если модель у них общая.
        if (mesh.Bones.Length != bones.Length)
            mesh.Bones = new Matrix[bones.Length];
        Array.Copy(bones, mesh.Bones, bones.Length);

        Count++;
        if (!transparent)
            OpaqueCount++;
    }

    public void DrawDepth(Matrix view, Matrix projection, SceneLighting lighting,
        Effect depthEffect, float depthFar)
    {
        ArgumentNullException.ThrowIfNull(depthEffect);
        if (!float.IsFinite(depthFar) || depthFar <= 0f)
            throw new ArgumentOutOfRangeException(nameof(depthFar));
        DrawPass(view, projection, lighting, GeometryPass.Depth, depthEffect, depthFar);
    }

    public void DrawOpaque(Matrix view, Matrix projection, SceneLighting lighting)
    {
        DrawPass(view, projection, lighting, GeometryPass.Opaque);
    }

    public void DrawTransparent(Matrix view, Matrix projection, SceneLighting lighting)
    {
        DrawPass(view, projection, lighting, GeometryPass.Transparent);
    }

    private void DrawPass(Matrix view, Matrix projection, SceneLighting lighting,
        GeometryPass geometryPass, Effect? depthEffect = null, float depthFar = 0f)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        BlendState previousBlend = graphicsDevice.BlendState;
        DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
        RasterizerState previousRasterizer = graphicsDevice.RasterizerState;
        Vector3 cameraPosition = Matrix.Invert(view).Translation;

        try
        {
            SetRenderStates(geometryPass);

            if (geometryPass == GeometryPass.Transparent)
            {
                PrepareTransparentMeshes(view, lighting, cameraPosition);
                foreach (QueuedMesh mesh in transparentMeshes)
                    DrawMesh(mesh, view, projection, cameraPosition, lighting, geometryPass);
            }
            else
            {
                DrawOpaqueMeshes(view, projection, cameraPosition, lighting,
                    geometryPass, depthEffect, depthFar);
            }
        }
        finally
        {
            graphicsDevice.BlendState = previousBlend;
            graphicsDevice.DepthStencilState = previousDepth;
            graphicsDevice.RasterizerState = previousRasterizer;
        }
    }

    private void SetRenderStates(GeometryPass geometryPass)
    {
        if (geometryPass == GeometryPass.Transparent)
        {
            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        }
        else
        {
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
    }

    private void DrawOpaqueMeshes(Matrix view, Matrix projection, Vector3 cameraPosition,
        SceneLighting lighting, GeometryPass geometryPass, Effect? depthEffect, float depthFar)
    {
        // И глубина, и непрозрачный цвет рисуют один и тот же набор мешей.
        for (int index = 0; index < Count; index++)
        {
            QueuedMesh mesh = meshPool[index];
            if (mesh.Transparent || IsHiddenByFog(mesh, lighting, cameraPosition))
                continue;

            DrawMesh(mesh, view, projection, cameraPosition, lighting,
                geometryPass, depthEffect, depthFar);
        }
    }

    private void PrepareTransparentMeshes(Matrix view, SceneLighting lighting, Vector3 cameraPosition)
    {
        transparentMeshes.Clear();
        for (int index = 0; index < Count; index++)
        {
            QueuedMesh mesh = meshPool[index];
            if (!mesh.Transparent || IsHiddenByFog(mesh, lighting, cameraPosition))
                continue;

            Vector3 localCenter = (mesh.LocalBounds.Min + mesh.LocalBounds.Max) * 0.5f;
            Vector3 worldCenter = Vector3.Transform(localCenter, mesh.World);
            Vector3 viewCenter = Vector3.Transform(worldCenter, view);
            mesh.ViewDepth = -viewCenter.Z;
            transparentMeshes.Add(mesh);
        }
        transparentMeshes.Sort(CompareBackToFront);
    }

    private static int CompareBackToFront(QueuedMesh first, QueuedMesh second)
    {
        // Отрицательное число ставит first раньше second.
        // Сравниваем в обратном порядке: дальний меш должен рисоваться первым.
        int depthOrder = second.ViewDepth.CompareTo(first.ViewDepth);
        if (depthOrder != 0)
            return depthOrder;

        // На одинаковой глубине сохраняем порядок добавления в очередь.
        return first.SubmissionIndex.CompareTo(second.SubmissionIndex);
    }

    private static bool IsHiddenByFog(QueuedMesh mesh, SceneLighting lighting, Vector3 cameraPosition)
    {
        if (mesh.Skinned)
            return false;

        return lighting.Fog.IsFullyFogged(mesh.LocalBounds, mesh.World, cameraPosition);
    }

    private void DrawMesh(QueuedMesh mesh, Matrix view, Matrix projection, Vector3 cameraPosition,
        SceneLighting lighting, GeometryPass geometryPass, Effect? depthEffect = null, float depthFar = 0f)
    {
        Effect effect;
        if (geometryPass == GeometryPass.Depth)
        {
            ArgumentNullException.ThrowIfNull(depthEffect);
            effect = depthEffect;
        }
        else
            effect = mesh.Effect;

        SetShaderParameters(mesh, effect, view, projection, cameraPosition,
            lighting, geometryPass, depthFar);

        graphicsDevice.SetVertexBuffer(mesh.Vertices);
        graphicsDevice.Indices = mesh.Indices;
        foreach (EffectPass effectPass in effect.CurrentTechnique.Passes)
        {
            effectPass.Apply();
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                baseVertex: 0, startIndex: 0, primitiveCount: mesh.Indices.IndexCount / 3);
        }
    }

    private static void SetShaderParameters(QueuedMesh mesh, Effect effect, Matrix view,
        Matrix projection, Vector3 cameraPosition, SceneLighting lighting,
        GeometryPass geometryPass, float depthFar)
    {
        EffectParameterCollection parameters = effect.Parameters;
        parameters["World"]?.SetValue(mesh.World);
        parameters["View"]?.SetValue(view);
        parameters["Projection"]?.SetValue(projection);
        parameters["CameraPosition"]?.SetValue(cameraPosition);
        parameters["Bones"]?.SetValue(mesh.Bones);
        parameters["ModelTexture"]?.SetValue(mesh.Texture);
        parameters["AlphaCutoff"]?.SetValue(mesh.AlphaCutoff);

        if (geometryPass == GeometryPass.Depth)
        {
            // Этот шейдер пишет расстояние, поэтому свет и цвет тумана ему не нужны.
            parameters["DepthFar"]?.SetValue(depthFar);
            return;
        }

        float alpha = 1f;
        if (mesh.Transparent)
            alpha = mesh.Opacity;
        parameters["Alpha"]?.SetValue(alpha);
        parameters["UseTexture"]?.SetValue(1f);
        lighting.Apply(parameters);
        lighting.Fog.Apply(parameters);

        // Непрозрачный цвет получит туман позже, отдельным проходом.
        // Стеклу нужен туман по его собственному расстоянию.
        float fogEnabled = 0f;
        if (geometryPass == GeometryPass.Transparent && lighting.Fog.Enabled)
            fogEnabled = 1f;
        parameters["FogEnabled"]?.SetValue(fogEnabled);
    }

    private enum GeometryPass
    {
        Depth,
        Opaque,
        Transparent
    }

    private sealed class QueuedMesh
    {
        public VertexBuffer Vertices = null!;
        public IndexBuffer Indices = null!;
        public Effect Effect = null!;
        public Texture2D Texture = null!;
        public Matrix World;
        public Matrix[] Bones = [];
        public BoundingBox LocalBounds;
        public bool Skinned;
        public bool Transparent;
        public float Opacity;
        public float AlphaCutoff;
        public float ViewDepth;
        public int SubmissionIndex;
    }
}