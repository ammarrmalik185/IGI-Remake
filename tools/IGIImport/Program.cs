using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenIGI.Formats.Ilff;
using OpenIGI.Formats.World;
using OpenIGI.Scripting.Qvm;
using OpenIGI.Scripting.Runtime;
using OpenIGI.Formats.Animation;
using OpenIGI.Core.Numerics;

if (args.Length < 2) throw new ArgumentException("Usage: IGIImport <retail pc folder> <project root> [--finalize]");
string source = Path.GetFullPath(args[0]), project = Path.GetFullPath(args[1]);
string root = Path.Combine(project, "Assets/IGIImported");
if (args.Length > 2 && args[2] == "--finalize") { FinishExport.Run(source, project); return; }
var json = new JsonSerializerOptions { WriteIndented = false };
var failures = new List<string>();
var records = new List<object>();
var models = new Dictionary<string, (string path, MefModel model)>(StringComparer.OrdinalIgnoreCase);
var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
string Rel(string path) => Path.GetRelativePath(source, path).Replace('\\', '/');
string Asset(string path) => Path.GetRelativePath(project, path).Replace('\\', '/');
void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
void Json(string path, object value) => Write(path, JsonSerializer.Serialize(value, json));
if (args.Length > 2 && args[2] == "--definitions")
{
    foreach (string file in Directory.GetFiles(source, "*.qvm", SearchOption.AllDirectories)
        .Where(f => Path.GetFileName(f) is "objects.qvm" or "mission.qvm" or "weapon.qvm" or "weaponconfig.qvm"))
        Json(Path.Combine(root, "Definitions", Rel(file) + ".json"), Capture(QvmProgram.Read(File.ReadAllBytes(file))));
    DefinitionExport.Run(root, JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "Story/english.json")))!);
    return;
}
string Safe(string name)
{
    name = name.Replace('\\', '/');
    int colon = name.IndexOf(':');
    if (colon >= 0) name = name[(colon + 1)..];
    name = name.TrimStart('/');
    if (name.Split('/').Any(p => p is ".." or ".") || name.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        throw new IOException("Unsafe resource path: " + name);
    return string.Join('/', name.Split('/').Select(part => string.Concat(part.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))));
}
// Preserve all data, including cutscenes, animation sets and terrain, outside Unity's importer.
foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Order())
{
    string ext = Path.GetExtension(file).ToLowerInvariant();
    if (ext is ".exe" or ".url") continue;
    string relative = Rel(file), dest = Path.Combine(root, "SourceData~", relative);
    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    File.Copy(file, dest, true);
    records.Add(new { source = relative, bytes = new FileInfo(file).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) });
    try
    {
        if (ext == ".qvm")
        {
            var program = QvmProgram.Read(File.ReadAllBytes(file));
            Write(Path.Combine(root, "Scripts", relative + ".txt"), string.Join('\n', QvmDisassembler.Disassemble(program)));
            if (Path.GetFileName(file) is "objects.qvm" or "mission.qvm" or "weapon.qvm" or "weaponconfig.qvm")
                Json(Path.Combine(root, "Definitions", relative + ".json"), Capture(program));
        }
        if (ext != ".res") continue;
        var archive = IresArchive.Read(File.ReadAllBytes(file));
        string scope = relative[..^4];
        foreach (var entry in archive.Entries)
        {
            string name = Safe(entry.Name);
            if (entry.Kind == IresEntryKind.ContextString)
            {
                Write(Path.Combine(root, "Story", scope, name + ".txt"), entry.AsText());
                if (relative.StartsWith("language/english/")) strings[Path.GetFileName(entry.Name)] = entry.AsText();
            }
            if (entry.Kind != IresEntryKind.Body) continue;
            WriteBytes(Path.Combine(root, "SourceData~", "Unpacked", scope, name), entry.Data.ToArray());
            if (!name.EndsWith(".mef", StringComparison.OrdinalIgnoreCase)) continue;
            string hash = Convert.ToHexString(SHA256.HashData(entry.Data.Span));
            string meshPath = Path.Combine(root, "Meshes", hash + ".igimesh");
            var model = MefModel.Read(entry.Data);
            models[scope + "/" + Path.GetFileNameWithoutExtension(name)] = (Asset(meshPath), model);
            if (File.Exists(meshPath)) continue;
            // IGI is Z-up, 4096 engine units per metre. Swap Y/Z; reverse winding for reflection.
            var vertices = model.Vertices.ToArray();
            Json(meshPath, new {
                version = 1, source = relative + ":" + entry.Name, skinned = model.Format == MefMeshFormat.Skinned,
                positions = vertices.SelectMany(v => new[] { v.Position.X / 4096f, v.Position.Z / 4096f, v.Position.Y / 4096f }).ToArray(),
                normals = vertices.SelectMany(v => new[] { v.Normal.X, v.Normal.Z, v.Normal.Y }).ToArray(),
                uv = vertices.SelectMany(v => new[] { v.DiffuseU, 1 - v.DiffuseV }).ToArray(),
                groups = model.DrawRecords.Select(d => new { slot = d.MaterialSlot, transparent = d.IsTransparent,
                    indices = model.Triangles(d).SelectMany(t => new[] { t.A, t.C, t.B }).ToArray() }).ToArray()
            });
        }
    }
    catch (Exception error) { failures.Add(relative + ": " + error.Message); }
}
int instances = 0;
foreach (string file in Directory.EnumerateFiles(source, "*.mtp", SearchOption.AllDirectories).Order())
{
    string relative = Rel(file), folder = Path.GetDirectoryName(relative)!.Replace('\\', '/');
    var pack = MtpPack.Read(File.ReadAllBytes(file));
    string packName = Path.GetFileNameWithoutExtension(file);
    foreach (var instance in pack.Instances)
    {
        if (instance.Model.IsNone || instance.Model.IsCommon) { failures.Add(relative + ": unsupported common model " + instance.Name); continue; }
        string name = pack.ModelNames[instance.Model.Index];
        string modelFolder = folder == "common" ? "missions/location0/common" : folder;
        string archiveName = folder == "common" ? "location0" : packName;
        if (!models.TryGetValue(modelFolder + "/models/" + archiveName + "/" + Path.GetFileNameWithoutExtension(name), out var mesh))
        { failures.Add(relative + ": missing model " + name); continue; }
        string[] textures = instance.MaterialIndices.ToArray().Select(index => {
            if (index < 0 || index >= pack.Materials.Count) return "";
            var id = pack.Materials[index].Texture;
            if (id.IsNone || id.IsCommon) return "";
            string texture = Path.GetFileNameWithoutExtension(pack.TextureNames[id.Index]);
            return "Assets/IGIImported/Textures/" + modelFolder + "/textures/" + texture + ".png";
        }).ToArray();
        Json(Path.Combine(root, "Models", folder, instance.Name + ".igimodel"), new {
            source = relative, instance = instance.Name, model = name, mesh = mesh.path,
            skinned = mesh.model.Format == MefMeshFormat.Skinned, textures });
        instances++;
    }
}
var missions = Enumerable.Range(1, 14).Select(i => new { number = i,
    title = strings.GetValueOrDefault($"Mission {i} Name", $"Mission {i}"),
    briefing = strings.GetValueOrDefault($"Mission {i} Desc", ""),
    definition = $"Assets/IGIImported/Definitions/missions/location0/level{i}/objects.qvm.json",
    source = $"missions/location0/level{i}" }).ToArray();
