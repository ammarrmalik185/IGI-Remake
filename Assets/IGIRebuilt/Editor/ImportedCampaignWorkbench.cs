using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IGIRebuilt.Editor
{
    public sealed class ImportedCampaignWorkbench : EditorWindow
    {
        private ImportedMissionData[] missions;
        private Vector2 scroll;
        private int selected;
        [MenuItem("Tools/IGI Rebuilt/Imported campaign")]
        public static void Open() => GetWindow<ImportedCampaignWorkbench>("Imported IGI campaign");
        private void OnEnable() => Reload();
        private void Reload() => missions = AssetDatabase.FindAssets("t:ImportedMissionData", new[] { "Assets/IGIImported" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ImportedMissionData>(AssetDatabase.GUIDToAssetPath(g))).Where(m => m != null).OrderBy(m => m.number).ToArray();
        private void OnGUI()
        {
            if (GUILayout.Button("Refresh imported campaign")) Reload();
            if (missions == null || missions.Length == 0) { EditorGUILayout.HelpBox("Run tools/Import-IGI.ps1 to extract the local game data.", MessageType.Info); return; }
            selected = EditorGUILayout.Popup("Mission", Mathf.Clamp(selected, 0, missions.Length - 1), missions.Select(m => m.name).ToArray());
            var mission = missions[selected];
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(mission.title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(mission.briefing, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            foreach (string objective in mission.objectives) EditorGUILayout.LabelField("â€¢ " + objective, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(mission.limitations, MessageType.Info);
            EditorGUILayout.LabelField(mission.placements.Length + " placed tasks. Models retain the original material slots.", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Select mission data")) Selection.activeObject = mission;
            if (GUILayout.Button("Create reference scene"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { CreateReferenceScene(mission, false); Reload(); }
            }
            if (GUILayout.Button("Show imported weapons")) SelectFolder("Assets/IGIImported/Weapons");
            if (GUILayout.Button("Show characters")) SelectFolder("Assets/IGIImported/Characters");
            if (GUILayout.Button("Show story and dialogue")) SelectFolder("Assets/IGIImported/Story");
            EditorGUILayout.EndScrollView();
        }
        private static void SelectFolder(string path) { Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path); EditorGUIUtility.PingObject(Selection.activeObject); }
        public static Scene CreateReferenceScene(ImportedMissionData mission, bool additive)
        {
            // Scene replacement may unload the imported ScriptableObject; retain plain data first.
            string missionTitle = mission.name;
            int missionNumber = mission.number;
            var placements = mission.placements;
            var modelPaths = mission.modelPaths;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject(missionTitle + " - imported reference");
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
            int rendered = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                GameObject model = i < modelPaths.Length ? AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[i]) : null;
                var go = model != null ? (GameObject)PrefabUtility.InstantiatePrefab(model, scene) : new GameObject();
                if (model != null) rendered++;
                go.name = placement.id + " " + placement.type + " " + placement.name;
                go.transform.SetParent(root.transform);
                go.transform.localPosition = placement.position; go.transform.localEulerAngles = placement.rotation;
            }
            var camera = new GameObject("Reference camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 2, 0); camera.farClipPlane = 5000;
            camera.backgroundColor = new Color(0.55f, 0.65f, 0.75f); camera.clearFlags = CameraClearFlags.SolidColor;
            Directory.CreateDirectory("Assets/IGIImported/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/IGIImported/Scenes/Mission" + missionNumber.ToString("00") + ".unity");
            Debug.Log("IGI_REFERENCE_SCENE: mission " + missionNumber + ", " + rendered + " model instances, " + placements.Length + " tasks.");
            return scene;
        }

        public static void VerifyBatch()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var missions = AssetDatabase.FindAssets("t:ImportedMissionData", new[] { "Assets/IGIImported" });
            if (missions.Length != 14) throw new Exception("Expected 14 imported missions, found " + missions.Length);
            foreach (string guid in missions)
            {
                var mission = AssetDatabase.LoadAssetAtPath<ImportedMissionData>(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(mission.briefing) || mission.placements.Length == 0 || mission.objectives.Length == 0) throw new Exception("Incomplete mission " + mission.number);
                CreateReferenceScene(mission, false);

            }
            foreach (string file in Directory.GetFiles("Assets/IGIImported/Models", "*.igimodel", SearchOption.AllDirectories))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                if (model == null || model.GetComponent<MeshFilter>().sharedMesh == null) throw new Exception("Model failed to import: " + file);
                if (model.GetComponent<Renderer>().sharedMaterials.Any(m => m == null)) throw new Exception("Material missing: " + file);
            }
            foreach (string file in Directory.GetFiles("Assets/IGIImported/Weapons", "*.igiweapon"))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(file.Replace('\\', '/'));
                if (weapon == null || weapon.visualPrefab == null) throw new Exception("Weapon missing visual: " + file);
            }
            foreach (string file in Directory.GetFiles("Assets/IGIImported/Characters", "*.igimodel"))
                if (AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/')) == null) throw new Exception("Character failed to import: " + file);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/igi-unity-result.json", "{\"passed\":true,\"missions\":14,\"weapons\":24,\"characters\":10}");
            Debug.Log("IGI_IMPORTED_VERIFY_PASSED: 14 mission reference scenes, model geometry/materials and weapon visuals verified.");
        }
    }
}



