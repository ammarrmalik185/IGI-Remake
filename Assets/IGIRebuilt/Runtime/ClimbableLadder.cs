using UnityEngine;

namespace IGIRebuilt
{
    public sealed class ClimbableLadder : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<ClimbableLadder> active = new System.Collections.Generic.List<ClimbableLadder>();
        public static System.Collections.Generic.IReadOnlyList<ClimbableLadder> Active => active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { active.Clear(); }
        private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        private void OnDisable() { active.Remove(this); }
        public Vector3 bottom, top, bottomExit, topExit, outward;
        public float climbSpeed = 2;
        public float mountDistance = 1.8f;
        public bool scriptedPassage;
        public Vector3 Bottom => transform.TransformPoint(bottom);
        public Vector3 Top => transform.TransformPoint(top);
        public Vector3 BottomExit => transform.TransformPoint(bottomExit);
        public Vector3 TopExit => transform.TransformPoint(topExit);
        public Vector3 Outward => transform.TransformDirection(outward).normalized;
        public bool CanMount(FirstPersonMotor player, out bool atTop)
        {
            float low = Vector3.Distance(player.transform.position, BottomExit);
            float high = Vector3.Distance(player.transform.position, TopExit);
            atTop = high < low;
            if (Mathf.Min(low, high) > mountDistance) return false;
            Vector3 target = atTop ? Top : Bottom;
            Vector3 facing = target - player.transform.position; facing.y = 0;
            Vector3 forward = player.transform.forward; forward.y = 0;
            if (facing.sqrMagnitude > 0.1f && Vector3.Dot(forward.normalized, facing.normalized) < 0.25f) return false;
            Vector3 approach = atTop ? TopExit : BottomExit;
            return !Physics.Linecast(player.eye.transform.position, approach + Vector3.up, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore);
        }
        public static bool ClearBody(Vector3 feet)
        {
            return !Physics.CheckCapsule(feet + Vector3.up * 0.34f, feet + Vector3.up * 1.51f, 0.29f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore);
        }
    }
}
