using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class EnemySecurityValidation
    {
        static IEnumerator routine; static double started;
        static EnemySecurityValidation()
        {
            if (SessionState.GetBool("IGI_SECURITY_TEST", false))
            {
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Update;
            }
        }
        public static void Run()
        {
            TrainyardSetup.BuildMission();
            SessionState.SetBool("IGI_SECURITY_TEST", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.isPlaying = true;
        }
        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 150)
                    throw new Exception("Play Mode validation timed out");
                if (!EditorApplication.isPlaying)
                    return;
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (bootstrap == null || bootstrap.Mission == null)
                    return;
                if (routine == null)
                    routine = Checks(bootstrap.Mission);
                if (!routine.MoveNext())
                {
                    SessionState.SetBool("IGI_SECURITY_TEST", false);
                    Debug.Log("ENEMY_SECURITY_VALIDATION_PASS: native animation deformation, all guard sound bindings, gunfire hearing, occlusion, vision, camera detection/destruction, alarms, hacking, pause and door-aware navigation");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception e) { SessionState.SetBool("IGI_SECURITY_TEST", false); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        static void Require(bool ok, string message)
        {
            if (!ok)
                throw new Exception(message);
        }
        static EnemyGuard Probe(MissionDirector mission, Vector3 at)
        {
            var go = new GameObject("Hearing probe");
            go.layer = DemoWorld.GuardLayer;
            go.transform.position = at;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = Vector3.up * .9f;
            var h = go.AddComponent<Health>();
            h.team = Team.Guard;
            var guard = go.AddComponent<EnemyGuard>();
            guard.enabled = false;
            guard.mission = mission;
            guard.target = mission.player.transform;
            guard.sightRange = 50;
            mission.RegisterGuard(guard);
            return guard;
        }
        static IEnumerator Checks(MissionDirector m)
        {
            var guards = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None);
            Require(guards.Length == 21, "Expected 21 authored enemy guards");
            m.SetPaused(false);
            m.player.enabled = false;
            var playerController = m.player.GetComponent<CharacterController>();
            playerController.enabled = false;
            m.player.transform.position = new Vector3(1000, 100, 1000);
            playerController.enabled = true;
            var patrolStarts = guards.Select(g => g.transform.position).ToArray();
            float patrolEnd = Time.time + 6;
            while (Time.time < patrolEnd)
                yield return null;
            int moving = guards.Where((g, i) => Vector3.Distance(g.transform.position, patrolStarts[i]) > .5f).Count();
            Require(guards.All(g => g.authoredPatrol != null && g.authoredPatrol.Length > 0), "Missing authored patrol commands");
            Debug.Log("PATROL_MOVEMENT_PASS: " + moving + " guards moved on authored terrain");
            foreach (var guard in guards)
            {
                guard.enabled = false;
                var presentation = guard.GetComponent<GuardPresentation>();
                Require(presentation != null && presentation.animationPlayer != null, "Guard animation missing " + guard.name);
                Require(guard.GetComponents<AudioSource>().Length >= 3, "Positional enemy audio missing");
                presentation.enabled = false;
                var animation = presentation.animationPlayer;
                var filter = animation.GetComponentInChildren<MeshFilter>();
                var before = filter.sharedMesh.vertices;
                animation.Play("Walk"); animation.SetMovementSpeed(1.55f);
                animation.Advance(.24f);
                var after = filter.sharedMesh.vertices;
                Require(before.Where((p, i) => Vector3.Distance(p, after[i]) > .005f).Any(), "Guard mesh did not animate");
            }
            var settings = Resources.Load<GuardPresentationSettings>("GuardPresentationSettings");
            Require(settings.shot != null && settings.shot.length > 0 && settings.footsteps.Length == 6 && settings.hits.Length == 3 && settings.deaths.Length == 4, "Native sound assets missing");
            Vector3 arena = new Vector3(1000, 100, 1000);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = DemoWorld.WorldLayer;
            floor.transform.position = arena + new Vector3(0, -.6f, 0);
            floor.transform.localScale = new Vector3(220, 1, 220);
            var cc = m.player.GetComponent<CharacterController>();
            cc.enabled = false;
            m.player.transform.SetPositionAndRotation(arena, Quaternion.identity);
            cc.enabled = true;
            m.player.enabled = false;
            Physics.SyncTransforms();
            var listener = Probe(m, arena + Vector3.back * 25);
            m.weapon.Equip(0);
            yield return null;
            yield return null;
            float ready = Time.time + .3f;
            while (Time.time < ready)
                yield return null;
            Require(m.weapon.Fire(), "Player gun did not fire");
            Require(listener.State == EnemyGuard.GuardState.Investigate, "Enemy did not hear actual player gunfire");
            Require(Vector3.Distance(listener.LastKnown, m.player.eye.transform.position) < .1f, "Gunfire investigation did not use sound origin");
            var distant = Probe(m, arena + new Vector3(70, 0, 0));
            Require(!distant.HearNoise(arena + Vector3.up, 18), "Suppressed noise reached distant guard");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = DemoWorld.WorldLayer;
            wall.transform.position = arena + new Vector3(35, 2, 0);
            wall.transform.localScale = new Vector3(1, 6, 30);
            Physics.SyncTransforms();
            Require(!distant.HearNoise(arena + Vector3.up, 100), "Wall did not attenuate distant gunfire");
            UnityEngine.Object.DestroyImmediate(wall);
            yield return null;
            Physics.SyncTransforms();
            Require(distant.HearNoise(arena + Vector3.up, 100), "Unobstructed loud shot not heard");
            var sight = Probe(m, arena + Vector3.back * 10);
            sight.transform.forward = Vector3.forward;
            Physics.SyncTransforms();
            Require(sight.CanSee(arena + Vector3.up * 1.2f), "Guard cannot see exposed player");
            sight.transform.forward = Vector3.back;
            Require(!sight.CanSee(arena + Vector3.up * 1.2f), "Guard sees through back of head");
            sight.transform.forward = Vector3.forward;
            sight.Tick(.7f);
            Require(sight.State == EnemyGuard.GuardState.Attack && sight.HasSight, "Guard did not confirm visible target");
            var remembered = sight.LastKnown;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = DemoWorld.WorldLayer;
            wall.transform.position = arena + new Vector3(0, 2, -5);
            wall.transform.localScale = new Vector3(8, 6, 1);
            Physics.SyncTransforms();
            sight.Tick(.1f);
            Require(!sight.HasSight && sight.State == EnemyGuard.GuardState.Investigate && sight.LastKnown == remembered, "Guard tracks player through walls");
            UnityEngine.Object.DestroyImmediate(wall);
            var n = m.Security;
            Require(n != null && n.cameras.Length == 5 && n.panels.Length == 3, "Security placements missing");
            var camera = n.cameras[0];
            camera.enabled = false;
            camera.left = 0;
            camera.right = 0;
            camera.speed = 0;
            camera.transform.position = arena + new Vector3(0, 2, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.head.localRotation = Quaternion.identity;
            camera.Initialize();
            Physics.SyncTransforms();
            Vector3 point = arena + Vector3.up * 1.2f;
            Require(camera.CanSee(point), "Camera cannot see exposed subject");
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = DemoWorld.WorldLayer;
            wall.transform.position = arena + new Vector3(0, 2, -5);
            wall.transform.localScale = new Vector3(8, 6, 1);
            Physics.SyncTransforms();
            Require(!camera.CanSee(point), "Camera sees through walls");
            UnityEngine.Object.DestroyImmediate(wall);
            yield return null;
            Physics.SyncTransforms();
            camera.Tick(1.2f);
            Require(n.AlarmActive, "Camera detection did not trigger alarm");
            n.Silence();
            Require(!n.AlarmActive, "Alarm did not silence");
            n.panels[0].Use();
            Require(n.AlarmActive, "Manual alarm button did not trigger");
            var consoles = UnityEngine.Object.FindObjectsByType<SecurityConsole>(FindObjectsSortMode.None);
            Require(consoles.Length == 2, "Expected two original computers");
            Require(consoles.All(c => !c.alarmControl), "Computer configured as alarm control");
            var cameraConsole = consoles[0];
            var alarmConsole = consoles[1];
            cameraConsole.enabled = false;
            alarmConsole.enabled = false;
            cameraConsole.AdvanceHack(true, 2);
            Require(!n.CamerasDisabled && cameraConsole.Progress > .3f, "Hack completed too early");
            cameraConsole.AdvanceHack(false, .1f);
            Require(cameraConsole.Progress == 0, "Interrupted hack retained progress");
            m.SetPaused(true);
            cameraConsole.AdvanceHack(true, 10);
            Require(!n.CamerasDisabled, "Hacked while paused");
            m.SetPaused(false);
            cameraConsole.AdvanceHack(true, 5);
            Require(n.CamerasDisabled && n.cameras.All(c => c.Detection == 0), "Camera computer did not disable network");
            Require(!camera.CanSee(point), "Disabled camera still detects");
            alarmConsole.AdvanceHack(true, 5);
            Require(!n.AlarmsDisabled && n.AlarmActive, "Camera computer changed alarm state");
            Require(n.RaiseAlarm(arena), "Camera computer disabled the alarm network");
            var otherCamera = n.cameras[1];
            otherCamera.GetComponent<Health>().TakeDamage(100, Team.Player);
            Require(otherCamera.Destroyed, "Shooting camera did not disable it");
            sight.GetComponent<Health>().TakeDamage(100, Team.Player);
            Require(sight.State == EnemyGuard.GuardState.Dead && !sight.GetComponent<CharacterController>().enabled, "Dead guard remains active");
            var navObject = new GameObject("Door navigation probe");
            var nav = navObject.AddComponent<GridNavigation>();
            nav.cellSize = 1;
            nav.width = 20;
            nav.depth = 20;
            nav.worldCentre = arena + new Vector3(10, 0, 30);
            nav.sampleTerrain = true;
            foreach (float z in new[] { 10f, 50f })
            {
                var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.layer = DemoWorld.WorldLayer;
                barrier.transform.position = arena + new Vector3(10, 1, z);
                barrier.transform.localScale = new Vector3(.2f, 2, 37);
            }
            var doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorPanel.layer = DemoWorld.WorldLayer;
            doorPanel.transform.position = arena + new Vector3(10, 1, 30);
            doorPanel.transform.localScale = new Vector3(.2f, 2, 3);
            var door = doorPanel.AddComponent<InteractiveDoor>();
            Physics.SyncTransforms();
            nav.Rebuild();
            Require(nav.FindPath(arena + new Vector3(5, 0, 30), arena + new Vector3(15, 0, 30)).Count > 0, "Navigation cannot plan through an operable door");
            UnityEngine.Object.DestroyImmediate(door);
            nav.Rebuild();
            Require(nav.FindPath(arena + new Vector3(5, 0, 30), arena + new Vector3(15, 0, 30)).Count == 0, "Navigation plans through solid wall");
            Debug.Log("SECURITY_RUNTIME_CHECKS_COMPLETE");
            yield return null;
        }
    }
}
