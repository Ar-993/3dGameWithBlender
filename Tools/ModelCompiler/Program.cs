using System.Numerics;
using System.Text.Json;
using Assimp;
using _3DLight.Assets;
using AssetVectorKey = _3DLight.Assets.VectorKey;
using AssetQuaternionKey = _3DLight.Assets.QuaternionKey;

if(args.Length!=1){Console.Error.WriteLine("Usage: ModelCompiler <models.json>");return 2;}
string manifestPath=Path.GetFullPath(args[0]);string root=Path.GetDirectoryName(manifestPath)!;
var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Пустой manifest.");
foreach(var entry in manifest.Models)
{
    string source=Resolve(root,entry.Source),output=Resolve(root,entry.Output);
    Console.WriteLine($"Compiling {entry.Name}...");
    ModelDataIo.Write(output,Compile(source,entry.Clips,root));
    Console.WriteLine($"  -> {output}");
}
return 0;

static ModelData Compile(string sourcePath,Dictionary<string,string>? clipFiles,string manifestRoot)
{
    using var importer=new AssimpContext();
    const PostProcessSteps flags=PostProcessSteps.Triangulate|PostProcessSteps.JoinIdenticalVertices|PostProcessSteps.GenerateSmoothNormals|PostProcessSteps.FlipUVs|PostProcessSteps.LimitBoneWeights|PostProcessSteps.FlipWindingOrder;
    Scene scene=importer.ImportFile(sourcePath,flags)??throw new InvalidDataException(sourcePath);
    bool transpose=ChooseLayout(scene);var data=new ModelData();var nodeMap=new Dictionary<string,int>(StringComparer.Ordinal);
    AddNode(scene.RootNode,-1,data,nodeMap,transpose);
    var owners=new int[scene.MeshCount];IndexOwners(scene.RootNode,nodeMap,owners);
    for(int i=0;i<scene.MeshCount;i++)data.Meshes.Add(Mesh(scene.Meshes[i],owners[i],nodeMap,transpose));
    if(clipFiles is not null)foreach(var pair in clipFiles)
    {
        string path=Resolve(manifestRoot,pair.Value);Scene clip=Path.GetFullPath(path).Equals(Path.GetFullPath(sourcePath),StringComparison.OrdinalIgnoreCase)?scene:importer.ImportFile(path,flags);
        data.Clips.Add(Clip(pair.Key,clip,nodeMap));
    }
    Console.WriteLine($"  {data.Meshes.Count} meshes, {data.Nodes.Count} nodes, {data.Clips.Count} clips, matrices {(transpose?"transposed":"direct")}");
    return data;
}
static void AddNode(Assimp.Node n,int parent,ModelData d,Dictionary<string,int> map,bool transpose)
{if(map.ContainsKey(n.Name))throw new InvalidDataException($"Duplicate node {n.Name}");int index=d.Nodes.Count;map[n.Name]=index;d.Nodes.Add(new(n.Name,parent,M(n.Transform,transpose)));foreach(var c in n.Children)AddNode(c,index,d,map,transpose);}
static void IndexOwners(Assimp.Node n,Dictionary<string,int> map,int[] owners){foreach(int m in n.MeshIndices)owners[m]=map[n.Name];foreach(var c in n.Children)IndexOwners(c,map,owners);}
static MeshData Mesh(Assimp.Mesh s,int owner,Dictionary<string,int> nodes,bool transpose)
{
    if(s.BoneCount>72)throw new InvalidDataException($"{s.Name}: {s.BoneCount} bones, max 72");
    var weights=new List<(int,float)>[s.VertexCount];for(int i=0;i<weights.Length;i++)weights[i]=[];
    var bones=new BoneData[s.BoneCount];for(int b=0;b<s.BoneCount;b++){var bone=s.Bones[b];if(!nodes.TryGetValue(bone.Name,out int node))throw new InvalidDataException($"Bone {bone.Name} missing");bones[b]=new(node,M(bone.OffsetMatrix,transpose));foreach(var w in bone.VertexWeights)weights[w.VertexID].Add((b,w.Weight));}
    var vertices=new VertexData[s.VertexCount];for(int i=0;i<vertices.Length;i++)
    {
        weights[i].Sort((a,b)=>b.Item2.CompareTo(a.Item2));float total=weights[i].Take(4).Sum(x=>x.Item2);if(total<=0)total=1;
        byte B(int x)=>x<weights[i].Count?checked((byte)weights[i][x].Item1):(byte)0;float W(int x)=>x<weights[i].Count?weights[i][x].Item2/total:x==0?1:0;
        Vector3 p=s.Vertices[i],n=s.HasNormals?s.Normals[i]:Vector3.UnitY,uv=s.HasTextureCoords(0)?s.TextureCoordinateChannels[0][i]:Vector3.Zero;
        vertices[i]=new(p,n,new(uv.X,uv.Y),B(0),B(1),B(2),B(3),new(W(0),W(1),W(2),W(3)));
    }
    return new(){Name=s.Name,Node=owner,Vertices=vertices,Indices=s.GetIndices().ToArray(),Bones=bones};
}
static ClipData Clip(string name,Scene scene,Dictionary<string,int> nodes)
{
    if(scene.AnimationCount==0)throw new InvalidDataException($"{name} has no animation");var a=scene.Animations[0];var result=new ClipData{Name=name,Duration=a.DurationInTicks,TicksPerSecond=a.TicksPerSecond>0?a.TicksPerSecond:25};
    foreach(var c in a.NodeAnimationChannels)if(nodes.TryGetValue(c.NodeName,out int node))result.Channels.Add(new(){Node=node,Positions=c.PositionKeys.Select(k=>new AssetVectorKey(k.Time,k.Value)).ToArray(),Rotations=c.RotationKeys.Select(k=>new AssetQuaternionKey(k.Time,k.Value)).ToArray(),Scales=c.ScalingKeys.Select(k=>new AssetVectorKey(k.Time,k.Value)).ToArray()});
    return result;
}
static bool ChooseLayout(Scene scene)
{
    if(!scene.Meshes.Any(x=>x.HasBones))return true;
    float Error(bool transpose){var globals=new Dictionary<string,Matrix4x4>();void Visit(Assimp.Node n,Matrix4x4 p){var g=M(n.Transform,transpose)*p;globals[n.Name]=g;foreach(var c in n.Children)Visit(c,g);}Visit(scene.RootNode,Matrix4x4.Identity);Matrix4x4.Invert(M(scene.RootNode.Transform,transpose),out var inv);float e=0;int count=0;foreach(var mesh in scene.Meshes)foreach(var b in mesh.Bones)if(globals.TryGetValue(b.Name,out var g)){var m=M(b.OffsetMatrix,transpose)*g*inv;e+=MathF.Abs(m.M11-1)+MathF.Abs(m.M22-1)+MathF.Abs(m.M33-1)+MathF.Abs(m.M44-1)+MathF.Abs(m.M12)+MathF.Abs(m.M13)+MathF.Abs(m.M14)+MathF.Abs(m.M21)+MathF.Abs(m.M23)+MathF.Abs(m.M24)+MathF.Abs(m.M31)+MathF.Abs(m.M32)+MathF.Abs(m.M34)+MathF.Abs(m.M41)+MathF.Abs(m.M42)+MathF.Abs(m.M43);count++;}return count==0?float.MaxValue:e/count;}return Error(true)<Error(false);
}
static Matrix4x4 M(Matrix4x4 m,bool transpose)=>transpose?Matrix4x4.Transpose(m):m;
static string Resolve(string root,string path)=>Path.GetFullPath(Path.Combine(root,path));
sealed class Manifest{public List<Entry> Models{get;set;}=[];}sealed class Entry{public string Name{get;set;}="";public string Source{get;set;}="";public string Output{get;set;}="";public Dictionary<string,string>? Clips{get;set;}}
