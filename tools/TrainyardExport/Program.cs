using System.Text.Json;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Vfs;
using OpenIGI.Engine.Tasks;
using OpenIGI.Formats.World;
using OpenIGI.Game.World;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Scripting.Runtime;

string project = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
if (args.Contains("--models")) { ModelGeometryExport.Run(project); return; }
if (args.Contains("--repair-poses")) { ModelPoseRepair.Run(project); return; }
if (args.Contains("--ladders")) { LadderExport.Run(project); return; }
string source = Path.GetFullPath(Path.Combine(project, "../IGIEnhanced/base_igi/pc"));
string output = Path.Combine(project, "Assets/IGIRebuilt/Trainyard");
Directory.CreateDirectory(output);
var resources = new ResourceRegistry(new VirtualFileSystem());
foreach (string file in Directory.GetFiles(Path.Combine(source, "missions/location0/level1/terrain")))
    resources.Register("LOCAL:" + Path.GetRelativePath(source, file).Replace('\\', '/'), File.ReadAllBytes(file));
var natives = new NativeRegistry(); natives.RegisterConstant("TRUE", 1); natives.RegisterConstant("FALSE", 0);
var terrain = new TerrainLoader(resources, natives, new ExportLogger()).Load("LOCAL:missions/location0/level1/terrain/terrain") ?? throw new Exception("No terrain");
if (terrain.Materials.Count == 0 || terrain.TileMaps.Count == 0) throw new Exception("Terrain material script did not load.");
if (!terrain.IsUsable) throw new Exception(string.Join("; ", terrain.Defects));
var types = new TaskTypeRegistry(); var tree = new TaskTree(types);
using var definitions = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "Assets/IGIImported/Definitions/missions/location0/level1/objects.qvm.json")));
var schemas = new Dictionary<string, TaskParameter[]>();
var relevant = new HashSet<string> { "HeightMap", "TextureModifier", "DiscardTerrain", "TerrainLightMap" };
foreach (var call in definitions.RootElement.GetProperty("calls").EnumerateArray())
{
    var a = call.GetProperty("args").EnumerateArray().ToArray();
    if (call.GetProperty("function").GetString() == "Task_DeclareParameters")
    {
        string name = a[0].ToString(); if (!relevant.Contains(name)) continue;
        var parameters = new List<TaskParameter>();
        for (int j = 1; j + 1 < a.Length; j += 2) parameters.Add(new TaskParameter(a[j].ToString(), Enum.Parse<ScriptParamType>(a[j + 1].ToString(), true)));
        schemas[name] = parameters.ToArray(); types.Declare(name, schemas[name]); continue;
    }
    if (a.Length < 3 || !schemas.TryGetValue(a[1].ToString(), out var schema)) continue;
    var values = new Dictionary<string, object?>(); int cursor = 3;
    foreach (var field in schema)
    {
        double N(int offset) => a[cursor + offset].GetDouble();
        values[field.Name] = field.ArgumentCount == 3
            ? field.Type is ScriptParamType.ObjectPos or ScriptParamType.Real64x3 ? (object)new Vector3d(N(0), N(1), N(2)) : new Vector3f((float)N(0), (float)N(1), (float)N(2))
            : field.Type is ScriptParamType.Real32 or ScriptParamType.EnumReal32 ? (object)(float)N(0) : a[cursor].ValueKind == JsonValueKind.Number ? (object)a[cursor].GetInt32() : a[cursor].ToString();
        cursor += field.ArgumentCount;
    }
    tree.New(tree.Root, types.Get(a[1].ToString()), values);
}


