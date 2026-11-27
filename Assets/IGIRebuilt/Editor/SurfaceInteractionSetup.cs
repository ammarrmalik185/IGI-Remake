using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    public static class SurfaceInteractionSetup
    {
        static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IGIImported/Audio/" + name + ".wav");
            if (clip == null) throw new Exception("Missing imported surface sound " + name);
            return clip;
        }
        [MenuItem("Tools/IGI Rebuilt/Update fence and glass interactions")]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureSettings();
            var root = PrefabUtility.LoadPrefabContents(TrainyardSetup.EnvironmentPath);
            try
            {
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>())
                    if (collider.sharedMesh != null && collider.sharedMesh.triangles.Length == 0) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var surface in root.GetComponentsInChildren<BulletSurface>())
                    if (surface.kind == BulletSurfaceKind.Glass) surface.gameObject.isStatic = false;
                PrefabUtility.SaveAsPrefabAsset(root, TrainyardSetup.EnvironmentPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("SURFACE_INTERACTIONS_READY: native fence and glass materials, two fence impact clips, three glass break clips");
        }
        internal static void EnsureSettings()
        {
            const string path = "Assets/IGIRebuilt/Resources/SurfaceInteractionSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<SurfaceInteractionSettings>(path);
            if (settings == null) { settings = ScriptableObject.CreateInstance<SurfaceInteractionSettings>(); AssetDatabase.CreateAsset(settings, path); }
            settings.fenceImpacts = Enumerable.Range(1, 2).Select(i => Clip("bul_metal_fnc_" + i)).ToArray();
            settings.glassBreaks = Enumerable.Range(1, 3).Select(i => Clip("glass_crush_" + i)).ToArray();
            const string materialPath = "Assets/IGIRebuilt/Prefabs/GlassShards.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, materialPath); }
            settings.shardMaterial = material;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
