using System.Numerics;

namespace _3DLight.Assets;

internal static class CompiledModelFormat
{
    public const uint Magic = 0x4D4C4433;
    public const int Version = 1;
}

internal sealed class ModelData
{
    public List<NodeData> Nodes { get; } = [];
    public List<MeshData> Meshes { get; } = [];
    public List<ClipData> Clips { get; } = [];
}

internal sealed record NodeData(string Name, int Parent, Matrix4x4 Bind);
internal sealed record BoneData(int Node, Matrix4x4 Offset);
internal readonly record struct VertexData(Vector3 Position, Vector3 Normal, Vector2 Uv, byte B0, byte B1, byte B2, byte B3, Vector4 Weights);
internal sealed class MeshData
{
    public string Name { get; init; } = "Mesh";
    public int Node { get; init; }
    public VertexData[] Vertices { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public BoneData[] Bones { get; init; } = [];
}
internal readonly record struct VectorKey(double Time, Vector3 Value);
internal readonly record struct QuaternionKey(double Time, Quaternion Value);
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
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var w = new BinaryWriter(File.Create(path));
        w.Write(CompiledModelFormat.Magic); w.Write(CompiledModelFormat.Version);
        w.Write(model.Nodes.Count);
        foreach (var n in model.Nodes) { w.Write(n.Name); w.Write(n.Parent); Matrix(w, n.Bind); }
        w.Write(model.Meshes.Count);
        foreach (var m in model.Meshes)
        {
            w.Write(m.Name); w.Write(m.Node); w.Write(m.Vertices.Length);
            foreach (var v in m.Vertices)
            {
                V3(w,v.Position); V3(w,v.Normal); w.Write(v.Uv.X);w.Write(v.Uv.Y);
                w.Write(v.B0);w.Write(v.B1);w.Write(v.B2);w.Write(v.B3);
                w.Write(v.Weights.X);w.Write(v.Weights.Y);w.Write(v.Weights.Z);w.Write(v.Weights.W);
            }
            w.Write(m.Indices.Length); foreach(int i in m.Indices)w.Write(i);
            w.Write(m.Bones.Length); foreach(var b in m.Bones){w.Write(b.Node);Matrix(w,b.Offset);}
        }
        w.Write(model.Clips.Count);
        foreach(var c in model.Clips)
        {
            w.Write(c.Name);w.Write(c.Duration);w.Write(c.TicksPerSecond);w.Write(c.Channels.Count);
            foreach(var ch in c.Channels){w.Write(ch.Node);Keys(w,ch.Positions);Keys(w,ch.Rotations);Keys(w,ch.Scales);}
        }
    }

    public static ModelData Read(Stream stream)
    {
        using var r=new BinaryReader(stream,System.Text.Encoding.UTF8,true);
        if(r.ReadUInt32()!=CompiledModelFormat.Magic)throw new InvalidDataException("Файл не является моделью 3DLight.");
        int version=r.ReadInt32();if(version!=CompiledModelFormat.Version)throw new InvalidDataException($"Версия модели {version} не поддерживается.");
        var d=new ModelData();
        for(int i=0,n=Count(r);i<n;i++)d.Nodes.Add(new(r.ReadString(),r.ReadInt32(),Matrix(r)));
        for(int i=0,n=Count(r);i<n;i++)
        {
            string name=r.ReadString();int node=r.ReadInt32();var vertices=new VertexData[Count(r)];
            for(int v=0;v<vertices.Length;v++)vertices[v]=new(V3(r),V3(r),new(r.ReadSingle(),r.ReadSingle()),r.ReadByte(),r.ReadByte(),r.ReadByte(),r.ReadByte(),new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()));
            var indices=new int[Count(r)];for(int x=0;x<indices.Length;x++)indices[x]=r.ReadInt32();
            var bones=new BoneData[Count(r)];for(int b=0;b<bones.Length;b++)bones[b]=new(r.ReadInt32(),Matrix(r));
            d.Meshes.Add(new(){Name=name,Node=node,Vertices=vertices,Indices=indices,Bones=bones});
        }
        for(int i=0,n=Count(r);i<n;i++)
        {
            var clip=new ClipData{Name=r.ReadString(),Duration=r.ReadDouble(),TicksPerSecond=r.ReadDouble()};
            for(int c=0,k=Count(r);c<k;c++)clip.Channels.Add(new(){Node=r.ReadInt32(),Positions=VectorKeys(r),Rotations=QuaternionKeys(r),Scales=VectorKeys(r)});
            d.Clips.Add(clip);
        }
        return d;
    }
    private static int Count(BinaryReader r){int n=r.ReadInt32();if(n<0||n>100_000_000)throw new InvalidDataException("Повреждённый размер массива модели.");return n;}
    private static void Keys(BinaryWriter w,VectorKey[] a){w.Write(a.Length);foreach(var k in a){w.Write(k.Time);V3(w,k.Value);}}
    private static void Keys(BinaryWriter w,QuaternionKey[] a){w.Write(a.Length);foreach(var k in a){w.Write(k.Time);w.Write(k.Value.X);w.Write(k.Value.Y);w.Write(k.Value.Z);w.Write(k.Value.W);}}
    private static VectorKey[] VectorKeys(BinaryReader r){var a=new VectorKey[Count(r)];for(int i=0;i<a.Length;i++)a[i]=new(r.ReadDouble(),V3(r));return a;}
    private static QuaternionKey[] QuaternionKeys(BinaryReader r){var a=new QuaternionKey[Count(r)];for(int i=0;i<a.Length;i++)a[i]=new(r.ReadDouble(),new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()));return a;}
    private static void V3(BinaryWriter w,Vector3 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);} private static Vector3 V3(BinaryReader r)=>new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    private static void Matrix(BinaryWriter w,Matrix4x4 m){w.Write(m.M11);w.Write(m.M12);w.Write(m.M13);w.Write(m.M14);w.Write(m.M21);w.Write(m.M22);w.Write(m.M23);w.Write(m.M24);w.Write(m.M31);w.Write(m.M32);w.Write(m.M33);w.Write(m.M34);w.Write(m.M41);w.Write(m.M42);w.Write(m.M43);w.Write(m.M44);}
    private static Matrix4x4 Matrix(BinaryReader r)=>new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
}
