using UnityEngine;

namespace IGIRebuilt
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public GameConfiguration configuration;
        public MissionDirector Mission { get; private set; }
        private void Start()
        {
            if (configuration == null) configuration = Resources.Load<GameConfiguration>("GameConfiguration");
            if (configuration == null) configuration = RuntimeDefaults();
            Application.targetFrameRate = Mathf.Clamp(configuration.targetFrameRate, 30, 240);
            QualitySettings.vSyncCount = 0; // Use the configured frame cap rather than the display's refresh rate.
            SetLighting();
            MissionLayout layout = FindFirstObjectByType<MissionLayout>();
            if (layout == null && configuration.environmentPrefab != null)
            {
                var environment = Instantiate(configuration.environmentPrefab);
                layout = environment.GetComponentInChildren<MissionLayout>();
                if (layout == null || layout.playerSpawn == null || layout.terminal == null || layout.extraction == null)
                {
                    Debug.LogError("The environment prefab needs MissionLayout with Player Spawn, Terminal and Extraction markers. Using the prototype compound.");
                    Destroy(environment);
                    layout = null;
                }
            }
            if (layout == null || layout.playerSpawn == null || layout.terminal == null || layout.extraction == null) layout = DemoWorld.Create(transform);
            Mission = new GameObject("Mission controller").AddComponent<MissionDirector>();
#if UNITY_EDITOR
            Mission.gameObject.AddComponent<PlayModePerformanceMonitor>().mission = Mission;
#endif
            Mission.configuration = configuration;
            Mission.terminal = layout.terminal;
            Mission.extraction = layout.extraction;
            var player = new GameObject("Player - gameplay independent of visual assets");
            player.layer = DemoWorld.PlayerLayer;
            player.transform.SetPositionAndRotation(layout.playerSpawn.position, layout.playerSpawn.rotation);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.85f;
            controller.center = Vector3.up * 0.925f;
            controller.radius = 0.32f;
            controller.stepOffset = 0.32f;
            controller.slopeLimit = 48;
            var health = player.AddComponent<Health>();
            health.team = Team.Player;
            var eyeObject = new GameObject("Player camera");
            eyeObject.tag = "MainCamera";
            eyeObject.transform.SetParent(player.transform, false);
            eyeObject.transform.localPosition = Vector3.up * 1.73f;
            var camera = eyeObject.AddComponent<Camera>();
            camera.fieldOfView = 78;
            camera.nearClipPlane = 0.025f;
            camera.farClipPlane = layout.viewDistance;
            if (configuration.captureTruck) { RenderSettings.fogStartDistance = 550; RenderSettings.fogEndDistance = layout.viewDistance; }
            camera.backgroundColor = new Color(0.55f, 0.64f, 0.68f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            eyeObject.AddComponent<AudioListener>();
            var motor = player.AddComponent<FirstPersonMotor>();
            motor.eye = camera;
            motor.mission = Mission;
            motor.minimumHeight = layout.minimumPlayerHeight;
            var mount = new GameObject("Weapon mount - replace visuals using WeaponDefinition").transform;
            mount.SetParent(camera.transform, false);
            var weapon = player.AddComponent<WeaponController>();
            weapon.eye = camera;
            weapon.modelMount = mount;
            weapon.mission = Mission;
            weapon.Configure(configuration.weapons);
            Mission.Initialize(motor, weapon);
            Mission.Security = FindFirstObjectByType<SecurityNetwork>();
            if (Mission.Security != null) Mission.Security.Initialize(Mission);
            var navigation = new GameObject("Navigation grid").AddComponent<GridNavigation>();
            navigation.worldCentre = layout.navigationCentre;
            navigation.cellSize = 2;
            navigation.width = Mathf.CeilToInt(layout.navigationSize.x / navigation.cellSize);
            navigation.depth = Mathf.CeilToInt(layout.navigationSize.y / navigation.cellSize);
            navigation.sampleTerrain = configuration.captureTruck;
            Physics.SyncTransforms();
            navigation.Rebuild();
            int count = layout.guardSpawns != null ? Mathf.Min(configuration.enemyCount, layout.guardSpawns.Length) : 0;
            for (int i = 0; i < count; i++) SpawnGuard(layout.guardSpawns[i], motor, navigation, i);
            var hud = new GameObject("Mission HUD").AddComponent<MissionHud>();
            hud.mission = Mission;
            Debug.Log("IGI_REBUILT_READY: player, " + configuration.weapons.Length + " weapons, " + count + " guards, terminal and extraction initialized.");
        }
        private void SpawnGuard(Transform marker, FirstPersonMotor player, GridNavigation navigation, int index)
        {
            var go = new GameObject("Guard " + (index + 1));
            go.layer = DemoWorld.GuardLayer;
            if (marker.GetComponent<GuardSpawnVisual>() != null) go.name += " [" + marker.GetComponent<GuardSpawnVisual>().sourceId + "]";
            go.transform.SetPositionAndRotation(marker.position, configuration.captureTruck ? marker.rotation : Quaternion.Euler(0, index % 2 == 0 ? 180 : 0, 0));
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            controller.radius = 0.32f;
            controller.stepOffset = .4f; controller.skinWidth = .05f;
            var health = go.AddComponent<Health>();
            health.team = Team.Guard;
            health.ResetHealth(configuration.enemyHealth);
            GameObject visual;
            var spawnVisual = marker.GetComponent<GuardSpawnVisual>();
            var prefab = spawnVisual != null && spawnVisual.visualPrefab != null ? spawnVisual.visualPrefab : configuration.enemyVisualPrefab;
            if (prefab != null)
                visual = VisualFactory.Attach(prefab, go.transform, Vector3.up * 0.9f, configuration.enemyVisualRotation, configuration.enemyHeight, true);
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Guard placeholder - assign any character prefab in GameConfiguration";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = Vector3.up * 0.9f;
                visual.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
                visual.GetComponent<Collider>().enabled = false;
                Destroy(visual.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.28f, 0.19f) };
                visual.GetComponent<Renderer>().sharedMaterial = mat;
                var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visor.name = "Visor";
                visor.transform.SetParent(visual.transform, false);
                visor.transform.localPosition = new Vector3(0, 0.63f, 0.38f);
                visor.transform.localScale = new Vector3(0.68f, 0.18f, 0.12f);
                visor.GetComponent<Collider>().enabled = false;
                Destroy(visor.GetComponent<Collider>());
                visor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.08f, 0.16f, 0.18f) };
            }
            var guard = go.AddComponent<EnemyGuard>();
            guard.visual = visual.transform;
            guard.target = player.transform;
            guard.mission = Mission;
            guard.navigation = navigation;
            guard.authoredNavigation = FindFirstObjectByType<AuthoredNavigation>();
            if (spawnVisual != null) { guard.graphId = spawnVisual.graphId; guard.authoredPatrol = spawnVisual.patrol; guard.authoredAlarm = spawnVisual.alarm; }
            guard.sightRange = configuration.enemySightRange;
            guard.damage = configuration.enemyDamage;
            guard.fireRange = configuration.captureTruck ? configuration.enemySightRange : 23;
            Vector3 at = marker.position;
            guard.patrolPoints = new[] { at, at + Vector3.forward * 6, at + new Vector3(4, 0, 6), at + Vector3.right * 4 };
            var presentation = go.AddComponent<GuardPresentation>();
            var modelInfo = visual.GetComponentInChildren<ImportedModelInfo>();
            guard.respondsToAlarm = modelInfo != null && modelInfo.originalModel.StartsWith("019_");
            presentation.Initialize(guard, Resources.Load<GuardPresentationSettings>("GuardPresentationSettings"), modelInfo != null ? modelInfo.originalModel : "");
            Mission.RegisterGuard(guard);
        }
        private static GameConfiguration RuntimeDefaults()
        {
            var config = ScriptableObject.CreateInstance<GameConfiguration>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.displayName = "Prototype rifle";
            config.weapons = new[] { weapon };
            return config;
        }
        private static void SetLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.53f, 0.57f, 0.61f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.64f, 0.68f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55;
            RenderSettings.fogEndDistance = 135;
            var sun = new GameObject("Morning sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1, 0.92f, 0.78f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38, -28, 0);
            QualitySettings.shadowDistance = 65;
        }
    }
}
