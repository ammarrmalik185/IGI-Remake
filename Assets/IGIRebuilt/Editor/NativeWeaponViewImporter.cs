using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [Serializable] internal sealed class WeaponViewPart { public string modelPath; public int slot; public Vector3 position; public Quaternion rotation; }
    [Serializable] internal sealed class WeaponViewDocument { public WeaponViewPart[] parts; public WeaponPoseClip idle, fire, reload; }

    [ScriptedImporter(3, "igiweaponview", 300)]
    public sealed class NativeWeaponViewImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            ctx.DependsOnSourceAsset("Assets/IGIRebuilt/Runtime/NativeWeaponView.cs");
            var data = JsonUtility.FromJson<WeaponViewDocument>(File.ReadAllText(ctx.assetPath));
            var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath) + " complete view");
            var view = root.AddComponent<NativeWeaponView>();
            view.anchors = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                view.anchors[i] = new GameObject(i == 0 ? "Body bone 3" : i == 1 ? "Magazine bone 52" : "Moving part bone 55").transform;
                view.anchors[i].SetParent(root.transform, false);
            }
            foreach (var part in data.parts)
            {
                ctx.DependsOnArtifact(part.modelPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(part.modelPath);
                if (prefab == null) throw new InvalidDataException("Missing original weapon part: " + part.modelPath);
                var instance = Instantiate(prefab, view.anchors[part.slot]);
                instance.name = prefab.name;
                instance.transform.localPosition = part.position;
                instance.transform.localRotation = part.rotation;
            }
            view.idle = data.idle; view.fire = data.fire; view.reload = data.reload; view.partCount = data.parts.Length;
            view.ResetPose();
            ctx.AddObjectToAsset("view", root); ctx.SetMainObject(root);
        }
    }
}
