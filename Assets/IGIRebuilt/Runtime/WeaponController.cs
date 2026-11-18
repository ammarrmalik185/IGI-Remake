using System.Collections;
using UnityEngine;

namespace IGIRebuilt
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class WeaponController : MonoBehaviour
    {
        public Camera eye;
        public Transform modelMount;
        public MissionDirector mission;
        public WeaponDefinition[] definitions;
        public WeaponDefinition Active => definitions != null && definitions.Length > 0 ? definitions[index] : null;
        public MagazineState Ammo => magazines != null && magazines.Length > 0 ? magazines[index] : null;
        public bool IsReloading { get; private set; }
        public bool IsAiming { get; private set; }
        public float HitMarkerUntil { get; private set; }
        public int ShotsFired { get; private set; }
        private MagazineState[] magazines;
        private int index;
        private float nextShot;
        private float kick;
        private Transform visual;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private AudioSource audioSource;
        private AudioClip generatedShot;

        public void Configure(WeaponDefinition[] weapons)
        {
            definitions = weapons;
            if (definitions == null || definitions.Length == 0) return;
            magazines = new MagazineState[definitions.Length];
            for (int i = 0; i < magazines.Length; i++) magazines[i] = new MagazineState(definitions[i].magazineSize, definitions[i].reserveAmmo);
            audioSource = GetComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            generatedShot = CreateShotSound();
            Equip(0);
        }
        private void Update()
        {
            if (Active == null || mission == null) return;
            if (!mission.InputBlocked)
            {
                for (int i = 0; i < Mathf.Min(3, definitions.Length); i++)
                    if (GameInput.Pressed(KeyCode.Alpha1 + i)) Equip(i);
                if (GameInput.Scroll != 0) Equip((index + (GameInput.Scroll > 0 ? 1 : definitions.Length - 1)) % definitions.Length);
                IsAiming = GameInput.Aim && !IsReloading;
                if (!Active.melee && GameInput.Pressed(KeyCode.R) && !IsReloading && Ammo.Loaded < Ammo.Capacity && Ammo.Reserve > 0) StartCoroutine(Reload());
                if (!IsReloading && GameInput.Fire(Active.automatic) && Time.time >= nextShot) Fire();
            }
            else IsAiming = false;
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * 14);
            if (visual != null)
            {
                Vector3 aimOffset = IsAiming ? new Vector3(-Active.viewPosition.x * 0.77f, 0.06f, -0.06f) : Vector3.zero;
                Vector3 reloadOffset = IsReloading ? new Vector3(0, -0.28f, -0.05f) : Vector3.zero;
                visual.localPosition = Vector3.Lerp(visual.localPosition, restPosition + aimOffset + reloadOffset + (Active.melee ? Vector3.forward * kick * 0.14f : Vector3.back * kick * 0.012f), Time.deltaTime * 14);
                visual.localRotation = restRotation * Quaternion.Euler(Active.melee ? -kick * 35 : -kick * Active.recoilDegrees, 0, Active.melee ? -kick * 40 : IsReloading ? -12 : 0);
            }
            eye.fieldOfView = Mathf.Lerp(eye.fieldOfView, IsAiming ? Active.aimFieldOfView : 78, 12 * Time.deltaTime);
        }
        public void Equip(int selected)
        {
            if (definitions == null || selected < 0 || selected >= definitions.Length || definitions[selected] == null) return;
            StopAllCoroutines();
            IsReloading = false;
            index = selected;
            nextShot = Time.time + 0.18f;
            if (visual != null) Destroy(visual.gameObject);
            WeaponDefinition definition = Active;
            visual = VisualFactory.Attach(definition.visualPrefab, modelMount, definition.viewPosition, definition.viewRotation, definition.modelLength).transform;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            // Player geometry is excluded from hitscan and guard visibility checks.
            SetLayer(visual, DemoWorld.PlayerLayer);
        }
        public bool Fire()
        {
            if (Active == null || IsReloading || mission == null || mission.InputBlocked || Time.time < nextShot) return false;
            nextShot = Time.time + Active.secondsPerShot;
            if (!Active.melee && !Ammo.Fire())
            {
                if (Ammo.Reserve > 0) StartCoroutine(Reload());
                return false;
            }
            ShotsFired++;
            kick = 1;
            if (Active.fireSound != null || !Active.melee) audioSource.PlayOneShot(Active.fireSound != null ? Active.fireSound : generatedShot, 0.25f);
            Vector2 spread = Random.insideUnitCircle * Active.spreadDegrees * (IsAiming ? 0.2f : 1);
            Vector3 direction = Quaternion.Euler(spread.y, spread.x, 0) * eye.transform.forward;
            Vector3 end = eye.transform.position + direction * Active.range;
            int mask = (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.GuardLayer);
            if (Physics.Raycast(eye.transform.position, direction, out RaycastHit hit, Active.range, mask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                Health target = hit.collider.GetComponentInParent<Health>();
                if (target != null && target.TakeDamage(Active.damage, Team.Player)) HitMarkerUntil = Time.time + 0.16f;
                else CreateImpact(hit.point, hit.normal);
            }
            Vector3 muzzle = eye.transform.TransformPoint(new Vector3(0.18f, -0.13f, 0.8f));
            if (!Active.melee) DrawTracer(muzzle, end, new Color(1, 0.78f, 0.35f), 0.035f);
            mission.MakeNoise(eye.transform.position, Active.melee ? 3 : Active.displayName.Contains("Mp5") ? 8 : 55);
            return true;
        }
        private IEnumerator Reload()
        {
            IsReloading = true;
            if (Active.reloadSound != null) audioSource.PlayOneShot(Active.reloadSound);
            yield return new WaitForSeconds(Active.reloadSeconds);
            Ammo.Reload();
            IsReloading = false;
        }
        public static void DrawTracer(Vector3 start, Vector3 end, Color colour, float duration)
        {
            var go = new GameObject("Shot tracer");
            var line = go.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.012f;
            line.endWidth = 0.003f;
            line.startColor = line.endColor = colour;
            Destroy(go, duration);
            Destroy(line.material, duration);
        }
        private static void CreateImpact(Vector3 point, Vector3 normal)
        {
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.name = "Bullet impact";
            impact.transform.position = point + normal * 0.012f;
            impact.transform.localScale = Vector3.one * 0.045f;
            Destroy(impact.GetComponent<Collider>());
            // This shader is retained by GraphicsSettings for runtime effects. Standard can
            // be stripped now that imported models use their own two-sided shaders.
            var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.15f, 0.12f, 0.09f) };
            impact.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(impact, 4);
            Destroy(mat, 4);
        }
        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }
        private static AudioClip CreateShotSound()
        {
            const int frequency = 22050;
            float[] samples = new float[frequency / 7];
            var random = new System.Random(51);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / frequency;
                samples[i] = ((float)random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 48) * 0.65f + Mathf.Sin(t * 180 * Mathf.PI) * Mathf.Exp(-t * 35) * 0.2f;
            }
            AudioClip clip = AudioClip.Create("Prototype shot", samples.Length, 1, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
