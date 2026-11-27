using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace IGIRebuilt.Editor {
 [Serializable] internal class AttachedPart { public int parentId; public string parentKey,key,name,type,modelPath; public Vector3 position,forward,up; }
 [Serializable] internal class AttachmentDocument { public AttachedPart[] attachments; }
 [Serializable] internal class DoorPart { public int id; public string modelPath; public Vector3 position,rotation,slide; public float angle,seconds; }
 [Serializable] internal class DoorDocument { public DoorPart[] doors; }
 internal static class TrainyardAttachmentSetup {
  static GameObject Spawn(string path, Transform parent, bool moving=false) {
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new Exception("Missing attached model: "+path);
   var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.transform.SetParent(parent,false);
   foreach(var t in go.GetComponentsInChildren<Transform>()){t.gameObject.layer=DemoWorld.WorldLayer;t.gameObject.isStatic=!moving;}
   foreach(var f in go.GetComponentsInChildren<MeshFilter>()) {if(f.GetComponent<MeshCollider>()!=null){f.gameObject.isStatic=false;continue;}var mesh=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)).OfType<Mesh>().FirstOrDefault(m=>m.name.EndsWith("_collision"));if(mesh!=null&&mesh.triangles.Length>0)f.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;}
   return go;
  }
  public static void Add(Dictionary<int,GameObject> placed, Transform root) {
   var doc=JsonUtility.FromJson<AttachmentDocument>(File.ReadAllText("Assets/IGIRebuilt/Trainyard/TrainyardAttachments.json"));var parts=new Dictionary<string,Transform>();int count=0;
   foreach(var a in doc.attachments) {
    if(a.type=="TASKTYPE_LADDER"||a.type=="TASKTYPE_CARDOOR")continue;
    Transform parent;if(string.IsNullOrEmpty(a.parentKey)){if(!placed.TryGetValue(a.parentId,out var p))continue;parent=p.transform;}else if(!parts.TryGetValue(a.parentKey,out parent))continue;
    // The first building's three room components also provide its doorway validation metadata.
    var interior=parent.GetComponent<FirstBuildingInterior>();
    Transform existing=interior==null?null:interior.rooms.FirstOrDefault(t=>t.name.Contains(a.name));
    if(existing!=null){parts[a.key]=existing;continue;}
    var go=Spawn(a.modelPath,parent);go.name="Authored attachment "+a.name;go.transform.localPosition=a.position;go.transform.localRotation=Quaternion.LookRotation(a.forward,a.up);parts[a.key]=go.transform;count++;
   }
   var doors=JsonUtility.FromJson<DoorDocument>(File.ReadAllText("Assets/IGIRebuilt/Trainyard/TrainyardDoors.json"));var gates=new List<InteractiveDoor>();
   foreach(var d in doors.doors) {
    var go=Spawn(d.modelPath,root,true);go.name=(d.modelPath.Contains("304_01")?"Sliding gate ":"Sliding door ")+d.id;
    go.transform.localPosition=d.position;go.transform.localRotation=Quaternion.Euler(d.rotation);
    var door=go.AddComponent<InteractiveDoor>();door.slide=d.slide;door.angle=d.angle;door.seconds=Mathf.Max(.4f,d.seconds);door.isGate=d.modelPath.Contains("304_01");if(door.isGate)gates.Add(door);
   }
   foreach(var gate in gates) gate.partner=gates.Where(g=>g!=gate).OrderBy(g=>Vector3.Distance(g.transform.position,gate.transform.position)).FirstOrDefault(g=>Vector3.Distance(g.transform.position,gate.transform.position)<5);
   Physics.SyncTransforms();Debug.Log($"ATTACHMENTS_READY: {count} attached parts; {doors.doors.Length} operable doors and gates");
  }
 }
}
