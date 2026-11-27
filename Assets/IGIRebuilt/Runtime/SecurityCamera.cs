using UnityEngine;
namespace IGIRebuilt
{
    public sealed class SecurityCamera : MonoBehaviour
    {
        public SecurityNetwork network; public Transform head; public Vector3 lens; public float left, right, speed = 15, pause = 4, range = 25, horizontalFov = 55, verticalFov = 90; public Light indicator;
        public AudioClip detectionClip, destructionClip;
        public GameObject destructionEffect;
        [Min(.1f)] public float detectionSeconds = 4;
        [Min(.1f)] public float detectionRecoverySeconds = 2;
        [Range(0, 60)] public float sweepLimit = 60;
        [Min(.04f)] public float slowBeepInterval = .8f, fastBeepInterval = .08f;
        AudioSource audioSource;
        float nextBeep;
        public float Detection
        {
            get; private set;
        }
        public bool Destroyed
        {
            get; private set;
        }
        float yaw, direction = 1, wait; Quaternion rest; bool initialized;
        public void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            rest = head.localRotation;
            audioSource = head.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2;
            audioSource.maxDistance = 45;
            audioSource.volume = .8f;
            var health = GetComponent<Health>();
            if (health != null)
                health.Died += OnDestroyed;
        }
        void OnDestroyed(Health health)
        {
            if (Destroyed) return;
            ClearDetection();
            Destroyed = true;
            if (indicator != null) indicator.enabled = false;
            if (destructionClip != null) audioSource.PlayOneShot(destructionClip);
            if (destructionEffect != null)
            {
                var effect = Instantiate(destructionEffect, head.TransformPoint(lens), Quaternion.identity);
                Destroy(effect, 3);
            }
            head.localRotation *= Quaternion.Euler(35, 0, 15);
        }
        public void ClearDetection()
        {
            Detection = 0;
            nextBeep = 0;
            if (audioSource != null && !Destroyed) audioSource.Stop();
        }
        public bool CanSee(Vector3 point)
        {
            if (Destroyed || head == null || (network != null && network.CamerasDisabled))
                return false;
            Vector3 at = head.TransformPoint(lens), local = head.InverseTransformDirection(point - at);
            if (local.z <= 0 || local.magnitude > range || Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) > horizontalFov * .5f || Mathf.Abs(Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg) > verticalFov * .5f)
                return false;
            return !WorldOcclusion.Blocked(at, point, 0, transform);
        }
        public void Tick(float dt)
        {
            if (head == null || network == null || network.mission == null)
                return;
            var m = network.mission;
            if (audioSource != null) audioSource.mute = m.InputBlocked;
            if (m.InputBlocked)
                return;
            bool online = !Destroyed && !network.CamerasDisabled;
            if (online)
            {
                var player = m.player;
                bool sees = player != null && !player.GetComponent<Health>().IsDead && CanSee(player.transform.position + Vector3.up * (player.IsCrouching ? .75f : 1.25f));
                // Pan around the upright mount, keeping the authored pitch and forward sector.
                float minYaw = -Mathf.Clamp(left, 0, sweepLimit);
                float maxYaw = Mathf.Clamp(right, 0, sweepLimit);
                if (!sees && wait > 0)
                    wait -= dt;
                else if (!sees && maxYaw > minYaw && speed > 0)
                {
                    yaw += direction * speed * dt;
                    if (yaw >= maxYaw || yaw <= minYaw)
                    {
                        yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
                        direction = -direction;
                        wait = pause;
                    }
                    // Limits are already expressed as Unity yaw: left negative, right positive.
                    head.localRotation = Quaternion.Euler(0, yaw, 0) * rest;
                }
                Detection = Mathf.Clamp01(Detection + dt * (sees ? 1 / Mathf.Max(.1f, detectionSeconds) : -1 / Mathf.Max(.1f, detectionRecoverySeconds)));
                if (sees && detectionClip != null)
                {
                    float interval = Mathf.Max(detectionClip.length, Mathf.Lerp(slowBeepInterval, fastBeepInterval, Mathf.Pow(Detection, 1.5f)));
                    nextBeep = Mathf.Min(nextBeep, interval) - dt;
                    if (nextBeep <= 0)
                    {
                        audioSource.PlayOneShot(detectionClip);
                        nextBeep = interval;
                    }
                }
                else
                {
                    nextBeep = 0;
                    audioSource.Stop();
                }
                network.ReportDetection(Detection);
                if (Detection >= 1 && sees)
                    network.RaiseAlarm(player.transform.position);
            }
            else
                Detection = 0;
            if (indicator != null)
            {
                indicator.enabled = online;
                indicator.color = Detection > .1f ? Color.red : Color.green;
            }
        }
        void Start()
        {
            Initialize();
        }
        void OnDisable()
        {
            if (audioSource != null) audioSource.Stop();
        }
        void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
