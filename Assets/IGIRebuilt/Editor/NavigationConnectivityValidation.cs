using System;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    internal static class NavigationConnectivityValidation
    {
        public static void Check()
        {
            var root = new GameObject("Disconnected rooftop navigation regression");
            bool diagnostics = NavigationDiagnostics.Enabled;
            try
            {
                Vector3 origin = new Vector3(1300, 100, 1300);
                GameObject Box(string name, Vector3 centre, Vector3 size)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
                    box.transform.SetParent(root.transform); box.layer = DemoWorld.WorldLayer;
                    box.transform.position = centre; box.transform.localScale = size; return box;
                }
                Box("Walkable ground", origin + Vector3.down * .5f, new Vector3(24, 1, 24));
                Box("Disconnected rooftop", origin + Vector3.up * 3.5f, new Vector3(6, 1, 6));
                var grid = root.AddComponent<GridNavigation>(); grid.cellSize = 1; grid.width = grid.depth = 20;
                grid.worldCentre = origin; grid.sampleTerrain = true;
                Physics.SyncTransforms(); grid.Rebuild();
                NavigationDiagnostics.Enabled = true; NavigationDiagnostics.Reset();
                Vector3 start = origin + Vector3.right * 8;
                if (grid.FindPath(start, origin + Vector3.up * 4).Count != 0) throw new Exception("Ground guard found an impossible rooftop route");
                if (NavigationDiagnostics.ExpandedNodes != 0 || NavigationDiagnostics.DisconnectedRejects != 1)
                    throw new Exception("Disconnected rooftop triggered an exhaustive search");
                Vector3 destination = origin + Vector3.left * 8;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    var path = grid.FindPath(start, destination);
                    if (path.Count < 2 || Vector3.Distance(path[path.Count - 1], destination) > 1) throw new Exception("Reachable ground route was rejected");
                    for (int i = 1; i < path.Count; i++)
                        if (!AuthoredNavigation.ClearSegment(path[i - 1], path[i], .32f)) throw new Exception("Route crossed an obstacle");
                }
                Debug.Log("NAVIGATION_CONNECTIVITY_PASS: disconnected roof rejects without A*, reachable ground detour, and repeated searches preserve clearance");
            }
            finally
            {
                NavigationDiagnostics.Enabled = diagnostics; root.SetActive(false); UnityEngine.Object.Destroy(root); Physics.SyncTransforms();
            }
        }
    }
}
