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
        public bool IsThrowing { get; private set; }
        public bool IsScoped => IsAiming && Active != null && Active.scoped;
        public float TargetFieldOfView => IsAiming ? Active.aimFieldOfView : 78;
        public float HitMarkerUntil { get; private set; }
        public int ShotsFired { get; private set; }
        public float FlashUntil { get; private set; }
        public int SelectedIndex => index;
        public void Flash(float seconds) { FlashUntil = Mathf.Max(FlashUntil, Time.time + seconds); }
        private MagazineState[] magazines;
        private int index;
        private float nextShot;
        private float kick;
        private Transform visual;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private AudioSource audioSource;
        private AudioClip generatedShot;
        public const int ViewLayer = 11;
        public Camera ViewCamera { get; private set; }
        private NativeWeaponView nativeView;
        private WeaponScopeEffect scopeEffect;

        public void Configure(WeaponDefinition[] weapons)
        {
            StopAllCoroutines();
            IsReloading = IsAiming = IsThrowing = false;
            index = 0;
            definitions = weapons;
            if (definitions == null || definitions.Length == 0) return;
            magazines = new MagazineState[definitions.Length];
            for (int i = 0; i < magazines.Length; i++) magazines[i] = new MagazineState(definitions[i].magazineSize, definitions[i].reserveAmmo);
            audioSource = GetComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            audioSource.loop = false;
            if (generatedShot != null) Destroy(generatedShot);
            generatedShot = CreateShotSound();
            if (ViewCamera == null)
            {
                var cameraObject = new GameObject("First person weapon camera");
                cameraObject.transform.SetParent(eye.transform, false);
                ViewCamera = cameraObject.AddComponent<Camera>();
                ViewCamera.clearFlags = CameraClearFlags.Depth;
                ViewCamera.cullingMask = 1 << ViewLayer;
                ViewCamera.depth = eye.depth + 1;
                ViewCamera.nearClipPlane = .025f;
                ViewCamera.farClipPlane = 5;
                eye.cullingMask &= ~(1 << ViewLayer);
                cameraObject = eye.gameObject;
                scopeEffect = cameraObject.GetComponent<WeaponScopeEffect>();
                if (scopeEffect == null) scopeEffect = cameraObject.AddComponent<WeaponScopeEffect>();
                scopeEffect.owner = this;
                scopeEffect.enabled = false;
            }
            Equip(0);
        }
        private void Update()
        {
            if (Active == null || mission == null) return;
            if (!mission.InputBlocked && (mission.player == null || !mission.player.IsClimbing))
            {
                for (int i = 0; i < Mathf.Min(3, definitions.Length); i++)
                    if (GameInput.Pressed(KeyCode.Alpha1 + i)) Equip(i);
                if (GameInput.Scroll != 0) Equip((index + (GameInput.Scroll > 0 ? 1 : definitions.Length - 1)) % definitions.Length);
                SetAim(GameInput.Aim);
                if (!Active.melee && Active.action != WeaponAction.Binoculars && GameInput.Pressed(KeyCode.R) && !IsReloading && !IsThrowing && Ammo.Loaded < Ammo.Capacity && Ammo.Reserve > 0) StartCoroutine(Reload());
                if (!IsReloading && GameInput.Fire(Active.automatic) && Time.time >= nextShot) Fire();
            }
            else IsAiming = false;
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * 14);
            if (nativeView != null)
            {
                if (!mission.InputBlocked) nativeView.Tick(Time.deltaTime, IsReloading, Active.action);
            }
            else if (visual != null)
            {
                Vector3 aimOffset = IsAiming ? new Vector3(Active.dualWield ? 0 : -Active.viewPosition.x * 0.77f, 0.06f, -0.06f) : Vector3.zero;
                Vector3 reloadOffset = IsReloading ? new Vector3(0, -0.28f, -0.05f) : Vector3.zero;
                visual.localPosition = Vector3.Lerp(visual.localPosition, restPosition + aimOffset + reloadOffset + (Active.melee ? Vector3.forward * kick * 0.14f : Vector3.back * kick * 0.012f), Time.deltaTime * 14);
                visual.localRotation = restRotation * Quaternion.Euler(Active.melee ? -kick * 35 : -kick * Active.recoilDegrees, 0, Active.melee ? -kick * 40 : IsReloading ? -12 : 0);
            }
            eye.fieldOfView = Mathf.Lerp(eye.fieldOfView, TargetFieldOfView, 12 * Time.deltaTime);
            UpdateViewProjection(eye.aspect);
        }
        public void SetAim(bool held)
        {
            IsAiming = held && Active != null && Active.canZoom && !IsReloading && !IsThrowing &&
                mission != null && !mission.InputBlocked && (mission.player == null || !mission.player.IsClimbing);
            UpdateScopeState();
        }
        private void UpdateScopeState()
        {
            bool scoped = IsScoped;
            if (ViewCamera != null && ViewCamera.enabled == scoped) ViewCamera.enabled = !scoped;
            if (scopeEffect != null && scopeEffect.enabled != scoped) scopeEffect.enabled = scoped;
        }
        private void UpdateViewProjection(float aspect)
        {
            if (ViewCamera == null) return;
            float horizontal = Active != null ? Active.viewHorizontalFieldOfView : 68;
            float fov = 2 * Mathf.Atan(Mathf.Tan(horizontal * Mathf.Deg2Rad / 2) / Mathf.Max(.1f, aspect)) * Mathf.Rad2Deg;
            if (!Mathf.Approximately(ViewCamera.fieldOfView, fov)) ViewCamera.fieldOfView = fov;
            if (ViewCamera.rect != eye.rect) ViewCamera.rect = eye.rect;
            UpdateScopeState();
        }
        public void RenderView(RenderTexture target)
        {
            if (ViewCamera == null || nativeView == null || IsScoped) return;
            UpdateViewProjection((float)target.width / target.height);
            ViewCamera.targetTexture = target; ViewCamera.Render(); ViewCamera.targetTexture = null;
        }
        public void Equip(int selected)
        {
            if (definitions == null || selected < 0 || selected >= definitions.Length || definitions[selected] == null) return;
            StopAllCoroutines();
            IsThrowing = false;
            IsReloading = false;
            IsAiming = false;
            kick = 0;
            index = selected;
            nextShot = Time.time + 0.18f;
            if (visual != null) { visual.gameObject.SetActive(false); Destroy(visual.gameObject); }
            WeaponDefinition definition = Active;
            nativeView = null;
            if (definition.visualPrefab != null && definition.visualPrefab.GetComponent<NativeWeaponView>() != null)
            {
                visual = Instantiate(definition.visualPrefab, modelMount, false).transform;
                nativeView = visual.GetComponent<NativeWeaponView>();
                nativeView.ResetPose();
            }
            else if (definition.dualWield)
            {
                visual = new GameObject("Dual Uzis").transform;
                visual.SetParent(modelMount, false);
                visual.localPosition = new Vector3(0, definition.viewPosition.y, definition.viewPosition.z);
                foreach (float side in new[] { -1f, 1f })
                    VisualFactory.Attach(definition.visualPrefab, visual, Vector3.right * definition.viewPosition.x * side, definition.viewRotation, definition.modelLength);
            }
            else visual = VisualFactory.Attach(definition.visualPrefab, modelMount, definition.viewPosition, definition.viewRotation, definition.modelLength).transform;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            // Player geometry is excluded from hitscan and guard visibility checks.
            SetLayer(visual, nativeView != null ? ViewLayer : DemoWorld.PlayerLayer);
            UpdateScopeState();
        }
        public bool Fire()
        {
            if (Active == null || IsReloading || IsThrowing || mission == null || mission.InputBlocked || (mission.player != null && mission.player.IsClimbing) || Time.time < nextShot) return false;
            nextShot = Time.time + Active.secondsPerShot;
            if (Active.action == WeaponAction.Binoculars) return false;
            Health operatorHealth = mission.player != null ? mission.player.GetComponent<Health>() : null;
            if (Active.action == WeaponAction.Medipack && (operatorHealth == null || operatorHealth.IsDead || operatorHealth.Current >= operatorHealth.maximum)) return false;
            RaycastHit mineSupport = default;
            if (Active.action == WeaponAction.ProximityMine &&
                (!Physics.Raycast(eye.transform.position, (eye.transform.forward + Vector3.down).normalized, out mineSupport, 3, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore) || mineSupport.normal.y < .6f)) return false;
            if (Active.action == WeaponAction.Grenade || Active.action == WeaponAction.Flashbang)
            {
                if (Ammo.Loaded == 0) { if (Ammo.Reserve > 0) StartCoroutine(Reload()); return false; }
                StartCoroutine(Throw());
                return true;
            }
            if (!Active.melee && !Ammo.Fire())
            {
                if (Ammo.Reserve > 0) StartCoroutine(Reload());
                return false;
            }
            ShotsFired++;
            kick = 1;
            if (nativeView != null) nativeView.Fire();
            if (Active.action == WeaponAction.Medipack) { operatorHealth.Heal(60); return true; }
            if (Active.fireSound != null || !Active.melee) audioSource.PlayOneShot(Active.fireSound != null ? Active.fireSound : generatedShot, 0.25f);
            if (Active.action != WeaponAction.Hitscan)
            {
                WeaponProjectile.Launch(Active, eye.transform.position, eye.transform.forward, mission, this, mineSupport);
                mission.MakeNoise(eye.transform.position, Active.noiseRadius);
                return true;
            }
            for (int pellet = 0; pellet < Mathf.Max(1, Active.pellets); pellet++)
            {
                Vector2 spread = Random.insideUnitCircle * Active.spreadDegrees * (IsAiming ? 0.2f : 1);
                Vector3 direction = Quaternion.Euler(spread.y, spread.x, 0) * eye.transform.forward;
                Vector3 end = eye.transform.position + direction * Active.range;
                int mask = (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.GuardLayer);
                if (BulletTrace.Cast(eye.transform.position, direction, Active.range, mask, transform, out RaycastHit hit, !Active.melee))
                {
                    end = hit.point;
                    Health target = hit.collider.GetComponentInParent<Health>();
                    if (target != null && target.TakeDamage(Active.damage, Team.Player, eye.transform.position)) HitMarkerUntil = Time.time + 0.16f;
                    else if (BulletSurface.KindOf(hit) == BulletSurfaceKind.Solid) CreateImpact(hit.point, hit.normal);
                }
                Vector3 muzzle = eye.transform.TransformPoint(new Vector3(Active.dualWield && ShotsFired % 2 == 0 ? -.18f : .18f, -0.13f, 0.8f));
                if (!Active.melee) DrawTracer(muzzle, end, new Color(1, 0.78f, 0.35f), 0.035f);
            }
            mission.MakeNoise(eye.transform.position, Active.noiseRadius);
            return true;
        }
        private IEnumerator Reload()
        {
            IsReloading = true;
            if (nativeView != null) nativeView.Reload(Active.reloadSeconds);
            if (Active.reloadSound != null) audioSource.PlayOneShot(Active.reloadSound);
            yield return new WaitForSeconds(Active.reloadSeconds);
            Ammo.Reload();
            IsReloading = false;
        }
        private IEnumerator Throw()
        {
            IsThrowing = true;
            IsAiming = false;
            var definition = Active;
            float release = nativeView != null && nativeView.fire.releaseSeconds > 0 ? nativeView.fire.releaseSeconds : .7f;
            float duration = nativeView != null && NativeWeaponView.Valid(nativeView.fire) ? nativeView.fire.duration : release + .3f;
            if (nativeView != null) nativeView.Fire();
            float elapsed = 0;
            while (elapsed < release)
            {
                yield return null;
                if (mission.Failed || mission.Progress.Complete) { IsThrowing = false; nativeView?.ResetPose(); yield break; }
                if (mission.InputBlocked) continue;
                if (mission.player != null && mission.player.IsClimbing) { IsThrowing = false; nativeView?.ResetPose(); yield break; }
                elapsed += Time.deltaTime;
            }
            if (Ammo.Fire())
            {
                ShotsFired++;
                if (definition.fireSound != null) audioSource.PlayOneShot(definition.fireSound, .25f);
                WeaponProjectile.Launch(definition, eye.transform.position, eye.transform.forward, mission, this);
                mission.MakeNoise(eye.transform.position, definition.noiseRadius);
                if (nativeView != null) nativeView.gameObject.SetActive(false);
            }
            while (elapsed < duration)
            {
                yield return null;
                if (!mission.InputBlocked) elapsed += Time.deltaTime;
            }
            IsThrowing = false;
            if (nativeView != null) { nativeView.gameObject.SetActive(true); nativeView.ResetPose(); }
            nextShot = Time.time + .1f;
        }
        private void OnDestroy() { if (generatedShot != null) Destroy(generatedShot); }
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
