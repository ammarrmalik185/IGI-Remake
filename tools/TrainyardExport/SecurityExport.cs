using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Formats.World;
internal static class SecurityExport {
 public static void Run(string project){
  string Find(string name){foreach(var f in new[]{"missions/location0/level1","missions/location0/common","common"}){var path="Assets/IGIImported/Models/"+f+"/"+name+".igimodel";if(File.Exists(Path.Combine(project,path)))return path;}throw new Exception("Missing security model "+name);}
  MefModel Read(string path){var b=JsonNode.Parse(File.ReadAllText(Path.Combine(project,path)))!;var d=JsonNode.Parse(File.ReadAllText(Path.Combine(project,b["mesh"]!.ToString())))!;string s=d["source"]!.ToString();int c=s.IndexOf(':');string e=s[(c+1)..];if(e.Contains(':'))e=e[(e.IndexOf(':')+1)..];return MefModel.Read(File.ReadAllBytes(Path.Combine(project,"Assets/IGIImported/SourceData~/Unpacked",s[..(c-4)],e)));}
  object V(OpenIGI.Core.Numerics.Vector3f p)=>new{x=p.X/4096f,y=p.Z/4096f,z=p.Y/4096f};
  var mission=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"Assets/IGIImported/Missions/01.igimission")))!;var origin=mission["origin"]!;float ox=origin["x"]!.GetValue<float>(),oy=origin["y"]!.GetValue<float>(),oz=origin["z"]!.GetValue<float>();
  var definitions=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"Assets/IGIImported/Definitions/missions/location0/level1/objects.qvm.json")))!;var cameras=new List<object>();var panels=new List<object>();
  foreach(var call in definitions["calls"]!.AsArray()){if(call!["function"]!.ToString()!="Task_New")continue;var a=call["args"]!.AsArray();if(a.Count<3)continue;float F(int i)=>a[i]!.GetValue<float>();object Position()=>new{x=F(3)/4096-ox,y=F(5)/4096-oy,z=F(4)/4096-oz};
   if(a[1]!.ToString()=="SCamera"){string holder=Find(a[7]!.ToString()),head=Find(a[10]!.ToString());var h=Read(holder);var lens=Read(head);h.TryGetMagicVertex(0,out var mount);lens.TryGetMagicVertex(1,out var eye);FirstBuildingRepair.ExportArchitecture(project,holder,h);FirstBuildingRepair.ExportArchitecture(project,head,lens);
    cameras.Add(new{id=a[0]!.GetValue<int>(),position=Position(),holderPath=holder,headPath=head,mount=V(mount),lens=V(eye),yaw=-F(6)*180/MathF.PI,pitch=-F(8)*180/MathF.PI,headYaw=-F(9)*180/MathF.PI,right=F(12),left=F(13),speed=F(14),pause=F(15),verticalFov=F(16),horizontalFov=F(17),range=F(18)});
   }
   if(a[1]!.ToString()=="Switch"&&a[0]!.GetValue<int>() is 92 or 94 or 99){string path=Find(a[11]!.ToString());FirstBuildingRepair.ExportArchitecture(project,path,Read(path));panels.Add(new{id=a[0]!.GetValue<int>(),modelPath=path,position=Position(),rotation=new{x=-F(6)*180/MathF.PI,y=-F(8)*180/MathF.PI,z=-F(7)*180/MathF.PI}});}
  }
  File.WriteAllText(Path.Combine(project,"Assets/IGIRebuilt/Trainyard/TrainyardSecurity.json"),JsonSerializer.Serialize(new{cameras,panels},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Exported {cameras.Count} native cameras and {panels.Count} alarm buttons.");
 }
}
