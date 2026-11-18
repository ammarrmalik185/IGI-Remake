using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats.World;
using OpenIGI.Game.World;
using OpenIGI.Scripting.Runtime;

internal static class LadderExport
{
    public static void Run(string project)
    {
        var resources = new ResourceRegistry(new VirtualFileSystem());
        resources.Register(MagicObjectRegistry.ScriptResource, File.ReadAllBytes(Path.Combine(project, "../IGIEnhanced/base_igi/pc/magicobj/magicobj.qvm")));
        var natives = new NativeRegistry(); natives.RegisterConstant("TRUE", 1); natives.RegisterConstant("FALSE", 0);
        var registry = new MagicObjectRegistry(); registry.Register(natives); registry.Load(resources, natives);
        var mission = JsonNode.Parse(File.ReadAllText(Path.Combine(project, "Assets/IGIImported/Missions/01.igimission")))!;
        var ladders = new List<object>();
        string? ModelPath(string name)
        {
            foreach (string folder in new[] { "missions/location0/level1", "missions/location0/common", "common" })
            { string p = "Assets/IGIImported/Models/" + folder + "/" + name + ".igimodel"; if (File.Exists(Path.Combine(project, p))) return p; }
            return null;
        }
        MefModel Read(string modelPath)
        {
            var model = JsonNode.Parse(File.ReadAllText(Path.Combine(project, modelPath)))!;
            var mesh = JsonNode.Parse(File.ReadAllText(Path.Combine(project, model["mesh"]!.ToString())))!;
            string source = mesh["source"]!.ToString(); int colon = source.IndexOf(':');
            string entry = source[(colon + 1)..]; int prefix = entry.IndexOf(':'); if (prefix >= 0) entry = entry[(prefix + 1)..];
            return MefModel.Read(File.ReadAllBytes(Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry)));
        }
        var placements = mission["placements"]!.AsArray(); var paths = mission["modelPaths"]!.AsArray();
        for (int i = 0; i < placements.Count; i++)
        {
            if (placements[i]!["type"]!.ToString() != "Building" || paths[i]!.ToString().Length == 0 || placements[i]!["id"]!.GetValue<int>() < 0) continue;
            var parent = Read(paths[i]!.ToString());
            foreach (var attachment in parent.Attachments)
            {
                if (!registry.TryGet(attachment.Name, out var definition) || registry.TaskTypeName(definition.TaskType) != "TASKTYPE_LADDER") continue;
                var path = ModelPath(definition.Model); if (path == null) throw new Exception("Ladder model missing: " + definition.Model);
                var model = Read(path);
                if (!model.TryGetMagicVertex(2, out var foot) || !model.TryGetMagicVertex(3, out var head)) throw new Exception("Missing ladder endpoints");
                object V(OpenIGI.Core.Numerics.Vector3f v) => new { x = v.X / 4096f, y = v.Z / 4096f, z = v.Y / 4096f };
                var m = attachment.Orientation;
                var row = new { parentId = placements[i]!["id"]!.GetValue<int>(), modelPath = path,
                    position = V(attachment.Position), foot = V(foot), head = V(head),
                    forward = new { x = m.M21, y = m.M23, z = m.M22 }, up = new { x = m.M31, y = m.M33, z = m.M32 } };
                ladders.Add(row); Console.WriteLine(JsonSerializer.Serialize(row));
            }
        }
        File.WriteAllText(Path.Combine(project, "Assets/IGIRebuilt/Trainyard/TrainyardLadders.json"), JsonSerializer.Serialize(new { ladders }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Exported {ladders.Count} original attached ladders.");
    }
}
