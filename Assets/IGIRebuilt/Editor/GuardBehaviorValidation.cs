using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class GuardBehaviorValidation
    {
        static IEnumerator routine;
        static double started;
        static GuardBehaviorValidation()
        {
            if (!SessionState.GetBool("IGI_GUARD_BEHAVIOR_TEST", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Update;
        }
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetBool("IGI_GUARD_BEHAVIOR_TEST", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.isPlaying = true;
        }
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Guard behavior validation timed out");
                if (!EditorApplication.isPlaying) return;
                var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (boot == null || boot.Mission == null) return;
                if (routine == null) routine = Checks(boot.Mission);
                if (routine.MoveNext()) return;
                SessionState.SetBool("IGI_GUARD_BEHAVIOR_TEST", false);
                Debug.Log("GUARD_BEHAVIOR_VALIDATION_PASS: native moving-fire clips, graph-to-grid fallback, wall detour, single-hit retaliation, interrupted-sight firing, moving-fire presentation, double-sided sight, 40 blocked wall shots and barracks walls");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                SessionState.SetBool("IGI_GUARD_BEHAVIOR_TEST", false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        static void Place(Transform actor, Vector3 position, Vector3 forward)
        {
            var controller = actor.GetComponent<CharacterController>();
            controller.enabled = false;
            actor.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            controller.enabled = true;
            Physics.SyncTransforms();
        }
        static IEnumerator Checks(MissionDirector mission)
        {
            NavigationConnectivityValidation.Check();
            mission.SetPaused(false);
            mission.player.enabled = false;
            mission.player.GetComponent<Health>().ResetHealth(10000);
            var guards = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None);
            foreach (var other in guards) { other.enabled = false; other.GetComponent<GuardPresentation>().enabled = false; }
            foreach (var camera in mission.Security.cameras) camera.enabled = false;
            // Sample real vertical collision faces in every authored barracks from both sides.
            var barracks = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.Contains("[418_01_1]")).ToArray();
            Require(barracks.Length == 5, "Expected all five barracks");
            foreach (var building in barracks)
            {
                int samples = 0;
                foreach (var collider in building.GetComponentsInChildren<MeshCollider>())
                {
                    var vertices = collider.sharedMesh.vertices;
                    var triangles = collider.sharedMesh.triangles;
                    for (int i = 0; i < triangles.Length && samples < 12; i += 3)
                    {
                        Vector3 a = collider.transform.TransformPoint(vertices[triangles[i]]), b = collider.transform.TransformPoint(vertices[triangles[i + 1]]), c = collider.transform.TransformPoint(vertices[triangles[i + 2]]);
                        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                        if (Mathf.Abs(normal.y) > .1f || Vector3.Cross(b - a, c - a).magnitude < .2f) continue;
                        Vector3 centre = (a + b + c) / 3;
                        Require(WorldOcclusion.Blocked(centre - normal * .3f, centre + normal * .3f) && WorldOcclusion.Blocked(centre + normal * .3f, centre - normal * .3f), "One-sided barracks visibility: " + building.name);
                        samples++;
                    }
                }
                Require(samples > 0, "No barracks walls checked");
            }
            var settings = Resources.Load<GuardPresentationSettings>("GuardPresentationSettings");
            foreach (var binding in settings.motions)
                foreach (var pair in new[] { new[] { "Run", "RunFire" }, new[] { "Walk", "WalkFire" } })
                {
                    var gait = binding.motion.clips.First(c => c.name == pair[0]);
                    var combined = binding.motion.clips.First(c => c.name == pair[1]);
                    Require(gait.frames == combined.frames && combined.metresPerSecond > .5f, "Moving-fire gait metadata missing");
                    int lower = 0, changed = 0;
                    for (int i = 0; i < binding.motion.vertexCount; i++)
                    {
                        float difference = Vector3.Distance(gait.positions[i], combined.positions[i]);
                        if (gait.positions[i].y < -.5f) { Require(difference < .001f, "Firing overlay changed feet"); lower++; }
                        if (difference > .01f) changed++;
                    }
                    Require(lower > 0 && changed > 10, "Moving-fire clip lacks independent lower and upper body motion");
                }
            Vector3 arena = new Vector3(1000, 100, 1000);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = DemoWorld.WorldLayer; floor.transform.position = arena + Vector3.down * .6f; floor.transform.localScale = new Vector3(100, 1, 100);
            Place(mission.player.transform, arena + Vector3.forward * 6, Vector3.back);
            var guard = guards.First();
            guard.authoredPatrol = null; guard.authoredAlarm = null; guard.sightRange = guard.fireRange = 60;
            var health = guard.GetComponent<Health>(); health.ResetHealth(10000);
            var presentation = guard.GetComponent<GuardPresentation>();
            var animation = presentation.animationPlayer;
            Place(guard.transform, arena + Vector3.back * 6, Vector3.forward);
            // Single triangles must obstruct sight regardless of their winding.
            var sheet = new GameObject("Single-sided sight wall"); sheet.layer = DemoWorld.WorldLayer;
            var mesh = new Mesh(); mesh.vertices = new[] { arena + new Vector3(-4, -1, 0), arena + new Vector3(4, -1, 0), arena + new Vector3(0, 5, 0) }; mesh.triangles = new[] { 0, 1, 2 };
            sheet.AddComponent<MeshCollider>().sharedMesh = mesh;
            Physics.SyncTransforms();
            Require(!guard.CanSee(mission.player.transform.position + Vector3.up * 1.25f), "Guard sees through back of single-sided wall");
            var shoot = typeof(EnemyGuard).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
            var playerHealth = mission.player.GetComponent<Health>();
            float beforeWallShots = playerHealth.Current;
            for (int shot = 0; shot < 20; shot++) shoot.Invoke(guard, new object[] { mission.player.transform.position + Vector3.up * 1.25f });
            Require(playerHealth.Current == beforeWallShots, "Guard bullets passed through the back face of a wall");
            mesh.triangles = new[] { 2, 1, 0 }; sheet.GetComponent<MeshCollider>().sharedMesh = mesh; Physics.SyncTransforms();
            Require(!guard.CanSee(mission.player.transform.position + Vector3.up * 1.25f), "Guard sees through front of single-sided wall");
            for (int shot = 0; shot < 20; shot++) shoot.Invoke(guard, new object[] { mission.player.transform.position + Vector3.up * 1.25f });
            Require(playerHealth.Current == beforeWallShots, "Guard bullets passed through the front face of a wall");
            typeof(EnemyGuard).GetField("rounds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(guard, 24);
            UnityEngine.Object.DestroyImmediate(sheet); UnityEngine.Object.DestroyImmediate(mesh);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = DemoWorld.WorldLayer;
            wall.transform.position = arena + Vector3.up; wall.transform.localScale = new Vector3(6, 3, .5f);
            var nav = new GameObject("Fallback navigation").AddComponent<GridNavigation>();
            nav.cellSize = 1; nav.width = nav.depth = 30; nav.worldCentre = arena;
            Physics.SyncTransforms(); nav.Rebuild();
            guard.navigation = nav; guard.graphId = int.MinValue;
            guard.Alert(mission.player.transform.position + Vector3.up * 1.73f);
            guard.enabled = true;
            float end = Time.time + 14;
            float largestDetour = 0;
            while (Time.time < end && !guard.HasSight)
            {
                largestDetour = Mathf.Max(largestDetour, Mathf.Abs(guard.transform.position.x - arena.x));
                yield return null;
            }
            Require(largestDetour > 3 && guard.HasSight, "Guard failed to route around wall after authored graph failed: " + guard.transform.position);
            Debug.Log("GUARD_WALL_DETOUR_PASS: lateral detour=" + largestDetour);
            guard.enabled = false;
            UnityEngine.Object.DestroyImmediate(wall);
            Place(guard.transform, arena + Vector3.back * 10, Vector3.back);
            Place(mission.player.transform, arena, Vector3.back);
            mission.SetPaused(true); presentation.SendMessage("LateUpdate"); mission.SetPaused(false);
            presentation.enabled = true;
            animation.Play("Idle");
            health.TakeDamage(1, Team.Player, mission.player.transform.position + Vector3.up * 1.73f);
            Require(animation.Current == "Hit", "Expected single-hit reaction");
            var rounds = typeof(EnemyGuard).GetField("rounds", BindingFlags.Instance | BindingFlags.NonPublic);
            int before = (int)rounds.GetValue(guard);
            guard.enabled = true;
            end = Time.time + 2;
            while (Time.time < end) yield return null;
            Require((int)rounds.GetValue(guard) < before, "Guard stood idle after one hit");
            var shutter = GameObject.CreatePrimitive(PrimitiveType.Cube); shutter.layer = DemoWorld.WorldLayer;
            shutter.transform.position = arena + new Vector3(0, 1, -5); shutter.transform.localScale = new Vector3(6, 3, .3f);
            before = (int)rounds.GetValue(guard);
            for (int segment = 0; segment < 30; segment++)
            {
                shutter.SetActive(segment % 2 == 0); Physics.SyncTransforms();
                end = Time.time + .1f;
                while (Time.time < end) yield return null;
            }
            Require((int)rounds.GetValue(guard) < before, "Brief sight interruptions repeatedly postponed retaliation");
            UnityEngine.Object.DestroyImmediate(shutter);
            guard.enabled = false;
            Place(guard.transform, arena + Vector3.back * 30, Vector3.forward);
            mission.SetPaused(true); presentation.SendMessage("LateUpdate"); mission.SetPaused(false);
            guard.ReceiveAttack(mission.player.transform.position);
            guard.enabled = true;
            bool movingFire = false;
            before = (int)rounds.GetValue(guard);
            end = Time.time + 2;
            while (Time.time < end)
            {
                movingFire |= animation.Current == "RunFire";
                yield return null;
            }
            Require(movingFire && (int)rounds.GetValue(guard) < before && guard.transform.position.z > arena.z - 29, "Running attack did not combine movement, shooting and RunFire animation");
            Require(animation.WeaponSocket != null, "Moving-fire weapon socket missing");
        }
    }
}
