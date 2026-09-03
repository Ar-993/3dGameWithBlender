using System.Numerics;

namespace _3DLight.Assets;

internal static class CompiledModelFormat
{
    public const uint Magic = 0x4D4C4433;
    public const int Version = 2;
    public const int MaximumArrayLength = 100_000_000;
}

internal sealed class ModelData
{
    public List<NodeData> Nodes { get; } = [];
    public List<MeshData> Meshes { get; } = [];
    public List<ClipData> Clips { get; } = [];
}

internal sealed record NodeData(
    string Name,
    int Parent,
    Matrix4x4 Bind);

internal sealed record BoneData(
    int Node,
    Matrix4x4 Offset);

internal readonly record struct VertexData(
    Vector3 Position,
    Vector3 Normal,
    Vector2 Uv,
    byte B0,
    byte B1,
    byte B2,
    byte B3,
    Vector4 Weights);

internal sealed class MeshData
{
    public string Name { get; init; } = "Mesh";
    public int Node { get; init; }
    public string TextureName { get; init; } = string.Empty;
    public VertexData[] Vertices { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public BoneData[] Bones { get; init; } = [];
}

internal readonly record struct VectorKey(
    double Time,
    Vector3 Value);

internal readonly record struct QuaternionKey(
    double Time,
    Quaternion Value);

internal sealed class ChannelData
{
    public int Node { get; init; }
    public VectorKey[] Positions { get; init; } = [];
    public QuaternionKey[] Rotations { get; init; } = [];
    public VectorKey[] Scales { get; init; } = [];
}

internal sealed class ClipData
{
    public string Name { get; init; } = "Clip";
    public double Duration { get; init; }
    public double TicksPerSecond { get; init; }
    public List<ChannelData> Channels { get; } = [];
}

internal static class ModelDataIo
{
    public static void Write(string path, ModelData model)
    {
        string absolutePath = Path.GetFullPath(path);
        string outputDirectory = Path.GetDirectoryName(absolutePath)!;
        Directory.CreateDirectory(outputDirectory);

        using FileStream outputStream = File.Create(absolutePath);
        using var writer = new BinaryWriter(outputStream);

        writer.Write(CompiledModelFormat.Magic);
        writer.Write(CompiledModelFormat.Version);

        WriteNodes(writer, model.Nodes);
        WriteMeshes(writer, model.Meshes);
        WriteClips(writer, model.Clips);
    }

    public static ModelData Read(Stream stream)
    {
        using var reader = new BinaryReader(
            stream,
            System.Text.Encoding.UTF8,
            leaveOpen: true);

        ValidateHeader(reader);

        var model = new ModelData();
        ReadNodes(reader, model.Nodes);
        ReadMeshes(reader, model.Meshes);
        ReadClips(reader, model.Clips);

        return model;
    }

    private static void ValidateHeader(BinaryReader reader)
    {
        uint magic = reader.ReadUInt32();

        if (magic != CompiledModelFormat.Magic)
            throw new InvalidDataException("Файл не является моделью 3DLight.");

        int version = reader.ReadInt32();

        if (version != CompiledModelFormat.Version)
        {
            throw new InvalidDataException(
                $"Версия модели {version} не поддерживается.");
        }
    }

    private static void WriteNodes(BinaryWriter writer, List<NodeData> nodes)
    {
        writer.Write(nodes.Count);

        foreach (NodeData node in nodes)
        {
            writer.Write(node.Name);
            writer.Write(node.Parent);
            WriteMatrix(writer, node.Bind);
        }
    }

    private static void ReadNodes(BinaryReader reader, List<NodeData> nodes)
    {
        int nodeCount = ReadArrayLength(reader, "узлов");

        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            string name = reader.ReadString();
            int parent = reader.ReadInt32();
            Matrix4x4 bindTransform = ReadMatrix(reader);

            nodes.Add(new NodeData(name, parent, bindTransform));
        }
    }

    private static void WriteMeshes(BinaryWriter writer, List<MeshData> meshes)
    {
        writer.Write(meshes.Count);

        foreach (MeshData mesh in meshes)
        {
            writer.Write(mesh.Name);
            writer.Write(mesh.Node);
            writer.Write(mesh.TextureName);
            WriteVertices(writer, mesh.Vertices);
            WriteIndices(writer, mesh.Indices);
            WriteBones(writer, mesh.Bones);
        }
    }

    private static void ReadMeshes(BinaryReader reader, List<MeshData> meshes)
    {
        int meshCount = ReadArrayLength(reader, "мешей");

        for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
        {
            string name = reader.ReadString();
            int node = reader.ReadInt32();
            string textureName = reader.ReadString();

            meshes.Add(new MeshData
            {
                Name = name,
                Node = node,
                TextureName = textureName,
                Vertices = ReadVertices(reader),
                Indices = ReadIndices(reader),
                Bones = ReadBones(reader)
            });
        }
    }

    private static void WriteVertices(BinaryWriter writer, VertexData[] vertices)
    {
        writer.Write(vertices.Length);

        foreach (VertexData vertex in vertices)
        {
            WriteVector3(writer, vertex.Position);
            WriteVector3(writer, vertex.Normal);
            writer.Write(vertex.Uv.X);
            writer.Write(vertex.Uv.Y);
            writer.Write(vertex.B0);
            writer.Write(vertex.B1);
            writer.Write(vertex.B2);
            writer.Write(vertex.B3);
            writer.Write(vertex.Weights.X);
            writer.Write(vertex.Weights.Y);
            writer.Write(vertex.Weights.Z);
            writer.Write(vertex.Weights.W);
        }
    }

