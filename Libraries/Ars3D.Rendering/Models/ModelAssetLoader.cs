using Ars3D.ModelFormat;
using Microsoft.Xna.Framework.Graphics;

namespace Ars3D.Rendering;

public static class ModelAssetLoader
{
    public static CompiledModel Load(
        GraphicsDevice graphicsDevice,
        string modelPath,
        Texture2D texture,
        Effect toonEffect,
        string textureDirectory)
    {
        using FileStream modelStream = File.OpenRead(modelPath);
        ModelData modelData = ModelDataIo.Read(modelStream);

        return CreateModel(
            graphicsDevice,
            modelData,
            texture,
            toonEffect,
            textureDirectory);
    }

    public static CompiledModel LoadFromBytes(
        GraphicsDevice graphicsDevice,
        byte[] modelBytes,
        Texture2D texture,
        Effect toonEffect,
        string textureDirectory)
    {
        using var modelStream = new MemoryStream(
            modelBytes,
            writable: false);
        ModelData modelData = ModelDataIo.Read(modelStream);

        return CreateModel(
            graphicsDevice,
            modelData,
            texture,
            toonEffect,
            textureDirectory);
    }

    private static CompiledModel CreateModel(
        GraphicsDevice graphicsDevice,
        ModelData modelData,
        Texture2D fallbackTexture,
        Effect toonEffect,
        string textureDirectory)
    {
        Dictionary<string, Texture2D> materialTextures =
            LoadMaterialTextures(graphicsDevice, textureDirectory);
        try
        {
            Texture2D[] meshTextures = modelData.Meshes
                .Select(mesh => ResolveMeshTexture(
                    mesh, materialTextures, fallbackTexture))
                .ToArray();

            // On success the model owns these loaded textures.
            return new CompiledModel(
                graphicsDevice,
                modelData,
                meshTextures,
                materialTextures.Values.ToArray(),
                toonEffect);
        }
        catch
        {
            // Construction did not transfer ownership to a model.
            foreach (Texture2D texture in materialTextures.Values)
                texture.Dispose();
            throw;
        }
    }

    private static Dictionary<string, Texture2D> LoadMaterialTextures(
        GraphicsDevice graphicsDevice,
        string textureDirectory)
    {
        var textures = new Dictionary<string, Texture2D>(
            StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(textureDirectory))
            return textures;

        try
        {
            foreach (string path in Directory.EnumerateFiles(textureDirectory)
                         .Where(path => Path.GetExtension(path).ToLowerInvariant()
                         is ".png" or ".jpg" or ".jpeg")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                using FileStream stream = File.OpenRead(path);
                Texture2D loaded = Texture2D.FromStream(graphicsDevice, stream);
                string name = Path.GetFileName(path);

                if (textures.TryGetValue(name, out Texture2D? previous))
                    previous.Dispose();
                textures[name] = loaded;
            }

            Console.WriteLine(
                $"Loaded {textures.Count} material textures from '{textureDirectory}'.");
            return textures;
        }
        catch
        {
            foreach (Texture2D texture in textures.Values)
                texture.Dispose();
            throw;
        }
    }

    private static Texture2D ResolveMeshTexture(
        MeshData mesh,
        IReadOnlyDictionary<string, Texture2D> materialTextures,
        Texture2D fallbackTexture)
    {
        if (!string.IsNullOrWhiteSpace(mesh.TextureName) &&
            materialTextures.TryGetValue(mesh.TextureName, out Texture2D? texture))
        {
            return texture;
        }

        if (!string.IsNullOrWhiteSpace(mesh.TextureName))
        {
            Console.WriteLine(
                $"Texture '{mesh.TextureName}' required by mesh '{mesh.Name}' " +
                "was not found; using the fallback texture.");
        }

        return fallbackTexture;
    }

}
