using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string Root = "Assets/IGIRebuilt";
        public const string ScenePath = Root + "/Scenes/Trainyard.unity";
        public const string ConfigurationPath = Root + "/Resources/GameConfiguration.asset";
        static ProjectSetup() { EditorApplication.delayCall += FirstImport; }
        private static void FirstImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += FirstImport;
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameConfiguration>(ConfigurationPath) == null) EnsureDemo();
        }

        [MenuItem("Tools/IGI Rebuilt/Create missing demo assets")]
        public static void EnsureDemo()
        {
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory(Root + "/Data/Weapons");
            Directory.CreateDirectory(Root + "/Prefabs/Weapons");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
            GameConfiguration configuration = AssetDatabase.LoadAssetAtPath<GameConfiguration>(ConfigurationPath);
            if (configuration == null)
            {
                configuration = ScriptableObject.CreateInstance<GameConfiguration>();
                configuration.weapons = new[]
                {
                    Weapon("AK47", "AK-47", 30, 34, 0.1f, 0.95f, new Vector3(-90, 90, 0), true),
                    Weapon("MP5", "MP5", 30, 24, 0.075f, 0.65f, Vector3.zero, true),
                    Weapon("DesertEagle", "Desert Eagle", 7, 70, 0.3f, 0.28f, new Vector3(-90, 0, 0), false)
                };
                AssetDatabase.CreateAsset(configuration, ConfigurationPath);
            }
            if (!File.Exists(ScenePath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("IGI Rebuilt - independent FPS prototype");
                root.AddComponent<GameBootstrap>().configuration = configuration;
                root.AddComponent<SmokeTestDriver>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Personal Projects";
            PlayerSettings.productName = "IGI Rebuilt";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.vSyncCount = 1;
            AssetDatabase.SaveAssets();
            Debug.Log("IGI_REBUILT_SETUP_COMPLETE: open " + ScenePath + " and press Play.");
        }
        private static WeaponDefinition Weapon(string folder, string title, int capacity, float damage, float rate, float length, Vector3 rotation, bool automatic)
        {
            string path = Root + "/Data/Weapons/" + folder + ".asset";
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition != null) return definition;
            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.displayName = title;
            definition.magazineSize = capacity;
            definition.reserveAmmo = capacity * 5;
            definition.damage = damage;
            definition.secondsPerShot = rate;
            definition.modelLength = length;
            definition.viewRotation = rotation;
            definition.automatic = automatic;
            definition.viewPosition = folder == "DesertEagle" ? new Vector3(0.19f, -0.17f, 0.46f) : new Vector3(0.22f, -0.21f, 0.64f);
            definition.visualPrefab = WeaponPrefab(folder);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }
        private static GameObject WeaponPrefab(string folder)
        {
            string prefabPath = Root + "/Prefabs/Weapons/" + folder + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;
            string modelPath = "Assets/Art/Weapons/" + folder + "/" + folder + ".fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning("Starter model missing at " + modelPath + "; using the runtime prototype gun until a prefab is assigned.");
                return null;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = folder + " Visual";
            try
            {
                Material main = WeaponMaterial(folder, false);
                Material magazine = folder == "MP5" ? WeaponMaterial(folder, true) : main;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    Material[] assigned = renderer.sharedMaterials;
                    for (int i = 0; i < assigned.Length; i++)
                        assigned[i] = folder == "MP5" && assigned[i] != null && assigned[i].name.IndexOf("mag", StringComparison.OrdinalIgnoreCase) >= 0 ? magazine : main;
                    renderer.sharedMaterials = assigned;
                }
                return PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        private static Material WeaponMaterial(string folder, bool magazine)
        {
            string suffix = magazine ? "_Magazine" : "_Body";
            string path = Root + "/Materials/" + folder + suffix + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            string art = "Assets/Art/Weapons/" + folder + "/Textures/";
            string baseFile, normalFile = null, metallicFile = null, occlusionFile = null;
            if (folder == "AK47")
            {
                baseFile = "TEXTURE_MAP.jpg";
                occlusionFile = "AMBIENT_OCLUSION.png";
            }
            else if (folder == "MP5")
            {
                string prefix = magazine ? "Low_Maglow" : "MP5";
                baseFile = prefix + "_BaseColor.png";
                normalFile = prefix + "_Normal.png";
                metallicFile = prefix + "_Metallic.png";
            }
            else
            {
                const string prefix = "DesertEagle_Desert_Eagle";
                baseFile = prefix + "_BaseColor.png";
                normalFile = prefix + "_Normal.png";
                metallicFile = prefix + "_Metallic.png";
            }
            material = new Material(Shader.Find("Standard")) { name = folder + suffix };
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + baseFile);
            material.SetFloat("_Glossiness", 0.35f);
            if (normalFile != null)
            {
                string normalPath = art + normalFile;
                TextureImporter importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal != null) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
            }
            if (metallicFile != null)
            {
                Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(art + metallicFile);
                if (metallic != null) { material.SetTexture("_MetallicGlossMap", metallic); material.EnableKeyword("_METALLICGLOSSMAP"); }
            }
            if (occlusionFile != null) material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(art + occlusionFile));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void VerifyBatch()
        {
            EnsureDemo();
            var config = AssetDatabase.LoadAssetAtPath<GameConfiguration>(ConfigurationPath);
            if (config == null || config.weapons == null || config.weapons.Length == 0) throw new Exception("Expected at least one configured weapon.");
            foreach (WeaponDefinition weapon in config.weapons)
            {
                if (weapon.visualPrefab == null) throw new Exception("Missing starter visual for " + weapon.displayName);
                if (weapon.visualPrefab.GetComponentsInChildren<Renderer>().Length == 0) throw new Exception("No renderers for " + weapon.displayName);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new Exception("Boot scene missing.");
            Debug.Log("IGI_REBUILT_VERIFY_PASSED: Configured weapon prefabs and boot scene found.");
        }
        public static void BuildWindows()
        {
            VerifyBatch();
            Directory.CreateDirectory("Build/Windows");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Build/Windows/IGIRebuilt.exe",
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
        }
    }
}
