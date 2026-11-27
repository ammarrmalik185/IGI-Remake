using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IGIRebuilt
{
    public static class BulletTrace
    {
        public static bool Cast(Vector3 origin, Vector3 direction, float range, int mask, Transform owner, out RaycastHit result, bool penetrates = true)
        {
            result = default;
            direction.Normalize();
            if (direction.sqrMagnitude < .5f || range <= 0) return false;
            var hits = Physics.RaycastAll(origin, direction, range, mask, QueryTriggerInteraction.Ignore)
                .Concat(Physics.RaycastAll(origin + direction * range, -direction, range, mask, QueryTriggerInteraction.Ignore))
                .OrderBy(h => Vector3.SqrMagnitude(h.point - origin));
            var processed = new List<(Collider collider, BulletSurfaceKind kind, float distance)>();
            foreach (var hit in hits)
            {
                if (!hit.collider.enabled || (owner != null && hit.collider.transform.IsChildOf(owner))) continue;
                float distance = Vector3.Distance(origin, hit.point);
                var kind = BulletSurface.KindOf(hit);
                if (processed.Any(p => p.collider == hit.collider && p.kind == kind)) continue;
                processed.Add((hit.collider, kind, distance));
                var surface = hit.collider.GetComponent<BulletSurface>();
                if (surface != null && !surface.Intercept(hit, penetrates)) continue;
                result = hit;
                return true;
            }
            return false;
        }
    }
}
