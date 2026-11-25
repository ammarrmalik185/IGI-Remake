using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Formats.Ai;
internal static class GuardRouteExport {
 public static void Run(string project){
 var mission=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"Assets/IGIImported/Missions/01.igimission")))!;var origin=mission["origin"]!;double ox=origin["x"]!.GetValue<float>(),oy=origin["y"]!.GetValue<float>(),oz=origin["z"]!.GetValue<float>();
 var calls=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"Assets/IGIImported/Definitions/missions/location0/level1/objects.qvm.json")))!["calls"]!.AsArray();
 var offsets=new Dictionary<int,double[]>();var guardGraphs=new Dictionary<int,int>();var paths=new Dictionary<int,List<(int kind,int arg)>>();var pending=new List<(int,int)>();
 foreach(var call in calls){if(call!["function"]!.ToString()!="Task_New")continue;var a=call["args"]!.AsArray();if(a.Count<3)continue;string type=a[1]!.ToString();int id=a[0]!.GetValue<int>();
 if(type=="AIGraph")offsets[id]=new[]{a[3]!.GetValue<double>(),a[4]!.GetValue<double>(),a[5]!.GetValue<double>()};
 if(type=="HumanAI"&&id>=2200&&id<=2225)guardGraphs[id-200]=a[4]!.GetValue<int>();
 if(type=="PatrolPathCommand")pending.Add((a[3]!.GetValue<int>(),a[4]!.GetValue<int>()));
 if(type=="PatrolPath"){paths[id]=pending.ToList();pending.Clear();}}
 var nodesByGraph=new Dictionary<int,Dictionary<int,object>>();var graphRows=new List<object>();
 foreach(int id in guardGraphs.Values.Distinct()){var g=GraphFile.Read(File.ReadAllBytes(Path.Combine(project,$"../IGIEnhanced/base_igi/pc/missions/location0/level1/graphs/graph{id}.dat")));var at=offsets[id];var map=new Dictionary<int,object>();foreach(var n in g.Nodes)map[n.Id]=new{x=(float)((n.Position.X+at[0])/4096-ox),y=(float)((n.Position.Z+at[2])/4096-oy),z=(float)((n.Position.Y+at[1])/4096-oz)};nodesByGraph[id]=map;graphRows.Add(new{id,nodes=g.Nodes.Select(n=>new{id=n.Id,position=map[n.Id]}),links=g.Links.Select(l=>new{a=l.NodeA,b=l.NodeB})});}
 var guards=new List<object>();foreach(var pair in guardGraphs){object[] Commands(int path){if(!paths.TryGetValue(path,out var list))return Array.Empty<object>();return list.Select(c=>new{kind=c.kind,seconds=c.kind==1?c.arg/30f:0,position=c.kind is 2 or 3 or 4 or 5?nodesByGraph[pair.Value][c.arg]:new{x=0f,y=0f,z=0f}}).Cast<object>().ToArray();}guards.Add(new{id=pair.Key,graphId=pair.Value,patrol=Commands(pair.Key+400),alarm=Commands(pair.Key+600)});}
 File.WriteAllText(Path.Combine(project,"Assets/IGIRebuilt/Trainyard/TrainyardGuardRoutes.json"),JsonSerializer.Serialize(new{graphs=graphRows,guards},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Exported {graphRows.Count} navigation graphs and {guards.Count} authored guard patrol/alarm routes.");
 }
}