var surface = TerrainSurface.Build(terrain, tree.Root);
using var mission = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "Assets/IGIImported/Missions/01.igimission")));
var orig = mission.RootElement.GetProperty("origin");
var origin = new Vector3d(orig.GetProperty("x").GetDouble() * 4096, orig.GetProperty("z").GetDouble() * 4096, orig.GetProperty("y").GetDouble() * 4096);
var chunks = new List<object>(); int triangles = 0;
Walk(TerrainOctree.RootIndex, 0, 0, new Vector3d());
File.WriteAllText(Path.Combine(output, "Trainyard.igiground"), JsonSerializer.Serialize(new { chunks }));
File.WriteAllText(Path.Combine(output, "terrain-report.json"), JsonSerializer.Serialize(new { chunks = chunks.Count, triangles, heightBindings = surface.Heights.Entries.Count, textureModifiers = surface.Layers.Length, discardRegions = surface.Discards.Length, origin = new[] { origin.X, origin.Y, origin.Z } }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Exported {chunks.Count} original terrain chunks, {triangles} triangles, {surface.Heights.Entries.Count} height bindings, {surface.Layers.Length} texture modifiers.");

void Walk(int index, int orientation, int level, Vector3d centre)
{
    if (level >= 16) return;
    var node = terrain.Octree[index]; if (node.IsLeaf) return;
    int childLevel = level + 1, extent = TerrainOctree.RootExtent >> childLevel;
    for (int octant = 0; octant < 8; octant++)
    {
        int slot = TerrainOrientation.ChildSlot(orientation, octant);
        if (!node.HasChild(slot) || node.Child(slot) >= terrain.Octree.Count) continue;
        var p = new Vector3d(centre.X + ((octant & 1) != 0 ? extent : -extent), centre.Y + ((octant & 2) != 0 ? extent : -extent), centre.Z + ((octant & 4) != 0 ? extent : -extent));
        double dx = Math.Max(0, Math.Abs(p.X - origin.X) - extent) / 4096, dy = Math.Max(0, Math.Abs(p.Y - origin.Y) - extent) / 4096;
        if (dx > 1600 || dy > 1600 || Math.Abs(p.Z - origin.Z) - extent > 400 * 4096) continue;
        if (surface.Discards.Any(d => d.ContainsCube(p, extent))) continue;
        int child = node.Child(slot), facing = TerrainOrientation.Compose(orientation, node.Orientation(slot) & 7);
        bool refineDiscard = surface.Discards.Any(d => childLevel < d.Level && d.IntersectsCube(p, extent));
        int targetLevel = Math.Max(dx, dy) < 500 ? 15 : 12;
        if (childLevel < targetLevel || refineDiscard) { Walk(child, facing, childLevel, p); continue; }
        var chunk = new TerrainChunk(p, childLevel, facing, terrain.Octree[child].CommandOffset);
        if (!terrain.TryGetBlock(chunk, out var block)) continue;
        var geometry = TerrainChunkMesh.Build(chunk, block, surface, 1f / 4096, surface.Heights.Resolve(chunk));
        if (geometry == null) continue;
        var positions = geometry.Vertices.SelectMany(v => new[] { v.Position.X, v.Position.Z, v.Position.Y }).ToArray();
        var layers = geometry.Layers.Where(l => !l.IsDetail).Select(l => {
            var indices = geometry.Indices.Skip(l.IndexOffset).Take(l.IndexCount).Select(i => (int)i).ToArray();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i] * 3, b = indices[i + 1] * 3, c = indices[i + 2] * 3;
                float crossY = (positions[b + 2] - positions[a + 2]) * (positions[c] - positions[a]) - (positions[b] - positions[a]) * (positions[c + 2] - positions[a + 2]);
                if (crossY < 0) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            }
            triangles += indices.Length / 3;
            return new { tile = l.TileIndex, overlay = !l.IsBase, indices };
        }).ToArray();
        chunks.Add(new { position = new { x = (p.X - origin.X) / 4096, y = (p.Z - origin.Z) / 4096, z = (p.Y - origin.Y) / 4096 }, positions,
            uv = geometry.Vertices.SelectMany(v => new[] { v.DiffuseU, 1 - v.DiffuseV }).ToArray(),
            alpha = geometry.Vertices.Select(v => v.Colour.A / 255f).ToArray(), layers });
    }
}

sealed class ExportLogger : OpenIGI.Core.Diagnostics.IOpenIgiLogger
{
    public bool IsEnabled(OpenIGI.Core.Diagnostics.LogLevel level, string category) => true;
    public void Log(in OpenIGI.Core.Diagnostics.LogRecord record) => Console.WriteLine(record.ToString());
}
