using _3DLight.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using NMatrix=System.Numerics.Matrix4x4;

namespace _3DLight;

internal sealed class CompiledModel:IDisposable
{
    private readonly GraphicsDevice device;private readonly ModelData data;private readonly RuntimeMesh[] meshes;private readonly Dictionary<string,ClipData> clips;
    private readonly Matrix[] locals,globals,bindGlobals;private readonly Matrix inverseRoot;
    public IEnumerable<string> ClipNames=>clips.Keys;

    private CompiledModel(GraphicsDevice device,ModelData data,Texture2D texture,Effect toonEffect)
    {
        this.device=device;this.data=data;clips=data.Clips.ToDictionary(x=>x.Name,StringComparer.OrdinalIgnoreCase);
        locals=data.Nodes.Select(n=>M(n.Bind)).ToArray();globals=new Matrix[locals.Length];bindGlobals=new Matrix[locals.Length];BuildGlobals(locals,bindGlobals);inverseRoot=locals.Length==0?Matrix.Identity:Matrix.Invert(bindGlobals[0]);
        meshes=data.Meshes.Select(m=>Create(m,texture,toonEffect)).ToArray();
    }
    public static CompiledModel Load(GraphicsDevice device,string name,Texture2D texture,Effect toonEffect)
    {string path=Path.Combine(AppContext.BaseDirectory,"Content","Models",name+".3dmodel");using var stream=File.OpenRead(path);return new(device,ModelDataIo.Read(stream),texture,toonEffect);}
    public float GetClipDuration(string name)=>clips.TryGetValue(name,out var c)?(float)(c.Duration/c.TicksPerSecond):throw new InvalidOperationException($"Анимация '{name}' отсутствует.");
    public void Draw(Matrix world,Matrix view,Matrix projection){Array.Copy(bindGlobals,globals,globals.Length);DrawMeshes(world,view,projection);}
    public void Draw(string clipName,float seconds,bool loop,Matrix world,Matrix view,Matrix projection)
    {
        if(!clips.TryGetValue(clipName,out var clip))throw new InvalidOperationException($"Анимация '{clipName}' отсутствует.");
        for(int i=0;i<locals.Length;i++)locals[i]=M(data.Nodes[i].Bind);double tick=seconds*clip.TicksPerSecond;tick=loop&&clip.Duration>0?tick%clip.Duration:Math.Min(tick,clip.Duration);
        foreach(var c in clip.Channels){Matrix bind=locals[c.Node];bind.Decompose(out Vector3 bs,out Quaternion br,out Vector3 bp);locals[c.Node]=Matrix.CreateScale(Interp(c.Scales,tick,bs))*Matrix.CreateFromQuaternion(Interp(c.Rotations,tick,br))*Matrix.CreateTranslation(Interp(c.Positions,tick,bp));}
        BuildGlobals(locals,globals);DrawMeshes(world,view,projection);
    }
    public List<Level.Platform> BuildPlatforms()
    {
        var result=new List<Level.Platform>();int id=0;
        foreach(var mesh in data.Meshes)
        {
            string name=data.Nodes[mesh.Node].Name;if(!Walkable(name)||mesh.Vertices.Length==0)continue;Vector3 min=new(float.MaxValue),max=new(float.MinValue);Matrix transform=bindGlobals[mesh.Node];
            foreach(var v in mesh.Vertices){Vector3 p=Vector3.Transform(V(v.Position),transform);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}if(max.X-min.X>=.5f&&max.Z-min.Z>=.5f)result.Add(new(id++,name,new BoundingBox(min,max)));
        }
        if(result.Count==0)throw new InvalidOperationException("В скомпилированной модели уровня нет платформ.");return result;
    }
    private static bool Walkable(string n)=>n.StartsWith("Ground",StringComparison.OrdinalIgnoreCase)||n.StartsWith("Bridge",StringComparison.OrdinalIgnoreCase)||n.StartsWith("Trail",StringComparison.OrdinalIgnoreCase)||n.StartsWith("Cylinder",StringComparison.OrdinalIgnoreCase)||n.Contains("Platform",StringComparison.OrdinalIgnoreCase);
    private RuntimeMesh Create(MeshData mesh,Texture2D texture,Effect toonEffect)
    {
        var vertices=new RuntimeVertex[mesh.Vertices.Length];for(int i=0;i<vertices.Length;i++){var v=mesh.Vertices[i];vertices[i]=new(V(v.Position),V(v.Normal),new(v.Uv.X,v.Uv.Y),new Byte4(v.B0,v.B1,v.B2,v.B3),V(v.Weights));}
        var vb=new VertexBuffer(device,RuntimeVertex.Declaration,vertices.Length,BufferUsage.WriteOnly);vb.SetData(vertices);var ib=new IndexBuffer(device,IndexElementSize.ThirtyTwoBits,mesh.Indices.Length,BufferUsage.WriteOnly);ib.SetData(mesh.Indices);
        Effect effect=toonEffect.Clone();var bones=new Matrix[72];for(int i=0;i<bones.Length;i++)bones[i]=Matrix.Identity;
        return new(mesh,vb,ib,effect,bones,texture);
    }
    private void DrawMeshes(Matrix world,Matrix view,Matrix projection)
    {
        var old=device.RasterizerState;device.RasterizerState=RasterizerState.CullNone;
        Matrix inverseView=Matrix.Invert(view);Vector3 cameraPosition=inverseView.Translation;Vector3 lightDirection=Vector3.Normalize(new Vector3(-.5f,-1f,-.4f));
        foreach(var mesh in meshes){device.SetVertexBuffer(mesh.Vb);device.Indices=mesh.Ib;Matrix meshWorld=world;if(mesh.Source.Bones.Length>0){for(int i=0;i<mesh.Source.Bones.Length;i++){var b=mesh.Source.Bones[i];mesh.Bones[i]=M(b.Offset)*globals[b.Node]*inverseRoot;}}else{meshWorld=globals[mesh.Source.Node]*world;mesh.Bones[0]=Matrix.Identity;}mesh.Effect.Parameters["World"]?.SetValue(meshWorld);mesh.Effect.Parameters["View"]?.SetValue(view);mesh.Effect.Parameters["Projection"]?.SetValue(projection);mesh.Effect.Parameters["CameraPosition"]?.SetValue(cameraPosition);mesh.Effect.Parameters["LightDirection"]?.SetValue(lightDirection);mesh.Effect.Parameters["ModelTexture"]?.SetValue(mesh.Texture);mesh.Effect.Parameters["Bones"]?.SetValue(mesh.Bones);foreach(var pass in mesh.Effect.CurrentTechnique.Passes){pass.Apply();device.DrawIndexedPrimitives(PrimitiveType.TriangleList,0,0,mesh.Source.Indices.Length/3);}}
        device.RasterizerState=old;
    }
    private void BuildGlobals(Matrix[] l,Matrix[] g){for(int i=0;i<l.Length;i++){int p=data.Nodes[i].Parent;g[i]=p<0?l[i]:l[i]*g[p];}}
    private static Vector3 Interp(VectorKey[] a,double t,Vector3 fallback){if(a.Length==0)return fallback;int i=Key(a,t,x=>x.Time);if(i==a.Length-1)return V(a[i].Value);float q=(float)((t-a[i].Time)/Math.Max(a[i+1].Time-a[i].Time,double.Epsilon));return Vector3.Lerp(V(a[i].Value),V(a[i+1].Value),q);}
    private static Quaternion Interp(QuaternionKey[] a,double t,Quaternion fallback){if(a.Length==0)return fallback;int i=Key(a,t,x=>x.Time);if(i==a.Length-1)return Q(a[i].Value);float q=(float)((t-a[i].Time)/Math.Max(a[i+1].Time-a[i].Time,double.Epsilon));return Quaternion.Normalize(Quaternion.Slerp(Q(a[i].Value),Q(a[i+1].Value),q));}
    private static int Key<T>(T[] a,double t,Func<T,double> f){int l=0,h=a.Length-1;while(l<h){int m=(l+h+1)/2;if(f(a[m])<=t)l=m;else h=m-1;}return l;}
    public void Dispose(){foreach(var m in meshes){m.Vb.Dispose();m.Ib.Dispose();m.Effect.Dispose();}}
    private static Matrix M(NMatrix m)=>new(m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44);private static Vector3 V(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);private static Vector4 V(System.Numerics.Vector4 v)=>new(v.X,v.Y,v.Z,v.W);private static Quaternion Q(System.Numerics.Quaternion q)=>new(q.X,q.Y,q.Z,q.W);
    private sealed record RuntimeMesh(MeshData Source,VertexBuffer Vb,IndexBuffer Ib,Effect Effect,Matrix[] Bones,Texture2D Texture);
}
internal readonly struct RuntimeVertex(Vector3 p,Vector3 n,Vector2 uv,Byte4 bi,Vector4 bw):IVertexType
{
    public static readonly VertexDeclaration Declaration=new(new VertexElement(0,VertexElementFormat.Vector3,VertexElementUsage.Position,0),new VertexElement(12,VertexElementFormat.Vector3,VertexElementUsage.Normal,0),new VertexElement(24,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,0),new VertexElement(32,VertexElementFormat.Byte4,VertexElementUsage.BlendIndices,0),new VertexElement(36,VertexElementFormat.Vector4,VertexElementUsage.BlendWeight,0));
    private readonly Vector3 p=p,n=n;private readonly Vector2 uv=uv;private readonly Byte4 bi=bi;private readonly Vector4 bw=bw;VertexDeclaration IVertexType.VertexDeclaration=>Declaration;
}
