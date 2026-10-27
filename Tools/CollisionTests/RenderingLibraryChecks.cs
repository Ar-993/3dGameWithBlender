using Ars3D.ModelFormat;
using Ars3D.Rendering;

internal static class RenderingLibraryChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("rendering is a public library independent of game and item logic", () =>
        {
            var rendering = typeof(CompiledModel).Assembly;
            Require(rendering.GetName().Name == "Ars3D.Rendering" &&
                rendering != typeof(Player).Assembly,
                "Rendering code is still compiled into the game assembly");

            string[] references = rendering.GetReferencedAssemblies()
                .Select(reference => reference.Name ?? "").ToArray();
            Require(!references.Any(name => name == "3DLight" || name == "Ars3D.Items" ||
                name.StartsWith("Assimp", StringComparison.OrdinalIgnoreCase)),
                "The rendering library depends on game, item or import logic");

            Type[] publicTypes =
            [
                typeof(CompiledModel), typeof(ModelAssetLoader), typeof(ModelHierarchy),
                typeof(BonePose), typeof(AnimationSampler), typeof(SceneLighting), typeof(SceneFog)
            ];
            Require(publicTypes.All(type => type.IsPublic && type.Assembly == rendering),
                "The rendering API is not available from the standalone assembly");
            Require(typeof(ModelData).IsPublic && typeof(ModelDataIo).IsPublic,
                "The model format is hidden behind game-only access");
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
