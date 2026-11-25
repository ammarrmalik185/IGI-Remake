using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [Serializable] internal class InteriorRoom { public string modelPath; public Vector3 position, forward, up; }
    [Serializable] internal class FirstBuildingDocument { public int parentId; public InteriorRoom[] rooms; public BuildingDoorway[] doors; }
    internal static class FirstBuildingSetup
    {
        public static void Add(Dictionary<int, GameObject> placed)
        {
            var data = JsonUtility.FromJson<FirstBuildingDocument>(File.ReadAllText("Assets/IGIRebuilt/Trainyard/FirstBuildingInterior.json"));
            if (!placed.TryGetValue(data.parentId, out var parent)) throw new Exception("First building missing");
            var layout = parent.AddComponent<FirstBuildingInterior>(); layout.doorways = data.doors;
            var rooms = new List<Transform>();
            foreach (var room in data.rooms)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(room.modelPath);
                if (source == null) throw new Exception("Missing first-building interior: " + room.modelPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = "Authored interior " + source.name;
                instance.transform.SetParent(parent.transform, false); instance.transform.localPosition = room.position;
                instance.transform.localRotation = Quaternion.LookRotation(room.forward, room.up);
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    filter.gameObject.layer = DemoWorld.WorldLayer; filter.gameObject.isStatic = true;
                    string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    var collision = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name.EndsWith("_collision"));
                    if (collision == null) throw new Exception("Missing room collision " + room.modelPath);
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = collision;
                }
                rooms.Add(instance.transform);
            }
            layout.rooms = rooms.ToArray(); Physics.SyncTransforms();
            Debug.Log("FIRST_BUILDING_READY: " + rooms.Count + " authored interiors, " + data.doors.Length + " open doorway portals");
        }
    }
}
