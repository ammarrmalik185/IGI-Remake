using System.Collections.Generic;
using UnityEngine;

namespace IGIRebuilt
{
    // Swept projectiles avoid tunnelling and respect the mission's pause state.
    public sealed class WeaponProjectile : MonoBehaviour
    {
        private WeaponDefinition definition;
        private MissionDirector mission;
        private WeaponController owner;
        private Vector3 velocity;
        private float age;
        private bool detonated;
        private Material material;

        public static WeaponProjectile Launch(WeaponDefinition definition, Vector3 origin, Vector3 direction,
            MissionDirector mission, WeaponController owner, RaycastHit support = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = definition.displayName + " projectile";
            go.layer = DemoWorld.PlayerLayer;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var projectile = go.AddComponent<WeaponProjectile>();
            projectile.definition = definition; projectile.mission = mission; projectile.owner = owner;
            projectile.velocity = direction.normalized * definition.projectileSpeed;
            if (definition.action != WeaponAction.Rocket) projectile.velocity += Vector3.up * 3;
            go.transform.position = definition.action == WeaponAction.ProximityMine ? support.point + support.normal * .07f : origin;
            go.transform.localScale = definition.action == WeaponAction.ProximityMine ? new Vector3(.24f, .08f, .24f) : Vector3.one * .12f;
            projectile.material = new Material(Shader.Find("Sprites/Default")) { color = definition.action == WeaponAction.Rocket ? new Color(1, .6f, .2f) : new Color(.25f, .32f, .16f) };
            go.GetComponent<Renderer>().sharedMaterial = projectile.material;
            return projectile;
        }

        private void Update()
        {
            if (mission == null) { Destroy(gameObject); return; }
            if (mission.InputBlocked || detonated) return;
            float dt = Time.deltaTime;
            age += dt;
            if (definition.action == WeaponAction.ProximityMine)
            {
                if (age < 1.5f) return;
                foreach (var hit in Physics.OverlapSphere(transform.position, 2.5f, 1 << DemoWorld.GuardLayer, QueryTriggerInteraction.Ignore))
                {
                    var health = hit.GetComponentInParent<Health>();
                    if (health != null && !health.IsDead && !WorldOcclusion.Blocked(transform.position + Vector3.up * .15f, hit.bounds.center)) { Detonate(); break; }
                }
                return;
            }
            bool rocket = definition.action == WeaponAction.Rocket;
            if (!rocket) velocity += Physics.gravity * dt;
            Vector3 step = velocity * dt;
            int mask = (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.GuardLayer);
            // Check backfaces too: several retail walls use one collision winding.
            bool collision = Physics.SphereCast(transform.position, .06f, step.normalized, out var hitInfo, step.magnitude, mask, QueryTriggerInteraction.Ignore);
            if (WorldOcclusion.TryHit(transform.position, transform.position + step, out var wall) && (!collision || Vector3.Distance(transform.position, wall.point) < hitInfo.distance)) { hitInfo = wall; collision = true; }
            if (collision)
            {
                Vector3 normal = Vector3.Dot(hitInfo.normal, step) > 0 ? -hitInfo.normal : hitInfo.normal;
                transform.position = hitInfo.point + normal * .08f;
                if (rocket) { Detonate(); return; }
                velocity = Vector3.Reflect(velocity, normal) * .4f;
            }
            else transform.position += step;
            if (!rocket && age >= definition.fuseSeconds) Detonate();
            if (rocket && age >= definition.range / definition.projectileSpeed) Destroy(gameObject);
        }

        public void Detonate()
        {
            if (detonated) return;
            detonated = true;
            Vector3 origin = transform.position;
            bool flash = definition.action == WeaponAction.Flashbang;
            var seen = new HashSet<Health>();
            foreach (var collider in Physics.OverlapSphere(origin, definition.blastRadius,
                (1 << DemoWorld.GuardLayer) | (1 << DemoWorld.PlayerLayer) | (1 << DemoWorld.WorldLayer), QueryTriggerInteraction.Ignore))
            {
                var health = collider.GetComponentInParent<Health>();
                if (health == null || health.IsDead || !seen.Add(health)) continue;
                Vector3 centre = collider.bounds.center;
                if (WorldOcclusion.Blocked(origin, centre, 0, health.transform)) continue;
                if (flash)
                {
                    var guard = health.GetComponent<EnemyGuard>();
                    if (guard != null) guard.Stun(5);
                    if (health.team == Team.Player && owner != null) owner.Flash(3);
                }
                else
                {
                    float falloff = Mathf.Clamp01(1 - Vector3.Distance(origin, centre) / definition.blastRadius);
                    health.TakeDamage(definition.damage * falloff, health.team == Team.Player ? Team.Guard : Team.Player, origin);
                }
            }
            mission?.MakeNoise(origin, 120);
            if (definition.explosionSound != null) AudioSource.PlayClipAtPoint(definition.explosionSound, origin, .8f);
            if (definition.explosionEffect != null)
            {
                var effect = Instantiate(definition.explosionEffect, origin, Quaternion.identity);
                effect.transform.localScale *= 2;
                Destroy(effect, 3);
            }
            WeaponController.DrawTracer(origin - Vector3.up * .35f, origin + Vector3.up * .35f, flash ? Color.white : new Color(1, .4f, .05f), .3f);
            Destroy(gameObject);
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
