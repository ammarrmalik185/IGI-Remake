using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IGIRebuilt
{
    public sealed class MissionProgress
    {
        public bool HasIntel { get; private set; }
        public bool Complete { get; private set; }
        public bool AcquireIntel()
        {
            if (HasIntel || Complete) return false;
            HasIntel = true;
            return true;
        }
        public bool Extract(bool playerAlive, bool insideZone)
        {
            if (!HasIntel || !playerAlive || !insideZone || Complete) return false;
            Complete = true;
            return true;
        }
    }

    public sealed class MissionDirector : MonoBehaviour
    {
        public SecurityNetwork Security { get; set; }
        public FirstPersonMotor player;
        public WeaponController weapon;
        public GameConfiguration configuration;
        public Transform terminal;
        public Transform extraction;
        public MissionProgress Progress { get; private set; } = new MissionProgress();
        public bool Paused { get; private set; }
        public bool BriefingOpen { get; private set; }
        public bool Failed { get; private set; }
        public bool InputBlocked => Paused || Failed || Progress.Complete;
        public int GuardsRemaining { get; private set; }
        public int GuardsEliminated { get; private set; }
        public float DamageFlash { get; private set; }
        public float Elapsed { get; private set; }
        public bool CapturingTruck => configuration != null && configuration.captureTruck;
        public string Objective => CapturingTruck ? "STEAL THE TRUCK NEAR THE MAIN ENTRANCE" : !Progress.HasIntel ? "INFILTRATE THE CONTROL ROOM" : "RETURN TO EXTRACTION";
        public string InteractionPrompt => CapturingTruck ? "[ E ]  STEAL THE TRUCK" : "[ E ]  DOWNLOAD INTELLIGENCE";
        public string CompletionTitle => CapturingTruck ? "TRUCK SECURED" : "EXTRACTION COMPLETE";
        public string CompletionDetail => CapturingTruck ? "Trainyard infiltrated. The truck is secured." : "Intelligence secured. " + GuardsEliminated + " guards eliminated.";
        public Transform ObjectiveTarget => Progress.HasIntel ? extraction : terminal;
        private readonly List<EnemyGuard> guards = new List<EnemyGuard>();

        public bool CanInteract
        {
            get
            {
                if (player == null || player.IsClimbing || player.AvailableConsole != null || player.AvailableAlarm != null || player.AvailableDoor != null || player.AvailableLadder != null || terminal == null || Progress.HasIntel || InputBlocked) return false;
                Vector3 delta = terminal.position - player.eye.transform.position;
                bool close = delta.magnitude < 2.9f && Vector3.Dot(player.eye.transform.forward, delta.normalized) > 0.35f;
                if (CapturingTruck && close && Physics.Raycast(player.eye.transform.position, delta.normalized, Mathf.Max(0, delta.magnitude - 0.2f), 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) return false;
                return close;
            }
        }
        public void Initialize(FirstPersonMotor motor, WeaponController weapons)
        {
            player = motor;
            weapon = weapons;
            var health = motor.GetComponent<Health>();
            health.Died += _ => { Failed = true; UnlockCursor(); };
            health.Damaged += _ => DamageFlash = 0.55f;
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (CapturingTruck) { BriefingOpen = true; SetPaused(true); }
        }
        public void RegisterGuard(EnemyGuard guard)
        {
            guards.Add(guard);
            GuardsRemaining++;
        }
        public void GuardDown()
        {
            GuardsRemaining = Mathf.Max(0, GuardsRemaining - 1);
            GuardsEliminated++;
        }
        public void MakeNoise(Vector3 point, float radius)
        {
            foreach (EnemyGuard guard in guards)
                if (guard != null) guard.HearNoise(point, radius);
        }
        public void AlertAlarmResponders(Vector3 point)
        {
            foreach (var guard in guards)
                if (guard != null && guard.respondsToAlarm) guard.ReceiveAlarm(point);
        }
        public void AlertGuards(Vector3 point, float radius, EnemyGuard source = null)
        {
            foreach (var guard in guards)
                if (guard != null && guard != source && Vector3.Distance(guard.transform.position, source != null ? source.transform.position : point) < radius) guard.Alert(point);
        }
        private void Update()
        {
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0, Time.unscaledDeltaTime * 1.2f);
            if (BriefingOpen && GameInput.Pressed(KeyCode.Return)) SetPaused(false);
            if (GameInput.Pressed(KeyCode.Escape) && !Failed && !Progress.Complete) SetPaused(!Paused);
            if ((Failed || Progress.Complete) && GameInput.Pressed(KeyCode.R)) Restart();
            if (InputBlocked || player == null) return;
            Elapsed += Time.deltaTime;
            if (CanInteract && GameInput.Pressed(KeyCode.E)) CollectIntel();
            if (Progress.HasIntel && extraction != null && Vector3.Distance(player.transform.position, extraction.position) < 3.5f)
            {
                if (Progress.Extract(!player.GetComponent<Health>().IsDead, true)) UnlockCursor();
            }
        }
        public bool CollectIntel()
        {
            if (CapturingTruck && (!CanInteract || player.GetComponent<Health>().IsDead)) return false;
            if (InputBlocked || !Progress.AcquireIntel()) return false;
            if (CapturingTruck) { Progress.Extract(true, true); UnlockCursor(); return true; }
            MakeNoise(terminal != null ? terminal.position : player.transform.position, 100);
            if (terminal != null)
                foreach (Light light in terminal.GetComponentsInChildren<Light>()) light.color = Color.green;
            return true;
        }
        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (!paused) BriefingOpen = false;
            Time.timeScale = paused ? 0 : 1;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        private void OnDestroy() { Time.timeScale = 1; UnlockCursor(); }
    }
}
