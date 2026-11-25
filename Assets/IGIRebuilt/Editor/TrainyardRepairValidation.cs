using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace IGIRebuilt.Editor {
 public static class TrainyardRepairValidation {
  public static void Run(){
   TrainyardSetup.BuildMission();
   var doors=UnityEngine.Object.FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None);if(doors.Length!=23)throw new Exception("Expected 23 doors/gates");
   int open=0;foreach(var d in doors){d.Initialize();var start=d.transform.position;var closedBounds=d.GetComponentInChildren<Collider>().bounds;d.SetOpen(true);d.Advance(10);Physics.SyncTransforms();
    if(d.Fraction<.999f||Vector3.Distance(start,d.transform.position)<.5f)throw new Exception("Door failed to open: "+d.name);open++;
    Vector3 foot=closedBounds.center;foot.y=closedBounds.min.y+.1f;
    var blocked=Physics.OverlapCapsule(foot+Vector3.up*.36f,foot+Vector3.up*1.5f,.28f,1<<DemoWorld.WorldLayer,QueryTriggerInteraction.Ignore);
    if(blocked.Length>0)throw new Exception("Open doorway blocked: "+d.name+" by "+string.Join(",",blocked.Select(c=>c.name)));

    d.SetOpen(false);d.Advance(10);Physics.SyncTransforms();if(d.Fraction>.001f||Vector3.Distance(start,d.transform.position)>.001f)throw new Exception("Door failed to close: "+d.name);
    if(d.isGate&&(d.partner==null||d.partner.partner!=d))throw new Exception("Unpaired gate");
   }
   var barracks=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.Contains("[418_01_1]")).ToArray();
   if(barracks.Length==0||barracks.Any(b=>!b.GetComponentsInChildren<Transform>().Any(t=>t.name=="Authored attachment 418_02_1")))throw new Exception("Barracks interior missing");
   var terrain=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IGIRebuilt/Trainyard/Trainyard.igiground");var detail=terrain.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.shader.name=="IGI Rebuilt/Terrain detail").ToArray();
   if(detail.Length==0||detail.Any(r=>r.sharedMaterial.mainTexture==null))throw new Exception("Terrain detail missing");
   // Closing must wait while an actor occupies the panel's next swept volume.
   var test=doors.First(d=>!d.isGate);test.SetOpen(true);test.Advance(10);Physics.SyncTransforms();var panel=test.GetComponentInChildren<Collider>();
   var actor=new GameObject("Door obstruction probe");actor.AddComponent<Health>();actor.AddComponent<BoxCollider>().size=Vector3.one*.2f;actor.transform.position=panel.bounds.center;Physics.SyncTransforms();
   test.SetOpen(false);test.Advance(.1f);if(test.Fraction<.999f)throw new Exception("Door closed onto actor");UnityEngine.Object.DestroyImmediate(actor);test.Advance(10);
   Debug.Log($"REPAIR_VALIDATION_PASS: {barracks.Length} barracks interiors, {open} doors opened/closed, paired gates, actor obstruction, {detail.Length} terrain detail materials");
  }
 }
}
