using UnityEditor;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    public sealed class AssetWorkbench : EditorWindow
    {
        private GameConfiguration configuration;
        private UnityEditor.Editor inspector;
        private Object selected;
        private Vector2 scroll;
        [MenuItem("Tools/IGI Rebuilt/Asset workbench")]
        public static void Open() { GetWindow<AssetWorkbench>("IGI Assets"); }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("ASSETS WITHOUT GAMEPLAY COUPLING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Import a model under Assets/Art, make it a prefab, and assign it below. Geometry, materials and animation remain ordinary Unity assets.", MessageType.Info);
            if (configuration == null) configuration = AssetDatabase.LoadAssetAtPath<GameConfiguration>(ProjectSetup.ConfigurationPath);
            configuration = (GameConfiguration)EditorGUILayout.ObjectField("Game configuration", configuration, typeof(GameConfiguration), false);
            if (configuration == null)
            {
                if (GUILayout.Button("Create demo configuration")) ProjectSetup.EnsureDemo();
                return;
            }
            if (GUILayout.Button("Environment, enemy and mission settings")) Select(configuration);
            if (configuration.weapons != null)
                foreach (WeaponDefinition weapon in configuration.weapons)
                    if (weapon != null && GUILayout.Button("Edit " + weapon.displayName)) Select(weapon);
            EditorGUILayout.Space();
            if (GUILayout.Button("Open playable mission")) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            if (selected == null) Select(configuration);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (inspector != null) inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
        private void Select(Object target)
        {
            selected = target;
            if (inspector != null) DestroyImmediate(inspector);
            inspector = UnityEditor.Editor.CreateEditor(target);
        }
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    }
}
