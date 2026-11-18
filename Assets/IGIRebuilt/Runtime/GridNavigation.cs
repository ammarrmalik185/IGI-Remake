using System.Collections.Generic;
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
                    walkable[index] = !Physics.CheckBox(Centre(x, z) + Vector3.up * 1.05f, new Vector3(0.38f, 0.65f, 0.38f), Quaternion.identity, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore);
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
            var open = new List<int> { from };
            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (cost[open[i]] + Heuristic(open[i], to) < cost[open[best]] + Heuristic(open[best], to)) best = i;
                int current = open[best];
                open.RemoveAt(best);
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
                        if (sampleTerrain && Physics.Linecast(Centre(current % width, current / width) + Vector3.up,
                            Centre(x, z) + Vector3.up, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) continue;
                        if (dx != 0 && dz != 0 && (!walkable[(current / width) * width + x] || !walkable[z * width + current % width])) continue;
                        float candidate = cost[current] + (dx != 0 && dz != 0 ? 1.414214f : 1);
                        if (candidate >= cost[next]) continue;
                        cost[next] = candidate;
                        previous[next] = current;
                        if (!open.Contains(next)) open.Add(next);
                    }
                }
            }
            return result;
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
        private Vector3 Centre(int x, int z) => origin + new Vector3((x + 0.5f) * cellSize, heights != null ? heights[z * width + x] : 0, (z + 0.5f) * cellSize);
    }
}
