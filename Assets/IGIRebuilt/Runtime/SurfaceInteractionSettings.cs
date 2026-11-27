using UnityEngine;

namespace IGIRebuilt
{
    public sealed class SurfaceInteractionSettings : ScriptableObject
    {
        public AudioClip[] fenceImpacts, glassBreaks;
        public Material shardMaterial;
    }
}
