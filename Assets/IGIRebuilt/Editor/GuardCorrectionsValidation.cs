using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class GuardCorrectionsValidation
    {
        static IEnumerator routine; static double started;
        static GuardCorrectionsValidation() { if (SessionState.GetBool("IGI_GUARD_CORRECTIONS", false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Update; } }
        public static void Run() { TrainyardSetup.BuildMission(); SessionState.SetBool("IGI_GUARD_CORRECTIONS", true); started = EditorApplication.timeSinceStartup; EditorApplication.update -= Update; EditorApplication.update += Update; EditorApplication.isPlaying = true; }
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Update() { try { if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Guard corrections validation timed out"); if (!EditorApplication.isPlaying) return; var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(); if (bootstrap == null || bootstrap.Mission == null) return; if (routine == null) routine = Checks(bootstrap.Mission); if (!routine.MoveNext()) { SessionState.SetBool("IGI_GUARD_CORRECTIONS", false); Debug.Log("GUARD_CORRECTIONS_VALIDATION_PASS: selective persistent alarms, camera-only computers, native weapon sockets, damage response, authored facing and barracks exits"); EditorApplication.Exit(0); } } catch (Exception e) { SessionState.SetBool("IGI_GUARD_CORRECTIONS", false); Debug.LogException(e); EditorApplication.Exit(1); } }
        static void DumpPath(EnemyGuard guard, Vector3 destination)
        {
            var graph = guard.authoredNavigation.graphs.First(g => g.id == guard.graphId); var from = graph.nodes.OrderBy(n => Vector3.Distance(n.position, guard.transform.position)).First(); var to = graph.nodes.OrderBy(n => Vector3.Distance(n.position, destination)).First(); var previous = new System.Collections.Generic.Dictionary<int, int>(); var queue = new System.Collections.Generic.Queue<int>(); queue.Enqueue(from.id); previous[from.id] = -1;
            while (queue.Count > 0) { int id = queue.Dequeue(); if (id == to.id) break; foreach (var l in graph.links.Where(l => l.a == id || l.b == id)) { int other = l.a == id ? l.b : l.a; if (!previous.ContainsKey(other)) { previous[other] = id; queue.Enqueue(other); } } }
            if (!previous.ContainsKey(to.id)) { Debug.Log("GRAPH_DISCONNECTED: " + guard.name); return; }
            var points = new System.Collections.Generic.List<Vector3> { destination }; for (int id = to.id; id >= 0; id = previous[id]) points.Add(graph.nodes.First(n => n.id == id).position); points.Add(guard.transform.position); points.Reverse();
            for (int i = 0; i + 1 < points.Count; i++) if (!AuthoredNavigation.ClearSegment(points[i], points[i + 1])) { Vector3 delta = points[i + 1] - points[i]; var hits = Physics.CapsuleCastAll(points[i] + Vector3.up * .4f, points[i] + Vector3.up * 1.45f, .29f, delta.normalized, delta.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore); Debug.Log("BLOCKED_EDGE: " + guard.name + " " + points[i] + " -> " + points[i + 1] + " hits=" + string.Join(";", hits.Select(h => h.collider.name + " at " + h.point + " normal " + h.normal))); }
        }
        static IEnumerator Checks(MissionDirector m)
        {
            m.SetPaused(false); m.player.enabled = false; var playerController = m.player.GetComponent<CharacterController>(); playerController.enabled = false; m.player.transform.SetPositionAndRotation(new Vector3(1000, 100, 1000), Quaternion.identity); playerController.enabled = true;
            var guards = UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None); var red = guards.Where(g => g.respondsToAlarm).ToArray(); var ordinary = guards.Where(g => !g.respondsToAlarm).ToArray(); Require(red.Length == 12 && ordinary.Length == 9, "Incorrect red beret classification");
            var ordinaryStates = ordinary.Select(g => g.State).ToArray(); var starts = red.Select(g => g.transform.position).ToArray();
            foreach (var guard in guards) { var look = guard.authoredPatrol.FirstOrDefault(c => c.kind == 5); if (look != null) { var dir = look.position - guard.transform.position; dir.y = 0; Require(Vector3.Angle(guard.transform.forward, dir) < 6, "Wrong authored facing " + guard.name); } var p = guard.GetComponent<GuardPresentation>(); var a = p.animationPlayer; Require(a.WeaponSocket != null && a.WeaponSocket.childCount > 0, "Weapon is not on animated hand"); var rifle = a.WeaponSocket.GetChild(0); Require(rifle.localPosition.sqrMagnitude < .0001f && Quaternion.Angle(rifle.localRotation, Quaternion.identity) < .01f, "Weapon has extra static offset"); a.Play("Walk"); a.SetMovementSpeed(1.55f); a.Advance(.2f); var hand = a.WeaponSocket.position; a.Advance(.12f); Require(Vector3.Distance(hand, a.WeaponSocket.position) > .001f, "Weapon socket does not animate"); }
            var n = m.Security; n.RaiseAlarm(new Vector3(-5, -2, -29)); for (int i = 0; i < ordinary.Length; i++) Require(ordinary[i].State == ordinaryStates[i], "Alarm alerted ordinary guard " + ordinary[i].name); Require(red.All(g => g.State != EnemyGuard.GuardState.Patrol), "Alarm missed a red beret");
            foreach (var g in red) { var command = g.authoredAlarm.FirstOrDefault(c => c.kind == 3); var path = g.authoredNavigation.FindPath(g.graphId, g.transform.position, command.position); Debug.Log("ALARM_ROUTE: " + g.name + " corners=" + path.Count); if (path.Count == 0) DumpPath(g, command.position); Require(path.Count > 0, "No route from barracks for " + g.name); }
            var consoles = UnityEngine.Object.FindObjectsByType<SecurityConsole>(FindObjectsSortMode.None); Require(consoles.Length == 2 && consoles.All(c => !c.alarmControl), "Computers are configured as alarm controls"); foreach (var console in consoles) console.enabled = false; consoles[0].AdvanceHack(true, 5); Require(n.CamerasDisabled && n.AlarmActive && !n.AlarmsDisabled, "Computer disabled alarm"); consoles[1].alarmControl = true; consoles[1].AdvanceHack(true, 5); Require(n.AlarmActive && !n.AlarmsDisabled, "Legacy computer setting disables alarm"); n.panels[0].Use(); Require(n.AlarmActive, "Alarm button cancelled persistent alarm");
            Time.timeScale = 3; float finish = Time.time + 55; while (Time.time < finish) yield return null; Time.timeScale = 1; Require(n.AlarmActive, "Alarm expired");
            var barracks = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.Contains("[418_01_1]")).Select(t => t.GetComponent<Renderer>().bounds).ToArray();
            int escaped = 0; for (int i = 0; i < red.Length; i++) { Vector3 at = red[i].transform.position; bool inside = barracks.Any(b => at.x > b.min.x + .1f && at.x < b.max.x - .1f && at.z > b.min.z + .1f && at.z < b.max.z - .1f); Debug.Log("BARRACKS_EXIT: " + red[i].name + " moved=" + Vector3.Distance(at, starts[i]) + " inside=" + inside + " at=" + at); if (!inside && Vector3.Distance(at, starts[i]) > 5) escaped++; }
            Require(escaped == 12, "Red berets stuck inside barracks: exited " + escaped + "/12");
            foreach (var guard in guards) guard.enabled = false;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = DemoWorld.WorldLayer; floor.transform.position = new Vector3(1000, 99.4f, 1000); floor.transform.localScale = new Vector3(40, 1, 40);
            var probe = new GameObject("Damage response probe"); probe.layer = DemoWorld.GuardLayer; probe.transform.position = new Vector3(1000, 100, 1010); probe.transform.forward = Vector3.forward; var cc = probe.AddComponent<CharacterController>(); cc.height = 1.8f; cc.center = Vector3.up * .9f; var health = probe.AddComponent<Health>(); health.team = Team.Guard; var enemy = probe.AddComponent<EnemyGuard>(); enemy.enabled = false; enemy.mission = m; enemy.target = m.player.transform; m.RegisterGuard(enemy); Physics.SyncTransforms();
            Require(health.TakeDamage(1, Team.Player, m.player.eye.transform.position), "Damage not applied"); Require(enemy.State == EnemyGuard.GuardState.Attack && enemy.Suspicion >= .99f, "Damage did not alert guard"); Require(Vector3.Angle(enemy.transform.forward, Vector3.back) < 1, "Attacked guard did not face shooter");
            m.weapon.Equip(0); m.player.eye.transform.LookAt(probe.transform.position + Vector3.up * 1.1f); float ready = Time.time + .3f; while (Time.time < ready) yield return null; float before = health.Current; Require(m.weapon.Fire(), "Gun did not fire"); Require(health.Current < before && enemy.State == EnemyGuard.GuardState.Attack, "Actual gunfire did not alert struck guard");
            Debug.Log("GUARD_CORRECTIONS_RUNTIME_CHECKS_COMPLETE"); yield return null;
        }
    }
}
