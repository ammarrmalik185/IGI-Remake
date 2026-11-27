using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenIGI.Formats.World;

internal static class SurfaceExport
{
    public static void Run(string project)
    {
        var paths = new HashSet<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(project, "Assets/IGIRebuilt/Trainyard"), "*.json").Append(Path.Combine(project, "Assets/IGIImported/Missions/01.igimission")))
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"(Assets/IGIImported/Models/[^\"]+\\.igimodel)\"")) paths.Add(match.Groups[1].Value);
        var seen = new HashSet<string>(); int changed = 0;
        foreach (string path in paths)
        {
            var binding = JsonNode.Parse(File.ReadAllText(Path.Combine(project, path)))!;
            // The checkpoint shell and boom barrier have coarse human collision that covers
            // open window space; retain the detailed native geometry for these two models.
            bool checkpointCollision = binding["model"]!.ToString() is "425_01_1" or "355_01_1";
            string meshPath = binding["mesh"]!.ToString();
            if (!seen.Add(meshPath)) continue;
            var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(project, meshPath)))!;
            bool materialScan = Environment.GetCommandLineArgs().Contains("--material-scan");
            if (doc["skinned"]?.GetValue<bool>() == true || (doc["collisionPositions"] == null && !materialScan)) continue;
            string source = doc["source"]!.ToString(); int colon = source.IndexOf(':');
            string entry = source[(colon + 1)..]; if (entry.Contains(':')) entry = entry[(entry.IndexOf(':') + 1)..];
            var native = MefModel.Read(File.ReadAllBytes(Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry)));
            if (materialScan)
            {
                if (binding["model"]!.ToString().StartsWith("30") || binding["model"]!.ToString() is "425_01_1" or "355_01_1")
                    for (int level = 0; level < 2; level++)
                        if (MefCollisionMesh.TryRead(native.Container, level, out var scan) && scan != null)
                            Console.WriteLine($"MATERIAL_SCAN: {binding["model"]} level={level} stored={doc["collisionPositions"] != null} " + string.Join(", ", scan.Faces.ToArray().GroupBy(f => scan.GlobalMaterial(f.Material)).Select(g => $"{g.Key}:{g.Count()} portal={scan.IsPortal(g.First().Material)}")));
                continue;
            }
            var stored = doc["collisionIndices"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
            MefCollisionMesh? collision = null;
            for (int level = 0; level < 2; level++)
            {
                if (!MefCollisionMesh.TryRead(native.Container, level, out var candidate) || candidate == null) continue;
                var expected = candidate.Faces.ToArray().Where(f => !candidate.IsPortal(f.Material)).SelectMany(f => new[] { f.I0, f.I1, f.I2, f.I0, f.I2, f.I1 }).ToArray();
                var stripped = candidate.Faces.ToArray().Where(f => !candidate.IsPortal(f.Material) && candidate.GlobalMaterial(f.Material) != 14).SelectMany(f => new[] { f.I0, f.I1, f.I2, f.I0, f.I2, f.I1 });
                if (stored.SequenceEqual(expected) || stored.SequenceEqual(stripped)) { collision = candidate; break; }
            }
            if (MefCollisionMesh.TryRead(native.Container, 0, out var detailed) && detailed != null && (checkpointCollision || detailed.Faces.ToArray().Any(f => detailed.GlobalMaterial(f.Material) is 14 or 18))) collision = detailed;
            if (collision == null) continue;
            var special = collision.Faces.ToArray().Where(f => collision.GlobalMaterial(f.Material) is 14 or 18).ToArray();
            if (special.Length == 0 && !checkpointCollision) continue;
            Console.WriteLine($"SURFACE_SOURCE: {binding["model"]} glass={special.Count(f => collision.GlobalMaterial(f.Material) == 14)} fence={special.Count(f => collision.GlobalMaterial(f.Material) == 18)}");
            if (Environment.GetCommandLineArgs().Contains("--surface-scan")) continue;
            var vertices = collision.Vertices.Select(v => new Vector3(v.X / 4096f, v.Z / 4096f, v.Y / 4096f)).ToArray();
            var glass = special.Where(f => collision.GlobalMaterial(f.Material) == 14).Select(f => new[] { f.I0, f.I1, f.I2 }).ToArray();
            var panes = new List<List<int[]>>();
            foreach (var face in glass)
            {
                Vector3 normal = Vector3.Normalize(Vector3.Cross(vertices[face[1]] - vertices[face[0]], vertices[face[2]] - vertices[face[0]]));
                var adjacent = panes.Where(p => p.Any(t => {
                    Vector3 n = Vector3.Normalize(Vector3.Cross(vertices[t[1]] - vertices[t[0]], vertices[t[2]] - vertices[t[0]]));
                    return MathF.Abs(Vector3.Dot(normal, n)) > .999f && face.Count(i => t.Any(j => Vector3.DistanceSquared(vertices[i], vertices[j]) < .0001f)) >= 2;
                })).ToArray();
                if (adjacent.Length == 0) panes.Add(new List<int[]> { face });
                else { adjacent[0].Add(face); foreach (var other in adjacent.Skip(1)) { adjacent[0].AddRange(other); panes.Remove(other); } }
            }
            var renderVertices = doc["positions"]!.AsArray().Select(v => v!.GetValue<float>()).Chunk(3).Select(v => new Vector3(v[0], v[1], v[2])).ToArray();
            bool Inside(Vector3 p, int[] triangle)
            {
                Vector3 a = vertices[triangle[0]], u = vertices[triangle[1]] - a, v = vertices[triangle[2]] - a, w = p - a;
                Vector3 n = Vector3.Cross(u, v); if (n.LengthSquared() < 1e-9f || MathF.Abs(Vector3.Dot(w, Vector3.Normalize(n))) > .03f) return false;
                float uu = Vector3.Dot(u, u), uv = Vector3.Dot(u, v), vv = Vector3.Dot(v, v), wu = Vector3.Dot(w, u), wv = Vector3.Dot(w, v), denom = uu * vv - uv * uv;
                float s = (wu * vv - wv * uv) / denom, t = (wv * uu - wu * uv) / denom;
                return s >= -.001f && t >= -.001f && s + t <= 1.001f;
            }
            var paneDocuments = new List<object>();
            foreach (var pane in panes)
            {
                var selections = new List<object>();
                int groupIndex = 0;
                foreach (var group in doc["groups"]!.AsArray())
                {
                    var ids = group!["indices"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
                    var selected = new List<int>();
                    for (int t = 0; t < ids.Length; t += 3)
                    {
                        Vector3 a = renderVertices[ids[t]], b = renderVertices[ids[t + 1]], c = renderVertices[ids[t + 2]];
                        if (pane.Any(face => Inside(a, face)) && pane.Any(face => Inside(b, face)) && pane.Any(face => Inside(c, face))) selected.Add(t / 3);
                    }
                    if (selected.Count > 0) selections.Add(new { group = groupIndex, triangles = selected.ToArray() });
                    groupIndex++;
                }
                paneDocuments.Add(new { indices = pane.SelectMany(t => new[] { t[0], t[1], t[2], t[0], t[2], t[1] }).ToArray(), render = selections.ToArray() });
            }
            var indices = new List<int>(); var kinds = new List<int>();
            foreach (var face in collision.Faces)
            {
                if (collision.IsPortal(face.Material) || collision.GlobalMaterial(face.Material) == 14) continue;
                indices.AddRange(new[] { face.I0, face.I1, face.I2, face.I0, face.I2, face.I1 });
                int kind = collision.GlobalMaterial(face.Material) == 18 ? 1 : 0;
                kinds.Add(kind); kinds.Add(kind);
            }
            doc["collisionIndices"] = JsonSerializer.SerializeToNode(indices);
            doc["collisionPositions"] = JsonSerializer.SerializeToNode(vertices.SelectMany(v => new[] { v.X, v.Y, v.Z }).ToArray());
            doc["collisionSource"] = "Detailed native surfaces, with glass split into independently breakable panes";
            doc["collisionSurfaceKinds"] = JsonSerializer.SerializeToNode(kinds);
            doc["glassPanes"] = JsonSerializer.SerializeToNode(paneDocuments);
            File.WriteAllText(Path.Combine(project, meshPath), doc.ToJsonString()); changed++;
            Console.WriteLine($"SURFACE_EXPORT: {binding["model"]}, {panes.Count} glass panes, {kinds.Count(k => k == 1)} fence triangles");
        }
        Console.WriteLine($"Imported surface metadata: {changed} meshes updated.");
    }
}
