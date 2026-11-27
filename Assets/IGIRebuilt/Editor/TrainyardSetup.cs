using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IGIRebuilt.Editor
{
    public static class TrainyardSetup
    {
        public const string ScenePath = "Assets/IGIRebuilt/Scenes/Trainyard.unity";
        public const string EnvironmentPath = "Assets/IGIRebuilt/Trainyard/TrainyardEnvironment.prefab";
        [MenuItem("Tools/IGI Rebuilt/Rebuild playable Trainyard")]
        public static void RebuildMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildMission(); }
        public static void BuildMission()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SurfaceInteractionSetup.EnsureSettings();
            var imported = AssetDatabase.LoadAssetAtPath<ImportedMissionData>("Assets/IGIImported/Missions/01.igimission");
            if (imported == null) throw new Exception("Import mission 1 first.");
            var placements = imported.placements; var paths = imported.modelPaths;
            string briefing = imported.briefing + "\n\n" + string.Join("\n", imported.objectives) + "\n\n1 Glock   2 MP5 SD3   3 Knife\nWASD / mouse to move. E to secure the truck.";
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Trainyard - original mission environment");
            var layout = root.AddComponent<MissionLayout>(); layout.minimumPlayerHeight = -300; layout.viewDistance = 1800;
            var terrain = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IGIRebuilt/Trainyard/Trainyard.igiground");
            if (terrain == null) throw new Exception("Run tools/TrainyardExport before building the mission.");
            var ground = (GameObject)PrefabUtility.InstantiatePrefab(terrain); ground.transform.SetParent(root.transform, false);
            var guards = new List<Transform>(); GameObject truck = null; int staticModels = 0;
            var placedObjects = new Dictionary<int, GameObject>();
            var bounds = new Bounds(Vector3.zero, new Vector3(260, 0, 260));
            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                if (p.type == "HumanPlayer") { layout.playerSpawn = Marker(root.transform, "Jones spawn", p.position + Vector3.up * 0.15f, p.rotation); continue; }
                GameObject prefab = string.IsNullOrEmpty(paths[i]) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab == null) continue;
                if (p.type.StartsWith("Human"))
                {
                    // Negative-id actors and the truck driver belong to the source cutscenes.
                    if (p.id < 0 || p.id == 4023 || p.id == 3031 || p.model.StartsWith("015") || new Vector2(p.position.x, p.position.z).magnitude > 500) continue;
                    var marker = Marker(root.transform, "Guard spawn " + p.id, p.position + Vector3.up * 0.1f, p.rotation);
                    var spawn = marker.gameObject.AddComponent<GuardSpawnVisual>(); spawn.visualPrefab = prefab; spawn.sourceId = p.id;
                    guards.Add(marker); bounds.Encapsulate(new Vector3(p.position.x, 0, p.position.z)); continue;
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = p.id + " " + p.name + " [" + p.model + "]";
                instance.transform.SetParent(root.transform, false); instance.transform.localPosition = p.position; instance.transform.localEulerAngles = p.rotation;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    filter.gameObject.layer = DemoWorld.WorldLayer; filter.gameObject.isStatic = true;
                    if (filter.GetComponent<MeshCollider>() != null) { filter.gameObject.isStatic = false; continue; }
                    if (filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0)
                    {
                        string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                        var solid = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().FirstOrDefault(m => m.name.EndsWith("_collision"));
                        if (solid == null) throw new Exception("Missing collision mesh for " + instance.name);
                        if (solid.triangles.Length > 0) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = solid;
                    }
                }
                if (p.id == 1099) truck = instance;
                if (p.id >= 0) placedObjects[p.id] = instance;
                staticModels++;
            }
            if (layout.playerSpawn == null || truck == null || guards.Count < 20) throw new Exception("Trainyard source markers incomplete.");
            FirstBuildingSetup.Add(placedObjects);
            TrainyardAttachmentSetup.Add(placedObjects, root.transform);
            TrainyardLadderSetup.Add(placedObjects);
            TrainyardSecuritySetup.Add(placedObjects, root.transform);
            TrainyardGuardRouteSetup.Add(root.transform, guards.ToArray());
            Physics.SyncTransforms(); SnapToGround(layout.playerSpawn); foreach (var marker in guards) SnapToGround(marker);
            if (!VisualFactory.TryBounds(truck, out var truckBounds)) throw new Exception("Truck has no render mesh.");
            foreach (var collider in truck.GetComponentsInChildren<Collider>()) truckBounds.Encapsulate(collider.bounds);
            Vector3 side = truckBounds.size.z > truckBounds.size.x ? Vector3.right : Vector3.back;
            Vector3 door = truckBounds.center + side * (truckBounds.size.z > truckBounds.size.x ? truckBounds.extents.x + 0.6f : truckBounds.extents.z + 0.6f); door.y = truckBounds.min.y + 1.1f;
            bool reachable = false;
            foreach (var candidateSide in new[] { side, -side, Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            {
                float extent = Mathf.Abs(candidateSide.x) > 0.5f ? truckBounds.extents.x : truckBounds.extents.z;
                Vector3 candidate = truckBounds.center + candidateSide * (extent + 0.7f);
                Vector3 approach = candidate + candidateSide * 1.8f;
                if (!Physics.Raycast(approach + Vector3.up * 4, Vector3.down, out var support, 12, 1 << DemoWorld.WorldLayer)) continue;
                if (Mathf.Abs(support.point.y - truckBounds.min.y) > 1.2f) continue;
                candidate.y = support.point.y + 0.9f; approach.y = support.point.y;
                if (Physics.CheckCapsule(approach + Vector3.up * 0.35f, approach + Vector3.up * 1.5f, 0.32f, 1 << DemoWorld.WorldLayer)) continue;
                Vector3 eye = approach + Vector3.up * 1.73f, delta = candidate - eye;
                if (Physics.Raycast(eye, delta.normalized, delta.magnitude - 0.2f, 1 << DemoWorld.WorldLayer)) continue;
                door = candidate; side = candidateSide; reachable = true; break;
            }
            if (!reachable) throw new Exception("No clear player approach to the truck interaction.");
            layout.terminal = Marker(truck.transform, "Truck driver interaction", Vector3.zero, Vector3.zero);
            layout.terminal.position = door; layout.terminal.rotation = Quaternion.LookRotation(side);
            layout.extraction = layout.terminal; layout.guardSpawns = guards.ToArray();
            layout.navigationCentre = new Vector3(bounds.center.x, 0, bounds.center.z); layout.navigationSize = new Vector2(bounds.size.x + 100, bounds.size.z + 100);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, EnvironmentPath, InteractionMode.AutomatedAction);
            var config = AssetDatabase.LoadAssetAtPath<GameConfiguration>(ProjectSetup.ConfigurationPath);
            if (config == null) { config = ScriptableObject.CreateInstance<GameConfiguration>(); AssetDatabase.CreateAsset(config, ProjectSetup.ConfigurationPath); }
            config.missionName = "01 / TRAINYARD"; config.captureTruck = true; config.briefing = briefing;
            config.environmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath);
            config.enemyVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IGIImported/Characters/Security.igimodel");
            config.enemyCount = guards.Count; config.enemyHealth = 100; config.enemyDamage = 8; config.enemySightRange = 70;
            config.weapons = OriginalArsenalSetup.Create();
            config.briefing = briefing.Replace("1 Glock   2 MP5 SD3   3 Knife", "1 Glock / 2 MP5 / 3 Knife; mouse wheel: full arsenal");
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            var bootstrap = new GameObject("Trainyard gameplay").AddComponent<GameBootstrap>(); bootstrap.configuration = config; bootstrap.gameObject.AddComponent<SmokeTestDriver>();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/trainyard-build-data.json", "{\"staticModels\":" + staticModels + ",\"guards\":" + guards.Count + ",\"truckId\":1099}");
            Debug.Log("TRAINYARD_READY: " + staticModels + " original placed models, " + guards.Count + " guards, original terrain, full player arsenal and truck objective.");
        }
        private static Transform Marker(Transform parent, string name, Vector3 position, Vector3 rotation)
        {
            var marker = new GameObject(name).transform; marker.SetParent(parent, false); marker.localPosition = position; marker.localEulerAngles = rotation; return marker;
        }
        private static void SnapToGround(Transform marker)
        {
            if (Physics.Raycast(marker.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 3, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) marker.position = hit.point + Vector3.up * 0.08f;
        }
        public static void BuildWindows()
        {
            BuildMission(); Directory.CreateDirectory("Build/Windows");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64, locationPathName = "Build/Windows/IGIRebuilt.exe", options = BuildOptions.None });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Trainyard player build failed.");
        }
    }
}

