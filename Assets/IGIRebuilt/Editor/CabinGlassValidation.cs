using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class CabinGlassValidation
    {
        static IEnumerator routine; static double started;
        static CabinGlassValidation()
        {
            if (!SessionState.GetBool("IGI_CABIN_GLASS_TEST", false)) return;
            started = EditorApplication.timeSinceStartup; EditorApplication.update += Update;
        }
        public static void Run()
        {
            SurfaceInteractionSetup.Apply();
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetBool("IGI_CABIN_GLASS_TEST", true); started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update; EditorApplication.update += Update; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Cabin glass validation timed out");
                if (!EditorApplication.isPlaying) return;
                var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(); if (boot == null || boot.Mission == null) return;
                if (routine == null) routine = Checks(boot.Mission);
                if (routine.MoveNext()) return;
                SessionState.SetBool("IGI_CABIN_GLASS_TEST", false);
                Debug.Log("CABIN_GLASS_VALIDATION_PASS: checkpoint 1110, all three cabin windows, guard sight, player and enemy shots, glass sounds and shards, cabin frame and barred barracks windows intact");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { SessionState.SetBool("IGI_CABIN_GLASS_TEST", false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static void Place(Transform actor, Vector3 at, Vector3 facing)
        {
            var controller = actor.GetComponent<CharacterController>(); controller.enabled = false;
            actor.SetPositionAndRotation(at, Quaternion.LookRotation(facing)); controller.enabled = true; Physics.SyncTransforms();
        }
        static Vector3 Normal(BulletSurface pane)
        {
            var collider = pane.GetComponent<MeshCollider>(); var vertices = collider.sharedMesh.vertices; var ids = collider.sharedMesh.triangles;
            return pane.transform.TransformDirection(Vector3.Cross(vertices[ids[1]] - vertices[ids[0]], vertices[ids[2]] - vertices[ids[0]]).normalized);
        }
        static void Capture(Transform cabin, string file)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var bounds = cabin.GetComponent<Renderer>().bounds;
            var camera = new GameObject("Checkpoint cabin preview").AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = bounds.center + cabin.TransformDirection(new Vector3(1, .3f, 1)).normalized * Mathf.Max(5, bounds.extents.magnitude * 2.2f);
            camera.transform.LookAt(bounds.center); camera.fieldOfView = 55;
            var target = new RenderTexture(640, 640, 24); var old = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(640, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + file + ".png", image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(camera.gameObject);
        }
        static IEnumerator Checks(MissionDirector mission)
        {
            mission.SetPaused(false); mission.player.enabled = false; mission.player.GetComponent<Health>().ResetHealth(10000);
            var guards = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None);
            foreach (var other in guards) { other.enabled = false; other.GetComponent<GuardPresentation>().enabled = false; }
            foreach (var camera in mission.Security.cameras) camera.enabled = false;
            var cabin = UnityEngine.Object.FindObjectsByType<ImportedModelInfo>(FindObjectsSortMode.None).First(info => info.originalModel == "425_01_1").transform;
            var panes = cabin.GetComponentsInChildren<BulletSurface>().Where(s => s.kind == BulletSurfaceKind.Glass && (s.GetComponentInParent<ImportedModelInfo>().originalModel == "425_02_1" || s.GetComponentInParent<ImportedModelInfo>().originalModel == "425_03_1")).ToArray();
            Debug.Log("CABIN_BINDINGS: " + cabin.name + " scene=" + cabin.gameObject.scene.name + " glass=" + panes.Length + " parts=" + string.Join(";", cabin.GetComponentsInChildren<ImportedModelInfo>().Select(i => i.name + ":" + i.originalModel)));
            foreach (var pane in panes) Debug.Log("CABIN_PANE: " + pane.name + " parent=" + pane.transform.parent.name + " breakable=" + pane.Breakable);
            Require(panes.Length == 3 && panes.All(p => p.Breakable), "Checkpoint cabin must have three breakable glass windows; found " + panes.Length);
            var barred = UnityEngine.Object.FindObjectsByType<BulletSurface>(FindObjectsSortMode.None).First(s => s.kind == BulletSurfaceKind.Glass && s.GetComponentInParent<ImportedModelInfo>().originalModel == "418_03_1");
            barred.Break(barred.GetComponent<Collider>().bounds.center);
            Require(!barred.Breakable && !barred.Broken && barred.GetComponent<Collider>().enabled && barred.GetComponent<Renderer>().enabled, "Barred barracks window should remain intact");
            var guard = guards.First(g => g.name.Contains("[2025]")); guard.sightRange = guard.fireRange = 40; guard.GetComponent<Health>().ResetHealth(1000);
            Place(guard.transform, new Vector3(1000, 100, 1000), Vector3.forward);
            Place(mission.player.transform, new Vector3(1000, 100, 1010), Vector3.back);
            Capture(cabin, "checkpoint-cabin-intact");
            for (int i = 0; i < panes.Length; i++)
            {
                var pane = panes[i]; Vector3 centre = pane.GetComponent<Collider>().bounds.center, normal = Normal(pane);
                // The centre of the front window lines up with a real fence post outside.
                // Sample a clear point on the pane rather than testing sight through that post.
                foreach (float offset in new[] { 0f, .35f, -.35f, .6f, -.6f })
                {
                    Vector3 sample = centre + Vector3.Cross(Vector3.up, normal) * offset;
                    if (!pane.GetComponent<Collider>().Raycast(new Ray(sample + normal * .3f, -normal), out _, .6f)) continue;
                    Vector3 a = sample + normal * .55f - Vector3.up * 1.4f, b = sample - normal * .55f - Vector3.up * 1.4f;
                    Place(guard.transform, a, -normal); Place(mission.player.transform, b, normal);
                    if (guard.CanSee(b + Vector3.up * 1.25f) && !WorldOcclusion.Blocked(a + Vector3.up * 1.4f, b + Vector3.up * 1.25f)) break;
                }
                Vector3 point = mission.player.transform.position + Vector3.up * 1.25f;
                if (!guard.CanSee(point))
                {
                    Vector3 eye = guard.transform.position + Vector3.up * 1.55f, delta = point - eye;
                    foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore))
                        Debug.Log("CABIN_SIGHT_HIT: " + hit.collider.name + " kind=" + BulletSurface.KindOf(hit) + " at=" + hit.point);
                    foreach (var hit in Physics.SphereCastAll(eye, .03f, delta.normalized, delta.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore).Concat(Physics.SphereCastAll(point, .03f, -delta.normalized, delta.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)))
                        Debug.Log("CABIN_SPHERE_HIT: " + hit.collider.name + " kind=" + BulletSurface.KindOf(hit) + " at=" + hit.point + " triangle=" + hit.triangleIndex + " distance=" + hit.distance);
                    bool playerHit = Physics.Raycast(eye, delta.normalized, out var playerRay, delta.magnitude + .2f, 1 << DemoWorld.PlayerLayer, QueryTriggerInteraction.Ignore);
                    Debug.Log("CABIN_SIGHT_CHECK: lineBlocked=" + WorldOcclusion.Blocked(eye, point) + " radiusBlocked=" + WorldOcclusion.Blocked(eye, point, .03f) + " angle=" + Vector3.Angle(guard.transform.forward, delta) + " playerHit=" + (playerHit ? playerRay.collider.name : "none"));
                }
                Require(guard.CanSee(point), "Cabin window " + i + " is blocked by its surrounding collision");
                int soundBefore = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.clip != null && s.clip.name.StartsWith("glass_crush_"));
                if (i == 0)
                {
                    // Player fires from inside the actual cabin through its window at the guard.
                    mission.weapon.Equip(0); mission.player.eye.transform.LookAt(guard.transform.position + Vector3.up * 1.25f);
                    float ready = Time.time + .25f; while (Time.time < ready) yield return null;
                    float health = guard.GetComponent<Health>().Current;
                    Require(mission.weapon.Fire(), "Player could not fire");
                    Require(guard.GetComponent<Health>().Current < health, "Player bullet did not reach guard through cabin glass");
                }
                else
                {
                    float health = mission.player.GetComponent<Health>().Current;
                    typeof(EnemyGuard).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guard, new object[] { point });
                    Require(mission.player.GetComponent<Health>().Current < health, "Enemy bullet did not reach player through cabin glass");
                }
                Require(pane.Broken && !pane.GetComponent<Renderer>().enabled && !pane.GetComponent<Collider>().enabled, "Cabin pane did not shatter");
                Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.clip != null && s.clip.name.StartsWith("glass_crush_")) == soundBefore + 1, "Cabin pane did not play one imported break sound");
                Require(panes.Skip(i + 1).All(p => !p.Broken), "A cabin shot broke neighboring windows");
            }
            Require(cabin.GetComponent<Renderer>().enabled && cabin.GetComponent<Collider>().enabled, "Breaking glass disabled cabin walls or frame");
            Place(guard.transform, new Vector3(1000, 100, 1000), Vector3.forward);
            Place(mission.player.transform, new Vector3(1000, 100, 1010), Vector3.back);
            Capture(cabin, "checkpoint-cabin-broken");
        }
    }
}
