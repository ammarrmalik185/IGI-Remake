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
    public static class SurfaceInteractionValidation
    {
        static IEnumerator routine; static double started;
        static SurfaceInteractionValidation()
        {
            if (!SessionState.GetBool("IGI_SURFACE_TEST", false)) return;
            started = EditorApplication.timeSinceStartup; EditorApplication.update += Update;
        }
        public static void Run()
        {
            SurfaceInteractionSetup.Apply();
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetBool("IGI_SURFACE_TEST", true); started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update; EditorApplication.update += Update; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Surface validation timed out");
                if (!EditorApplication.isPlaying) return;
                var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(); if (boot == null || boot.Mission == null) return;
                if (routine == null) routine = Checks(boot.Mission);
                if (routine.MoveNext()) return;
                SessionState.SetBool("IGI_SURFACE_TEST", false);
                Debug.Log("SURFACE_INTERACTION_VALIDATION_PASS: native fence classification, glass pane splitting, guard vision through fence/glass, probabilistic fence interception, imported positional audio, glass shattering, player/enemy bullet penetration, solid wall protection");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { SessionState.SetBool("IGI_SURFACE_TEST", false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static BulletSurface Panel(string name, Vector3 point, BulletSurfaceKind kind)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = DemoWorld.WorldLayer;
            go.transform.position = point; go.transform.localScale = new Vector3(4, 3, .03f);
            var surface = go.AddComponent<BulletSurface>(); surface.kind = kind; return surface;
        }
        static void Place(Transform actor, Vector3 at, Vector3 facing)
        {
            var controller = actor.GetComponent<CharacterController>(); controller.enabled = false;
            actor.SetPositionAndRotation(at, Quaternion.LookRotation(facing)); controller.enabled = true; Physics.SyncTransforms();
        }
        static void Capture(BulletSurface pane, string name)
        {
            var mesh = pane.GetComponent<MeshFilter>().sharedMesh;
            var ids = mesh.triangles; var points = mesh.vertices;
            Vector3 normal = pane.transform.TransformDirection(Vector3.Cross(points[ids[1]] - points[ids[0]], points[ids[2]] - points[ids[0]]).normalized);
            var bounds = pane.GetComponent<Renderer>().bounds;
            var camera = new GameObject("Glass preview camera").AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = bounds.center + normal * 3;
            if (WorldOcclusion.Blocked(camera.transform.position, bounds.center)) camera.transform.position = bounds.center - normal * 3;
            camera.transform.LookAt(bounds.center); camera.orthographic = true; camera.orthographicSize = Mathf.Max(.6f, bounds.extents.magnitude * 1.4f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.4f, .5f, .6f);
            var target = new RenderTexture(512, 512, 24); var old = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("Logs"); System.IO.File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(camera.gameObject);
        }
        static IEnumerator Checks(MissionDirector mission)
        {
            mission.SetPaused(false); mission.player.enabled = false; mission.player.GetComponent<Health>().ResetHealth(10000);
            var guards = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None);
            foreach (var other in guards) { other.enabled = false; other.GetComponent<GuardPresentation>().enabled = false; }
            foreach (var camera in mission.Security.cameras) camera.enabled = false;
            var imported = UnityEngine.Object.FindObjectsByType<BulletSurface>(FindObjectsSortMode.None);
            var fences = imported.Where(s => s.data != null && s.data.triangleKinds.Contains((int)BulletSurfaceKind.Fence)).ToArray();
            var panes = imported.Where(s => s.kind == BulletSurfaceKind.Glass).ToArray();
            Require(fences.Length > 0 && panes.Length > 0, "Trainyard fence/glass metadata not bound");
            Require(fences.Any(s => s.GetComponent<ImportedModelInfo>()?.originalModel == "303_02_1"), "Perimeter fence still uses coarse unlabeled collision");
            Require(panes.All(p => p.GetComponent<MeshFilter>().sharedMesh.triangles.Length > 0 && p.GetComponent<MeshCollider>() != null), "Imported glass lacks independent render/collision geometry");
            bool nativeFenceChecked = false;
            foreach (var fence in fences)
            {
                var collider = fence.GetComponent<MeshCollider>(); if (collider == null) continue;
                var vertices = collider.sharedMesh.vertices; var ids = collider.sharedMesh.triangles;
                for (int triangle = 0; triangle < fence.data.triangleKinds.Length && !nativeFenceChecked; triangle++)
                {
                    if (fence.KindAt(triangle) != BulletSurfaceKind.Fence) continue;
                    Vector3 a = fence.transform.TransformPoint(vertices[ids[triangle * 3]]), b = fence.transform.TransformPoint(vertices[ids[triangle * 3 + 1]]), c = fence.transform.TransformPoint(vertices[ids[triangle * 3 + 2]]);
                    Vector3 n = Vector3.Cross(b - a, c - a).normalized, centre = (a + b + c) / 3;
                    if (!collider.Raycast(new Ray(centre + n * .2f, -n), out var hit, .4f)) continue;
                    if (BulletSurface.KindOf(hit) != BulletSurfaceKind.Fence) continue;
                    if (WorldOcclusion.Blocked(centre + n * .2f, centre - n * .2f, .03f)) continue;
                    nativeFenceChecked = true;
                }
                if (nativeFenceChecked) break;
            }
            Require(nativeFenceChecked, "Native fence material still occludes vision");
            var settings = SurfaceEffects.Settings;
            Require(settings != null && settings.fenceImpacts.Length == 2 && settings.glassBreaks.Length == 3 && settings.shardMaterial != null, "Imported impact sounds or shard material missing");
            Require(settings.fenceImpacts.All(c => c != null && c.length > 0) && settings.glassBreaks.All(c => c != null && c.length > 0), "Surface audio clips invalid");
            Vector3 arena = new Vector3(1000, 100, 1000);
            var guard = guards.First(); guard.sightRange = guard.fireRange = 40; guard.GetComponent<Health>().ResetHealth(1000);
            Place(guard.transform, arena, Vector3.forward); Place(mission.player.transform, arena + Vector3.forward * 10, Vector3.back);
            var fenceProbe = Panel("Fence probe", arena + new Vector3(0, 1.4f, 3), BulletSurfaceKind.Fence);
            var glass = Panel("Glass probe", arena + new Vector3(0, 1.4f, 6), BulletSurfaceKind.Glass);
            Physics.SyncTransforms();
            Require(guard.CanSee(mission.player.transform.position + Vector3.up * 1.25f), "Guard cannot see player through fence and intact glass");
            int mask = (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.PlayerLayer);
            Vector3 eye = arena + Vector3.up * 1.4f;
            fenceProbe.fenceBlockChance = 1;
            Require(BulletTrace.Cast(eye, Vector3.forward, 15, mask, guard.transform, out var stopped) && stopped.collider.gameObject == fenceProbe.gameObject && !glass.Broken, "Fence failed to intercept bullet before glass");
            Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a.clip != null && a.clip.name.StartsWith("bul_metal_fnc_")), "Fence hit did not play imported audio");
            fenceProbe.fenceBlockChance = 0;
            Require(BulletTrace.Cast(eye, Vector3.forward, 15, mask, guard.transform, out var passed) && passed.collider.GetComponentInParent<FirstPersonMotor>() != null, "Bullet failed to penetrate fence and glass");
            Require(glass.Broken && !glass.GetComponent<Renderer>().enabled && !glass.GetComponent<Collider>().enabled, "Glass did not break visually and physically");
            int breaks = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.clip != null && a.clip.name.StartsWith("glass_crush_"));
            Require(breaks == 1, "Glass break should play once");
            glass.Break(glass.transform.position);
            Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.clip != null && a.clip.name.StartsWith("glass_crush_")) == breaks, "Already-broken glass played sound twice");
            fenceProbe.fenceBlockChance = .2f;
            var random = UnityEngine.Random.state; UnityEngine.Random.InitState(137);
            int blocked = 0, through = 0;
            for (int i = 0; i < 200; i++)
                if (BulletTrace.Cast(eye, Vector3.forward, 15, mask, guard.transform, out var hit))
                { if (hit.collider.gameObject == fenceProbe.gameObject) blocked++; else if (hit.collider.GetComponentInParent<FirstPersonMotor>() != null) through++; }
            UnityEngine.Random.state = random;
            Require(blocked >= 15 && blocked <= 70 && blocked + through == 200, "Fence interception should be occasional, once per crossing: " + blocked + "/200");
            Debug.Log("FENCE_INTERCEPTION_PASS: " + blocked + " blocked, " + through + " penetrated");
            fenceProbe.fenceBlockChance = 0;
            UnityEngine.Object.DestroyImmediate(glass.gameObject);
            glass = Panel("Enemy glass probe", arena + new Vector3(0, 1.4f, 6), BulletSurfaceKind.Glass); Physics.SyncTransforms();
            float before = mission.player.GetComponent<Health>().Current;
            var shoot = typeof(EnemyGuard).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 20; i++) shoot.Invoke(guard, new object[] { mission.player.transform.position + Vector3.up * 1.25f });
            Require(glass.Broken && mission.player.GetComponent<Health>().Current < before, "Enemy fire did not break glass and damage player behind it");
            UnityEngine.Object.DestroyImmediate(glass.gameObject);
            glass = Panel("Player glass probe", arena + new Vector3(0, 1.4f, 6), BulletSurfaceKind.Glass); Physics.SyncTransforms();
            mission.weapon.Equip(0); mission.player.eye.transform.LookAt(guard.transform.position + Vector3.up * 1.25f);
            float ready = Time.time + .25f; while (Time.time < ready) yield return null;
            before = guard.GetComponent<Health>().Current;
            Require(mission.weapon.Fire() && glass.Broken && guard.GetComponent<Health>().Current < before, "Player fire did not penetrate glass and fence");
            var wall = Panel("Solid wall probe", arena + new Vector3(0, 1.4f, 7), BulletSurfaceKind.Solid); Physics.SyncTransforms();
            Require(!guard.CanSee(mission.player.transform.position + Vector3.up * 1.25f), "Solid wall became transparent");
            Require(BulletTrace.Cast(eye, Vector3.forward, 15, mask, guard.transform, out var solid) && solid.collider.gameObject == wall.gameObject, "Bullet penetrated solid wall behind fence/glass");
            var native = panes.First(p => p.transform.parent.GetComponentsInChildren<BulletSurface>().Count(s => s.kind == BulletSurfaceKind.Glass) > 1);
            var siblings = native.transform.parent.GetComponentsInChildren<BulletSurface>().Where(s => s.kind == BulletSurfaceKind.Glass && s != native).ToArray();
            native.Break(native.GetComponent<Collider>().bounds.center);
            Require(siblings.All(s => !s.Broken && s.GetComponent<Renderer>().enabled && s.GetComponent<Collider>().enabled), "Breaking a native pane removed neighboring windows");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var preview = panes.First(p => p.GetComponentInParent<ImportedModelInfo>().originalModel == "425_02_1" && !p.Broken);
                Capture(preview, "cabin-glass-intact"); preview.Break(preview.GetComponent<Collider>().bounds.center);
                foreach (var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) particles.Simulate(.1f, true, false);
                Capture(preview, "cabin-glass-broken");
            }
            Debug.Log("NATIVE_SURFACES_PASS: " + fences.Length + " fence objects, " + panes.Length + " independent glass panes");
        }
    }
}
