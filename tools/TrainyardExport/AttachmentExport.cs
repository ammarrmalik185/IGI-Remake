using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats.World;
using OpenIGI.Game.World;
using OpenIGI.Scripting.Runtime;
internal static class AttachmentExport {
 public static void Run(string project) {
 var resources=new ResourceRegistry(new VirtualFileSystem()); resources.Register(MagicObjectRegistry.ScriptResource,File.ReadAllBytes(Path.Combine(project,"../IGIEnhanced/base_igi/pc/magicobj/magicobj.qvm")));
 var natives=new NativeRegistry(); natives.RegisterConstant("TRUE",1); natives.RegisterConstant("FALSE",0); var registry=new MagicObjectRegistry();registry.Register(natives);registry.Load(resources,natives);
 string? Find(string name) { foreach(var f in new[]{"missions/location0/level1","missions/location0/common","common"}) {var p="Assets/IGIImported/Models/"+f+"/"+name+".igimodel";if(File.Exists(Path.Combine(project,p)))return p;}return null; }
 MefModel Read(string path) {var b=JsonNode.Parse(File.ReadAllText(Path.Combine(project,path)))!;var d=JsonNode.Parse(File.ReadAllText(Path.Combine(project,b["mesh"]!.ToString())))!;string s=d["source"]!.ToString();int c=s.IndexOf(':');string e=s[(c+1)..];if(e.Contains(':'))e=e[(e.IndexOf(':')+1)..];return MefModel.Read(File.ReadAllBytes(Path.Combine(project,"Assets/IGIImported/SourceData~/Unpacked",s[..(c-4)],e)));}
 object V(OpenIGI.Core.Numerics.Vector3f p)=>new{x=p.X/4096f,y=p.Z/4096f,z=p.Y/4096f};
 var repaired=new HashSet<string>(); void Repair(string path) {if(repaired.Add(path)) {var b=JsonNode.Parse(File.ReadAllText(Path.Combine(project,path)))!;if(b["skinned"]?.GetValue<bool>()!=true)FirstBuildingRepair.ExportArchitecture(project,path,Read(path));}}
 var rows=new List<object>();var mission=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"Assets/IGIImported/Missions/01.igimission")))!;var ps=mission["placements"]!.AsArray();var paths=mission["modelPaths"]!.AsArray();
 void Walk(string path,int parentId,string parentKey,int depth) { if(depth>6)return;foreach(var a in Read(path).Attachments) {if(!registry.TryGet(a.Name,out var d))continue;string type=registry.TaskTypeName(d.TaskType);string? child=Find(d.Model);if(child==null)continue;Repair(child); if(Read(child).IsSector)Repair(path); string key=parentKey+"/"+a.Name+"_"+rows.Count;var m=a.Orientation; rows.Add(new{parentId,parentKey,key,name=a.Name,type,modelPath=child,position=V(a.Position),forward=new{x=m.M21,y=m.M23,z=m.M22},up=new{x=m.M31,y=m.M33,z=m.M32}}); Console.WriteLine($"{parentId} {Path.GetFileName(path)} > {a.Name} {type} {d.Model}");Walk(child,parentId,key,depth+1);}}
 for(int i=0;i<ps.Count;i++)if(ps[i]!["id"]!.GetValue<int>()>=0&&paths[i]!.ToString().Length>0)Walk(paths[i]!.ToString(),ps[i]!["id"]!.GetValue<int>(),"",0);
 var doors=new List<object>();
 var origin=mission["origin"]!;float ox=origin["x"]!.GetValue<float>(),oy=origin["y"]!.GetValue<float>(),oz=origin["z"]!.GetValue<float>();
 var defs=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"Assets/IGIImported/Definitions/missions/location0/level1/objects.qvm.json")))!;
 foreach(var call in defs["calls"]!.AsArray()) {var a=call!["args"]!.AsArray();if(call["function"]!.ToString()!="Task_New"||a.Count<23||a[1]!.ToString()!="Door")continue; float F(int n)=>a[n]!.GetValue<float>(); var path=Find(a[12]!.ToString())??throw new Exception("Door missing");Repair(path);doors.Add(new{id=a[0]!.GetValue<int>(),modelPath=path,position=new{x=F(3)/4096-ox,y=F(5)/4096-oy,z=F(4)/4096-oz},rotation=new{x=-F(9)*180/MathF.PI,y=-F(11)*180/MathF.PI,z=-F(10)*180/MathF.PI},slide=new{x=F(6),y=0,z=F(7)},angle=-F(13),seconds=F(14)});}
 File.WriteAllText(Path.Combine(project,"Assets/IGIRebuilt/Trainyard/TrainyardDoors.json"),JsonSerializer.Serialize(new{doors},new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine($"Exported {rows.Count} attachments, {doors.Count} doors and gates, repaired {repaired.Count} source meshes.");
 File.WriteAllText(Path.Combine(project,"Assets/IGIRebuilt/Trainyard/TrainyardAttachments.json"),JsonSerializer.Serialize(new{attachments=rows},new JsonSerializerOptions{WriteIndented=true}));
 }
}
