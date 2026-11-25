using System;
using UnityEngine;
namespace IGIRebuilt
{
    [Serializable]
    public sealed class GuardMotionClip
    {
        public string name; public float duration; public float metresPerSecond; public bool loop; public int frames; public Vector3[] positions, normals; public Vector3[] weaponPositions; public Quaternion[] weaponRotations;
    }
    public sealed class GuardMotionData : ScriptableObject
    {
        public int vertexCount; public GuardMotionClip[] clips;
    }
}
