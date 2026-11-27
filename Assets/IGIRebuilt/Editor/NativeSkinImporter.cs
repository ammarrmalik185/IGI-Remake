using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [ScriptedImporter(1, "igiskin", 250)]
    public sealed class NativeSkinImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            using var reader = new BinaryReader(File.OpenRead(ctx.assetPath));
            if (reader.ReadInt32() != 1) throw new InvalidDataException("Unknown native skin version");
            string model = reader.ReadString(); ctx.DependsOnArtifact(model);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(model);
            var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
            root.AddComponent<MeshFilter>().sharedMesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = prefab.GetComponent<MeshRenderer>().sharedMaterials;
            var data = ScriptableObject.CreateInstance<NativeSkinData>();
            data.boneCount = reader.ReadInt32();
            data.weights = new NativeSkinWeight[reader.ReadInt32()];
            Vector3 V() => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < data.weights.Length; i++)
                data.weights[i] = new NativeSkinWeight { vertex = reader.ReadInt32(), bone = reader.ReadInt32(), weight = reader.ReadSingle(), position = V(), normal = V() };
            data.clips = new NativeSkinClip[reader.ReadInt32()];
            for (int c = 0; c < data.clips.Length; c++)
            {
                var clip = new NativeSkinClip { name = reader.ReadString(), duration = reader.ReadSingle(), frames = reader.ReadInt32() };
                int count = clip.frames * data.boneCount;
                clip.positions = new Vector3[count]; clip.rotations = new Quaternion[count];
                for (int i = 0; i < count; i++)
                {
                    clip.positions[i] = V();
                    clip.rotations[i] = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
                data.clips[c] = clip;
            }
            root.AddComponent<NativeSkinRenderer>().data = data;
            ctx.AddObjectToAsset("motion", data); ctx.AddObjectToAsset("player", root); ctx.SetMainObject(root);
        }
    }
}
