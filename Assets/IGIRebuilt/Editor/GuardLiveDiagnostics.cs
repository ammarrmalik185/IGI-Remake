using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class GuardLiveDiagnostics
    {
        [Serializable] sealed class Snapshot
        {
            public string capturedUtc, project, scene, runtimeAssembly;
            public bool playing, paused, inputBlocked;
            public float time, deltaTime;
            public Vector3 player;
            public Row[] guards;
        }
        [Serializable] sealed class Row
        {
            public string name, state, animation, alarmTarget, sightBlocker, muzzleBlocker;
            public string[] clips;
            public bool enabled, hasSight, canSeeNow, controllerEnabled, onGround;
            public float health, suspicion, firstShotAt, nextShot, reloadUntil, stuck;
            public int rounds, pathIndex, graphId;
            public Vector3 position, forward, lastKnown;
            public Vector3[] path;
        }
        static double nextCapture;
        static GuardLiveDiagnostics() { EditorApplication.update += Update; }
        static T Field<T>(EnemyGuard guard, string name) => (T)typeof(EnemyGuard).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(guard);
        static string Blocker(Vector3 a, Vector3 b)
        {
            if (Physics.Linecast(a, b, out var hit, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore) || Physics.Linecast(b, a, out hit, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore))
                return hit.collider.name + " at " + hit.point;
            return "";
        }
        static void Update()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextCapture || !File.Exists("Logs/guard-live-capture.enabled")) return;
            nextCapture = EditorApplication.timeSinceStartup + 1;
            Capture();
        }
        [MenuItem("Tools/IGI Rebuilt/Capture guard diagnostics")]
        public static void Capture()
        {
            var mission = UnityEngine.Object.FindFirstObjectByType<MissionDirector>();
            var rows = new List<Row>();
            if (mission != null)
                foreach (var guard in UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None))
                {
                    var animation = guard.GetComponent<GuardPresentation>()?.animationPlayer;
                    var controller = guard.GetComponent<CharacterController>();
                    Vector3 point = mission.player.transform.position + Vector3.up * (mission.player.IsCrouching ? .75f : 1.25f);
                    rows.Add(new Row {
                        name = guard.name, state = guard.State.ToString(), animation = animation?.Current,
                        clips = animation?.data?.clips.Select(c => c.name).ToArray(),
                        enabled = guard.enabled, hasSight = guard.HasSight, canSeeNow = guard.CanSee(point),
                        controllerEnabled = controller.enabled, onGround = controller.isGrounded,
                        health = guard.GetComponent<Health>().Current, suspicion = guard.Suspicion,
                        firstShotAt = Field<float>(guard, "firstShotAt"), nextShot = Field<float>(guard, "nextShot"), reloadUntil = Field<float>(guard, "reloadUntil"), stuck = Field<float>(guard, "stuck"),
                        rounds = Field<int>(guard, "rounds"), pathIndex = Field<int>(guard, "pathIndex"), graphId = guard.graphId,
                        position = guard.transform.position, forward = guard.transform.forward, lastKnown = guard.LastKnown,
                        path = Field<List<Vector3>>(guard, "path")?.ToArray(), alarmTarget = Field<AlarmPanel>(guard, "alarmTarget")?.name,
                        sightBlocker = Blocker(guard.transform.position + Vector3.up * 1.55f, point),
                        muzzleBlocker = Blocker(guard.transform.position + Vector3.up * 1.4f, point)
                    });
                }
            var snapshot = new Snapshot {
                capturedUtc = DateTime.UtcNow.ToString("O"), project = Application.dataPath,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                runtimeAssembly = typeof(EnemyGuard).Assembly.Location,
                playing = EditorApplication.isPlaying, paused = mission != null && mission.Paused,
                inputBlocked = mission != null && mission.InputBlocked,
                time = Time.time, deltaTime = Time.deltaTime, player = mission != null ? mission.player.transform.position : Vector3.zero,
                guards = rows.ToArray()
            };
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/guard-live-state.json", JsonUtility.ToJson(snapshot, true));
            File.AppendAllText("Logs/guard-live-history.jsonl", JsonUtility.ToJson(snapshot) + Environment.NewLine);
        }
    }
}
