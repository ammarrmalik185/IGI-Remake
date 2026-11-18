using UnityEngine;

namespace IGIRebuilt
{
    public static class VisualFactory
    {
        public static GameObject Attach(GameObject prefab, Transform parent, Vector3 position, Vector3 rotation, float size, bool height = false)
        {
            GameObject visual = prefab != null ? Object.Instantiate(prefab, parent) : CreateFallbackGun(parent);
            visual.name = prefab != null ? prefab.name : "Prototype weapon";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(rotation);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>()) Object.Destroy(body);
            if (TryBounds(visual, out Bounds bounds))
            {
                Vector3 extent = bounds.size;
                float longest = height ? extent.y : Mathf.Max(extent.x, extent.y, extent.z);
                if (longest > 0.00001f) visual.transform.localScale *= size / longest;
                TryBounds(visual, out bounds);
                visual.transform.localPosition += position - parent.InverseTransformPoint(bounds.center);
            }
            else visual.transform.localPosition = position;
            return visual;
        }
        public static bool TryBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            bounds = new Bounds(root.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }
        private static GameObject CreateFallbackGun(Transform parent)
        {
            GameObject root = new GameObject("Prototype rifle");
            root.transform.SetParent(parent, false);
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.12f, 0.14f, 0.16f);
            CreatePart(root.transform, "Receiver", Vector3.zero, new Vector3(0.1f, 0.13f, 0.42f), material);
            CreatePart(root.transform, "Barrel", new Vector3(0, 0.025f, 0.34f), new Vector3(0.035f, 0.035f, 0.35f), material);
            CreatePart(root.transform, "Stock", new Vector3(0, -0.015f, -0.3f), new Vector3(0.09f, 0.12f, 0.24f), material);
            CreatePart(root.transform, "Magazine", new Vector3(0, -0.18f, -0.03f), new Vector3(0.06f, 0.25f, 0.12f), material);
            return root;
        }
        private static void CreatePart(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