Json(Path.Combine(root, "Story", "english.json"), strings);
Json(Path.Combine(root, "campaign.json"), new { missions });
DefinitionExport.Run(root, strings);
Json(Path.Combine(root, "import-report.json"), new { source, generatedUtc = DateTime.UtcNow, sourceFiles = records.Count,
    modelResources = models.Count, modelInstances = instances, uniqueMeshes = Directory.GetFiles(Path.Combine(root, "Meshes"), "*.igimesh").Length,
    failures, files = records });
FinishExport.Run(source, project);
Console.WriteLine($"Preserved {records.Count} files; {models.Count} model resources, {instances} textured instances; {failures.Count} failures.");
foreach (string failure in failures.Take(15)) Console.WriteLine(failure);
if (failures.Any(f => !f.StartsWith("common/common.mtp: missing model "))) Environment.ExitCode = 1;

void WriteBytes(string path, byte[] bytes) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); }
object Capture(QvmProgram program)
{
    var calls = new List<object>();
    var natives = new NativeRegistry();
    natives.RegisterConstant("TRUE", 1); natives.RegisterConstant("FALSE", 0);
    natives.RegisterConstant("MISSION_NEXT_MISSION_UNDEFINED", -1);
    void Constants(string prefix, params string[] names) { for (int i = 0; i < names.Length; i++) natives.RegisterConstant(prefix + names[i], i); }
    Constants("WEAPONTYPE_", "GUN", "PISTOL", "SHOTGUN", "KNIFE", "GRENADE", "BINOCULAR", "MEDIPACK", "PROXIMITYMINE");
    Constants("SIGHTDISPLAYTYPE_", "NONE", "CROSSHAIR");
    Constants("AMMODISPLAYTYPE_", "NONE", "NORMAL", "CLIP", "BARREL", "SHELL", "GRENADE");
    natives.RegisterConstant("HUMANAI_DETECTIONEVENT_GUNSHOT_RANGE", 122880);
    natives.RegisterConstant("HUMANAI_DETECTIONEVENT_GUNSHOT_SILENCED_RANGE", 4096);
    // Task ids are runtime registrations. Preserve their names instead of inventing numeric ids.
    foreach (string symbol in program.SymbolNames.OfType<string>().Where(s => s.StartsWith("TASKTYPE_")))
        natives.RegisterVariable(symbol, new QvmStringVariable(() => symbol, _ => { }, 256));
    foreach (string name in new[] { "Task_DeclareParameters", "Task_New", "DefineMission", "DefineWeaponType", "DefineWeapon" })
        natives.RegisterFunction(name, call => {
            var values = Enumerable.Range(0, call.ArgumentCount).Select(i => {
                var v = call.GetValue(i);
                return (object)(v.Kind == QvmValueKind.Float ? v.Number : v.Kind == QvmValueKind.Integer ? (object)v.Integer : v.Text);
            }).ToArray();
            calls.Add(new { function = name, args = values });
            return QvmValue.Zero;
        });
    var vm = new QvmInterpreter(program, natives);
    var error = vm.Run();
    return new { complete = error == QvmError.None, error = vm.ErrorMessage, identifier = vm.ErrorIdentifier, calls };
}

