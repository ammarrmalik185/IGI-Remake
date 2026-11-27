using System.Text.Json.Nodes;
using OpenIGI.Core.Numerics;
using OpenIGI.Formats.Animation;
using OpenIGI.Formats.World;

internal sealed class PlayerSkinExport
{
    internal sealed class Clip(string name, float duration)
    {
        public string Name = name;
        public float Duration = duration;
        public List<BonePose> Frames = new();
    }
    public readonly List<Clip> Clips = new();
    public void Write(string project, string output, string modelPath, int bones)
    {
        var binding = JsonNode.Parse(File.ReadAllText(Path.Combine(project, modelPath)))!;
        var mesh = JsonNode.Parse(File.ReadAllText(Path.Combine(project, binding["mesh"]!.ToString())))!;
        string source = mesh["source"]!.ToString(); int colon = source.IndexOf(':');
        string entry = source[(colon + 1)..]; if (entry.Contains(':')) entry = entry[(entry.IndexOf(':') + 1)..];
        var model = MefModel.Read(File.ReadAllBytes(Path.Combine(project, "Assets/IGIImported/SourceData~/Unpacked", source[..(colon - 4)], entry)));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var w = new BinaryWriter(File.Create(output));
        w.Write(1); w.Write(modelPath); w.Write(bones); w.Write(model.Vertices.Length);
        var vertices = model.Vertices.ToArray(); var weights = model.SkinInfluences.ToArray();
        void V(Vector3f v, float scale = 1) { w.Write(v.X * scale); w.Write(v.Z * scale); w.Write(v.Y * scale); }
        for (int i = 0; i < vertices.Length; i++)
        {
            var weight = weights[i];
            w.Write(i < model.BaseVertexCount ? i : weight.TargetVertex);
            w.Write((int)weight.BoneIndex); w.Write(weight.Weight);
            V(vertices[i].Position, 1f / 4096); V(vertices[i].Normal);
        }
        w.Write(Clips.Count);
        foreach (var clip in Clips)
        {
            w.Write(clip.Name); w.Write(clip.Duration); w.Write(clip.Frames.Count);
            foreach (var pose in clip.Frames)
                for (int i = 0; i < bones; i++)
                {
                    V(pose.Positions[i], 1f / 4096);
                    var r = pose.Rotations[i];
                    var matrix = new System.Numerics.Matrix4x4(r.M11,r.M13,r.M12,0,r.M31,r.M33,r.M32,0,r.M21,r.M23,r.M22,0,0,0,0,1);
                    var q = System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromRotationMatrix(System.Numerics.Matrix4x4.Transpose(matrix)));
                    w.Write(q.X); w.Write(q.Y); w.Write(q.Z); w.Write(q.W);
                }
        }
    }
    public void Add(BoneAnimationSet set, string name, int baseId, int upperId, bool loop)
    {
        if (!set.TryGet(baseId, out var animation)) throw new Exception("Missing body animation " + baseId);
        var bone = new BoneObject(set);
        bone.Play(0, 2, false); float restZ = bone.SampleRootMotion().Z;
        var lower = bone.Play(0, baseId, false)!;
        float duration = animation.Duration / 4800;
        if (!loop && upperId >= 0 && set.TryGet(upperId, out var upperAnimation)) duration = upperAnimation.Duration / 4800;
        int count = Math.Max(2, (int)MathF.Ceiling(duration * 30) + 1);
        lower.Rate = duration * 4800 / (count - 1);
        if (upperId >= 0 && set.TryGet(upperId, out _)) bone.Play(1, upperId, loop)!.Rate = lower.Rate;
        var clip = new Clip(name, duration);
        for (int frame = 0; frame < count; frame++)
        {
            var root = bone.SampleRootMotion();
            clip.Frames.Add(bone.Pose(rootPosition: new Vector3f(0, 0, root.Z - restZ)));
            bone.Tick();
        }
        if (loop) clip.Frames[^1] = clip.Frames[0];
        Clips.Add(clip);
    }
}
