using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace IGIRebuilt.Editor
{
    [Serializable] internal sealed class GuardRouteRow { public int id, graphId; public GuardPatrolCommand[] patrol, alarm; }
    [Serializable] internal sealed class GuardRouteDocument { public AuthoredGraph[] graphs; public GuardRouteRow[] guards; }
    internal static class TrainyardGuardRouteSetup
    {
        public static void Add(Transform root, Transform[] guards)
        {
            var data = JsonUtility.FromJson<GuardRouteDocument>(File.ReadAllText("Assets/IGIRebuilt/Trainyard/TrainyardGuardRoutes.json")); var nav = root.gameObject.AddComponent<AuthoredNavigation>(); nav.graphs = data.graphs;
            foreach (var marker in guards) { var info = marker.GetComponent<GuardSpawnVisual>(); var route = Array.Find(data.guards, g => g.id == info.sourceId); if (route == null) throw new Exception("Guard route missing " + info.sourceId); info.graphId = route.graphId; info.patrol = route.patrol; info.alarm = route.alarm; var look = route.patrol.FirstOrDefault(c => c.kind == 5); if (look != null) { Vector3 direction = look.position - marker.position; direction.y = 0; if (direction.sqrMagnitude > .01f) marker.rotation = Quaternion.LookRotation(direction); } }
            Debug.Log("GUARD_ROUTES_READY: native graphs, source patrol/look commands, authored alarm response paths");
        }
    }
}
