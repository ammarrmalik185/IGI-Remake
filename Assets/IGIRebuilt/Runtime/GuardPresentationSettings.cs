using System;
using UnityEngine;
namespace IGIRebuilt
{
    [Serializable]
    public sealed class GuardMotionBinding
    {
        public string model; public GuardMotionData motion;
    }
    public sealed class GuardPresentationSettings : ScriptableObject
    {
        public GuardMotionBinding[] motions; public AudioClip shot, reload; public AudioClip[] footsteps, hits, deaths; public GameObject rifle;
        public GuardMotionData ForModel(string model)
        {
            if (motions == null)
                return null;
            var b = Array.Find(motions, m => m.model == model);
            return b != null ? b.motion : null;
        }
    }
}
