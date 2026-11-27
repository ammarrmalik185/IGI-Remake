using UnityEngine;

namespace IGIRebuilt
{
    public sealed class SurfaceEffects : MonoBehaviour
    {
        static SurfaceInteractionSettings settings;
        static Mesh shard;
        AudioSource source;
        bool paused;
        public static SurfaceInteractionSettings Settings => settings != null ? settings : settings = Resources.Load<SurfaceInteractionSettings>("SurfaceInteractionSettings");
        public static void Play(AudioClip[] clips, Vector3 point)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            var effect = new GameObject("Imported surface impact").AddComponent<SurfaceEffects>();
            effect.transform.position = point;
            effect.source = effect.gameObject.AddComponent<AudioSource>();
            effect.source.playOnAwake = false;
            effect.source.spatialBlend = 1;
            effect.source.rolloffMode = AudioRolloffMode.Linear;
            effect.source.minDistance = 2;
            effect.source.maxDistance = 55;
            effect.source.volume = .8f;
            effect.source.clip = clip;
            effect.source.Play();
            Destroy(effect.gameObject, clip.length + .1f);
        }
        public static void Shards(Vector3 point)
        {
            if (Settings == null || Settings.shardMaterial == null) return;
            if (shard == null)
            {
                shard = new Mesh { name = "Glass shard", hideFlags = HideFlags.HideAndDontSave };
                shard.vertices = new[] { new Vector3(-.5f, -.4f, 0), new Vector3(.5f, -.4f, 0), new Vector3(-.2f, .6f, 0) };
                shard.triangles = new[] { 0, 1, 2, 2, 1, 0 };
                shard.RecalculateNormals(); shard.RecalculateBounds();
            }
            var go = new GameObject("Glass shards"); go.transform.position = point;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = .1f; main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.04f, .12f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(.65f, .85f, 1, .65f); main.gravityModifier = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .08f;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 24) });
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = shard; renderer.sharedMaterial = Settings.shardMaterial;
            particles.Play(); Destroy(go, 1.2f);
        }
        void Update()
        {
            if (source == null) return;
            bool shouldPause = Time.timeScale == 0;
            if (shouldPause == paused) return;
            paused = shouldPause;
            if (paused) source.Pause(); else source.UnPause();
        }
    }
}
