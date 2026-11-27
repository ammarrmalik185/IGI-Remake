using System.Text.Json;
using System.Text.Json.Nodes;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Resources;
using OpenIGI.Engine.Vfs;
using OpenIGI.Formats.Animation;
using OpenIGI.Formats.World;
using OpenIGI.Game.World;
using OpenIGI.Scripting.Runtime;

internal static class WeaponViewExport
{
    public static void Run(string project)
    {
        string game = Path.GetFullPath(Path.Combine(project, "../IGIEnhanced/base_igi/pc"));
        var resources = new ResourceRegistry(new VirtualFileSystem());
        resources.Register(MagicObjectRegistry.ScriptResource, File.ReadAllBytes(Path.Combine(game, "magicobj/magicobj.qvm")));
        var natives = new NativeRegistry(); natives.RegisterConstant("TRUE", 1); natives.RegisterConstant("FALSE", 0);
        var magic = new MagicObjectRegistry(); magic.Register(natives); magic.Load(resources, natives);
        var set = BoneAnimationSet.Read(File.ReadAllBytes(Path.Combine(game, "common/anims/005.iff")));
        var bodySet = BoneAnimationSet.Read(File.ReadAllBytes(Path.Combine(game, "common/anims/000.iff")));
        var bodySkin = new PlayerSkinExport();
        string output = Path.Combine(project, "Assets/IGIRebuilt/Data/WeaponViews"); Directory.CreateDirectory(output);
        string[] names = { "GLOCK", "MP5SD", "KNIFE", "COLTANACONDA", "DESERTEAGLE", "UZI", "UZIX2", "AK47", "M16A2", "SPAS12", "JACKHAMMER", "MINIMI", "DRAGUNOV", "RPG18", "GRENADE", "FLASHBANG", "PROXIMITYMINE", "BINOCULARS", "MEDIPACK" };
        string Find(string model) => "Assets/IGIImported/Models/missions/location0/common/" + model + ".igimodel";
        MefModel Read(string path)
        {
            var binding = JsonNode.Parse(File.ReadAllText(Path.Combine(project, path)))!;
            var mesh = JsonNode.Parse(File.ReadAllText(Path.Combine(project, binding["mesh"]!.ToString())))!;
            string source = mesh["source"]!.ToString(); int colon = source.IndexOf(':');
            string entry = source[(colon + 1)..]; if (entry.Contains(':')) entry = entry[(entry.IndexOf(':') + 1)..];
            return MefModel.Read(File.ReadAllBytes(Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry)));
        }
        foreach (string name in names)
        {
            var document = JsonNode.Parse(File.ReadAllText(Path.Combine(project, "Assets/IGIImported/Definitions/weapons/" + name + "/weapon.qvm.json")))!;
            var args = document["calls"]!.AsArray().First(c => c!["function"]!.ToString() == "DefineWeaponType")!["args"]!.AsArray();
            int idleId = args[31]!.GetValue<int>(), fireId = args[33]!.GetValue<int>(), reloadId = args[36]!.GetValue<int>();
            var armSkin = new PlayerSkinExport();
            foreach (var state in new[] { ("Idle", 2, 37), ("Walk", 57, 38), ("Run", 4, 41), ("Crouch", 19, 39), ("CrouchWalk", 17, 40), ("Reload", 2, 43), ("CrouchReload", 19, 43), ("Fire", 2, 42) })
                bodySkin.Add(bodySet, name + "/" + state.Item1, state.Item2, args[state.Item3]!.GetValue<int>(), state.Item1 is not ("Reload" or "CrouchReload" or "Fire"));
            var parts = new List<object>();
            void Walk(string model, int slot, Vector3f position, Matrix3f draw, Matrix3f task, int depth)
            {
                if (depth > 8) throw new Exception("Weapon attachment cycle: " + name);
                string path = Find(model); if (!File.Exists(Path.Combine(project, path))) throw new Exception("Missing weapon part " + model);
                parts.Add(new { modelPath = path, slot, position = V(position), rotation = Q(task) });
                foreach (var attachment in Read(path).Attachments)
                {
                    if (!magic.TryGet(attachment.Name, out var definition)) continue;
                    string type = magic.TaskTypeName(definition.TaskType);
                    if (type == "TASKTYPE_GUNFLAME" || string.IsNullOrEmpty(definition.Model)) continue;
                    int ownSlot = type is "TASKTYPE_GUNCLIP" or "TASKTYPE_CABARREL" or "TASKTYPE_GUNX2" ? 1 : type is "TASKTYPE_GUNMOVINGPART" or "TASKTYPE_CACLIP" ? 2 : -1;
                    Walk(definition.Model, ownSlot >= 0 ? ownSlot : slot,
                        ownSlot >= 0 ? Vector3f.Zero : position + draw.Transposed().Transform(attachment.Position),
                        ownSlot >= 0 ? Matrix3f.Identity : draw * attachment.Orientation,
                        ownSlot >= 0 ? Matrix3f.Identity : task * attachment.Orientation.Transposed(), depth + 1);
                }
            }
            Walk(args[29]!.ToString(), 0, Vector3f.Zero, Matrix3f.Identity, Matrix3f.Identity, 0);
            object Clip(int id, bool loop, string state)
            {
                if (id < 0 || !set.TryGet(id, out _)) return new { id = -1, duration = 0f, frames = Array.Empty<object>() };
                var frames = new List<object>();
                var skinClip = new PlayerSkinExport.Clip(state, 0);
                float duration = 0, releaseSeconds = 0;
                // SPAS reload is three native clips: open, insert a shell, close.
                int[] sequence = name == "SPAS12" && id == reloadId ? new[] { 61, 62, 63 } : new[] { id };
                bool thrown = name is "GRENADE" or "FLASHBANG" && id == fireId;
                if (thrown) sequence = new[] { fireId, args[34]!.GetValue<int>() };
                foreach (int clipId in sequence)
                {
                    if (!set.TryGet(clipId, out var animation)) throw new Exception("Missing native weapon clip " + clipId);
                    int count = Math.Max(2, (int)MathF.Ceiling(animation.Duration / 4800 * 30) + 1);
                    var bone = new BoneObject(set); var clip = bone.Play(0, clipId, false)!; clip.Rate = animation.Duration / (count - 1);
                    if (thrown && clipId == sequence[1])
                    {
                        var release = animation.Events.First(e => e.EventId == 3);
                        releaseSeconds = duration + release.Time / 4800;
                    }
                    duration += animation.Duration / 4800;
                    for (int frame = 0; frame < count; frame++)
                    {
                        var root = bone.SampleRootMotion(); var pose = bone.Pose();
                        if (frames.Count == 0 || frame > 0)
                        {
                            frames.Add(new { positions = new[] { V(pose.Positions[3] + root), V(pose.Positions[52] + root), V(pose.Positions[55] + root) }, rotations = new[] { Q(pose.Rotations[3]), Q(pose.Rotations[52]), Q(pose.Rotations[55]) } });
                            skinClip.Frames.Add(bone.Pose(rootPosition: root));
                        }
                        bone.Tick();
                    }
                }
                if (loop) frames[^1] = frames[0];
                if (loop) skinClip.Frames[^1] = skinClip.Frames[0];
                skinClip.Duration = duration; armSkin.Clips.Add(skinClip);
                return new { id, duration, releaseSeconds, frames };
            }
            string armsPath = "Assets/IGIRebuilt/Data/WeaponViews/" + name + ".igiskin";
            File.WriteAllText(Path.Combine(output, name + ".igiweaponview"), JsonSerializer.Serialize(new { version = 2, source = "005.iff / " + name, armsPath, parts, idle = Clip(idleId, true, "Idle"), fire = Clip(fireId, false, "Fire"), reload = Clip(reloadId, false, "Reload") }));
            armSkin.Write(project, Path.Combine(project, armsPath), Find("005_01_1"), 56);
            Console.WriteLine($"{name}: {parts.Count} original parts; idle {idleId}, fire {fireId}, reload {reloadId}");
        }
        bodySkin.Add(bodySet, "Climb", 170, -1, true);
        bodySkin.Write(project, Path.Combine(project, "Assets/IGIRebuilt/Resources/PlayerBody.igiskin"), "Assets/IGIImported/Characters/Jones.igimodel", 33);
    }
    static object V(Vector3f v) => new { x = v.X / 4096, y = v.Z / 4096, z = v.Y / 4096 };
    static object Q(Matrix3f r)
    {
        var matrix = new System.Numerics.Matrix4x4(r.M11, r.M13, r.M12, 0, r.M31, r.M33, r.M32, 0, r.M21, r.M23, r.M22, 0, 0, 0, 0, 1);
        var q = System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromRotationMatrix(System.Numerics.Matrix4x4.Transpose(matrix)));
        return new { x = q.X, y = q.Y, z = q.Z, w = q.W };
    }
}
