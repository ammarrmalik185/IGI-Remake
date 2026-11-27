using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class IdlePerformanceValidation
    {
        [Serializable] private sealed class Sample
        {
            public double seconds, frameMedianMs, frameP95Ms, frameMaxMs, gridMs, authoredMs, longestGridMs;
            public long gridCalls, authoredCalls, clearanceChecks, expandedNodes, disconnectedRejects, allocatedMB, reservedMB, monoMB, allocatedBytes;
            public int objects, materials, meshes, collections, errors, guardsMoving, guardsInCombat;
            public double mainThreadMs, renderThreadMs, guardAiMs, animationMs, hudMs, gcBytesPerFrame;
            public int frameCount;
        }
        [Serializable] private sealed class Report { public string label; public Sample[] samples; }
        private static MissionDirector mission;
        private static EnemyGuard[] guards;
        private static Vector3[] previous;
        private static readonly List<Sample> samples = new List<Sample>();
        private static readonly List<double> frames = new List<double>();
        private static double started, window, previousFrame;
        private static int collections, errors;
        private static long allocated;
        private static int lastFrame;
        private static ProfilerRecorder mainThread, renderThread, ai, animation, hud, gc;
        private static double mainSum, renderSum, aiSum, animationSum, hudSum, gcSum;
        private static RenderTexture renderTarget;
        static IdlePerformanceValidation()
        {
            if (SessionState.GetBool("IGI_IDLE_PERF", false)) EditorApplication.update += Update;
        }
        public static void Baseline() => Run("baseline");
        public static void Verify() => Run("optimized");
        public static void Diagnose() => Run("diagnose");
        public static void RenderBaseline() => Run("render-baseline");
        public static void RoofBaseline() => Run("roof-baseline");
        public static void RoofVerify() => Run("roof-optimized");
        private static void Run(string label)
        {
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetString("IGI_IDLE_PERF_LABEL", label);
            SessionState.SetBool("IGI_IDLE_PERF", true);
            EditorApplication.update -= Update; EditorApplication.update += Update;
            EditorApplication.isPlaying = true;
        }
        private static void Update()
        {
            try
            {
                if (!EditorApplication.isPlaying) return;
                double now = EditorApplication.timeSinceStartup;
                if (mission == null)
                {
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                    if (bootstrap == null || bootstrap.Mission == null) return;
                    mission = bootstrap.Mission; mission.SetPaused(false);
                    mission.player.GetComponent<Health>().ResetHealth(1000000);
                    string runLabel = SessionState.GetString("IGI_IDLE_PERF_LABEL", "unknown");
                    if (runLabel == "optimized" || runLabel == "render-baseline" || runLabel.StartsWith("roof-"))
                        renderTarget = new RenderTexture(1280, 720, 24);
                    if (runLabel.StartsWith("roof-"))
                    {
                        var body = mission.player.GetComponent<CharacterController>(); body.enabled = false;
                        mission.player.transform.position = new Vector3(-28.386608f, 4.444532f, -17.122732f);
                        body.enabled = true; mission.player.enabled = false; Physics.SyncTransforms();
                        mission.MakeNoise(mission.player.eye.transform.position, 100);
                    }
                    if (runLabel == "optimized") ValidateIdleWork(mission);
                    Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
                    guards = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None);
                    previous = guards.Select(g => g.transform.position).ToArray();
                    NavigationDiagnostics.Enabled = true; NavigationDiagnostics.Reset();
                    mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                    renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
                    ai = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "IGI.GuardAI", 1);
                    animation = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "IGI.GuardAnimation", 1);
                    hud = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "IGI.HUD", 1);
                    gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                    lastFrame = Time.frameCount;
                    started = window = previousFrame = now;
                    collections = GC.CollectionCount(0); allocated = GC.GetAllocatedBytesForCurrentThread();
                    Application.logMessageReceived += OnLog;
                    return;
                }
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                frames.Add(Time.unscaledDeltaTime * 1000); previousFrame = now;
                if (renderTarget != null)
                {
                    var camera = mission.player.eye; var previousTarget = camera.targetTexture;
                    camera.targetTexture = renderTarget; camera.Render(); mission.weapon.RenderView(renderTarget); camera.targetTexture = previousTarget;
                }
                mainSum += mainThread.LastValue / 1e6; renderSum += renderThread.LastValue / 1e6;
                aiSum += ai.LastValue / 1e6; animationSum += animation.LastValue / 1e6; hudSum += hud.LastValue / 1e6; gcSum += gc.LastValue;
                if (now - window < 30) return;
                frames.Sort();
                var sample = new Sample {
                    seconds = now - started, frameMedianMs = frames[frames.Count / 2], frameP95Ms = frames[(int)((frames.Count - 1) * .95)], frameMaxMs = frames[frames.Count - 1],
                    gridCalls = NavigationDiagnostics.GridCalls, authoredCalls = NavigationDiagnostics.AuthoredCalls, clearanceChecks = NavigationDiagnostics.ClearanceChecks,
                    expandedNodes = NavigationDiagnostics.ExpandedNodes, disconnectedRejects = NavigationDiagnostics.DisconnectedRejects, gridMs = NavigationDiagnostics.GridMilliseconds, authoredMs = NavigationDiagnostics.AuthoredMilliseconds, longestGridMs = NavigationDiagnostics.LongestGridMilliseconds,
                    allocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024), reservedMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024), monoMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024),
                    allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated, collections = GC.CollectionCount(0) - collections, errors = errors,
                    objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Length, materials = Resources.FindObjectsOfTypeAll<Material>().Length, meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length
                };
                sample.frameCount = frames.Count;
                sample.mainThreadMs = mainSum / frames.Count; sample.renderThreadMs = renderSum / frames.Count;
                sample.guardAiMs = aiSum / frames.Count; sample.animationMs = animationSum / frames.Count; sample.hudMs = hudSum / frames.Count; sample.gcBytesPerFrame = gcSum / frames.Count;
                mainSum = renderSum = aiSum = animationSum = hudSum = gcSum = 0;
                for (int i = 0; i < guards.Length; i++)
                {
                    if (Vector3.Distance(previous[i], guards[i].transform.position) > .5f) sample.guardsMoving++;
                    if (guards[i].State == EnemyGuard.GuardState.Attack) sample.guardsInCombat++;
                    previous[i] = guards[i].transform.position;
                }
                samples.Add(sample); frames.Clear(); NavigationDiagnostics.Reset();
                collections = GC.CollectionCount(0); allocated = GC.GetAllocatedBytesForCurrentThread(); window = now;
                string label = SessionState.GetString("IGI_IDLE_PERF_LABEL", "unknown");
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/idle-performance-" + label + ".json", JsonUtility.ToJson(new Report { label = label, samples = samples.ToArray() }, true));
                Debug.Log("IDLE_PERF " + label + " " + JsonUtility.ToJson(sample));
                if (label == "diagnose")
                {
                    if (samples.Count == 1) foreach (var display in UnityEngine.Object.FindObjectsByType<MissionHud>(FindObjectsSortMode.None)) display.enabled = false;
                    if (samples.Count == 2) foreach (var guard in guards) { guard.GetComponentInChildren<GuardAnimation>().enabled = false; guard.GetComponent<GuardPresentation>().enabled = false; }
                }
                if (now - started < (label == "render-baseline" || label.StartsWith("roof-") ? 30 : label == "diagnose" ? 90 : 180)) return;
                SessionState.SetBool("IGI_IDLE_PERF", false); NavigationDiagnostics.Enabled = false;
                mainThread.Dispose(); renderThread.Dispose(); ai.Dispose(); animation.Dispose(); hud.Dispose(); gc.Dispose();
                if (renderTarget != null) { renderTarget.Release(); UnityEngine.Object.Destroy(renderTarget); }
                EditorApplication.Exit(errors == 0 ? 0 : 1);
            }
            catch (Exception exception) { SessionState.SetBool("IGI_IDLE_PERF", false); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        private static void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && !stack.Contains("UnityEditor.Search")) errors++;
        }
        private static void ValidateIdleWork(MissionDirector mission)
        {
            void Require(bool value, string message) { if (!value) throw new Exception(message); }
            Require(ClimbableLadder.Active.Count == UnityEngine.Object.FindObjectsByType<ClimbableLadder>(FindObjectsSortMode.None).Length, "Active ladder registry is incomplete");
            int count = ClimbableLadder.Active.Count;
            var temporaryLadder = new GameObject("Ladder registry probe"); temporaryLadder.AddComponent<ClimbableLadder>();
            Require(ClimbableLadder.Active.Count == count + 1, "New ladder was not registered"); temporaryLadder.SetActive(false);
            Require(ClimbableLadder.Active.Count == count, "Disabled ladder remains usable"); UnityEngine.Object.Destroy(temporaryLadder);
            var weapons = mission.weapon;
            var scope = weapons.eye.GetComponent<WeaponScopeEffect>();
            Require(scope != null && !scope.enabled, "Scope pass still runs for ordinary weapons");
            int sniper = Array.FindIndex(weapons.definitions, w => w.scoped);
            weapons.Equip(sniper); weapons.SetAim(true); Require(scope.enabled && !weapons.ViewCamera.enabled, "Scope pass cannot be enabled");
            weapons.SetAim(false); Require(!scope.enabled && weapons.ViewCamera.enabled, "Scope pass did not stop after zoom"); weapons.Equip(0);
            var source = UnityEngine.Object.FindFirstObjectByType<GuardAnimation>();
            var fixture = new GameObject("Animation visibility probe"); fixture.transform.position = Vector3.up * 1000;
            fixture.AddComponent<MeshFilter>().sharedMesh = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            fixture.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var animation = fixture.AddComponent<GuardAnimation>(); animation.Initialize(source.data); animation.enabled = false;
            animation.Play("Walk"); animation.SetMovementSpeed(2);
            int uploads = animation.MeshUploads; Vector3 socket = animation.WeaponSocket.localPosition;
            animation.Advance(.25f, false);
            Require(animation.MeshUploads == uploads, "Invisible guard still uploads its mesh");
            Require(Vector3.Distance(socket, animation.WeaponSocket.localPosition) > .001f, "Invisible guard's weapon socket froze");
            animation.Advance(.1f, true); Require(animation.MeshUploads == uploads + 1, "Visible guard did not catch up its pose");
            var bounds = fixture.GetComponent<MeshFilter>().sharedMesh.bounds; bounds.Expand(.002f);
            foreach (var clip in source.data.clips)
                for (int i = 0; i < clip.positions.Length; i += 31) Require(bounds.Contains(clip.positions[i]), "Animation bounds clip a valid pose");
            animation.Play("Death"); animation.Advance(10, false); uploads = animation.MeshUploads;
            animation.Advance(0, true); Require(animation.MeshUploads == uploads + 1, "Invisible death did not restore its final pose when seen");
            animation.Advance(1, true); Require(animation.MeshUploads == uploads + 1, "Settled corpse continues deforming every frame");
            fixture.SetActive(false); UnityEngine.Object.Destroy(fixture);
            Debug.Log("IDLE_WORK_REGRESSION_PASS: dynamic ladder registry, scope pass gating, invisible socket motion, visible pose restoration, animation bounds, settled corpses");
        }
    }
}
