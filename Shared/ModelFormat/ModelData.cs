using System.Numerics;

namespace Ars3D.ModelFormat;

public sealed class ModelData
{
    public List<NodeData> Nodes { get; } = [];
    public List<MeshData> Meshes { get; } = [];
    public List<ClipData> Clips { get; } = [];
}

public sealed record NodeData(
    string Name,
    int Parent,
    Matrix4x4 Bind);

public sealed record BoneData(
    int Node,
    Matrix4x4 Offset);

public readonly record struct VertexData(
    Vector3 Position,
    Vector3 Normal,
    Vector2 Uv,
    byte B0,
    byte B1,
    byte B2,
    byte B3,
    Vector4 Weights);

public sealed class MeshData
{
    public string Name { get; init; } = "Mesh";
    public int Node { get; init; }
    public string TextureName { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public float MaterialOpacity { get; init; } = 1f;
    public VertexData[] Vertices { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public BoneData[] Bones { get; init; } = [];
}

public readonly record struct VectorKey(
    double Time,
    Vector3 Value);

public readonly record struct QuaternionKey(
    double Time,
    Quaternion Value);

public sealed class ChannelData
{
    public int Node { get; init; }
    public VectorKey[] Positions { get; init; } = [];
    public QuaternionKey[] Rotations { get; init; } = [];
    public VectorKey[] Scales { get; init; } = [];
}

public sealed class ClipData
{
    public string Name { get; init; } = "Clip";
    public double Duration { get; init; }
    public double TicksPerSecond { get; init; }
    public List<ChannelData> Channels { get; } = [];
}
