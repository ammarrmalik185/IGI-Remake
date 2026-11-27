using System;
using UnityEngine;

namespace IGIRebuilt
{
    // Native MEF influences store a separate bone-local vertex for each weight.
    // Reuse the palettes and vertex buffers; no mesh/material allocation during animation.
    public sealed class NativeSkinRenderer : MonoBehaviour
    {
        public NativeSkinData data;
        private Mesh mesh;
        private Vector3[] positions, normals, bonePositions;
        private Quaternion[] boneRotations;
        public NativeSkinClip Current { get; private set; }
        public float SampleTime { get; private set; }
        public Vector3 BonePosition(int bone) => bonePositions[bone];
        public Quaternion BoneRotation(int bone) => boneRotations[bone];

        public void Sample(string name, float seconds, bool loop = false)
        {
            var clip = data.Find(name);
            if (clip == null) return;
            if (mesh == null)
            {
                var filter = GetComponent<MeshFilter>();
                mesh = Instantiate(filter.sharedMesh);
                mesh.name += " player animated instance";
                mesh.MarkDynamic(); filter.sharedMesh = mesh;
                positions = new Vector3[mesh.vertexCount]; normals = new Vector3[mesh.vertexCount];
                bonePositions = new Vector3[data.boneCount]; boneRotations = new Quaternion[data.boneCount];
            }
            Current = clip; SampleTime = seconds;
            float phase = loop ? Mathf.Repeat(seconds, Mathf.Max(.001f, clip.duration)) : Mathf.Clamp(seconds, 0, clip.duration);
            float frame = phase / Mathf.Max(.001f, clip.duration) * (clip.frames - 1);
            int a = Mathf.FloorToInt(frame) * data.boneCount, b = Mathf.Min(Mathf.FloorToInt(frame) + 1, clip.frames - 1) * data.boneCount;
            float t = frame - Mathf.Floor(frame);
            for (int i = 0; i < data.boneCount; i++)
            {
                bonePositions[i] = Vector3.Lerp(clip.positions[a + i], clip.positions[b + i], t);
                boneRotations[i] = Quaternion.Slerp(clip.rotations[a + i], clip.rotations[b + i], t);
            }
            Array.Clear(positions, 0, positions.Length); Array.Clear(normals, 0, normals.Length);
            foreach (var w in data.weights)
            {
                positions[w.vertex] += (boneRotations[w.bone] * w.position + bonePositions[w.bone]) * w.weight;
                normals[w.vertex] += (boneRotations[w.bone] * w.normal) * w.weight;
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].normalized;
            mesh.vertices = positions; mesh.normals = normals; mesh.RecalculateBounds();
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
