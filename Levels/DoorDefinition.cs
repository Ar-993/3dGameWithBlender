using System.Text.Json;
using System.Text.Json.Serialization;

namespace _3DLight;

internal sealed class DoorDefinition
{
    public string Node { get; init; } = "";
    public string DisplayName { get; init; } = "ДВЕРЬ";
    public float OpenAngleDegrees { get; init; } = 100f;
    public float DegreesPerSecond { get; init; } = 180f;
    public float InteractionDistance { get; init; } = 2.5f;

    public static DoorDefinition[] Load(string modelName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "Levels", modelName + ".doors.json");
        if (!File.Exists(path))
            return [];

        return JsonSerializer.Deserialize<DoorDefinition[]>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException($"Пустые настройки дверей: {path}");
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Node) || string.IsNullOrWhiteSpace(DisplayName) ||
            !float.IsFinite(OpenAngleDegrees) || MathF.Abs(OpenAngleDegrees) < 1f || MathF.Abs(OpenAngleDegrees) > 180f ||
            !float.IsFinite(DegreesPerSecond) || DegreesPerSecond <= 0f ||
            !float.IsFinite(InteractionDistance) || InteractionDistance <= 0f)
            throw new InvalidDataException($"Некорректные настройки двери '{Node}'.");
    }
}
