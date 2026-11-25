using System;
using UnityEngine;
namespace IGIRebuilt
{
    [DefaultExecutionOrder(100)]
    public sealed class GuardAnimation : MonoBehaviour
    {
        public GuardMotionData data; public string Current
        {
            get; private set;
        }
        Mesh mesh; Vector3[] positions, normals, blendFrom; GuardMotionClip clip; float clock, blend; bool stopped;
        private float movementSpeed, worldScale = 1;
        public Transform WeaponSocket { get; private set; }
        private Vector3 socketBlendPosition; private Quaternion socketBlendRotation;
        public float PlaybackRate { get; private set; } = 1;
        public void SetMovementSpeed(float speed) { movementSpeed = Mathf.Max(0, speed); }
        public void Initialize(GuardMotionData source)
        {
            data = source;
            var filter = GetComponentInChildren<MeshFilter>();
            if (data == null || filter == null)
                return;
            worldScale = Mathf.Max(.001f, Mathf.Abs(filter.transform.lossyScale.z));
            mesh = Instantiate(filter.sharedMesh);
            mesh.name += " animated instance";
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            positions = mesh.vertices;
            normals = mesh.normals;
            blendFrom = new Vector3[positions.Length];
            if (positions.Length != data.vertexCount)
            {
                Debug.LogError("Animation vertex mismatch", this);
                enabled = false;
                return;
            }
            WeaponSocket = new GameObject("Animated weapon hand (bone 22)").transform;
            WeaponSocket.SetParent(filter.transform, false);
            Play("Idle"); blend = 1; Advance(0);
        }
        public void Play(string state)
        {
            if (data == null || mesh == null || Current == state || stopped)
                return;
            var next = Array.Find(data.clips, c => c.name == state);
            if (next == null)
                return;
            Array.Copy(positions, blendFrom, positions.Length);
            if (WeaponSocket != null) { socketBlendPosition = WeaponSocket.localPosition; socketBlendRotation = WeaponSocket.localRotation; }
            clip = next;
            clock = 0;
            blend = 0;
            Current = state;
        }
        public void Advance(float dt)
        {
            if (mesh == null || clip == null)
                return;
            PlaybackRate = (Current == "Walk" || Current == "Run") && clip.metresPerSecond > .01f ? Mathf.Clamp(movementSpeed / (clip.metresPerSecond * worldScale), 0, 3) : 1;
            clock += dt * PlaybackRate;
            blend = Mathf.Min(1, blend + dt * 9);
            float phase = clip.loop ? Mathf.Repeat(clock, clip.duration) : Mathf.Min(clock, clip.duration);
            float frame = phase / clip.duration * (clip.frames - 1);
            int a = Mathf.FloorToInt(frame), b = Mathf.Min(a + 1, clip.frames - 1);
            float t = frame - a;
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = Vector3.Lerp(blendFrom[i], Vector3.Lerp(clip.positions[a * positions.Length + i], clip.positions[b * positions.Length + i], t), blend);
                normals[i] = Vector3.Lerp(clip.normals[a * positions.Length + i], clip.normals[b * positions.Length + i], t).normalized;
            }
            if (WeaponSocket != null && clip.weaponPositions != null && clip.weaponPositions.Length == clip.frames)
            {
                WeaponSocket.localPosition = Vector3.Lerp(socketBlendPosition, Vector3.Lerp(clip.weaponPositions[a], clip.weaponPositions[b], t), blend);
                WeaponSocket.localRotation = Quaternion.Slerp(socketBlendRotation, Quaternion.Slerp(clip.weaponRotations[a], clip.weaponRotations[b], t), blend);
            }
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            if (Current == "Death" && clock >= clip.duration)
                stopped = true;
        }
        void LateUpdate()
        {
            Advance(Time.deltaTime);
        }
        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
