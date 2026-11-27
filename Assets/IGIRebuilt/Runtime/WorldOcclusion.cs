using UnityEngine;

namespace IGIRebuilt
{
    public static class WorldOcclusion
    {
        public static bool TryHit(Vector3 from, Vector3 to, out RaycastHit closest)
        {
            closest = default;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < .001f) return false;
            float nearest = float.PositiveInfinity;
            RaycastHit best = default;
            void Check(RaycastHit[] hits)
            {
                foreach (var hit in hits)
                {
                    if (BulletSurface.KindOf(hit) != BulletSurfaceKind.Solid) continue;
                    float d = Vector3.SqrMagnitude(hit.point - from);
                    if (d < nearest) { nearest = d; best = hit; }
                }
            }
            var forward = Physics.RaycastAll(from, delta / distance, distance, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore);
            var backward = Physics.RaycastAll(to, -delta / distance, distance, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore);
            Check(forward); Check(backward);
            closest = best;
            return !float.IsPositiveInfinity(nearest);
        }
        public static bool Blocked(Vector3 from, Vector3 to, float radius = 0, Transform ignore = null)
        {
            // Imported walls can be single-sided. Test both windings without changing global physics.
            bool Opaque(RaycastHit[] hits)
            {
                foreach (var hit in hits)
                    if (hit.collider.enabled && (ignore == null || !hit.collider.transform.IsChildOf(ignore)) && BulletSurface.KindOf(hit) == BulletSurfaceKind.Solid) return true;
                return false;
            }
            if (radius > 0)
            {
                Vector3 delta = to - from;
                float distance = delta.magnitude;
                if (distance < .001f) return false;
                return Opaque(Physics.SphereCastAll(from, radius, delta / distance, distance, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) ||
                    Opaque(Physics.SphereCastAll(to, radius, -delta / distance, distance, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore));
            }
            Vector3 line = to - from;
            if (line.sqrMagnitude < .000001f) return false;
            return Opaque(Physics.RaycastAll(from, line.normalized, line.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) ||
                Opaque(Physics.RaycastAll(to, -line.normalized, line.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore));
        }
    }
}
