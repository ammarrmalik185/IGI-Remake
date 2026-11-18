using System.Collections.Generic;
using UnityEngine;

namespace IGIRebuilt
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class EnemyGuard : MonoBehaviour
    {
        public enum GuardState { Patrol, Investigate, Attack, Dead }
        public GuardState State { get; private set; }
        public Transform target;
        public MissionDirector mission;
        public GridNavigation navigation;
        public Vector3[] patrolPoints;
        public float sightRange = 27;
        public float damage = 8;
        public float fireRange = 23;
        public Transform visual;
        private CharacterController controller;
        private Health health;
        private List<Vector3> path = new List<Vector3>();
        private int pathIndex, patrolIndex;
        private Vector3 lastKnown;
        private float alertUntil, nextPath, nextShot, gravity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            health.Died += OnDeath;
            health.Damaged += _ => { if (target != null) Alert(target.position); };
        }
        public void Alert(Vector3 position)
        {
            if (health.IsDead) return;
            lastKnown = position;
            alertUntil = Time.time + 9;
            if (State != GuardState.Attack) State = GuardState.Investigate;
            nextPath = 0;
        }
        private void Update()
        {
            if (target == null || mission == null || mission.InputBlocked || health.IsDead) return;
            Vector3 eye = transform.position + Vector3.up * 1.55f;
            Vector3 point = target.position + Vector3.up * 1.25f;
            Vector3 toTarget = point - eye;
            bool close = toTarget.magnitude < sightRange;
            bool inCone = State != GuardState.Patrol || Vector3.Angle(transform.forward, toTarget) < 58;
            bool sees = close && inCone && Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, sightRange,
                (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.PlayerLayer), QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<FirstPersonMotor>() != null;
            if (sees)
            {
                State = GuardState.Attack;
                lastKnown = target.position;
                alertUntil = Time.time + 7;
                TurnTowards(target.position);
                if (Time.time >= nextShot && Vector3.Distance(target.position, transform.position) < fireRange)
                {
                    nextShot = Time.time + Random.Range(0.7f, 1.3f);
                    Vector3 aim = (point - eye).normalized;
                    aim = Quaternion.Euler(Random.Range(-2.3f, 2.3f), Random.Range(-2.3f, 2.3f), 0) * aim;
                    Vector3 end = eye + aim * sightRange;
                    if (Physics.Raycast(eye, aim, out RaycastHit shot, sightRange,
                        (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.PlayerLayer), QueryTriggerInteraction.Ignore))
                    {
                        end = shot.point;
                        shot.collider.GetComponentInParent<Health>()?.TakeDamage(damage, Team.Guard);
                    }
                    WeaponController.DrawTracer(eye + transform.forward * 0.45f, end, new Color(1, 0.3f, 0.15f), 0.07f);
                }
            }
            else if (Time.time > alertUntil) State = GuardState.Patrol;
            else State = GuardState.Investigate;
            Vector3 destination = State == GuardState.Patrol && patrolPoints != null && patrolPoints.Length > 0 ? patrolPoints[patrolIndex] : lastKnown;
            if (State == GuardState.Patrol && patrolPoints != null && patrolPoints.Length > 0 && Vector3.Distance(transform.position, destination) < 1.5f)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                destination = patrolPoints[patrolIndex];
                nextPath = 0;
            }
            bool shouldMove = !sees || Vector3.Distance(transform.position, target.position) > 15;
            if (shouldMove && navigation != null)
            {
                if (Time.time >= nextPath)
                {
                    path = navigation.FindPath(transform.position, destination);
                    pathIndex = 0;
                    nextPath = Time.time + 1.4f + Random.value * 0.4f;
                }
                if (pathIndex < path.Count)
                {
                    Vector3 direction = path[pathIndex] - transform.position;
                    direction.y = 0;
                    if (direction.magnitude < 0.4f) pathIndex++;
                    else
                    {
                        TurnTowards(path[pathIndex]);
                        controller.Move(direction.normalized * (State == GuardState.Patrol ? 1.65f : 3.1f) * Time.deltaTime);
                    }
                }
            }
            gravity = controller.isGrounded ? -2 : gravity - 17 * Time.deltaTime;
            controller.Move(Vector3.up * gravity * Time.deltaTime);
        }
        private void TurnTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6);
        }
        private void OnDeath(Health _)
        {
            State = GuardState.Dead;
            controller.enabled = false;
            if (visual != null)
            {
                visual.localRotation *= Quaternion.Euler(85, 0, 0);
                visual.position += Vector3.down * 0.65f;
            }
            mission?.GuardDown();
        }
    }
}
