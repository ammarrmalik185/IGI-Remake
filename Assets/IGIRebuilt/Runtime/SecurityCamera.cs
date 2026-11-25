using System.Linq;
using UnityEngine;
namespace IGIRebuilt
{
    public sealed class SecurityCamera : MonoBehaviour
    {
        public SecurityNetwork network; public Transform head; public Vector3 lens; public float left, right, speed = 15, pause = 4, range = 25, horizontalFov = 55, verticalFov = 90; public Light indicator;
        public float Detection
        {
            get; private set;
        }
        public bool Destroyed
        {
            get; private set;
        }
        float yaw, direction = 1, wait; Quaternion rest; bool initialized;
        public void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            rest = head.localRotation;
            var health = GetComponent<Health>();
            if (health != null)
                health.Died += _ => { Destroyed = true; ClearDetection(); head.localRotation *= Quaternion.Euler(35, 0, 15); };
        }
        public void ClearDetection()
        {
            Detection = 0;
        }
        public bool CanSee(Vector3 point)
        {
            if (Destroyed || head == null || (network != null && network.CamerasDisabled))
                return false;
            Vector3 at = head.TransformPoint(lens), local = head.InverseTransformDirection(point - at);
            if (local.z <= 0 || local.magnitude > range || Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) > horizontalFov * .5f || Mathf.Abs(Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg) > verticalFov * .5f)
                return false;
            var delta = point - at;
            return !Physics.RaycastAll(at, delta.normalized, delta.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore).Any(h => h.collider.GetComponentInParent<SecurityCamera>() != this);
        }
        public void Tick(float dt)
        {
            if (head == null || network == null || network.mission == null)
                return;
            var m = network.mission;
            if (m.InputBlocked)
                return;
            bool online = !Destroyed && !network.CamerasDisabled;
            if (online)
            {
                if (wait > 0)
                    wait -= dt;
                else if (right + left > 0)
                {
                    yaw += direction * speed * dt;
                    if (yaw >= right || yaw <= -left)
                    {
                        yaw = Mathf.Clamp(yaw, -left, right);
                        direction = -direction;
                        wait = pause;
                    }
                    head.localRotation = rest * Quaternion.Euler(0, -yaw, 0);
                }
                var player = m.player;
                bool sees = player != null && !player.GetComponent<Health>().IsDead && CanSee(player.transform.position + Vector3.up * (player.IsCrouching ? .75f : 1.25f));
                Detection = Mathf.Clamp01(Detection + dt * (sees ? 1.1f : -.7f));
                network.ReportDetection(Detection);
                if (Detection >= 1 && sees)
                    network.RaiseAlarm(player.transform.position);
            }
            else
                Detection = 0;
            if (indicator != null)
            {
                indicator.enabled = online;
                indicator.color = Detection > .1f ? Color.red : Color.green;
            }
        }
        void Start()
        {
            Initialize();
        }
        void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
