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
        [NonSerialized] private bool boundsReady;
        [NonSerialized] private Bounds animationBounds;
        public Bounds AnimationBounds
        {
            get
            {
                if (boundsReady) return animationBounds;
                bool found = false;
                foreach (var clip in clips)
                    foreach (var point in clip.positions)
                    {
                        if (!found) { animationBounds = new Bounds(point, Vector3.zero); found = true; }
                        else animationBounds.Encapsulate(point);
                    }
                boundsReady = true;
                return animationBounds;
            }
        }
    }
}
