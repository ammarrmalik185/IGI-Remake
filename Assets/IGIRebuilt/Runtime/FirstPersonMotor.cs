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

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }
        private void Update()
        {
            if (eye == null || health.IsDead || mission == null || mission.InputBlocked) return;
            Vector2 look = GameInput.Look * sensitivity;
            transform.Rotate(0, look.x, 0);
            Pitch = Mathf.Clamp(Pitch - look.y, -85, 85);
            eye.transform.localRotation = Quaternion.Euler(Pitch, 0, 0);
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
            if (transform.position.y < minimumHeight) health.TakeDamage(10000, Team.Guard);
        }
    }
}
