using UnityEngine;
namespace IGIRebuilt
{
    public sealed class SecurityNetwork : MonoBehaviour
    {
        public MissionDirector mission; public AudioClip alarmClip; public bool CamerasDisabled
        {
            get; private set;
        }
        public bool AlarmsDisabled
        {
            get; private set;
        }
        public bool AlarmActive
        {
            get; private set;
        }
        public string Message
        {
            get; private set;
        }
        public float MessageUntil
        {
            get; private set;
        }
        public float Detection
        {
            get; private set;
        }
        AudioSource siren; float nextBroadcast; public AlarmPanel[] panels; public SecurityCamera[] cameras;
        public void Initialize(MissionDirector owner)
        {
            mission = owner;
            panels = GetComponentsInChildren<AlarmPanel>();
            cameras = GetComponentsInChildren<SecurityCamera>();
            siren = gameObject.AddComponent<AudioSource>();
            siren.clip = alarmClip;
            siren.loop = true;
            siren.spatialBlend = 0;
            siren.volume = .32f;
            foreach (var p in panels)
                p.network = this;
            foreach (var c in cameras)
                c.network = this;
            foreach (var c in GetComponentsInChildren<SecurityConsole>())
                c.network = this;
        }
        public void ReportDetection(float amount)
        {
            Detection = Mathf.Max(Detection, amount);
        }
        public bool RaiseAlarm(Vector3 knownPosition)
        {
            if (AlarmsDisabled || mission == null || mission.InputBlocked)
                return false;
            bool wasActive = AlarmActive;
            AlarmActive = true;
            if (!wasActive)
            {
                if (siren != null && alarmClip != null)
                    siren.Play();
                Notify("ALARM RAISED - red berets alerted");
            }
            if (Time.time >= nextBroadcast)
            {
                mission.AlertAlarmResponders(knownPosition);
                nextBroadcast = Time.time + 2;
            }
            return true;
        }
        public void Silence()
        {
            AlarmActive = false;
            if (siren != null)
                siren.Stop();
            Notify("ALARM SILENCED");
        }
        public void DisableCameras()
        {
            CamerasDisabled = true;
            foreach (var c in cameras)
                c.ClearDetection();
            Notify("SURVEILLANCE DISABLED - all cameras offline");
        }
        public void Notify(string text)
        {
            Message = text;
            MessageUntil = Time.time + 5;
        }
        public AlarmPanel NearestPanel(Vector3 at)
        {
            if (AlarmsDisabled || AlarmActive || panels == null)
                return null;
            AlarmPanel best = null;
            float distance = 75;
            foreach (var p in panels)
            {
                if (!p.enabled)
                    continue;
                float d = Vector3.Distance(at, p.transform.position);
                if (d < distance)
                {
                    distance = d;
                    best = p;
                }
            }
            return best;
        }
        void Update()
        {
            if (mission == null)
                return;
            if (siren != null)
                siren.mute = mission.Paused;
            if (mission.InputBlocked)
                return;
            Detection = Mathf.MoveTowards(Detection, 0, Time.deltaTime * .6f);
        }
    }
}
