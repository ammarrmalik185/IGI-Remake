using System.Text.Json;

internal static class DefinitionExport
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static void Run(string root, Dictionary<string, string> strings)
    {
        string Model(string folder, string name)
        {
            foreach (string candidate in new[] { $"Models/{folder}/{name}.igimodel", $"Models/missions/location0/common/{name}.igimodel", $"Models/common/{name}.igimodel" })
                if (File.Exists(Path.Combine(root, candidate))) return "Assets/IGIImported/" + candidate;
            string? fallback = name.Length > 0 ? Directory.EnumerateFiles(Path.Combine(root, "Models"), name + ".igimodel", SearchOption.AllDirectories).Order().FirstOrDefault() : null;
            return fallback == null ? "" : "Assets/IGIImported/" + Path.GetRelativePath(root, fallback).Replace('\\', '/');
        }
        string Text(string key) => strings.GetValueOrDefault(key.Trim('<', '>'), key);
        void Write(string path, object data) { path = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(data, Options)); }
        foreach (int number in Enumerable.Range(1, 14))
        {
            string folder = $"missions/location0/level{number}";
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Definitions", folder, "objects.qvm.json")));
            if (!document.RootElement.GetProperty("complete").GetBoolean()) throw new InvalidDataException("Incomplete mission capture: " + folder);
            var schemas = new Dictionary<string, List<(string name, string type)>>();
            var placements = new List<Placement>(); var objectives = new List<string>(); var modelPaths = new List<string>();
            foreach (var call in document.RootElement.GetProperty("calls").EnumerateArray())
            {
                var args = call.GetProperty("args").EnumerateArray().ToArray();
                if (call.GetProperty("function").GetString() == "Task_DeclareParameters")
                {
                    var fields = new List<(string, string)>();
                    for (int j = 1; j + 1 < args.Length; j += 2) fields.Add((args[j].ToString(), args[j + 1].ToString()));
                    schemas[args[0].ToString()] = fields; continue;
                }
                if (args.Length < 3 || !schemas.TryGetValue(args[1].ToString(), out var schema)) continue;
                var p = new Placement { id = args[0].GetInt32(), type = args[1].ToString(), name = args[2].ToString() };
                int cursor = 3; bool hasPosition = false;
                foreach (var (name, type) in schema)
                {
                    int width = type is "ObjectPos" or "Real32x3" or "Real64x3" or "Real32x9" or "Quat32" or "RGB" ? 3 : 1;
                    if (cursor + width > args.Length) break;
                    float Num(int offset) => args[cursor + offset].ValueKind == JsonValueKind.Number ? args[cursor + offset].GetSingle() : 0;
                    if (name == "Position" && width == 3) { p.position = new Vec(Num(0) / 4096, Num(2) / 4096, Num(1) / 4096); hasPosition = true; }
                    if (name == "Orientation") p.rotation = new Vec(-Num(0) * 180 / MathF.PI, -Num(2) * 180 / MathF.PI, -Num(1) * 180 / MathF.PI);
                    if (name == "Gamma") p.rotation = new Vec(0, -Num(0) * 180 / MathF.PI, 0);
                    if (name == "Model") p.model = args[cursor].ToString();
                    if (name.Contains("Text Resource") && args[cursor].ToString().Length > 0) objectives.Add(Text(args[cursor].ToString()));
                    cursor += width;
                    // Variable-size task tails cannot be decoded with the fixed-field reader.
                    if (type is "AnimData" or "List" or "TaskList") break;
                }
                if (hasPosition)
                {
                    placements.Add(p); modelPaths.Add(Model(folder, p.model));
                }
            }
            Vec origin = placements.FirstOrDefault(p => p.type == "HumanPlayer")?.position ?? new Vec();
            foreach (var p in placements) p.position = new Vec(p.position.x - origin.x, p.position.y - origin.y, p.position.z - origin.z);
            Write($"Missions/{number:00}.igimission", new { number, title = Text($"Mission {number} Name"), briefing = Text($"Mission {number} Desc"), source = folder,
                objectives = objectives.Distinct().ToArray(), placements, modelPaths, origin });
        }
        var weapons = new List<object>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "Definitions/weapons"), "weapon.qvm.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (!doc.RootElement.GetProperty("complete").GetBoolean()) throw new InvalidDataException("Incomplete weapon capture: " + file);
            var call = doc.RootElement.GetProperty("calls").EnumerateArray().First(c => c.GetProperty("function").GetString() == "DefineWeaponType");
            var a = call.GetProperty("args").EnumerateArray().ToArray();
            string name = a[2].ToString(), gun = a[29].ToString();
            float F(int i) => a[i].ValueKind == JsonValueKind.Number ? a[i].GetSingle() : 0;
            string Audio(string sound) => Directory.EnumerateFiles(Path.Combine(root, "Audio"), sound + ".wav", SearchOption.AllDirectories).Select(p => "Assets/IGIImported/" + Path.GetRelativePath(root, p).Replace('\\', '/')).FirstOrDefault() ?? "";
            var weapon = new { id = (int)F(0), displayName = name, description = Text(a[4].ToString()), category = (int)F(5), model = gun,
                modelPath = Model("missions/location0/common", gun), magazineSize = (int)F(16), roundsPerMinute = F(15), reloadSeconds = F(12),
                automatic = F(17) > 1, range = F(25), modelLength = F(27) / 1000, damageFactor = F(10), calibre = (int)F(9), spreadDegrees = F(18),
                recoilDegrees = Math.Abs(F(20)), fireSoundPath = Audio(a[44].ToString()), source = Path.GetRelativePath(root, file).Replace('\\', '/') };
            weapons.Add(weapon); Write("Weapons/" + Path.GetFileName(Path.GetDirectoryName(file)) + ".igiweapon", weapon);
        }
        Write("weapons.json", new { weapons });
        Console.WriteLine($"Exported 14 mission definitions and {weapons.Count} weapon definitions.");
    }
    internal record Vec(float x = 0, float y = 0, float z = 0);
    internal sealed class Placement
    {
        public int id { get; set; }
        public string type { get; set; } = "";
        public string name { get; set; } = "";
        public string model { get; set; } = "";
        public Vec position { get; set; } = new();
        public Vec rotation { get; set; } = new();
    }
}
