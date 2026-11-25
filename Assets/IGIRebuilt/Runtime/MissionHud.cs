using UnityEngine;

namespace IGIRebuilt
{
    public sealed class MissionHud : MonoBehaviour
    {
        public MissionDirector mission;
        private GUIStyle small, normal, heading, ammo, centred;
        private readonly Color accent = new Color(0.37f, 0.91f, 0.75f);

        private void Initialize()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            small = new GUIStyle(GUI.skin.label) { font = font, fontSize = 12, normal = { textColor = new Color(0.72f, 0.79f, 0.8f) } };
            normal = new GUIStyle(small) { fontSize = 17, normal = { textColor = Color.white } };
            heading = new GUIStyle(normal) { fontSize = 22, fontStyle = FontStyle.Bold };
            ammo = new GUIStyle(heading) { fontSize = 38, alignment = TextAnchor.MiddleRight };
            centred = new GUIStyle(normal) { alignment = TextAnchor.MiddleCenter };
        }
        private void OnGUI()
        {
            if (mission == null || mission.player == null) return;
            if (small == null) Initialize();
            float scale = Mathf.Max(0.65f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            Health health = mission.player.GetComponent<Health>();
            WeaponController weapon = mission.weapon;
            Box(new Rect(28, 26, 370, 101), new Color(0.035f, 0.06f, 0.07f, 0.88f));
            Box(new Rect(28, 26, 4, 101), accent);
            GUI.Label(new Rect(47, 39, 330, 20), mission.configuration.missionName, small);
            GUI.Label(new Rect(47, 63, 330, 28), mission.CapturingTruck ? "01  /  STEAL THE TRUCK" : mission.Progress.HasIntel ? "02  /  EXTRACT" : "01  /  ACQUIRE INTELLIGENCE", normal);
            GUI.Label(new Rect(47, 96, 330, 20), mission.CapturingTruck ? "Find the truck near the main entrance." : mission.Progress.HasIntel ? "Return to the marked southern gate." : "Find the terminal inside the control room.", small);
            GUI.Label(new Rect(width - 250, 34, 220, 25), "GUARDS ACTIVE  " + mission.GuardsRemaining.ToString("00"), normal);
            GUI.Label(new Rect(width - 250, 65, 220, 20), "ELAPSED  " + Mathf.FloorToInt(mission.Elapsed / 60).ToString("00") + ":" + Mathf.FloorToInt(mission.Elapsed % 60).ToString("00"), small);
            if (mission.Security != null) {
                var security = mission.Security;
                GUI.Label(new Rect(28, 135, 600, 24), security.AlarmActive ? "ALARM ACTIVE" : security.AlarmsDisabled ? "ALARMS OFFLINE" : "ALARMS ARMED", normal);
                GUI.Label(new Rect(28, 160, 600, 22), security.CamerasDisabled ? "CAMERAS OFFLINE" : "CAMERAS ACTIVE" + (security.Detection > .05f ? " ? DETECTION " + Mathf.RoundToInt(security.Detection * 100) + "%" : ""), small);
                if (Time.time < security.MessageUntil) GUI.Label(new Rect(width/2-350, 160, 700, 28), security.Message, centred);
            }
            Box(new Rect(28, height - 118, 245, 78), new Color(0.035f, 0.06f, 0.07f, 0.88f));
            GUI.Label(new Rect(44, height - 108, 140, 22), "OPERATOR", small);
            GUI.Label(new Rect(193, height - 108, 64, 25), Mathf.CeilToInt(health.Current).ToString("000"), normal);
            Box(new Rect(44, height - 72, 212, 7), new Color(0.22f, 0.25f, 0.27f));
            Box(new Rect(44, height - 72, 212 * health.Current / health.maximum, 7), health.Current > 30 ? accent : new Color(1, 0.35f, 0.3f));
            if (weapon != null && weapon.Active != null && weapon.Ammo != null)
            {
                Box(new Rect(width - 280, height - 133, 252, 94), new Color(0.035f, 0.06f, 0.07f, 0.88f));
                GUI.Label(new Rect(width - 263, height - 122, 220, 25), weapon.Active.displayName.ToUpperInvariant(), small);
                GUI.Label(new Rect(width - 252, height - 100, 130, 49), weapon.Active.melee ? "--" : weapon.Ammo.Loaded.ToString("00"), ammo);
                GUI.Label(new Rect(width - 110, height - 82, 75, 30), "/ " + weapon.Ammo.Reserve.ToString("000"), normal);
                if (weapon.IsReloading) GUI.Label(new Rect(width / 2 - 130, height / 2 + 43, 260, 26), "RELOADING", centred);
            }
            GUI.Label(new Rect(width / 2 - 330, height - 32, 660, 24), "WASD  MOVE    SHIFT  SPRINT    C  CROUCH    R  RELOAD    1 / 2 / 3  WEAPONS    ESC  PAUSE", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            if (!mission.InputBlocked)
            {
                float x = width / 2, y = height / 2;
                Color colour = weapon != null && Time.time < weapon.HitMarkerUntil ? accent : new Color(1, 1, 1, 0.8f);
                Box(new Rect(x - 10, y, 6, 1.5f), colour);
                Box(new Rect(x + 4, y, 6, 1.5f), colour);
                Box(new Rect(x, y - 10, 1.5f, 6), colour);
                Box(new Rect(x, y + 4, 1.5f, 6), colour);
                DrawObjective(width, height, scale);
                if (mission.player.IsClimbing || mission.player.AvailableLadder != null || mission.player.AvailableConsole != null || mission.player.AvailableAlarm != null || mission.player.AvailableDoor != null || mission.CanInteract)
                {
                    Box(new Rect(x - 155, y + 70, 310, 44), new Color(0.035f, 0.06f, 0.07f, 0.94f));
                    string prompt = mission.player.IsClimbing ? "W/S  CLIMB   E/SPACE  LET GO" : mission.player.AvailableLadder != null ? "[ E ]  CLIMB LADDER" : mission.player.AvailableConsole != null ? mission.player.AvailableConsole.Prompt : mission.player.AvailableAlarm != null ? mission.player.AvailableAlarm.Prompt : mission.player.AvailableDoor != null ? "[ E ]  " + (mission.player.AvailableDoor.IsOpen ? "CLOSE " : "OPEN ") + (mission.player.AvailableDoor.isGate ? "GATE" : "DOOR") : mission.InteractionPrompt;
                    GUI.Label(new Rect(x - 145, y + 78, 290, 27), prompt, centred);
                }
            }
            if (mission.DamageFlash > 0)
            {
                Color damage = new Color(0.8f, 0.05f, 0.02f, mission.DamageFlash * 0.65f);
                Box(new Rect(0, 0, width, 12), damage);
                Box(new Rect(0, height - 12, width, 12), damage);
                Box(new Rect(0, 0, 12, height), damage);
                Box(new Rect(width - 12, 0, 12, height), damage);
            }
            if (mission.InputBlocked)
            {
                Box(new Rect(0, 0, width, height), new Color(0.015f, 0.025f, 0.035f, 0.8f));
                Rect panel = new Rect(width / 2 - 220, height / 2 - 145, 440, 290);
                Box(panel, new Color(0.055f, 0.08f, 0.095f));
                string title = mission.Progress.Complete ? mission.CompletionTitle : mission.Failed ? "MISSION FAILED" : mission.BriefingOpen ? "TRAINYARD" : "PAUSED";
                GUI.Label(new Rect(panel.x + 25, panel.y + 32, 390, 40), title, new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter });
                string detail = mission.Progress.Complete ? mission.CompletionDetail : mission.Failed ? "Your operator was killed. Press R to retry." : mission.BriefingOpen ? mission.configuration.briefing : mission.CapturingTruck ? "Infiltrate the trainyard and secure the truck. WASD / mouse to move, E to interact." : "Swap models and tuning through WeaponDefinition assets.";
                GUI.Label(new Rect(panel.x + 24, panel.y + 78, 392, 96), detail, new GUIStyle(centred) { wordWrap = true, fontSize = 15 });
                if (mission.Paused && GUI.Button(new Rect(panel.x + 80, panel.y + 179, 280, 36), mission.BriefingOpen ? "BEGIN MISSION" : "RESUME")) mission.SetPaused(false);
                if (GUI.Button(new Rect(panel.x + 80, panel.y + 225, 280, 36), "RESTART MISSION")) mission.Restart();
            }
            GUI.matrix = Matrix4x4.identity;
        }
        private void DrawObjective(float width, float height, float scale)
        {
            if (mission.ObjectiveTarget == null) return;
            Vector3 world = mission.ObjectiveTarget.position + Vector3.up * 1.3f;
            Vector3 screen = mission.player.eye.WorldToScreenPoint(world);
            if (screen.z < 0) { screen.x = Screen.width - screen.x; screen.y = Screen.height * 0.5f; }
            float x = Mathf.Clamp(screen.x / scale, 50, width - 50);
            float y = Mathf.Clamp((Screen.height - screen.y) / scale, 155, height - 175);
            Box(new Rect(x - 5, y - 5, 10, 10), accent);
            int metres = Mathf.RoundToInt(Vector3.Distance(mission.player.transform.position, mission.ObjectiveTarget.position));
            GUI.Label(new Rect(x - 50, y + 12, 100, 22), metres + " m", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = accent } });
        }
        private static void Box(Rect rect, Color colour)
        {
            Color previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}


