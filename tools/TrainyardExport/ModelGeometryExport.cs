using System.Text.Json.Nodes;
using OpenIGI.Formats.World;

internal static class ModelGeometryExport
{
    public static void Run(string project)
    {
        var paths = new HashSet<string>();
        var mission = JsonNode.Parse(File.ReadAllText(Path.Combine(project, "Assets/IGIImported/Missions/01.igimission")))!;
        foreach (var modelPath in mission["modelPaths"]!.AsArray())
        {
            string name = modelPath!.ToString();
            if (name.Length == 0) continue;
            var model = JsonNode.Parse(File.ReadAllText(Path.Combine(project, name)))!;
            paths.Add(model["mesh"]!.ToString());
        }
        int exported = 0, sectors = 0, portals = 0, linings = 0;
        foreach (string path in paths)
        {
            string meshPath = Path.Combine(project, path);
            var doc = JsonNode.Parse(File.ReadAllText(meshPath))!;
            if (doc["skinned"]?.GetValue<bool>() == true) continue; // Keep the separately baked character pose.
            string source = doc["source"]!.ToString(); int colon = source.IndexOf(':');
            if (colon < 0) continue;
            string entry = source[(colon + 1)..]; int prefix = entry.IndexOf(':');
            if (prefix >= 0) entry = entry[(prefix + 1)..];
            string nativePath = Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry.TrimStart('/'));
            if (!File.Exists(nativePath)) continue; // Baked character poses have no static source collision.
            var native = MefModel.Read(File.ReadAllBytes(nativePath));
            var renderVertices = native.Vertices.ToArray();
            var positions = renderVertices.SelectMany(v => new[] { v.Position.X / 4096f, v.Position.Z / 4096f, v.Position.Y / 4096f }).ToList();
            var normals = renderVertices.SelectMany(v => new[] { v.Normal.X, v.Normal.Z, v.Normal.Y }).ToList();
            var uv = renderVertices.SelectMany(v => new[] { v.DiffuseU, 1 - v.DiffuseV }).ToList();
            var groups = native.DrawRecords.Select(record => {
                var indices = record.Indices.ToArray().Select(i => record.ToVertexIndex(i)).ToList();
                int originalCount = indices.Count;
                for (int t = 0; native.IsSector && t < originalCount; t += 3)
                {
                    int[] ids = { indices[t], indices[t + 1], indices[t + 2] };
                    System.Numerics.Vector3 Point(int i) => new(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
                    var cross = System.Numerics.Vector3.Cross(Point(ids[1]) - Point(ids[0]), Point(ids[2]) - Point(ids[0]));
                    if (cross.Length() < 0.2f) continue;
                    var normal = System.Numerics.Vector3.Normalize(cross);
                    if (MathF.Abs(normal.Y) > 0.3f) continue;
                    // The room meshes contain single sheets for many walls. Give these an inward
                    // lining with the source UV/material instead of exposing the world through them.
                    int start = positions.Count / 3;
                    foreach (int id in ids)
                    {
                        var point = Point(id) - normal * 0.002f;
                        positions.AddRange(new[] { point.X, point.Y, point.Z });
                        normals.AddRange(new[] { -normal.X, -normal.Y, -normal.Z });
                        uv.AddRange(new[] { uv[id * 2], uv[id * 2 + 1] });
                    }
                    indices.AddRange(new[] { start, start + 2, start + 1 }); linings++;
                }
                return new { slot = record.MaterialSlot, transparent = record.IsTransparent, indices = indices.ToArray() };
            }).ToArray();
            doc["positions"] = System.Text.Json.JsonSerializer.SerializeToNode(positions);
            doc["normals"] = System.Text.Json.JsonSerializer.SerializeToNode(normals);
            doc["uv"] = System.Text.Json.JsonSerializer.SerializeToNode(uv);
            doc["groups"] = System.Text.Json.JsonSerializer.SerializeToNode(groups);
            doc["authoredWinding"] = true;
            doc["sector"] = native.IsSector;
            if (native.IsSector) sectors++;
            // The original human trace uses level one; fall back to world collision when absent.
            MefCollisionMesh? collision = null;
            if (!MefCollisionMesh.TryRead(native.Container, 1, out collision) || collision == null || collision.Faces.Length == 0)
                MefCollisionMesh.TryRead(native.Container, 0, out collision);
            if (collision != null && collision.Faces.Length > 0)
            {
                var vertices = collision.Vertices.SelectMany(v => new[] { v.X / 4096f, v.Z / 4096f, v.Y / 4096f }).ToArray();
                var indices = new List<int>();
                foreach (var face in collision.Faces)
                {
                    if (collision.IsPortal(face.Material)) { portals++; continue; }
                    // Keep collision solid on both sides, including thin authored walls.
                    indices.AddRange(new[] { face.I0, face.I1, face.I2, face.I0, face.I2, face.I1 });
                }
                doc["collisionPositions"] = System.Text.Json.JsonSerializer.SerializeToNode(vertices);
                doc["collisionIndices"] = System.Text.Json.JsonSerializer.SerializeToNode(indices);
                exported++;
            }
            File.WriteAllText(meshPath, doc.ToJsonString());
        }
        Console.WriteLine($"Trainyard: {exported} native collision meshes, {sectors} interior sectors, {portals} portal faces left open, {linings} inward wall lining triangles.");
        FirstBuildingRepair.Run(project);
        SurfaceExport.Run(project);
    }
}
