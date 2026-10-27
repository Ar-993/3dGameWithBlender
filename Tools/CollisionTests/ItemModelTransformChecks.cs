using _3DLight;
using Ars3D.ModelFormat;
using Ars3D.Rendering;
using _3DLight.Models;
using Microsoft.Xna.Framework;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsVector = System.Numerics.Vector3;

internal static class ItemModelTransformChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("item model fitting respects static parent scale, rotation and translation", () =>
        {
            var data = new ModelData();
            data.Nodes.Add(new NodeData("Root", -1,
                NumericsMatrix.CreateScale(2f) *
                NumericsMatrix.CreateRotationZ(MathF.PI / 2f) *
                NumericsMatrix.CreateTranslation(10f, 4f, -6f)));
            data.Nodes.Add(new NodeData("Mesh", 0, NumericsMatrix.CreateTranslation(2f, 3f, 1f)));
            data.Meshes.Add(new MeshData
            {
                Node = 1,
                Vertices = [Vertex(new(-1f, -1f, -1f)), Vertex(new(1f, 3f, 1f))]
            });

            Matrix fit = ItemModelTransform.FitToHeight(data, new ModelHierarchy(data), 0.5f);
            // The two imported points render at (6, 6, -6) and (-2, 10, -2).
            Near(Vector3.Transform(new Vector3(6f, 6f, -6f), fit), new(0.5f, 0f, -0.25f));
            Near(Vector3.Transform(new Vector3(-2f, 10f, -2f), fit), new(-0.5f, 0.5f, 0.25f));
        });

        run("item model fitting blends skin influences and removes the root bind transform", () =>
        {
            var data = new ModelData();
            data.Nodes.Add(new NodeData("Root", -1, NumericsMatrix.CreateTranslation(100f, 20f, -30f)));
            data.Nodes.Add(new NodeData("BoneA", 0,
                NumericsMatrix.CreateScale(2f) * NumericsMatrix.CreateTranslation(4f, 6f, 8f)));
            data.Nodes.Add(new NodeData("BoneB", 0, NumericsMatrix.CreateTranslation(8f, 2f, -4f)));
            data.Nodes.Add(new NodeData("Mesh", 0, NumericsMatrix.CreateTranslation(500f, 500f, 500f)));
            data.Meshes.Add(new MeshData
            {
                Node = 3,
                Bones =
                [
                    new BoneData(1, NumericsMatrix.CreateTranslation(-2f, -1f, -3f)),
                    new BoneData(2, NumericsMatrix.Identity)
                ],
                Vertices = [SkinnedVertex(new(-1f, 1f, -2f)), SkinnedVertex(new(3f, 5f, 4f))]
            });

            Matrix fit = ItemModelTransform.FitToHeight(data, new ModelHierarchy(data), 0.5f);
            // 25% of BoneA and 75% of BoneB render these points in model-world space.
            Near(Vector3.Transform(new Vector3(4.75f, 3.75f, -5f), fit), new(-0.25f, 0f, -0.375f));
            Near(Vector3.Transform(new Vector3(9.75f, 8.75f, 2.5f), fit), new(0.25f, 0.5f, 0.375f));
        });

        run("item model fitting rejects invalid target heights", () =>
        {
            var data = new ModelData();
            var hierarchy = new ModelHierarchy(data);
            foreach (float invalidHeight in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Throws<ArgumentOutOfRangeException>(() => ItemModelTransform.FitToHeight(data, hierarchy, invalidHeight));
        });

        run("item model fitting rejects empty and flat geometry", () =>
        {
            var data = new ModelData();
            data.Nodes.Add(new NodeData("Root", -1, NumericsMatrix.Identity));
            Throws<InvalidDataException>(() => ItemModelTransform.FitToHeight(data, new ModelHierarchy(data), 0.5f));
            data.Meshes.Add(new MeshData { Vertices = [Vertex(new(0f, 2f, 0f)), Vertex(new(1f, 2f, 1f))] });
            Throws<InvalidDataException>(() => ItemModelTransform.FitToHeight(data, new ModelHierarchy(data), 0.5f));
        });
    }

    private static VertexData Vertex(NumericsVector position) =>
        new(position, NumericsVector.UnitY, System.Numerics.Vector2.Zero, 0, 0, 0, 0, System.Numerics.Vector4.UnitX);

    private static VertexData SkinnedVertex(NumericsVector position) =>
        new(position, NumericsVector.UnitY, System.Numerics.Vector2.Zero, 0, 1, 0, 0, new(0.25f, 0.75f, 0f, 0f));

    private static void Near(Vector3 actual, Vector3 expected)
    {
        if (Vector3.Distance(actual, expected) > 0.0001f)
            throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }
}
