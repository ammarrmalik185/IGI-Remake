using System;
using UnityEngine;

namespace IGIRebuilt
{
    [Serializable] public struct NativeSkinWeight
    {
        public int vertex, bone;
        public float weight;
        public Vector3 position, normal;
    }
    [Serializable] public sealed class NativeSkinClip
    {
        public string name;
        public float duration;
        public int frames;
        public Vector3[] positions;
        public Quaternion[] rotations;
    }
    public sealed class NativeSkinData : ScriptableObject
    {
        public int boneCount;
        public NativeSkinWeight[] weights;
        public NativeSkinClip[] clips;
        public NativeSkinClip Find(string name)
        {
            foreach (var clip in clips) if (clip.name == name) return clip;
            return null;
        }
    }
}
