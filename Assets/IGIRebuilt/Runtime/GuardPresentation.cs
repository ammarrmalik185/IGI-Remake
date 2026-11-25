using UnityEngine;
namespace IGIRebuilt
{
    [DefaultExecutionOrder(50)]
    public sealed class GuardPresentation : MonoBehaviour
    {
        public GuardAnimation animationPlayer; public GuardPresentationSettings settings; AudioSource voice, weapon, feet; Vector3 previous; float nextStep, shotUntil, hitUntil; EnemyGuard guard; Transform rifle;
        private float smoothedSpeed;
        private bool moving, running;
        public void Initialize(EnemyGuard owner, GuardPresentationSettings data, string model)
        {
            guard = owner;
            settings = data;
            previous = transform.position;
            if (data == null)
                return;
            voice = Source(25, 1);
            weapon = Source(100, .6f);
            feet = Source(16, .4f);
            var health = GetComponent<Health>();
            health.Damaged += _ => { if (!health.IsDead) { Play(voice, settings.hits); hitUntil = Time.time + .25f; animationPlayer?.Play("Hit"); } };
            health.Died += _ => { Play(voice, settings.deaths); animationPlayer?.Play("Death"); if (rifle != null) { rifle.SetParent(transform, true); rifle.localPosition = new Vector3(.4f, .15f, .3f); rifle.localRotation = Quaternion.Euler(90, 0, 20); } };
            var motion = data.ForModel(model);
            if (motion != null)
            {
                animationPlayer = guard.visual.gameObject.AddComponent<GuardAnimation>();
                animationPlayer.Initialize(motion);
            }
            if (data.rifle != null)
            {
                var socket = animationPlayer != null ? animationPlayer.WeaponSocket : null;
                var rifleObject = Instantiate(data.rifle, socket != null ? socket : transform);
                rifleObject.transform.localPosition = socket != null ? Vector3.zero : new Vector3(.12f, 1.18f, .35f);
                rifleObject.transform.localRotation = Quaternion.identity;
                rifleObject.transform.localScale = Vector3.one;
                foreach (var collider in rifleObject.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
                rifle = rifleObject.transform;
                foreach (var t in rifleObject.GetComponentsInChildren<Transform>())
                    t.gameObject.layer = DemoWorld.GuardLayer;
            }
        }
        AudioSource Source(float distance, float volume)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.spatialBlend = 1;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 2;
            a.maxDistance = distance;
            a.volume = volume;
            return a;
        }
        void Play(AudioSource source, AudioClip[] clips)
        {
            if (source != null && clips != null && clips.Length > 0)
                source.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }
        public void Shot()
        {
            shotUntil = Time.time + .3f;
            if (weapon != null && settings.shot != null)
                weapon.PlayOneShot(settings.shot);
        }
        public void Reload()
        {
            if (weapon != null && settings.reload != null)
                weapon.PlayOneShot(settings.reload);
        }
        void LateUpdate()
        {
            if (guard == null)
                return;
            bool paused = guard.mission != null && guard.mission.InputBlocked;
            if (voice != null)
            {
                voice.mute = paused;
                weapon.mute = paused;
                feet.mute = paused;
            }
            if (paused)
            {
                previous = transform.position;
                return;
            }
            Vector3 delta = transform.position - previous;
            delta.y = 0;
            float measuredSpeed = delta.magnitude / Mathf.Max(.001f, Time.deltaTime);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, measuredSpeed, 1 - Mathf.Exp(-12 * Time.deltaTime));
            float speed = smoothedSpeed;
            moving = moving ? speed > .08f : speed > .2f;
            running = running ? speed > 2 : speed > 2.4f;
            animationPlayer?.SetMovementSpeed(speed);
            previous = transform.position;
            if (guard.State == EnemyGuard.GuardState.Dead)
            {
                animationPlayer?.Play("Death");
                return;
            }
            if (Time.time < hitUntil)
                return;
            animationPlayer?.Play(moving ? (running ? "Run" : "Walk") : Time.time < shotUntil || guard.State == EnemyGuard.GuardState.Attack ? "Fire" : "Idle");
            if (speed > .4f && Time.time >= nextStep)
            {
                Play(feet, settings != null ? settings.footsteps : null);
                nextStep = Time.time + (speed > 2.3f ? .28f : .46f);
            }
        }
    }
}
