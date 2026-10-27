namespace _3DLight.Models;

// Структура игрового Content задаётся здесь; библиотека рендера получает готовые пути.
internal static class ModelPaths
{
    public static string ModelFile(string modelName) =>
        Path.Combine(AppContext.BaseDirectory, "Content", "Models", modelName + ".3dmodel");

    public static string TextureDirectory(string folderName) =>
        Path.Combine(AppContext.BaseDirectory, "Content", "Assets", folderName);
}
