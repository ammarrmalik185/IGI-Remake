using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IGIRebuilt
{
    public sealed class GridNavigation : MonoBehaviour
    {
        public float cellSize = 2;
        public int width = 48;
        public int depth = 44;
        public Vector3 worldCentre;
        public bool sampleTerrain;
        private float[] heights;
        private bool[] walkable;
        private Vector3 origin;

        public void Rebuild()
        {
            origin = worldCentre - new Vector3(width * cellSize / 2, 0, depth * cellSize / 2);
            walkable = new bool[width * depth];
            heights = new float[width * depth];
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    if (sampleTerrain)
                    {
                        Vector3 at = origin + new Vector3((x + 0.5f) * cellSize, 180, (z + 0.5f) * cellSize);
                        if (!Physics.Raycast(at, Vector3.down, out RaycastHit hit, 480, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.65f) continue;
                        heights[index] = hit.point.y;
                    }
                    walkable[index] = !Physics.OverlapBox(Centre(x, z) + Vector3.up * 1.05f, new Vector3(0.32f, 0.65f, 0.32f), Quaternion.identity, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore).Any(c => c.GetComponentInParent<InteractiveDoor>() == null);
                }
        }
        public List<Vector3> FindPath(Vector3 start, Vector3 destination)
        {
            if (walkable == null) Rebuild();
            int from = Nearest(start), to = Nearest(destination);
            var result = new List<Vector3>();
            if (from < 0 || to < 0) return result;
            int[] previous = new int[walkable.Length];
            float[] cost = new float[walkable.Length];
            bool[] closed = new bool[walkable.Length];
            for (int i = 0; i < cost.Length; i++) { cost[i] = float.PositiveInfinity; previous[i] = -1; }
            cost[from] = 0;
            var open = new PathHeap(); open.Push(from, Heuristic(from, to));
            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                if (current == to)
                {
                    for (int at = to; at != from && at >= 0; at = previous[at]) result.Add(Centre(at % width, at / width));
                    result.Reverse();
                    return result;
                }
                closed[current] = true;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = current % width + dx, z = current / width + dz;
                        if (x < 0 || z < 0 || x >= width || z >= depth) continue;
                        int next = z * width + x;
                        if (!walkable[next] || closed[next]) continue;
                        if (sampleTerrain && Mathf.Abs(heights[next] - heights[current]) > cellSize * 0.7f) continue;
                        if (sampleTerrain && EdgeBlocked(Centre(current % width, current / width) + Vector3.up, Centre(x, z) + Vector3.up)) continue;
                        if (dx != 0 && dz != 0 && (!walkable[(current / width) * width + x] || !walkable[z * width + current % width])) continue;
                        float candidate = cost[current] + (dx != 0 && dz != 0 ? 1.414214f : 1);
                        if (candidate >= cost[next]) continue;
                        cost[next] = candidate;
                        previous[next] = current;
                        open.Push(next, candidate + Heuristic(next, to));
                    }
                }
            }
            return result;
        }
        private bool EdgeBlocked(Vector3 a, Vector3 b)
        {
            if (!Physics.Linecast(a,b,1 << DemoWorld.WorldLayer,QueryTriggerInteraction.Ignore)) return false;
            Vector3 delta=b-a;
            return Physics.RaycastAll(a,delta.normalized,delta.magnitude,1 << DemoWorld.WorldLayer,QueryTriggerInteraction.Ignore).Any(h=>h.collider.GetComponentInParent<InteractiveDoor>()==null);
        }
        private sealed class PathHeap
        {
            private readonly List<int> nodes=new List<int>(); private readonly List<float> scores=new List<float>();
            public int Count => nodes.Count;
            public void Push(int node,float score) {nodes.Add(node);scores.Add(score);int i=nodes.Count-1;while(i>0){int p=(i-1)/2;if(scores[p]<=score)break;nodes[i]=nodes[p];scores[i]=scores[p];i=p;}nodes[i]=node;scores[i]=score;}
            public int Pop(){int value=nodes[0],last=nodes.Count-1,node=nodes[last];float score=scores[last];nodes.RemoveAt(last);scores.RemoveAt(last);if(last==0)return value;int i=0;while(i*2+1<last){int child=i*2+1;if(child+1<last&&scores[child+1]<scores[child])child++;if(scores[child]>=score)break;nodes[i]=nodes[child];scores[i]=scores[child];i=child;}nodes[i]=node;scores[i]=score;return value;}
        }
        private float Heuristic(int a, int b)
        {
            float dx = Mathf.Abs(a % width - b % width), dz = Mathf.Abs(a / width - b / width);
            return Mathf.Max(dx, dz) + 0.414214f * Mathf.Min(dx, dz);
        }
        private int Nearest(Vector3 position)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((position.x - origin.x) / cellSize), 0, width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((position.z - origin.z) / cellSize), 0, depth - 1);
            int best = -1; float bestDistance = float.PositiveInfinity;
            for (int radius = 0; radius < 8; radius++)
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx >= 0 && nz >= 0 && nx < width && nz < depth && walkable[nz * width + nx])
                        {
                            Vector3 delta = Centre(nx, nz) - position;
                            float distance = delta.x * delta.x + delta.z * delta.z + delta.y * delta.y * 4;
                            if (distance < bestDistance) { bestDistance = distance; best = nz * width + nx; }
                        }
                    }
            return best;
        }
        private Vector3 Centre(int x, int z) => new Vector3(origin.x + (x + 0.5f) * cellSize, sampleTerrain && heights != null ? heights[z * width + x] : origin.y, origin.z + (z + 0.5f) * cellSize);
    }
}
