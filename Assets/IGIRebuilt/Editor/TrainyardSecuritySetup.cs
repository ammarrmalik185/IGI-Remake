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
                throw new Exception("Missing guard sound " + name);
            return c;
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
            settings.shot = Audio("ak47_loop");
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
