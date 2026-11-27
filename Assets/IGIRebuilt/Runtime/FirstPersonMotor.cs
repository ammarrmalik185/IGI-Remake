using UnityEngine;

namespace IGIRebuilt
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        public Camera eye;
        public MissionDirector mission;
        public float walkSpeed = 4.6f;
        public float sprintSpeed = 7.4f;
        public float crouchSpeed = 2.4f;
        public float sensitivity = 1;
        public float minimumHeight = -12;
        public bool IsCrouching { get; private set; }
        public float Pitch { get; private set; }
        private CharacterController controller;
        private Health health;
        private float velocityY;
        public ClimbableLadder ActiveLadder { get; private set; }
        public bool IsClimbing => ActiveLadder != null;
        public ClimbableLadder AvailableLadder { get; private set; }
        public SecurityConsole AvailableConsole => SecurityConsole.Find(this);
        public AlarmPanel AvailableAlarm => AlarmPanel.Find(this);
        private float nextFootstepNoise;
        public InteractiveDoor AvailableDoor => AvailableLadder == null ? InteractiveDoor.Find(this) : null;
        private float climbFraction;
        private bool mounting, exiting, exitAtTop;

        public bool TryClimb(ClimbableLadder ladder)
        {
            if (ladder == null || IsClimbing || health.IsDead || mission == null || mission.InputBlocked || !ladder.CanMount(this, out bool atTop)) return false;
            Vector3 target = atTop ? ladder.Top : ladder.Bottom;
            if ((!ladder.scriptedPassage && !ClimbableLadder.ClearBody(target)) || !ClimbableLadder.ClearBody(transform.position)) return false;
            ActiveLadder = ladder; climbFraction = atTop ? 1 : 0;
            mounting = true; exiting = false; IsCrouching = false; velocityY = 0;
            controller.enabled = false;
            controller.height = 1.85f; controller.center = Vector3.up * 0.925f;
            Vector3 eyePosition = eye.transform.localPosition; eyePosition.y = 1.73f; eye.transform.localPosition = eyePosition;
            AvailableLadder = null;
            transform.rotation = Quaternion.LookRotation(-ladder.Outward);
            if (mission.weapon != null) mission.weapon.modelMount.gameObject.SetActive(false);
            return true;
        }
        public void AdvanceClimb(float input, float deltaTime)
        {
            if (!IsClimbing || mission.InputBlocked || health.IsDead || deltaTime <= 0) return;
            Vector3 target;
            if (mounting)
            {
                target = Vector3.Lerp(ActiveLadder.Bottom, ActiveLadder.Top, climbFraction);
                if (target.y > transform.position.y + 0.03f) target = new Vector3(transform.position.x, target.y, transform.position.z);
            }
            else if (exiting)
            {
                target = exitAtTop ? ActiveLadder.TopExit : ActiveLadder.BottomExit;
                // Cross the roof edge before lowering the feet onto the landing.
                if (exitAtTop && Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(target.x, target.z)) > 0.03f)
                    target.y = Mathf.Max(transform.position.y, ActiveLadder.Top.y);
            }
            else
            {
                climbFraction = Mathf.Clamp01(climbFraction + Mathf.Clamp(input, -1, 1) * ActiveLadder.climbSpeed * deltaTime / Vector3.Distance(ActiveLadder.Bottom, ActiveLadder.Top));
                target = Vector3.Lerp(ActiveLadder.Bottom, ActiveLadder.Top, climbFraction);
            }
            Vector3 next = Vector3.MoveTowards(transform.position, target, ActiveLadder.climbSpeed * 1.5f * deltaTime);
            if (!ActiveLadder.scriptedPassage && !ClimbableLadder.ClearBody(next)) return;
            transform.position = next;
            if (Vector3.Distance(next, target) > 0.015f) return;
            if (mounting) { mounting = Vector3.Distance(next, Vector3.Lerp(ActiveLadder.Bottom, ActiveLadder.Top, climbFraction)) > 0.03f; return; }
            if (exiting)
            {
                Vector3 landing = exitAtTop ? ActiveLadder.TopExit : ActiveLadder.BottomExit;
                if (Vector3.Distance(transform.position, landing) < 0.03f && ClimbableLadder.ClearBody(landing)) EndClimb();
                return;
            }
            if ((input > 0 && climbFraction >= 1) || (input < 0 && climbFraction <= 0)) { exiting = true; exitAtTop = input > 0; }
        }
        public bool LetGoLadder()
        {
            if (!IsClimbing) return false;
            Vector3 release = transform.position + ActiveLadder.Outward * 0.25f;
            if (!ClimbableLadder.ClearBody(release)) return false;
            transform.position = release; EndClimb(); return true;
        }
        private void EndClimb()
        {
            ActiveLadder = null; mounting = exiting = false; velocityY = 0;
            controller.height = 1.85f; controller.center = Vector3.up * 0.925f; controller.enabled = true;
            if (mission != null && mission.weapon != null) mission.weapon.modelMount.gameObject.SetActive(true);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }
        private void Update()
        {
            if (health.IsDead && IsClimbing) EndClimb();
            if (eye == null || health.IsDead || mission == null || mission.InputBlocked) return;
            Vector2 look = GameInput.Look * sensitivity;
            transform.Rotate(0, look.x, 0);
            Pitch = Mathf.Clamp(Pitch - look.y, -85, 85);
            eye.transform.localRotation = Quaternion.Euler(Pitch, 0, 0);
            if (IsClimbing)
            {
                if (GameInput.Pressed(KeyCode.E) || GameInput.Pressed(KeyCode.Space)) LetGoLadder();
                else AdvanceClimb(GameInput.Move.y, Time.deltaTime);
                return;
            }
            AvailableLadder = null; float nearest = float.MaxValue;
            for (int i = 0; i < ClimbableLadder.Active.Count; i++)
            {
                var ladder = ClimbableLadder.Active[i];
                if (ladder.CanMount(this, out bool atTop))
                {
                    float distance = Vector3.Distance(transform.position, atTop ? ladder.TopExit : ladder.BottomExit);
                    if (distance < nearest) { nearest = distance; AvailableLadder = ladder; }
                }
            }
            if (AvailableLadder != null && GameInput.Pressed(KeyCode.E) && TryClimb(AvailableLadder)) return;
            var alarm = AvailableAlarm;
            if (alarm != null && GameInput.Pressed(KeyCode.E)) alarm.Use();
            var door = AvailableDoor;
            if (door != null && GameInput.Pressed(KeyCode.E)) door.Toggle();
            if (GameInput.Pressed(KeyCode.C) || GameInput.Pressed(KeyCode.LeftControl))
            {
                // Test the space above before allowing the capsule to stand.
                if (!IsCrouching || !Physics.CheckSphere(transform.position + Vector3.up * 1.55f, 0.28f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore))
                    IsCrouching = !IsCrouching;
            }
            float height = IsCrouching ? 1.15f : 1.85f;
            controller.height = height;
            controller.center = Vector3.up * height * 0.5f;
            Vector3 eyePosition = eye.transform.localPosition;
            eyePosition.y = Mathf.Lerp(eyePosition.y, height - 0.12f, 14 * Time.deltaTime);
            eye.transform.localPosition = eyePosition;
            Vector2 move = GameInput.Move;
            float speed = IsCrouching ? crouchSpeed : GameInput.Sprint && !GameInput.Aim ? sprintSpeed : walkSpeed;
            if (controller.isGrounded && velocityY < 0) velocityY = -2;
            if (controller.isGrounded && !IsCrouching && GameInput.Pressed(KeyCode.Space)) velocityY = 5.5f;
            velocityY -= 17 * Time.deltaTime;
            Vector3 motion = (transform.right * move.x + transform.forward * move.y) * speed;
            motion.y = velocityY;
            controller.Move(motion * Time.deltaTime);
            if (controller.isGrounded && move.sqrMagnitude > .1f && Time.time >= nextFootstepNoise)
            {
                mission.MakeNoise(transform.position + Vector3.up, IsCrouching ? 2 : speed > walkSpeed ? 15 : 7);
                nextFootstepNoise = Time.time + (speed > walkSpeed ? .3f : .48f);
            }
            if (transform.position.y < minimumHeight) health.TakeDamage(10000, Team.Guard);
        }
    }
}
