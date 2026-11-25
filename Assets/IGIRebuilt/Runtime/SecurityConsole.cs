using UnityEngine;
namespace IGIRebuilt
{
    public sealed class SecurityConsole : MonoBehaviour
    {
        public SecurityNetwork network; [HideInInspector] public bool alarmControl; // Legacy serialized field; every computer now controls cameras.
        public float hackSeconds = 5; public float Progress
        {
            get; private set;
        }
        public bool Complete => network != null && network.CamerasDisabled;
        public string Prompt => Complete ? "SYSTEM OFFLINE" : "HOLD E - DISABLE " + "CAMERAS" + "  " + Mathf.FloorToInt(Progress * 100) + "%";
        public void AdvanceHack(bool held, float dt)
        {
            if (network == null || network.mission == null || network.mission.InputBlocked || Complete)
                return;
            Progress = held ? Mathf.Clamp01(Progress + dt / Mathf.Max(.1f, hackSeconds)) : 0;
            if (Progress >= 1)
            {
                network.DisableCameras();
            }
        }
        void Update()
        {
            var m = network != null ? network.mission : null;
            if (m == null)
                return;
            bool focused = m.player != null && !m.player.IsClimbing && Find(m.player) == this;
            AdvanceHack(focused && GameInput.UseHeld, Time.deltaTime);
        }
        public static SecurityConsole Find(FirstPersonMotor p)
        {
            if (p == null || p.eye == null || p.IsClimbing)
                return null;
            if (Physics.Raycast(p.eye.transform.position, p.eye.transform.forward, out var hit, 2.8f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<SecurityConsole>();
            return null;
        }
    }
}
