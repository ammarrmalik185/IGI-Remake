using UnityEngine;
using System.Linq;
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
        public bool CanUseFrom(Vector3 feet)
        {
            var collider = GetComponentInChildren<Collider>();
            Vector3 button = collider != null ? collider.bounds.center : transform.position;
            if (Vector3.Distance(feet, button) > 2.1f) return false;
            Vector3 eye = feet + Vector3.up * 1.4f, delta = button - eye;
            bool Blocked(Vector3 from, Vector3 direction) => Physics.RaycastAll(from, direction, delta.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore).Any(hit => hit.collider.GetComponentInParent<AlarmPanel>() != this);
            return !Blocked(eye, delta.normalized) && !Blocked(button, -delta.normalized);
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
