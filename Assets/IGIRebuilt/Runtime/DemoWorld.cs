using System.Collections.Generic;
using UnityEngine;

namespace IGIRebuilt
{
    public sealed class DemoWorld : MonoBehaviour
    {
        public const int WorldLayer = 8;
        public const int PlayerLayer = 9;
        public const int GuardLayer = 10;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static MissionLayout Create(Transform parent)
        {
            var root = new GameObject("Training compound - replace through GameConfiguration");
            root.transform.SetParent(parent);
            var world = root.AddComponent<DemoWorld>();
            return world.Build();
        }
        private MissionLayout Build()
        {
            Color sand = new Color(0.45f, 0.43f, 0.34f);
            Color concrete = new Color(0.36f, 0.39f, 0.4f);
            Color steel = new Color(0.15f, 0.23f, 0.25f);
            Color rust = new Color(0.48f, 0.28f, 0.15f);
            Box("Ground", new Vector3(0, -0.3f, 0), new Vector3(100, 0.6f, 100), sand);
            Box("Western wall", new Vector3(-37, 1.5f, 0), new Vector3(0.5f, 3, 76), concrete);
            Box("Eastern wall", new Vector3(37, 1.5f, 0), new Vector3(0.5f, 3, 76), concrete);
            Box("North wall", new Vector3(0, 1.5f, 38), new Vector3(74, 3, 0.5f), concrete);
            Box("South wall left", new Vector3(-22, 1.5f, -38), new Vector3(30, 3, 0.5f), concrete);
            Box("South wall right", new Vector3(22, 1.5f, -38), new Vector3(30, 3, 0.5f), concrete);
            Box("Entry road", new Vector3(0, 0.015f, -20), new Vector3(8, 0.03f, 36), new Color(0.27f, 0.28f, 0.26f));
            // The control room has a real opening and an interior; no invisible wall at its door.
            Box("Control room west", new Vector3(-6, 2, 15), new Vector3(0.5f, 4, 14), concrete);
            Box("Control room east", new Vector3(6, 2, 15), new Vector3(0.5f, 4, 14), concrete);
            Box("Control room back", new Vector3(0, 2, 22), new Vector3(12, 4, 0.5f), concrete);
            Box("Control room door left", new Vector3(-4, 2, 8), new Vector3(4, 4, 0.5f), concrete);
            Box("Control room door right", new Vector3(4, 2, 8), new Vector3(4, 4, 0.5f), concrete);
            Box("Control room lintel", new Vector3(0, 3.6f, 8), new Vector3(4, 0.8f, 0.5f), concrete);
            Box("Control room roof", new Vector3(0, 4.2f, 15), new Vector3(13, 0.4f, 15), steel);
            Box("Warehouse", new Vector3(-22, 3.6f, 17), new Vector3(13, 7.2f, 20), steel);
            Box("Warehouse roof", new Vector3(-22, 7.3f, 17), new Vector3(14, 0.25f, 21), concrete);
            Box("Barracks", new Vector3(23, 2.5f, 23), new Vector3(17, 5, 12), concrete);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -21 : 21;
                float z = -13 + i % 2 * 11;
                Box("Cargo container " + i, new Vector3(x, 1.5f, z), new Vector3(11, 3, 4), i % 2 == 0 ? steel : rust);
                for (int rib = 0; rib < 10; rib++)
                    Box("Container rib", new Vector3(x - 4.8f + rib * 1.05f, 1.5f, z - 2.02f), new Vector3(0.07f, 2.8f, 0.06f), concrete, false);
            }
            for (int i = 0; i < 10; i++)
            {
                float x = i % 2 == 0 ? -8 - i : 8 + i;
                float z = -25 + i * 4.5f;
                Box("Supply crate " + i, new Vector3(x, 0.65f, z), new Vector3(1.5f, 1.3f, 1.5f), rust);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Watchtower platform", new Vector3(side * 32, 5.2f, 31), new Vector3(6, 0.4f, 6), steel);
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        Box("Tower support", new Vector3(side * 32 + x * 2.5f, 2.5f, 31 + z * 2.5f), new Vector3(0.25f, 5, 0.25f), steel);
            }
            Box("Radio mast", new Vector3(8, 8, 19), new Vector3(0.2f, 16, 0.2f), steel);
            for (int i = 0; i < 4; i++) Box("Radio antenna", new Vector3(8, 12 + i, 19), new Vector3(3, 0.08f, 0.08f), steel, false);
            var layout = gameObject.AddComponent<MissionLayout>();
            layout.playerSpawn = Marker("Player spawn", new Vector3(0, 0.12f, -33));
            layout.extraction = Marker("Extraction", new Vector3(0, 0.1f, -34));
            for (int i = 0; i < 4; i++)
            {
                Box("Extraction stripe", new Vector3(-3 + i * 2, 0.025f, -34), new Vector3(0.25f, 0.03f, 6), new Color(0.92f, 0.67f, 0.18f), false);
            }
            layout.terminal = Marker("Intelligence terminal", new Vector3(0, 1.25f, 19));
            Box("Terminal desk", new Vector3(0, 0.65f, 19), new Vector3(2.3f, 1.3f, 1), steel);
            Box("Terminal screen", new Vector3(0, 1.55f, 19), new Vector3(1.2f, 0.7f, 0.12f), new Color(0.1f, 0.75f, 0.7f), false);
            var terminalLight = new GameObject("Terminal light").AddComponent<Light>();
            terminalLight.transform.SetParent(layout.terminal, false);
            terminalLight.color = new Color(0.2f, 0.9f, 1);
            terminalLight.range = 5;
            terminalLight.intensity = 2;
            Vector3[] spawns = { new Vector3(-12, 0.1f, -8), new Vector3(12, 0.1f, -5), new Vector3(-11, 0.1f, 12), new Vector3(11, 0.1f, 13), new Vector3(-12, 0.1f, 28), new Vector3(15, 0.1f, 30), new Vector3(27, 0.1f, 5), new Vector3(-29, 0.1f, -23) };
            layout.guardSpawns = new Transform[spawns.Length];
            for (int i = 0; i < spawns.Length; i++) layout.guardSpawns[i] = Marker("Guard spawn " + i, spawns[i]);
            return layout;
        }
        private Transform Marker(string name, Vector3 position)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(transform);
            marker.position = position;
            return marker;
        }
        private GameObject Box(string name, Vector3 position, Vector3 scale, Color colour, bool solid = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = solid ? WorldLayer : 0;
            box.transform.SetParent(transform);
            box.transform.position = position;
            box.transform.localScale = scale;
            string key = ColorUtility.ToHtmlStringRGB(colour);
            if (!materials.TryGetValue(key, out Material material))
            {
                material = new Material(Shader.Find("Standard")) { color = colour };
                material.SetFloat("_Glossiness", 0.15f);
                materials.Add(key, material);
            }
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { box.GetComponent<Collider>().enabled = false; Destroy(box.GetComponent<Collider>()); }
            return box;
        }
        private void OnDestroy() { foreach (Material material in materials.Values) Destroy(material); }
    }
}
