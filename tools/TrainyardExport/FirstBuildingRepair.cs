using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats.World;
using OpenIGI.Game.World;
using OpenIGI.Scripting.Runtime;

internal static class FirstBuildingRepair
{
    static string MeshPath(string project) => Path.Combine(project, JsonNode.Parse(File.ReadAllText(Path.Combine(project, "Assets/IGIImported/Models/missions/location0/level1/400_20_1.igimodel")))!["mesh"]!.ToString());
    static MefModel Read(string project)
    {
        var doc = JsonNode.Parse(File.ReadAllText(MeshPath(project)))!;
        string source = doc["source"]!.ToString(); int colon = source.IndexOf(':');
        string entry = source[(colon + 1)..]; int prefix = entry.IndexOf(':'); if (prefix >= 0) entry = entry[(prefix + 1)..];
        return MefModel.Read(File.ReadAllBytes(Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry)));
    }
    static object V(OpenIGI.Core.Numerics.Vector3f p) => new { x = p.X / 4096f, y = p.Z / 4096f, z = p.Y / 4096f };
    public static void Scan(string project)
    {
        var model = Read(project);
        var resources = new ResourceRegistry(new VirtualFileSystem());
        resources.Register(MagicObjectRegistry.ScriptResource, File.ReadAllBytes(Path.Combine(project, "../IGIEnhanced/base_igi/pc/magicobj/magicobj.qvm")));
        var natives = new NativeRegistry(); natives.RegisterConstant("TRUE", 1); natives.RegisterConstant("FALSE", 0);
        var registry = new MagicObjectRegistry(); registry.Register(natives); registry.Load(resources, natives);
        var attachments = model.Attachments.Select(a => new { a.Name, position = V(a.Position), model = registry.TryGet(a.Name, out var d) ? d.Model : "", type = registry.TryGet(a.Name, out d) ? registry.TaskTypeName(d.TaskType) : "" }).ToArray();
        var portals = model.Portals.Portals.Select(p => new { p.Slot, vertices = model.Portals.Vertices.Skip(p.FirstVertex).Take(p.VertexCount).Select(V).ToArray() }).ToArray();
        var materials = new List<object>();
        for (int level = 0; level < 2; level++)
            if (MefCollisionMesh.TryRead(model.Container, level, out var collision) && collision != null)
            {
                var faces = collision.Faces.ToArray();
                materials.Add(new { level, count = faces.Length, groups = faces.GroupBy(f => f.Material).Select(g => new { material = g.Key, count = g.Count(), portal = collision.IsPortal(g.Key), global = collision.GlobalMaterial(g.Key) }).ToArray() });
            }
        Directory.CreateDirectory(Path.Combine(project, "Logs"));
        File.WriteAllText(Path.Combine(project, "Logs/first-building-source.json"), JsonSerializer.Serialize(new { attachments, portals, materials }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { attachments, portals, materials }));
    }
    static string ModelPath(string name) => "Assets/IGIImported/Models/missions/location0/level1/" + name + ".igimodel";
    static MefModel ReadModel(string project, string path)
    {
        var binding = JsonNode.Parse(File.ReadAllText(Path.Combine(project, path)))!;
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(project, binding["mesh"]!.ToString())))!;
        string source = document["source"]!.ToString(); int colon = source.IndexOf(':');
        string entry = source[(colon + 1)..]; int prefix = entry.IndexOf(':'); if (prefix >= 0) entry = entry[(prefix + 1)..];
        return MefModel.Read(File.ReadAllBytes(Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry)));
    }
    public static void Run(string project)
    {
        var parent = Read(project);
        var rooms = new List<object>();
        foreach (var attachment in parent.Attachments)
        {
            if (attachment.Name is not ("400_02_1" or "400_03_1" or "400_21_1")) continue;
            var m = attachment.Orientation;
            string path = ModelPath(attachment.Name);
            ExportArchitecture(project, path, ReadModel(project, path));
            rooms.Add(new { modelPath = path, position = V(attachment.Position),
                forward = new { x = m.M21, y = m.M23, z = m.M22 }, up = new { x = m.M31, y = m.M33, z = m.M32 } });
        }
        ExportArchitecture(project, ModelPath("400_20_1"), parent);
        var doors = new List<object>();
        foreach (var portal in parent.Portals.Portals)
        {
            var points = parent.Portals.Vertices.Skip(portal.FirstVertex).Take(portal.VertexCount)
                .Select(p => new Vector3(p.X / 4096, p.Z / 4096, p.Y / 4096)).ToArray();
            Vector3 min = points.Aggregate(Vector3.Min), max = points.Aggregate(Vector3.Max);
            float height = max.Y - min.Y, width = MathF.Max(max.X - min.X, max.Z - min.Z);
            if (height < 1.85f || width < 0.75f) continue; // Window portals remain windows.
            Vector3 normal = max.X - min.X < max.Z - min.Z ? Vector3.UnitX : Vector3.UnitZ;
            Vector3 foot = (min + max) / 2; foot.Y = min.Y;
            doors.Add(new { slot = portal.Slot, foot = new { x = foot.X, y = foot.Y, z = foot.Z }, normal = new { x = normal.X, y = normal.Y, z = normal.Z }, width, height });
        }
        string output = Path.Combine(project, "Assets/IGIRebuilt/Trainyard/FirstBuildingInterior.json");
        File.WriteAllText(output, JsonSerializer.Serialize(new { parentId = 1118, rooms, doors }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Restored {rooms.Count} authored first-building room meshes and {doors.Count} doorway portals; precision collision keeps their openings.");
    }
    public static void ExportArchitecture(string project, string modelPath, MefModel model)
    {
        var binding = JsonNode.Parse(File.ReadAllText(Path.Combine(project, modelPath)))!;
        string file = Path.Combine(project, binding["mesh"]!.ToString());
        var document = JsonNode.Parse(File.ReadAllText(file))!; var vertices = model.Vertices.ToArray();
        document["version"] = 1; document["sector"] = model.IsSector; document["authoredWinding"] = true;
        document["positions"] = JsonSerializer.SerializeToNode(vertices.SelectMany(v => new[] { v.Position.X / 4096f, v.Position.Z / 4096f, v.Position.Y / 4096f }).ToArray());
        document["normals"] = JsonSerializer.SerializeToNode(vertices.SelectMany(v => new[] { v.Normal.X, v.Normal.Z, v.Normal.Y }).ToArray());
        document["uv"] = JsonSerializer.SerializeToNode(vertices.SelectMany(v => new[] { v.DiffuseU, 1 - v.DiffuseV }).ToArray());
        document["groups"] = JsonSerializer.SerializeToNode(model.DrawRecords.Select(record => new { slot = record.MaterialSlot, transparent = record.IsTransparent,
            indices = record.Indices.ToArray().Select(i => record.ToVertexIndex(i)).ToArray() }).ToArray());
        if (MefCollisionMesh.TryRead(model.Container, 0, out var collision) && collision != null && collision.Faces.Length > 0)
        {
            document["collisionPositions"] = JsonSerializer.SerializeToNode(collision.Vertices.SelectMany(v => new[] { v.X / 4096f, v.Z / 4096f, v.Y / 4096f }).ToArray());
            document["collisionIndices"] = JsonSerializer.SerializeToNode(collision.Faces.ToArray().Where(f => !collision.IsPortal(f.Material))
                .SelectMany(f => new[] { f.I0, f.I1, f.I2, f.I0, f.I2, f.I1 }).ToArray());
            document["collisionSource"] = "Detailed level-zero architecture, excluding portal polygons";
        }
        else { document.AsObject().Remove("collisionPositions"); document.AsObject().Remove("collisionIndices"); }
        File.WriteAllText(file, document.ToJsonString());
    }
}
