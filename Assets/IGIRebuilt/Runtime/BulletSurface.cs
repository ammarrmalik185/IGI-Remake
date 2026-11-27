using UnityEngine;

namespace IGIRebuilt
{
    public sealed class BulletSurface : MonoBehaviour
    {
        public BulletSurfaceKind kind;
        public ImportedSurfaceData data;
        [Range(0, 1)] public float fenceBlockChance = .2f;
        public bool Broken { get; private set; }
        public bool Breakable => GetComponentInParent<ImportedModelInfo>()?.originalModel != "418_03_1";

        public BulletSurfaceKind KindAt(int triangle)
        {
            return data != null && triangle >= 0 && triangle < data.triangleKinds.Length ? (BulletSurfaceKind)data.triangleKinds[triangle] : kind;
        }
        public static BulletSurfaceKind KindOf(RaycastHit hit)
        {
            var surface = hit.collider.GetComponent<BulletSurface>();
            if (surface == null) return BulletSurfaceKind.Solid;
            int triangle = hit.triangleIndex;
            if (triangle < 0 && surface.data != null && hit.collider.Raycast(new Ray(hit.point + hit.normal * .04f, -hit.normal), out var face, .08f)) triangle = face.triangleIndex;
            return surface.KindAt(triangle);
        }
        void Awake()
        {
            if (data != null && data.triangleKinds.Length == 0)
                foreach (var collider in GetComponents<MeshCollider>()) collider.enabled = false;
        }
        public bool Intercept(RaycastHit hit, bool penetrates)
        {
            switch (KindAt(hit.triangleIndex))
            {
                case BulletSurfaceKind.Glass:
                    Break(hit.point);
                    return !penetrates;
                case BulletSurfaceKind.Fence:
                    if (!penetrates || Random.value < fenceBlockChance)
                    {
                        SurfaceEffects.Play(SurfaceEffects.Settings?.fenceImpacts, hit.point);
                        return true;
                    }
                    return false;
                default: return true;
            }
        }
        public void Break(Vector3 point)
        {
            if (Broken || !Breakable) return;
            Broken = true;
            foreach (var renderer in GetComponents<Renderer>()) renderer.enabled = false;
            foreach (var collider in GetComponents<Collider>()) collider.enabled = false;
            SurfaceEffects.Play(SurfaceEffects.Settings?.glassBreaks, point);
            SurfaceEffects.Shards(point);
        }
    }
}
