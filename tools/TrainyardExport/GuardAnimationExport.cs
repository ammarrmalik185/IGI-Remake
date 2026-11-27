using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Core.Numerics;
using OpenIGI.Formats.Animation;
using OpenIGI.Formats.World;
internal static class GuardAnimationExport {
 public static void Run(string project){
  var mission=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"Assets/IGIImported/Missions/01.igimission")))!;
  var paths=mission["modelPaths"]!.AsArray().Select(p=>p!.ToString()).Where(p=>p.Length>0).Distinct();
  var set=BoneAnimationSet.Read(File.ReadAllBytes(Path.Combine(project,"../IGIEnhanced/base_igi/pc/common/anims/001.iff")));
  if (Environment.GetCommandLineArgs().Contains("--motion-scan")) {
   foreach(int id in new[]{2,3,34,48,52,57,60,81}) {if(!set.TryGet(id,out var a))continue;var b=new BoneObject(set);var c=b.Play(0,id,false)!;c.Rate=160;var first=b.SampleRootMotion();float travel=0;var previous=first;var samples=new List<object>();for(int frame=0;frame<=MathF.Ceiling(a.Duration/160);frame++){var r=b.SampleRootMotion();travel+=MathF.Sqrt((r.X-previous.X)*(r.X-previous.X)+(r.Y-previous.Y)*(r.Y-previous.Y))/4096;previous=r;if(frame%10==0)samples.Add(new{frame,x=r.X/4096,y=r.Y/4096,z=r.Z/4096});b.Tick();}Console.WriteLine(JsonSerializer.Serialize(new{id,seconds=a.Duration/4800,travel,samples}));}return;
  }
  if(Environment.GetCommandLineArgs().Contains("--loop-scan")) { foreach(int id in new[]{34,57}) {var b=new BoneObject(set);b.Play(0,id,false);var first=b.Pose();for(int frame=1;frame<=210;frame++){b.Tick();var pose=b.Pose();float error=0;for(int j=0;j<b.BoneCount;j++){var a=first.Rotations[j];var r=pose.Rotations[j];error+=(a.M11-r.M11)*(a.M11-r.M11)+(a.M12-r.M12)*(a.M12-r.M12)+(a.M13-r.M13)*(a.M13-r.M13)+(a.M21-r.M21)*(a.M21-r.M21)+(a.M22-r.M22)*(a.M22-r.M22)+(a.M23-r.M23)*(a.M23-r.M23)+(a.M31-r.M31)*(a.M31-r.M31)+(a.M32-r.M32)*(a.M32-r.M32)+(a.M33-r.M33)*(a.M33-r.M33);}Console.WriteLine($"{id} {frame} {error}");}}return; }
  Directory.CreateDirectory(Path.Combine(project,"Assets/IGIRebuilt/Trainyard/Animations"));
  foreach(var path in paths){var binding=JsonNode.Parse(File.ReadAllText(Path.Combine(project,path)))!;string name=binding["model"]!.ToString();if(name is not ("001_01_1" or "003_01_1" or "019_01_1"))continue;
   var doc=JsonNode.Parse(File.ReadAllText(Path.Combine(project,binding["mesh"]!.ToString())))!;string source=doc["source"]!.ToString();int colon=source.IndexOf(':');string entry=source[(colon+1)..];if(entry.Contains(':'))entry=entry[(entry.IndexOf(':')+1)..];
   var model=MefModel.Read(File.ReadAllBytes(Path.Combine(project,"Assets/IGIImported/SourceData~/Unpacked",source[..(colon-4)],entry)));
   var verts=model.Vertices.ToArray();var weights=model.SkinInfluences.ToArray();var bone=new BoneObject(set);bone.Play(0,2,true);float restZ=bone.SampleRootMotion().Z;
   using var w=new BinaryWriter(File.Create(Path.Combine(project,"Assets/IGIRebuilt/Trainyard/Animations",name+".igianim")));w.Write(3);w.Write(model.BaseVertexCount);w.Write(8);
   foreach(var spec in new[]{("Idle",2,true),("Walk",57,true),("Run",3,true),("Fire",56,true),("Hit",27,false),("Death",23,false),("WalkFire",57,true),("RunFire",3,true)}){
    bone=new BoneObject(set);
    if(!set.TryGet(spec.Item2,out var anim))throw new Exception("Animation missing");var clip=bone.Play(0,spec.Item2,false)!;int count=Math.Clamp((int)MathF.Ceiling(anim.Duration/4800*30)+1,2,2048);float duration=MathF.Max(1f/30,anim.Duration/4800);clip.Rate=anim.Duration/(count-1);
    var firstRoot=bone.SampleRootMotion();clip.Rate=anim.Duration;bone.Tick();var lastRoot=bone.SampleRootMotion();float speed=MathF.Sqrt((lastRoot.X-firstRoot.X)*(lastRoot.X-firstRoot.X)+(lastRoot.Y-firstRoot.Y)*(lastRoot.Y-firstRoot.Y))/4096/duration;
    clip=bone.Play(0,spec.Item2,false)!;clip.Rate=anim.Duration/(count-1);
    if(spec.Item1 is "WalkFire" or "RunFire") {var upper=bone.Play(1,56,true) ?? throw new Exception("Upper-body firing animation missing");upper.Rate=clip.Rate;}
    w.Write(spec.Item1);w.Write(duration);w.Write(spec.Item3);w.Write(count);w.Write(speed);
    Vector3f[]? firstPositions=null,firstNormals=null;
    var sockets=new List<(Vector3f p,System.Numerics.Quaternion r)>();
    for(int frame=0;frame<count;frame++){
     var root=bone.SampleRootMotion();var pose=bone.Pose(rootPosition:new Vector3f(0,0,root.Z-restZ));var pos=new Vector3f[model.BaseVertexCount];var norms=new Vector3f[model.BaseVertexCount];
     for(int i=0;i<verts.Length;i++){var b=weights[i];int target=i<model.BaseVertexCount?i:b.TargetVertex;if(target<0||target>=pos.Length)continue;var p=Rotate(pose.Rotations[b.BoneIndex],verts[i].Position);var t=pose.Positions[b.BoneIndex];var n=Rotate(pose.Rotations[b.BoneIndex],verts[i].Normal);pos[target]=Add(pos[target],new Vector3f((p.X+t.X)*b.Weight/4096,(p.Y+t.Y)*b.Weight/4096,(p.Z+t.Z)*b.Weight/4096));norms[target]=Add(norms[target],new Vector3f(n.X*b.Weight,n.Y*b.Weight,n.Z*b.Weight));}
     var hand=pose.Positions[22];var r=pose.Rotations[22];
     var matrix=new System.Numerics.Matrix4x4(r.M11,r.M13,r.M12,0,r.M31,r.M33,r.M32,0,r.M21,r.M23,r.M22,0,0,0,0,1);
     sockets.Add((new Vector3f(hand.X/4096,hand.Z/4096,hand.Y/4096),System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromRotationMatrix(System.Numerics.Matrix4x4.Transpose(matrix)))));
     if(spec.Item3&&frame==count-1)sockets[^1]=sockets[0];
     if(frame==0){firstPositions=(Vector3f[])pos.Clone();firstNormals=(Vector3f[])norms.Clone();}
     if(spec.Item3&&frame==count-1){pos=firstPositions!;norms=firstNormals!;}
     for(int i=0;i<pos.Length;i++){w.Write(pos[i].X);w.Write(pos[i].Z);w.Write(pos[i].Y);w.Write(norms[i].X);w.Write(norms[i].Z);w.Write(norms[i].Y);}bone.Tick();
    }
    foreach(var socket in sockets){w.Write(socket.p.X);w.Write(socket.p.Y);w.Write(socket.p.Z);w.Write(socket.r.X);w.Write(socket.r.Y);w.Write(socket.r.Z);w.Write(socket.r.W);}
    Console.WriteLine($"{name} {spec.Item1}: original clip {spec.Item2}, {count} frames, {duration:F2}s");
   }
  }
 }
 static Vector3f Rotate(Matrix3f m,Vector3f v)=>new(m.M11*v.X+m.M12*v.Y+m.M13*v.Z,m.M21*v.X+m.M22*v.Y+m.M23*v.Z,m.M31*v.X+m.M32*v.Y+m.M33*v.Z);
 static Vector3f Add(Vector3f a,Vector3f b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
}
