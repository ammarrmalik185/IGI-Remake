using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class GuardEncounterValidation
    {
        static IEnumerator routine;
        static double started;
        static GuardEncounterValidation()
        {
            if (!SessionState.GetBool("IGI_ENCOUNTER_TEST", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Update;
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetBool("IGI_ENCOUNTER_TEST", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.isPlaying = true;
        }
        static object Field(EnemyGuard guard, string name) => typeof(EnemyGuard).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(guard);
        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Encounter probe timed out");
                if (!EditorApplication.isPlaying) return;
                var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (boot == null || boot.Mission == null) return;
                if (routine == null) routine = Checks(boot.Mission);
                if (routine.MoveNext()) return;
                SessionState.SetBool("IGI_ENCOUNTER_TEST", false);
                Debug.Log("TRAINYARD_ENCOUNTER_PROBE_COMPLETE");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { SessionState.SetBool("IGI_ENCOUNTER_TEST", false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static IEnumerator Checks(MissionDirector mission)
        {
            mission.SetPaused(false);
            mission.player.enabled = false;
            mission.player.GetComponent<Health>().ResetHealth(100000);
            foreach (var camera in mission.Security.cameras) camera.enabled = false;
            foreach (var building in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.Contains("[418_01_1]")))
            {
                int missing = 0, total = 0;
                foreach (var filter in building.GetComponentsInChildren<MeshFilter>().Where(f => f.transform == building || f.name.Contains("418_02_1")))
                {
                    // Static batching replaces runtime vertices with a combined buffer in another space.
                    var info = filter.GetComponent<ImportedModelInfo>();
                    var source = info != null ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IGIImported/Models/missions/location0/level1/" + info.originalModel + ".igimodel") : null;
                    var original = source != null ? source.GetComponent<MeshFilter>().sharedMesh : filter.sharedMesh;
                    var vertices = original.vertices; var triangles = original.triangles;
                    Debug.Log("BARRACKS_MESH_BINDING: " + building.name + " part=" + filter.name + " mesh=" + original.name + " vertices=" + vertices.Length + " triangles=" + triangles.Length / 3 + " asset=" + AssetDatabase.GetAssetPath(original));
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = filter.transform.TransformPoint(vertices[triangles[i]]), b = filter.transform.TransformPoint(vertices[triangles[i + 1]]), c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                        Vector3 cross = Vector3.Cross(b - a, c - a);
                        if (cross.magnitude < .3f || Mathf.Abs(cross.normalized.y) > .2f) continue;
                        total++;
                        Vector3 centre = (a + b + c) / 3;
                        if (!WorldOcclusion.Blocked(centre - cross.normalized * .2f, centre + cross.normalized * .2f))
                        {
                            missing++;
                            if (missing <= 8) Debug.Log("VISIBLE_WALL_WITHOUT_COLLISION: " + building.name + " part=" + filter.name + " at=" + centre + " normal=" + cross.normalized);
                        }
                    }
                }
                Debug.Log("BARRACKS_RENDER_COLLISION_AUDIT: " + building.name + " unblocked=" + missing + "/" + total);
            }
            if (Environment.GetCommandLineArgs().Contains("-igi-audit-only")) yield break;
            var controller = mission.player.GetComponent<CharacterController>();
            foreach (int id in new[] { 2002, 2017, 2018, 2005 })
            {
                var guard = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None).First(g => g.name.Contains("[" + id + "]"));
                guard.GetComponent<Health>().ResetHealth(1000);
                bool placed = false;
                foreach (float radius in new[] { 24f, 8f, 4f, 2f })
                {
                    for (int angle = 0; angle < 12 && !placed; angle++)
                    {
                        Vector3 at = guard.transform.position + Quaternion.Euler(0, angle * 30, 0) * guard.transform.forward * radius;
                        if (!Physics.Raycast(at + Vector3.up * 2, Vector3.down, out var support, 4, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) continue;
                        at.y = support.point.y + .1f;
                        if (Physics.CheckCapsule(at + Vector3.up * .38f, at + Vector3.up * 1.5f, .32f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) continue;
                        controller.enabled = false; mission.player.transform.position = at; controller.enabled = true; Physics.SyncTransforms();
                        placed = guard.CanSee(at + Vector3.up * 1.25f);
                    }
                    if (placed) break;
                }
                if (!placed) { Debug.Log("ENCOUNTER_NO_VISIBLE_POSITION: " + guard.name); continue; }
                Debug.Log("ENCOUNTER_BEGIN: " + guard.name + " player=" + mission.player.transform.position + " guard=" + guard.transform.position);
                guard.GetComponent<Health>().TakeDamage(1, Team.Player, mission.player.transform.position + Vector3.up * 1.73f);
                float start = Time.time, end = Time.time + 12, sample = 0;
                Vector3 previous = guard.transform.position;
                int stationaryAlarmSamples = 0;
                bool raised = false;
                while (Time.time < end)
                {
                    if (Time.time >= sample)
                    {
                        sample = Time.time + 1;
                        var animation = guard.GetComponent<GuardPresentation>().animationPlayer;
                        Debug.Log("ENCOUNTER_FRAME: " + guard.name + " t=" + Time.time + " state=" + guard.State + " at=" + guard.transform.position + " sight=" + guard.HasSight + " suspicion=" + guard.Suspicion + " rounds=" + Field(guard, "rounds") + " firstShot=" + Field(guard, "firstShotAt") + " nextShot=" + Field(guard, "nextShot") + " alarm=" + Field(guard, "alarmTarget") + " animation=" + animation.Current + " path=" + ((List<Vector3>)Field(guard, "path")).Count);
                        stationaryAlarmSamples = guard.State == EnemyGuard.GuardState.RaiseAlarm && Vector3.Distance(previous, guard.transform.position) < .05f ? stationaryAlarmSamples + 1 : 0;
                        if (stationaryAlarmSamples >= 4) throw new Exception("Reproduced alarm-route standstill: " + guard.name);
                        previous = guard.transform.position;
                    }
                    if (!raised && Time.time > start + 6) { mission.Security.RaiseAlarm(mission.player.transform.position); raised = true; }
                    if (raised && guard.State == EnemyGuard.GuardState.RaiseAlarm && Field(guard, "alarmTarget") == null) throw new Exception("Guard stayed in RaiseAlarm after another guard activated the alarm");
                    yield return null;
                }
            }
        }
    }
}
