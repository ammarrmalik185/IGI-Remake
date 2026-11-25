using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
namespace IGIRebuilt.Editor
{
    [ScriptedImporter(3, "igianim")]
    public sealed class GuardAnimationImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            using (var r = new BinaryReader(File.OpenRead(ctx.assetPath)))
            {
                int version = r.ReadInt32();
                if (version < 1 || version > 3)
                    throw new InvalidDataException("Unknown animation version");
                var data = ScriptableObject.CreateInstance<GuardMotionData>();
                data.vertexCount = r.ReadInt32();
                data.clips = new GuardMotionClip[r.ReadInt32()];
                for (int c = 0; c < data.clips.Length; c++)
                {
                    var clip = new GuardMotionClip { name = r.ReadString(), duration = r.ReadSingle(), loop = r.ReadBoolean(), frames = r.ReadInt32() };
                    clip.metresPerSecond = version >= 2 ? r.ReadSingle() : 0;
                    int n = clip.frames * data.vertexCount;
                    clip.positions = new Vector3[n];
                    clip.normals = new Vector3[n];
                    for (int i = 0; i < n; i++)
                    {
                        clip.positions[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        clip.normals[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    }
                    if (version >= 3)
                    {
                        clip.weaponPositions = new Vector3[clip.frames]; clip.weaponRotations = new Quaternion[clip.frames];
                        for (int f = 0; f < clip.frames; f++)
                        {
                            clip.weaponPositions[f] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            clip.weaponRotations[f] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        }
                    }
                    data.clips[c] = clip;
                }
                ctx.AddObjectToAsset("animation", data);
                ctx.SetMainObject(data);
            }
        }
    }
}
