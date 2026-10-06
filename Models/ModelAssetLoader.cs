using _3DLight.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Models;

internal static class ModelAssetLoader
{
    public static CompiledModel Load(
        GraphicsDevice graphicsDevice,
        string modelName,
        Texture2D texture,
        Effect toonEffect,
        string? textureFolderName = null)
    {
        string modelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Models",
            modelName + ".3dmodel");

        using FileStream modelStream = File.OpenRead(modelPath);
        ModelData modelData = ModelDataIo.Read(modelStream);

        return CreateModel(
            graphicsDevice,
            modelData,
            texture,
            toonEffect,
            textureFolderName ?? modelName);
    }

    public static CompiledModel LoadFromBytes(
        GraphicsDevice graphicsDevice,
        byte[] modelBytes,
        string modelName,
        Texture2D texture,
        Effect toonEffect,
        string? textureFolderName = null)
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
            textureFolderName ?? modelName);
    }

    private static CompiledModel CreateModel(
        GraphicsDevice graphicsDevice,
        ModelData modelData,
        Texture2D fallbackTexture,
        Effect toonEffect,
        string textureFolderName)
    {
        Dictionary<string, Texture2D> materialTextures =
            LoadMaterialTextures(graphicsDevice, textureFolderName);
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
        string textureFolderName)
    {
        var textures = new Dictionary<string, Texture2D>(
            StringComparer.OrdinalIgnoreCase);
        string directory = Path.Combine(
            AppContext.BaseDirectory, "Content", "Assets", textureFolderName);

        if (!Directory.Exists(directory))
            return textures;

        try
        {
            foreach (string path in Directory.EnumerateFiles(directory)
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
                $"Loaded {textures.Count} material textures from '{directory}'.");
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
