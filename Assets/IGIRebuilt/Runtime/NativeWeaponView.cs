using System;
using UnityEngine;

namespace IGIRebuilt
{
    [Serializable] public sealed class WeaponPoseFrame { public Vector3[] positions; public Quaternion[] rotations; }
    [Serializable] public sealed class WeaponPoseClip { public int id; public float duration, releaseSeconds; public WeaponPoseFrame[] frames; }

    // Native rig 005: body on bone 3, magazine/second Uzi on 52, slide/bolt on 55.
    public sealed class NativeWeaponView : MonoBehaviour
    {
        public Transform[] anchors;
        public WeaponPoseClip idle, fire, reload;
        public int partCount;
        private float idleTime, actionTime, reloadDuration;
        private bool firing;

        public void Fire() { firing = true; actionTime = 0; }
        public void Reload(float duration) { firing = false; actionTime = 0; reloadDuration = duration; }
        public void Tick(float dt, bool reloading, WeaponAction action)
        {
            idleTime += dt;
            actionTime += dt;
            if (reloading && action == WeaponAction.Hitscan && Valid(reload))
                Sample(reload, actionTime / Mathf.Max(.1f, reloadDuration) * reload.duration);
            else if (firing && Valid(fire) && actionTime < fire.duration) Sample(fire, actionTime);
            else { firing = false; Sample(idle, idleTime % Mathf.Max(.01f, idle.duration)); }
        }
        public void ResetPose() { firing = false; idleTime = actionTime = 0; Sample(idle, 0); }
        public static bool Valid(WeaponPoseClip clip) => clip != null && clip.frames != null && clip.frames.Length > 0;
        public void Sample(WeaponPoseClip clip, float time)
        {
            if (!Valid(clip)) return;
            float frame = Mathf.Clamp01(time / Mathf.Max(.001f, clip.duration)) * (clip.frames.Length - 1);
            int a = Mathf.FloorToInt(frame), b = Mathf.Min(a + 1, clip.frames.Length - 1);
            for (int i = 0; i < anchors.Length; i++)
            {
                anchors[i].localPosition = Vector3.Lerp(clip.frames[a].positions[i], clip.frames[b].positions[i], frame - a);
                anchors[i].localRotation = Quaternion.Slerp(clip.frames[a].rotations[i], clip.frames[b].rotations[i], frame - a);
            }
        }
    }
}