    private static VertexData[] ReadVertices(BinaryReader reader)
    {
        var vertices = new VertexData[ReadArrayLength(reader, "вершин")];

        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 position = ReadVector3(reader);
            Vector3 normal = ReadVector3(reader);
            var textureCoordinate = new Vector2(
                reader.ReadSingle(),
                reader.ReadSingle());

            byte bone0 = reader.ReadByte();
            byte bone1 = reader.ReadByte();
            byte bone2 = reader.ReadByte();
            byte bone3 = reader.ReadByte();

            var weights = new Vector4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());

            vertices[vertexIndex] = new VertexData(
                position,
                normal,
                textureCoordinate,
                bone0,
                bone1,
                bone2,
                bone3,
                weights);
        }

        return vertices;
    }

    private static void WriteIndices(BinaryWriter writer, int[] indices)
    {
        writer.Write(indices.Length);

        foreach (int index in indices)
            writer.Write(index);
    }

    private static int[] ReadIndices(BinaryReader reader)
    {
        var indices = new int[ReadArrayLength(reader, "индексов")];

        for (int index = 0; index < indices.Length; index++)
            indices[index] = reader.ReadInt32();

        return indices;
    }

    private static void WriteBones(BinaryWriter writer, BoneData[] bones)
    {
        writer.Write(bones.Length);

        foreach (BoneData bone in bones)
        {
            writer.Write(bone.Node);
            WriteMatrix(writer, bone.Offset);
        }
    }

    private static BoneData[] ReadBones(BinaryReader reader)
    {
        var bones = new BoneData[ReadArrayLength(reader, "костей")];

        for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
        {
            int node = reader.ReadInt32();
            Matrix4x4 offset = ReadMatrix(reader);
            bones[boneIndex] = new BoneData(node, offset);
        }

        return bones;
    }

    private static void WriteClips(BinaryWriter writer, List<ClipData> clips)
    {
        writer.Write(clips.Count);

        foreach (ClipData clip in clips)
        {
            writer.Write(clip.Name);
            writer.Write(clip.Duration);
            writer.Write(clip.TicksPerSecond);
            writer.Write(clip.Channels.Count);

            foreach (ChannelData channel in clip.Channels)
            {
                writer.Write(channel.Node);
                WriteVectorKeys(writer, channel.Positions);
                WriteQuaternionKeys(writer, channel.Rotations);
                WriteVectorKeys(writer, channel.Scales);
            }
        }
    }

    private static void ReadClips(BinaryReader reader, List<ClipData> clips)
    {
        int clipCount = ReadArrayLength(reader, "клипов");

        for (int clipIndex = 0; clipIndex < clipCount; clipIndex++)
        {
            var clip = new ClipData
            {
                Name = reader.ReadString(),
                Duration = reader.ReadDouble(),
                TicksPerSecond = reader.ReadDouble()
            };

            int channelCount = ReadArrayLength(reader, "каналов");

            for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
            {
                clip.Channels.Add(new ChannelData
                {
                    Node = reader.ReadInt32(),
                    Positions = ReadVectorKeys(reader),
                    Rotations = ReadQuaternionKeys(reader),
                    Scales = ReadVectorKeys(reader)
                });
            }

            clips.Add(clip);
        }
    }

    private static void WriteVectorKeys(BinaryWriter writer, VectorKey[] keys)
    {
        writer.Write(keys.Length);

        foreach (VectorKey key in keys)
        {
            writer.Write(key.Time);
            WriteVector3(writer, key.Value);
        }
    }

    private static VectorKey[] ReadVectorKeys(BinaryReader reader)
    {
        var keys = new VectorKey[ReadArrayLength(reader, "векторных ключей")];

        for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
        {
            double time = reader.ReadDouble();
            Vector3 value = ReadVector3(reader);
            keys[keyIndex] = new VectorKey(time, value);
        }

        return keys;
    }

    private static void WriteQuaternionKeys(
        BinaryWriter writer,
        QuaternionKey[] keys)
    {
        writer.Write(keys.Length);

        foreach (QuaternionKey key in keys)
        {
            writer.Write(key.Time);
            writer.Write(key.Value.X);
            writer.Write(key.Value.Y);
            writer.Write(key.Value.Z);
            writer.Write(key.Value.W);
        }
    }

    private static QuaternionKey[] ReadQuaternionKeys(BinaryReader reader)
    {
        var keys = new QuaternionKey[
            ReadArrayLength(reader, "кватернионных ключей")];

        for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
        {
            double time = reader.ReadDouble();
            var value = new Quaternion(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());

            keys[keyIndex] = new QuaternionKey(time, value);
        }

        return keys;
    }

    private static int ReadArrayLength(BinaryReader reader, string valueName)
    {
        int length = reader.ReadInt32();

        if (length < 0 || length > CompiledModelFormat.MaximumArrayLength)
        {
            throw new InvalidDataException(
                $"Повреждённое количество {valueName}: {length}.");
        }

        return length;
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }

    private static Vector3 ReadVector3(BinaryReader reader) => new(
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle());

    private static void WriteMatrix(BinaryWriter writer, Matrix4x4 matrix)
    {
        writer.Write(matrix.M11);
        writer.Write(matrix.M12);
        writer.Write(matrix.M13);
        writer.Write(matrix.M14);
        writer.Write(matrix.M21);
        writer.Write(matrix.M22);
        writer.Write(matrix.M23);
        writer.Write(matrix.M24);
        writer.Write(matrix.M31);
        writer.Write(matrix.M32);
        writer.Write(matrix.M33);
        writer.Write(matrix.M34);
        writer.Write(matrix.M41);
        writer.Write(matrix.M42);
        writer.Write(matrix.M43);
        writer.Write(matrix.M44);
    }

    private static Matrix4x4 ReadMatrix(BinaryReader reader) => new(
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
}
