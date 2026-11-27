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
    public static class OriginalArsenalValidation
    {
        private static IEnumerator routine;
        private static double started;
        static OriginalArsenalValidation()
        {
            if (SessionState.GetBool("IGI_ARSENAL_TEST", false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Update; }
        }
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            OriginalArsenalSetup.Apply();
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetBool("IGI_ARSENAL_TEST", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update; EditorApplication.update += Update;
            EditorApplication.isPlaying = true;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 150) throw new Exception("Arsenal validation timed out");
                if (!EditorApplication.isPlaying) return;
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (bootstrap == null || bootstrap.Mission == null) return;
                if (routine == null) routine = Checks(bootstrap.Mission);
                if (routine.MoveNext()) return;
                SessionState.SetBool("IGI_ARSENAL_TEST", false);
                Debug.Log("ORIGINAL_ARSENAL_PASS: 19 model/audio bindings, equip/fire/ammo, reload cancellation, pellets, zoom, rockets, grenade fuse/pause, blast occlusion/self damage, flash stun, mine arming/trigger, healing");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { SessionState.SetBool("IGI_ARSENAL_TEST", false); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        private static IEnumerator Checks(MissionDirector mission)
        {
            mission.SetPaused(false);
            mission.player.enabled = false;
            foreach (var guard in UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None)) guard.enabled = false;
            var weapon = mission.weapon;
            Require(ClimbableLadder.Active.Count == 6, "Playable ladders were lost from the interaction registry");
            Directory.CreateDirectory("Logs/PerformanceLadders");
            bool laddersValid = (bool)typeof(SmokeTestDriver).GetMethod("VerifyLadders", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { mission, "Logs/PerformanceLadders" });
            Require(laddersValid, "Ladder traversal regressed after registry changes");
            mission.player.enabled = false;
            int batched = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.isPartOfStaticBatch) continue;
                batched++;
                var camera = renderer.GetComponentInParent<SecurityCamera>();
                bool movingCameraHead = camera != null && camera.head != null && renderer.transform.IsChildOf(camera.head);
                Require(renderer.GetComponentInParent<InteractiveDoor>() == null && !movingCameraHead && renderer.GetComponentInParent<GuardAnimation>() == null && renderer.GetComponentInParent<FirstPersonMotor>() == null,
                    "A moving or damageable object was included in a static batch: " + renderer.name);
                var surface = renderer.GetComponent<BulletSurface>();
                Require(surface == null || surface.kind != BulletSurfaceKind.Glass || !surface.Breakable, "Breakable pane was included in a static batch");
            }
            Require(weapon.definitions.Length == 19, "Incomplete player roster");
            var guardAudio = Resources.Load<GuardPresentationSettings>("GuardPresentationSettings");
            Require(guardAudio != null && guardAudio.shot == weapon.definitions.Single(w => w.name == "AK47").fireSound, "Guards still play repeating AK47 audio");
            var contact = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            var rt = new RenderTexture(320, 180, 24);
            for (int i = 0; i < weapon.definitions.Length; i++)
            {
                weapon.Equip(i);
                var definition = weapon.Active;
                Require(definition.visualPrefab != null, "No model: " + definition.name);
                Require(definition.fireSound != null || definition.melee, "No sound: " + definition.name);
                float ready = Time.time + .2f; while (Time.time < ready) yield return null;
                Require(weapon.modelMount.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "No visible geometry: " + definition.name);
                var view = weapon.modelMount.GetComponentInChildren<NativeWeaponView>();
                Require(view != null && view.anchors.Length == 3, "Missing native weapon rig: " + definition.name);
                Require(view.partCount == view.GetComponentsInChildren<MeshRenderer>().Length, "Incomplete weapon assembly: " + definition.name);
                Require(view.transform.localPosition == Vector3.zero && view.transform.localScale == Vector3.one, "Native weapon pose was recentered/rescaled: " + definition.name);
                if (definition.name == "GLOCK" || definition.name == "MP5SD" || definition.name == "AK47")
                {
                    Require(view.partCount == 3 && view.anchors[1].childCount == 1 && view.anchors[2].childCount == 1, "Magazine or moving part missing: " + definition.name);
                    Bounds bodyBounds = view.anchors[0].GetComponentInChildren<Renderer>().bounds; bodyBounds.Expand(.06f);
                    Require(bodyBounds.Intersects(view.anchors[1].GetComponentInChildren<Renderer>().bounds), "Unseated magazine in ready pose: " + definition.name);
                    Require(bodyBounds.Intersects(view.anchors[2].GetComponentInChildren<Renderer>().bounds), "Unseated slide/bolt in ready pose: " + definition.name);
                }
                weapon.eye.targetTexture = rt; weapon.eye.Render();
                weapon.RenderView(rt);
                float horizontal = 2 * Mathf.Atan(Mathf.Tan(weapon.ViewCamera.fieldOfView * Mathf.Deg2Rad / 2) * rt.width / rt.height) * Mathf.Rad2Deg;
                Require(Mathf.Abs(horizontal - definition.viewHorizontalFieldOfView) < .1f, "Incorrect foreground field of view");
                Require(definition.viewHorizontalFieldOfView >= 66, "Overly tight weapon framing: " + definition.name);
                weapon.SetAim(true);
                bool zoomAllowed = definition.name == "MP5SD" || definition.name == "DRAGUNOV" || definition.name == "BINOCULARS";
                Require(weapon.IsAiming == zoomAllowed, "Incorrect zoom policy: " + definition.name);
                Require(weapon.IsScoped == (definition.name == "DRAGUNOV"), "Incorrect scoped view: " + definition.name);
                if (!zoomAllowed) Require(weapon.TargetFieldOfView == 78, "Unsupported weapon changed world FOV");
                weapon.SetAim(false);
                if (new[] { "AK47", "MP5SD", "M16A2", "UZI", "UZIX2", "MINIMI", "JACKHAMMER" }.Contains(definition.name))
                    Require(!definition.fireSound.name.EndsWith("_loop") && definition.fireSound.length <= definition.secondsPerShot + .001f,
                        "One bullet plays a gunfire burst: " + definition.name);
                RenderTexture.active = rt;
                contact.ReadPixels(new Rect(0, 0, 320, 180), i % 4 * 320, (4 - i / 4) * 180);
                weapon.eye.targetTexture = null; RenderTexture.active = null;
                if (definition.action == WeaponAction.Hitscan)
                {
                    int loaded = weapon.Ammo.Loaded;
                    Require(weapon.Fire(), "Cannot fire " + definition.name);
                    Require(weapon.Ammo.Loaded == loaded - (definition.melee ? 0 : 1), "Wrong ammo use: " + definition.name);
                }
            }
            contact.Apply(); Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/original-arsenal-contact.png", contact.EncodeToPNG());
            UnityEngine.Object.Destroy(contact); rt.Release(); UnityEngine.Object.Destroy(rt);
            // Inspect actual component poses through a reload, in addition to checking bindings.
            var reloadSheet = new Texture2D(1280, 540, TextureFormat.RGB24, false);
            rt = new RenderTexture(320, 180, 24);
            string[] reloadNames = { "GLOCK", "AK47", "MP5SD", "SPAS12" };
            for (int row = 0; row < reloadNames.Length; row++)
            {
                weapon.Equip(Array.FindIndex(weapon.definitions, d => d.name == reloadNames[row]));
                yield return null;
                var view = weapon.modelMount.GetComponentInChildren<NativeWeaponView>();
                Require(NativeWeaponView.Valid(view.reload), "Missing native reload: " + reloadNames[row]);
                Vector3 start = view.reload.frames[0].positions[1];
                Require(view.reload.frames.Any(f => Vector3.Distance(start, f.positions[1]) > .01f), "Reload does not move magazine: " + reloadNames[row]);
                for (int phase = 0; phase < 3; phase++)
                {
                    view.Sample(view.reload, view.reload.duration * phase / 2);
                    weapon.eye.targetTexture = rt; weapon.eye.Render(); weapon.RenderView(rt); RenderTexture.active = rt;
                    reloadSheet.ReadPixels(new Rect(0, 0, 320, 180), row * 320, (2 - phase) * 180);
                    weapon.eye.targetTexture = null; RenderTexture.active = null;
                }
                view.ResetPose();
                Require(view.anchors[1].localPosition == view.idle.frames[0].positions[1], "Magazine failed to return to ready pose");
            }
            reloadSheet.Apply(); File.WriteAllBytes("Logs/weapon-reload-contact.png", reloadSheet.EncodeToPNG());
            UnityEngine.Object.Destroy(reloadSheet); rt.Release(); UnityEngine.Object.Destroy(rt);
            weapon.Equip(Array.FindIndex(weapon.definitions, d => d.name == "DRAGUNOV"));
            weapon.SetAim(true); weapon.eye.fieldOfView = weapon.TargetFieldOfView;
            Require(!weapon.ViewCamera.enabled, "Rifle still drawn inside scope");
            rt = new RenderTexture(640, 360, 24); weapon.eye.targetTexture = rt; weapon.eye.Render(); RenderTexture.active = rt;
            var scoped = new Texture2D(640, 360, TextureFormat.RGB24, false);
            scoped.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); scoped.Apply();
            Require(scoped.GetPixel(4, 4).grayscale < .05f && scoped.GetPixel(636, 356).grayscale < .05f, "Missing black scope mask");
            Require(scoped.GetPixel(290, 200).grayscale > .1f, "Scope obscures the magnified scene");
            Require(scoped.GetPixel(320, 180).grayscale < .1f, "Missing native scope crosshair");
            File.WriteAllBytes("Logs/dragunov-scoped.png", scoped.EncodeToPNG());
            weapon.eye.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.Destroy(scoped); rt.Release(); UnityEngine.Object.Destroy(rt);
            weapon.SetAim(false);
            // An isolated patch of ground avoids accidental damage to the authored mission.
            var controller = mission.player.GetComponent<CharacterController>(); controller.enabled = false;
            mission.player.transform.SetPositionAndRotation(new Vector3(1000, 100, 1000), Quaternion.identity);
            controller.enabled = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = DemoWorld.WorldLayer;
            floor.transform.position = new Vector3(1000, 99.5f, 1000); floor.transform.localScale = new Vector3(50, 1, 50);
            var health = mission.player.GetComponent<Health>(); health.ResetHealth(100);
            int Index(string name) => Array.FindIndex(weapon.definitions, d => d.name == name);
            void Ready(string name) { weapon.Equip(Index(name)); typeof(WeaponController).GetField("nextShot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, 0f); }
            Ready("BINOCULARS"); Require(!weapon.Fire() && weapon.Ammo.Loaded == weapon.Ammo.Capacity && weapon.Active.aimFieldOfView < 20, "Binoculars fired or lack zoom");
            Ready("MEDIPACK"); Require(!weapon.Fire() && weapon.Ammo.Loaded == 1, "Medipack wasted at full health");
            health.TakeDamage(70, Team.Guard); Ready("MEDIPACK"); Require(weapon.Fire() && health.Current == 90 && weapon.Ammo.Loaded == 0, "Healing failed");
            Ready("DRAGUNOV"); Require(weapon.Active.aimFieldOfView == 15, "Sniper scope missing");
            Ready("SPAS12"); Require(weapon.Active.pellets == 8, "Shotgun pellets missing");
            int before = weapon.Ammo.Loaded; Require(weapon.Fire() && weapon.Ammo.Loaded == before - 1, "Shotgun used one round per pellet");
            weapon.StartCoroutine((IEnumerator)typeof(WeaponController).GetMethod("Reload", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null));
            Require(weapon.IsReloading, "Reload did not start"); weapon.Equip(Index("GLOCK")); Require(!weapon.IsReloading, "Switch did not cancel reload");
            health.ResetHealth(500);
            foreach (string thrownName in new[] { "GRENADE", "FLASHBANG" })
            {
                Ready(thrownName); weapon.eye.transform.rotation = Quaternion.identity;
                var view = weapon.modelMount.GetComponentInChildren<NativeWeaponView>();
                float release = view.fire.releaseSeconds;
                Require(release > .3f && release < view.fire.duration, "Missing native grenade release event: " + thrownName + " release=" + release + " duration=" + view.fire.duration);
                int loaded = weapon.Ammo.Loaded, shots = weapon.ShotsFired;
                float throwStarted = Time.time;
                Require(weapon.Fire() && weapon.IsThrowing, "Throw animation did not start");
                Require(weapon.Ammo.Loaded == loaded && weapon.ShotsFired == shots && !UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Any(), "Grenade launched before animation");
                float firstWait = Time.time + release * .4f; while (Time.time < firstWait) yield return null;
                Require(!UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Any(), "Grenade launched during wind-up");
                Require(!weapon.Fire(), "A second throw interrupted wind-up");
                mission.SetPaused(true);
                double pauseUntil = EditorApplication.timeSinceStartup + .2; while (EditorApplication.timeSinceStartup < pauseUntil) yield return null;
                Require(weapon.Ammo.Loaded == loaded && weapon.IsThrowing, "Paused throw advanced"); mission.SetPaused(false);
                float deadline = Time.time + release + .5f;
                while (weapon.ShotsFired == shots && Time.time < deadline) yield return null;
                Require(weapon.ShotsFired == shots + 1 && weapon.Ammo.Loaded == loaded - 1, "Throw release failed or consumed more than one grenade");
                Require(Time.time - throwStarted >= release - .05f && Time.time - throwStarted < release + .2f, "Projectile release was not synchronized to the animation event");
                var projectile = UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Single();
                Require(!view.gameObject.activeSelf, "Held grenade remained visible after release");
                UnityEngine.Object.Destroy(projectile.gameObject);
                while (weapon.IsThrowing) yield return null;
                Require(view.gameObject.activeSelf, "Grenade hand did not recover");
                // Cancelling before release keeps the round and creates no projectile.
                weapon.Ammo.Reload(); Ready(thrownName); loaded = weapon.Ammo.Loaded;
                Require(weapon.Fire(), "Cannot start cancel test"); weapon.Equip(Index("GLOCK"));
                Require(!weapon.IsThrowing && !UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Any(), "Switching did not cancel pending throw");
                Ready(thrownName); Require(weapon.Ammo.Loaded == loaded, "Cancelled throw consumed ammunition");
            }
            Ready("PROXIMITYMINE"); weapon.eye.transform.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * .5f);
            Physics.SyncTransforms(); Require(weapon.Fire(), "Mine placement failed");
            var mine = UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Single();
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule); target.layer = DemoWorld.GuardLayer;
            target.transform.position = mine.transform.position + Vector3.forward + Vector3.up;
            var victim = target.AddComponent<Health>(); victim.team = Team.Guard; victim.ResetHealth(500);
            Physics.SyncTransforms(); yield return null; Require(victim.Current == 500, "Mine triggered before arming");
            float until = Time.time + 1.7f; while (Time.time < until) yield return null;
            Require(mine == null && victim.Current < 500, "Armed mine did not trigger");
            // Blast damage must not cross an opaque wall, and must affect the operator nearby.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = DemoWorld.WorldLayer;
            Vector3 blast = new Vector3(1010, 101, 1000); target.transform.position = blast + Vector3.forward * 3;
            wall.transform.position = blast + Vector3.forward * 1.5f; wall.transform.localScale = new Vector3(5, 5, .3f);
            victim.ResetHealth(500); Physics.SyncTransforms();
            WeaponProjectile.Launch(weapon.definitions[Index("GRENADE")], blast, Vector3.forward, mission, weapon).Detonate();
            Require(victim.Current == 500, "Blast crossed solid wall");
            wall.SetActive(false); Physics.SyncTransforms();
            WeaponProjectile.Launch(weapon.definitions[Index("GRENADE")], blast, Vector3.forward, mission, weapon).Detonate();
            Require(victim.Current < 500, "Unobstructed blast did no damage");
            health.ResetHealth(500);
            WeaponProjectile.Launch(weapon.definitions[Index("GRENADE")], mission.player.transform.position + Vector3.up + Vector3.forward, Vector3.forward, mission, weapon).Detonate();
            Require(health.Current < 500, "Operator immune to own explosives");
            Ready("RPG18"); weapon.eye.transform.rotation = Quaternion.identity;
            wall.SetActive(true); wall.transform.position = weapon.eye.transform.position + Vector3.forward * 4; Physics.SyncTransforms();
            Require(weapon.Fire(), "Rocket did not launch"); yield return null;
            until = Time.time + .4f; while (Time.time < until) yield return null;
            Require(!UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Any(), "Rocket did not detonate on collision");
            wall.SetActive(false);
            var grenade = WeaponProjectile.Launch(weapon.definitions[Index("GRENADE")], blast, Vector3.zero, mission, weapon);
            mission.SetPaused(true); Vector3 paused = grenade.transform.position;
            double realUntil = EditorApplication.timeSinceStartup + .2; while (EditorApplication.timeSinceStartup < realUntil) yield return null;
            Require(grenade.transform.position == paused, "Paused projectile moved"); mission.SetPaused(false);
            until = Time.time + 3.3f; while (Time.time < until) yield return null; Require(grenade == null, "Grenade fuse failed");
            var flashGuard = target.AddComponent<EnemyGuard>(); flashGuard.enabled = false;
            target.transform.position = blast + Vector3.forward; Physics.SyncTransforms();
            WeaponProjectile.Launch(weapon.definitions[Index("FLASHBANG")], blast, Vector3.forward, mission, weapon).Detonate();
            Require(flashGuard.IsStunned, "Flashbang did not stun guard");
        }
    }
}
