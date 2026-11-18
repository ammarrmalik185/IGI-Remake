using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Core.Numerics;
using OpenIGI.Formats.Animation;
using OpenIGI.Formats.World;

internal static class ModelPoseRepair
{
    public static void Run(string project)
    {
        string game = Path.GetFullPath(Path.Combine(project, "../IGIEnhanced/base_igi/pc"));
        var mission = JsonNode.Parse(File.ReadAllText(Path.Combine(project, "Assets/IGIImported/Missions/01.igimission")))!;
        var paths = mission["modelPaths"]!.AsArray().Select(p => p!.ToString()).Where(p => p.Length > 0)
            .Select(p => JsonNode.Parse(File.ReadAllText(Path.Combine(project, p)))!["mesh"]!.ToString()).Distinct();
        int repaired = 0;
        foreach (string path in paths)
        {
            string file = Path.Combine(project, path); var doc = JsonNode.Parse(File.ReadAllText(file))!;
            if (doc["skinned"]?.GetValue<bool>() != true) continue;
            string source = doc["source"]!.ToString(); int colon = source.IndexOf(':');
            string entry = source[(colon + 1)..]; int prefix = entry.IndexOf(':'); if (prefix >= 0) entry = entry[(prefix + 1)..];
            string nativePath = Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry);
            var model = MefModel.Read(File.ReadAllBytes(nativePath));
            string family = Path.GetFileNameWithoutExtension(entry)[..3]; int rig = family == "000" ? 0 : family == "015" ? 6 : 1;
            var animation = BoneAnimationSet.Read(File.ReadAllBytes(Path.Combine(game, $"common/anims/{rig:000}.iff")));
            var bone = new BoneObject(animation); bone.Play(0, 2, true); var pose = bone.Pose();
            var vertices = model.Vertices.ToArray(); var bindings = model.SkinInfluences.ToArray();
            var positions = new Vector3f[model.BaseVertexCount]; var normals = new Vector3f[model.BaseVertexCount];
            for (int i = 0; i < vertices.Length; i++)
            {
                var binding = bindings[i]; int target = i < model.BaseVertexCount ? i : binding.TargetVertex;
                if (target < 0 || target >= positions.Length) continue;
                var p = Rotate(pose.Rotations[binding.BoneIndex], vertices[i].Position); var t = pose.Positions[binding.BoneIndex]; var previous = positions[target];
                positions[target] = new Vector3f(previous.X + (p.X + t.X) * binding.Weight / 4096, previous.Y + (p.Y + t.Y) * binding.Weight / 4096, previous.Z + (p.Z + t.Z) * binding.Weight / 4096);
                if (i < model.BaseVertexCount) normals[i] = Rotate(pose.Rotations[binding.BoneIndex], vertices[i].Normal);
            }
            doc["positions"] = JsonSerializer.SerializeToNode(positions.SelectMany(p => new[] { p.X, p.Z, p.Y }).ToArray());
            doc["normals"] = JsonSerializer.SerializeToNode(normals.SelectMany(p => new[] { p.X, p.Z, p.Y }).ToArray());
            doc["uv"] = JsonSerializer.SerializeToNode(vertices.Take(model.BaseVertexCount).SelectMany(v => new[] { v.DiffuseU, 1 - v.DiffuseV }).ToArray());
            doc["referencePose"] = $"Standing animation 2, rig {rig}, first frame; static reference";
            File.WriteAllText(file, doc.ToJsonString()); repaired++;
        }
        Console.WriteLine($"Restored {repaired} Trainyard character reference poses.");
    }
    private static Vector3f Rotate(Matrix3f m, Vector3f v) => new(m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z, m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z, m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z);
}
