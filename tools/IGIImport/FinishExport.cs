using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Core.Numerics;
using OpenIGI.Formats.Animation;
using OpenIGI.Formats.World;
using OpenIGI.Formats.Ilff;

internal static class FinishExport
{
    public static void Run(string source, string project)
    {
        string root = Path.Combine(project, "Assets/IGIImported");
        var options = new JsonSerializerOptions { WriteIndented = false };
        var models = Directory.GetFiles(Path.Combine(root, "Models"), "*.igimodel", SearchOption.AllDirectories).Order()
            .Select(f => (path: f, data: JsonNode.Parse(File.ReadAllText(f))!)).ToArray();
        var byName = models.Where(m => !m.path.Replace('\\', '/').Contains("/Models/common/"))
            .GroupBy(m => m.data["model"]!.ToString()).ToDictionary(g => g.Key, g => g.First());
        var pack = MtpPack.Read(File.ReadAllBytes(Path.Combine(source, "common/common.mtp")));
        var unavailable = new List<string>(); int aliases = 0;
        foreach (var instance in pack.Instances)
        {
            string path = Path.Combine(root, "Models/common", instance.Name + ".igimodel");
            string name = pack.ModelNames[instance.Model.Index];
            if (!byName.TryGetValue(name, out var found)) { unavailable.Add(name); continue; }
            // The global table declares prototypes whose actual resource resides in a level archive.
            // Keep that level's complete mesh/material binding rather than pairing unrelated textures by name.
            var data = found.data.DeepClone(); data["instance"] = instance.Name;
            data["globalPrototypeSource"] = "common/common.mtp";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, data.ToJsonString(options)); aliases++;
        }
        // Bake a neutral standing reference pose for the bone-local character meshes.
        // Animation sets and skin weights remain intact in SourceData~ for a future animated rig importer.
        var sets = Directory.GetFiles(Path.Combine(source, "common/anims"), "*.iff")
            .ToDictionary(f => int.Parse(Path.GetFileNameWithoutExtension(f)), f => BoneAnimationSet.Read(File.ReadAllBytes(f)));
        var poses = sets.ToDictionary(p => p.Key, p => { var bone = new BoneObject(p.Value); bone.Play(0, 2, true); return bone.Pose(); });
        var rigByFamily = new Dictionary<string, int> { ["000"] = 0, ["012"] = 6, ["015"] = 6, ["013"] = 2 };
        foreach (string definition in Directory.GetFiles(Path.Combine(root, "Definitions/missions"), "objects.qvm.json", SearchOption.AllDirectories))
        {
            var doc = JsonNode.Parse(File.ReadAllText(definition))!;
            var schemas = new Dictionary<string, JsonArray>();
            foreach (var call in doc["calls"]!.AsArray())
            {
                var a = call!["args"]!.AsArray();
                if (call["function"]!.ToString() == "Task_DeclareParameters") { schemas[a[0]!.ToString()] = a; continue; }
                if (a.Count < 3 || !a[1]!.ToString().StartsWith("Human") || !schemas.TryGetValue(a[1]!.ToString(), out var schema)) continue;
                string modelName = ""; int rig = a[1]!.ToString() == "HumanPlayer" ? 0 : 1, cursor = 3;
                for (int j = 1; j + 1 < schema.Count && cursor < a.Count; j += 2)
                {
                    string field = schema[j]!.ToString(), type = schema[j + 1]!.ToString();
                    if (field == "Model") modelName = a[cursor]!.ToString();
                    if (field is "Bone Heirachy" or "Bone Hierarchy") rig = a[cursor]!.GetValue<int>();
                    cursor += type is "ObjectPos" or "Real32x3" or "Real64x3" or "Real32x9" or "Quat32" or "RGB" ? 3 : 1;
                }
                if (modelName.Length >= 3) rigByFamily[modelName[..3]] = rig;
            }
        }
        int posed = 0;
        foreach (string archiveFile in Directory.GetFiles(source, "*.res", SearchOption.AllDirectories).Where(f => f.Contains("models", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var entry in IresArchive.Read(File.ReadAllBytes(archiveFile)).Entries.Where(e => e.Kind == IresEntryKind.Body && e.Name.EndsWith(".mef", StringComparison.OrdinalIgnoreCase)))
            {
                var model = MefModel.Read(entry.Data);
                if (model.Format != MefMeshFormat.Skinned) continue;
                string path = Path.Combine(root, "Meshes", Convert.ToHexString(SHA256.HashData(entry.Data.Span)) + ".igimesh");
                var data = JsonNode.Parse(File.ReadAllText(path))!;
                string family = Path.GetFileName(entry.Name)[..3];
                int rig = rigByFamily.GetValueOrDefault(family, 1);
                string poseName = $"Standing animation 2, rig {rig}, first frame; static reference";
                if (data["referencePose"]?.ToString() == poseName) continue;
                var pose = poses.GetValueOrDefault(rig, poses[0]);
                if (model.HighestSkinBone >= pose.BoneCount) pose = poses.Values.First(p => p.BoneCount > model.HighestSkinBone);
                var vertices = model.Vertices.ToArray(); var bindings = model.SkinInfluences.ToArray();
                var positions = new Vector3f[model.BaseVertexCount]; var normals = new Vector3f[model.BaseVertexCount];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var binding = bindings[i]; int target = i < model.BaseVertexCount ? i : binding.TargetVertex;
                    if (target < 0 || target >= positions.Length) continue;
                    Vector3f p = Rotate(pose.Rotations[binding.BoneIndex], vertices[i].Position);
                    var t = pose.Positions[binding.BoneIndex]; var prev = positions[target];
                    positions[target] = new Vector3f(prev.X + (p.X + t.X) * binding.Weight / 4096, prev.Y + (p.Y + t.Y) * binding.Weight / 4096, prev.Z + (p.Z + t.Z) * binding.Weight / 4096);
                    if (i < model.BaseVertexCount) normals[i] = Rotate(pose.Rotations[binding.BoneIndex], vertices[i].Normal);
                }
                data["version"] = 1; data["referencePose"] = poseName;
                data["positions"] = JsonSerializer.SerializeToNode(positions.SelectMany(p => new[] { p.X, p.Z, p.Y }).ToArray());
                data["normals"] = JsonSerializer.SerializeToNode(normals.SelectMany(p => new[] { p.X, p.Z, p.Y }).ToArray());
                data["uv"] = JsonSerializer.SerializeToNode(vertices.Take(model.BaseVertexCount).SelectMany(v => new[] { v.DiffuseU, 1 - v.DiffuseV }).ToArray());
                File.WriteAllText(path, data.ToJsonString(options)); posed++;
            }
        }
        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "Story/english.json")))!;
        DefinitionExport.Run(root, strings);
        var characters = new[] { ("Jones", "000_01_1"), ("Anya", "015_01_1"), ("Ekk", "012_01_1"), ("Jach Priboi", "009_01_1"), ("Harrison", "021_01_1"),
            ("Spetsnaz", "018_01_1"), ("Security", "019_01_1"), ("Sniper", "001_01_1"), ("Gunner", "011_02_1"), ("RPG Soldier", "013_01_1") };
        Directory.CreateDirectory(Path.Combine(root, "Characters"));
        foreach (var (character, modelName) in characters)
        {
            if (!byName.TryGetValue(modelName, out var found)) continue;
            var data = found.data.DeepClone(); data["instance"] = character;
            File.WriteAllText(Path.Combine(root, "Characters", character + ".igimodel"), data.ToJsonString(options));
        }
        string reportPath = Path.Combine(root, "import-report.json");
        if (File.Exists(reportPath))
        {
            var report = JsonNode.Parse(File.ReadAllText(reportPath))!;
            report["failures"] = new JsonArray(report["failures"]!.AsArray().Where(f => !f!.ToString().StartsWith("common/common.mtp: missing model ")).Select(f => f!.DeepClone()).ToArray());
            report["unavailableGlobalPrototypes"] = JsonSerializer.SerializeToNode(unavailable);
            report["modelInstances"] = Directory.GetFiles(Path.Combine(root, "Models"), "*.igimodel", SearchOption.AllDirectories).Length;
            report["note"] = "All available model resources extracted. The global prototype table also names resources absent from this installation; see unavailableGlobalPrototypes. These are preserved as table declarations in SourceData~.";
            File.WriteAllText(reportPath, report.ToJsonString(options));
        }
        File.WriteAllText(Path.Combine(root, "finalize-report.json"), JsonSerializer.Serialize(new { globalPrototypeAliases = aliases,
            posedMeshes = models.Where(m => m.data["skinned"]!.GetValue<bool>()).Select(m => m.data["mesh"]!.ToString()).Distinct().Count(), posedThisRun = posed, unavailableGlobalPrototypes = unavailable,
            limitations = "Terrain and QVM scripts preserved, not executed. Characters imported as static standing references; animations preserved in source format." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Resolved {aliases} global model aliases, posed {posed} character meshes; {unavailable.Count} unavailable global prototypes.");
        var missing = new List<string>(); int modelCount = 0, texturedSlots = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Models"), "*.igimodel", SearchOption.AllDirectories))
        {
            var data = JsonNode.Parse(File.ReadAllText(file))!; modelCount++;
            if (!File.Exists(Path.Combine(project, data["mesh"]!.ToString()))) missing.Add("Mesh: " + file);
            foreach (var texture in data["textures"]!.AsArray())
            {
                if (texture!.ToString().Length == 0) continue;
                texturedSlots++;
                if (!File.Exists(Path.Combine(project, texture.ToString()))) missing.Add("Texture: " + texture);
            }
        }
        File.WriteAllText(Path.Combine(root, "validation.json"), JsonSerializer.Serialize(new { passed = missing.Count == 0, modelCount, texturedSlots, missing = missing.Distinct().ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Validated {modelCount} model references and {texturedSlots} texture slots; {missing.Count} missing references.");
        if (missing.Count > 0) throw new InvalidDataException("Missing model/texture references; see validation.json");
    }
    private static Vector3f Rotate(Matrix3f m, Vector3f v) => new(m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z, m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z, m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z);
}
