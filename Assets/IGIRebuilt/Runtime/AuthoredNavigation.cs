using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IGIRebuilt
{
    [Serializable] public sealed class GuardPatrolCommand { public int kind; public float seconds; public Vector3 position; }
    [Serializable] public sealed class AuthoredNode { public int id; public Vector3 position; }
    [Serializable] public sealed class AuthoredLink { public int a, b; }
    [Serializable] public sealed class AuthoredGraph { public int id; public AuthoredNode[] nodes; public AuthoredLink[] links; }
    public sealed class AuthoredNavigation : MonoBehaviour
    {
        public AuthoredGraph[] graphs;
        public static bool ClearSegment(Vector3 a, Vector3 b, float radius = .29f)
        {
            Vector3 delta = b - a; delta.y = 0; float distance = delta.magnitude; if (distance < .01f) return true;
            // Check at the higher support plane. A descending sweep would hit the floor lip of a doorway.
            Vector3 start = new Vector3(a.x, Mathf.Max(a.y, b.y), a.z);
            return !Physics.CapsuleCastAll(start + Vector3.up * .4f, start + Vector3.up * 1.45f, radius, delta.normalized, distance, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore).Any(h => h.normal.y < .65f && h.collider.GetComponentInParent<InteractiveDoor>() == null);
        }
        public List<Vector3> FindPath(int graphId, Vector3 start, Vector3 destination)
        {
            var result = new List<Vector3>(); var graph = Array.Find(graphs, g => g.id == graphId); if (graph == null) return result;
            int Nearest(Vector3 at) { int best = -1; float distance = float.PositiveInfinity; for (int i = 0; i < graph.nodes.Length; i++) { var delta = graph.nodes[i].position - at; if (Mathf.Abs(delta.y) > 1.5f) continue; float d = delta.sqrMagnitude; if (d < distance && ClearSegment(at, graph.nodes[i].position)) { best = i; distance = d; } } return best; }
            int from = Nearest(start), to = Nearest(destination); if (from < 0 || to < 0) return result;
            var byId = new Dictionary<int, int>(); for (int i = 0; i < graph.nodes.Length; i++) byId[graph.nodes[i].id] = i;
            var edges = new List<int>[graph.nodes.Length]; for (int i = 0; i < edges.Length; i++) edges[i] = new List<int>(); foreach (var link in graph.links) if (byId.TryGetValue(link.a, out int a) && byId.TryGetValue(link.b, out int b)) { edges[a].Add(b); edges[b].Add(a); }
            var costs = Enumerable.Repeat(float.PositiveInfinity, edges.Length).ToArray(); var previous = Enumerable.Repeat(-1, edges.Length).ToArray(); var closed = new bool[edges.Length]; costs[from] = 0; var open = new List<int> { from };
            while (open.Count > 0)
            {
                int current = open.OrderBy(n => costs[n] + Vector3.Distance(graph.nodes[n].position, graph.nodes[to].position)).First(); open.Remove(current); if (current == to) { for (int n = to; n >= 0; n = previous[n]) { result.Add(graph.nodes[n].position); if (n == from) break; } result.Reverse(); if (ClearSegment(result[result.Count - 1], destination)) result.Add(destination); return result; }
                closed[current] = true;
                foreach (int next in edges[current]) { if (closed[next]) continue; float cost = costs[current] + Vector3.Distance(graph.nodes[current].position, graph.nodes[next].position); if (cost >= costs[next] || !ClearSegment(graph.nodes[current].position, graph.nodes[next].position)) continue; costs[next] = cost; previous[next] = current; if (!open.Contains(next)) open.Add(next); }
            }
            return result;
        }
    }
}
