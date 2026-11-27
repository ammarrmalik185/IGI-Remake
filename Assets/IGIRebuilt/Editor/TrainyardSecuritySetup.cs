using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace IGIRebuilt.Editor
{
    [Serializable]
    internal sealed class CameraPart
    {
        public int id; public string holderPath, headPath; public Vector3 position, mount, lens; public float yaw, pitch, headYaw, right, left, speed, pause, range, verticalFov, horizontalFov;
    }
    [Serializable]
    internal sealed class PanelPart
    {
        public int id; public string modelPath; public Vector3 position, rotation;
    }
    [Serializable]
    internal sealed class SecurityDocument
    {
        public CameraPart[] cameras; public PanelPart[] panels;
    }
    internal static class TrainyardSecuritySetup
    {
        static AudioClip Audio(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IGIImported/Audio/" + name + ".wav");
            if (c == null)
                throw new Exception("Missing imported sound " + name);
            return c;
        }
        const string EffectPath = "Assets/IGIRebuilt/Prefabs/CameraExplosion.prefab";
        static Material EffectMaterial(string name, string texture, string shader)
        {
            string path = "Assets/IGIRebuilt/Prefabs/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/IGIImported/Textures/common/sprites/" + texture + ".png");
            if (material.mainTexture == null) throw new Exception("Missing imported effect texture " + texture);
            EditorUtility.SetDirty(material);
            return material;
        }
        static void Burst(Transform parent, string name, Material material, int count, float lifetime, float size, float speed, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.duration = .1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = tint;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .08f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .4f, 1, 1.5f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }
        static GameObject CameraExplosion()
        {
            var root = new GameObject("Camera explosion");
            try
            {
                Burst(root.transform, "Imported fireball", EffectMaterial("CameraFireball", "fireball", "Legacy Shaders/Particles/Additive"), 8, .5f, .65f, 1.4f, Color.white);
                Burst(root.transform, "Imported smoke", EffectMaterial("CameraSmoke", "smoke1", "Legacy Shaders/Particles/Alpha Blended"), 5, 1.5f, .5f, .65f, new Color(.35f, .35f, .35f, .65f));
                return PrefabUtility.SaveAsPrefabAsset(root, EffectPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void BindCameraEffects(SecurityCamera camera, GameObject effect)
        {
            camera.detectionClip = Audio("camera_beep");
            camera.destructionClip = Audio("explo_camera");
            camera.destructionEffect = effect;
        }
        [MenuItem("Tools/IGI Rebuilt/Update camera effects")]
        public static void UpdateCameraEffects()
        {
            var effect = CameraExplosion();
            var root = PrefabUtility.LoadPrefabContents(TrainyardSetup.EnvironmentPath);
            try
            {
                foreach (var camera in root.GetComponentsInChildren<SecurityCamera>()) BindCameraEffects(camera, effect);
                PrefabUtility.SaveAsPrefabAsset(root, TrainyardSetup.EnvironmentPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        static GameObject Spawn(string path, Transform parent, bool moving = false)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
                throw new Exception("Missing security asset " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            go.transform.SetParent(parent, false);
            foreach (var f in go.GetComponentsInChildren<MeshFilter>())
            {
                f.gameObject.layer = DemoWorld.WorldLayer;
                f.gameObject.isStatic = !moving;
                if (f.GetComponent<MeshCollider>() != null) continue;
                var collision = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)).OfType<Mesh>().FirstOrDefault(m => m.name.EndsWith("_collision"));
                f.gameObject.AddComponent<MeshCollider>().sharedMesh = collision ?? f.sharedMesh;
            }
            return go;
        }
        public static void Add(Dictionary<int, GameObject> placed, Transform root)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GuardPresentationSettings>("Assets/IGIRebuilt/Resources/GuardPresentationSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GuardPresentationSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/IGIRebuilt/Resources/GuardPresentationSettings.asset");
            }
            settings.motions = new[] { "001_01_1", "003_01_1", "019_01_1" }.Select(name => new GuardMotionBinding { model = name, motion = AssetDatabase.LoadAssetAtPath<GuardMotionData>("Assets/IGIRebuilt/Trainyard/Animations/" + name + ".igianim") }).ToArray();
            if (settings.motions.Any(m => m.motion == null))
                throw new Exception("Export guard animation assets first");
            settings.shot = WeaponShotAudioSetup.SingleRound("AK47", Audio("ak47_loop"), 60f / 650);
            settings.reload = Audio("ak47_reload_1");
            settings.footsteps = Enumerable.Range(1, 6).Select(i => Audio("walk_gravel_" + i)).ToArray();
            settings.hits = Enumerable.Range(1, 3).Select(i => Audio("ai_hit_0" + i)).ToArray();
            settings.deaths = Enumerable.Range(1, 4).Select(i => Audio("ai_death_0" + i)).ToArray();
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/IGIImported/Weapons/AK47.igiweapon");
            if (weapon == null)
                throw new Exception("AK47 missing");
            settings.rifle = weapon.visualPrefab;
            EditorUtility.SetDirty(settings);
            var securityRoot = new GameObject("Trainyard security network");
            securityRoot.transform.SetParent(root, false);
            var network = securityRoot.AddComponent<SecurityNetwork>();
            network.alarmClip = Audio("alarm_1");
            var data = JsonUtility.FromJson<SecurityDocument>(File.ReadAllText("Assets/IGIRebuilt/Trainyard/TrainyardSecurity.json"));
            var cameras = new List<SecurityCamera>();
            var cameraExplosion = CameraExplosion();
            foreach (var c in data.cameras)
            {
                var holder = Spawn(c.holderPath, securityRoot.transform);
                holder.name = "Security camera " + c.id;
                holder.transform.localPosition = c.position;
                holder.transform.localRotation = Quaternion.Euler(0, c.yaw, 0);
                var head = Spawn(c.headPath, holder.transform, true);
                head.transform.localPosition = c.mount;
                head.transform.localRotation = Quaternion.Euler(c.pitch, c.headYaw, 0);
                var camera = holder.AddComponent<SecurityCamera>();
                camera.network = network;
                BindCameraEffects(camera, cameraExplosion);
                camera.head = head.transform;
                camera.lens = c.lens;
                camera.left = c.left;
                camera.right = c.right;
                camera.speed = c.speed;
                camera.pause = c.pause;
                camera.range = c.range;
                camera.horizontalFov = c.horizontalFov;
                camera.verticalFov = c.verticalFov;
                var health = holder.AddComponent<Health>();
                health.team = Team.Guard;
                health.maximum = 35;
                var lamp = new GameObject("Camera status light");
                lamp.transform.SetParent(head.transform, false);
                lamp.transform.localPosition = c.lens;
                camera.indicator = lamp.AddComponent<Light>();
                camera.indicator.type = LightType.Point;
                camera.indicator.range = .7f;
                camera.indicator.intensity = .8f;
                camera.indicator.color = Color.green;
                cameras.Add(camera);
            }
            var panels = new List<AlarmPanel>();
            foreach (var p in data.panels)
            {
                var go = Spawn(p.modelPath, securityRoot.transform);
                go.name = "Alarm button " + p.id;
                go.transform.localPosition = p.position;
                go.transform.localRotation = Quaternion.Euler(p.rotation);
                var panel = go.AddComponent<AlarmPanel>();
                panel.network = network;
                panels.Add(panel);
            }
            foreach (int id in new[] { 501, 502 })
            {
                if (!placed.TryGetValue(id, out var terminal))
                    throw new Exception("Security computer missing " + id);
                var console = terminal.AddComponent<SecurityConsole>();
                console.network = network;
                console.alarmControl = false;
                console.hackSeconds = 5;
                terminal.name += " - DISABLE CAMERAS";
            }
            network.cameras = cameras.ToArray();
            network.panels = panels.ToArray();
            Physics.SyncTransforms();
            Debug.Log("SECURITY_READY: 5 cameras, 3 alarm buttons, 2 hackable computers, native guard animation/audio settings");
        }
    }
}
