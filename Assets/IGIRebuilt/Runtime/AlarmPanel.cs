using UnityEngine;
namespace IGIRebuilt
{
    public sealed class AlarmPanel : MonoBehaviour
    {
        public SecurityNetwork network;
        public string Prompt => network != null && network.AlarmsDisabled ? "ALARMS OFFLINE" : "[ E ]  " + (network != null && network.AlarmActive ? "ALARM ACTIVE" : "ACTIVATE ALARM");
        public void Use()
        {
            if (network == null || network.AlarmsDisabled)
                return;
            network.RaiseAlarm(transform.position);
        }
        public static AlarmPanel Find(FirstPersonMotor p)
        {
            if (p == null || p.eye == null || p.IsClimbing)
                return null;
            if (Physics.Raycast(p.eye.transform.position, p.eye.transform.forward, out var hit, 2.8f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<AlarmPanel>();
            return null;
        }
    }
}
