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
    public static class GuardDamageReactionValidation
    {
        private static IEnumerator routine;
        private static double started;
        static GuardDamageReactionValidation()
        {
            if (!SessionState.GetBool("IGI_DAMAGE_REACTION_TEST", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Update;
        }
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetBool("IGI_DAMAGE_REACTION_TEST", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.isPlaying = true;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
        private static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Damage reaction test timed out");
                if (!EditorApplication.isPlaying) return;
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (bootstrap == null || bootstrap.Mission == null) return;
                if (routine == null) routine = Checks(bootstrap.Mission);
                if (routine.MoveNext()) return;
                SessionState.SetBool("IGI_DAMAGE_REACTION_TEST", false);
                Debug.Log("GUARD_DAMAGE_REACTION_PASS: idle/walking flinch, protected run/fire, firing cancels flinch, sustained fire under repeated damage, lethal death reaction");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                SessionState.SetBool("IGI_DAMAGE_REACTION_TEST", false);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
        private static IEnumerator Checks(MissionDirector mission)
        {
            mission.SetPaused(false);
            mission.player.enabled = false;
            var playerController = mission.player.GetComponent<CharacterController>();
            playerController.enabled = false;
            mission.player.transform.position = new Vector3(1000, 100, 1000);
            playerController.enabled = true;
            mission.player.GetComponent<Health>().ResetHealth(10000);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = DemoWorld.WorldLayer;
            floor.transform.position = new Vector3(1000, 99.4f, 1005);
            floor.transform.localScale = new Vector3(40, 1, 40);
            var guards = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None);
            foreach (var other in guards) { other.enabled = false; other.GetComponent<GuardPresentation>().enabled = false; }
            var guard = guards.First();
            var controller = guard.GetComponent<CharacterController>();
            controller.enabled = false;
            guard.transform.SetPositionAndRotation(new Vector3(1000, 100, 1010), Quaternion.LookRotation(Vector3.back));
            controller.enabled = true;
            var health = guard.GetComponent<Health>();
            health.ResetHealth(10000);
            var presentation = guard.GetComponent<GuardPresentation>();
            var animation = presentation.animationPlayer;
            Physics.SyncTransforms();
            // Teleport the fixture without interpreting relocation as running speed.
            mission.SetPaused(true);
            presentation.SendMessage("LateUpdate");
            mission.SetPaused(false);
            Vector3 origin = mission.player.eye.transform.position;
            animation.Play("Idle");
            health.TakeDamage(1, Team.Player, origin);
            Require(animation.Current == "Hit", "Idle guard no longer reacts to damage");
            animation.Play("Walk");
            health.TakeDamage(1, Team.Player, origin);
            Require(animation.Current == "Hit", "Walking guard no longer reacts to damage");
            animation.Play("Run");
            health.TakeDamage(1, Team.Player, origin);
            Require(animation.Current == "Run", "Damage interrupted running");
            animation.Play("Fire");
            health.TakeDamage(1, Team.Player, origin);
            Require(animation.Current == "Fire", "Damage interrupted firing");
            animation.Play("Idle");
            health.TakeDamage(1, Team.Player, origin);
            Require(animation.Current == "Hit", "Expected pending damage reaction");
            presentation.Shot();
            presentation.SendMessage("LateUpdate");
            Require(animation.Current == "Fire", "Firing did not take priority over a pending flinch");
            var rounds = typeof(EnemyGuard).GetField("rounds", BindingFlags.Instance | BindingFlags.NonPublic);
            int before = (int)rounds.GetValue(guard);
            presentation.enabled = true;
            guard.enabled = true;
            float end = Time.time + 1.6f, nextDamage = 0;
            int hits = 0;
            while (Time.time < end)
            {
                if (Time.time >= nextDamage)
                {
                    health.TakeDamage(1, Team.Player, origin);
                    Require(animation.Current != "Hit", "Repeated damage interrupted combat animation");
                    hits++;
                    nextDamage = Time.time + .06f;
                }
                yield return null;
            }
            Require(hits >= 10, "Insufficient repeated-hit coverage");
            Require((int)rounds.GetValue(guard) < before, "Repeated hits prevented guard from firing");
            health.TakeDamage(20000, Team.Player, origin);
            Require(guard.State == EnemyGuard.GuardState.Dead && animation.Current == "Death", "Combat protection suppressed death animation");
            Debug.Log("GUARD_REPEATED_HITS_PASS: hits=" + hits + ", shots=" + (before - (int)rounds.GetValue(guard)));
        }
    }
}
