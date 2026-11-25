using System.Collections.Generic;
using UnityEngine;
namespace IGIRebuilt
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class EnemyGuard : MonoBehaviour
    {
        public enum GuardState
        {
            Patrol, Investigate, Attack, Search, RaiseAlarm, Dead
        }
        public GuardState State
        {
            get; private set;
        }
        public bool respondsToAlarm;
        public AuthoredNavigation authoredNavigation; public int graphId;
        public GuardPatrolCommand[] authoredPatrol, authoredAlarm;
        private int patrolCommand, alarmCommand; private float commandWait; private bool alarmResponding, patrolFinished, patrolRunning;
        public Transform target; public MissionDirector mission; public GridNavigation navigation; public Vector3[] patrolPoints; public float sightRange = 45, damage = 8, fireRange = 45; public Transform visual;
        public float Suspicion
        {
            get; private set;
        }
        public Vector3 LastKnown => lastKnown; public bool HasSight
        {
            get; private set;
        }
        CharacterController controller; Health health; GuardPresentation presentation; List<Vector3> path = new List<Vector3>(); int pathIndex, patrolIndex, burst, rounds = 24; Vector3 lastKnown, lastPosition; float alertUntil, nextPath, nextShot, gravity, searchUntil, patrolWait, stuck, nextShare, confirmedAt = -1, reloadUntil, firstShotAt, alarmAttemptUntil, nextAlarm; AlarmPanel alarmTarget;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            health.Died += OnDeath;
            health.Attacked += origin => ReceiveAttack(origin ?? (target != null ? target.position : transform.position + transform.forward));
        }
        public void ReceiveAttack(Vector3 position)
        {
            if (health.IsDead) return;
            Alert(position); Suspicion = 1; State = GuardState.Attack;
            firstShotAt = Time.time + .25f;
            Vector3 direction = position - transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
            mission?.AlertGuards(position, 25, this);
        }
        public void ReceiveAlarm(Vector3 position)
        {
            if (!respondsToAlarm || health.IsDead) return;
            if (!alarmResponding) { alarmCommand = 0; commandWait = 0; nextPath = 0; }
            alarmResponding = true;
            Alert(position);
        }
        public void Alert(Vector3 position)
        {
            if (health.IsDead)
                return;
            lastKnown = position;
            searchUntil = 0;
            alertUntil = Time.time + 16;
            if (State != GuardState.Attack && State != GuardState.RaiseAlarm)
                State = GuardState.Investigate;
            nextPath = 0;
        }
        public bool HearNoise(Vector3 position, float radius)
        {
            if (health.IsDead)
                return false;
            float distance = Vector3.Distance(transform.position, position);
            if (distance > radius)
                return false;
            bool occluded = Physics.Linecast(transform.position + Vector3.up * 1.5f, position, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore);
            float effective = occluded ? radius * .55f : radius;
            if (distance > effective)
                return false;
            if (!HasSight)
            {
                Alert(position);
                Suspicion = Mathf.Max(Suspicion, .45f);
            }
            return true;
        }
        public bool CanSee(Vector3 point)
        {
            Vector3 eye = transform.position + Vector3.up * 1.55f, delta = point - eye;
            float range = sightRange;
            if (mission != null && mission.player != null && mission.player.IsCrouching)
                range *= .7f;
            if (delta.magnitude > range || Vector3.Angle(transform.forward, delta) > (State == GuardState.Patrol ? 55 : 80))
                return false;
            return Physics.Raycast(eye, delta.normalized, out var hit, delta.magnitude + .2f, (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.PlayerLayer), QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<FirstPersonMotor>() != null;
        }
        void Update()
        {
            Tick(Time.deltaTime);
        }
        public void Tick(float dt)
        {
            if (target == null || mission == null || mission.InputBlocked || health.IsDead || !controller.enabled)
                return;
            presentation = GetComponent<GuardPresentation>();
            Vector3 point = target.position + Vector3.up * (mission.player != null && mission.player.IsCrouching ? .75f : 1.25f);
            HasSight = !target.GetComponent<Health>().IsDead && CanSee(point);
            Suspicion = Mathf.Clamp01(Suspicion + dt * (HasSight ? 1.7f : -.3f));
            if (HasSight && Suspicion >= 1)
            {
                if (confirmedAt < 0)
                {
                    confirmedAt = Time.time;
                    firstShotAt = Time.time + .45f;
                }
                lastKnown = target.position;
                alertUntil = Time.time + 18;
                if (Time.time >= nextShare)
                {
                    mission.AlertGuards(lastKnown, 25, this);
                    nextShare = Time.time + 4;
                }
                if (alarmTarget == null && Time.time >= nextAlarm && Time.time - confirmedAt > 1.5f && mission.Security != null)
                {
                    alarmTarget = mission.Security.NearestPanel(transform.position);
                    alarmAttemptUntil = Time.time + 20;
                    nextAlarm = Time.time + 40;
                }
                if (State != GuardState.RaiseAlarm)
                    State = GuardState.Attack;
            }
            else if (!HasSight)
            {
                confirmedAt = -1;
                if (State == GuardState.Attack)
                {
                    State = GuardState.Investigate;
                    nextPath = 0;
                }
            }
            if (alarmTarget != null && (mission.Security.AlarmActive || mission.Security.AlarmsDisabled || Time.time > alarmAttemptUntil))
                alarmTarget = null;
            if (alarmTarget != null)
            {
                State = GuardState.RaiseAlarm;
                if (Vector3.Distance(transform.position, alarmTarget.transform.position) < 2.1f)
                {
                    mission.Security.RaiseAlarm(lastKnown);
                    alarmTarget = null;
                    State = GuardState.Attack;
                }
            }
            if (rounds == 0 && reloadUntil == 0)
            {
                reloadUntil = Time.time + 2.5f;
                presentation?.Reload();
            }
            if (reloadUntil > 0 && Time.time >= reloadUntil)
            {
                rounds = 24;
                reloadUntil = 0;
            }
            bool combat = HasSight && Suspicion >= 1;
            float distance = Vector3.Distance(transform.position, target.position);
            if (combat)
            {
                TurnTowards(target.position, dt);
                if (distance <= fireRange && Time.time >= firstShotAt && Time.time >= nextShot && rounds > 0 && State != GuardState.RaiseAlarm)
                    Shoot(point);
            }
            if (!combat && alarmTarget == null && State != GuardState.Patrol)
            {
                if (Time.time > alertUntil && !(alarmResponding && mission.Security != null && mission.Security.AlarmActive))
                {
                    State = GuardState.Patrol;
                    searchUntil = 0;
                    nextPath = 0;
                }
            }
            Vector3 destination = alarmTarget != null ? alarmTarget.transform.position : State == GuardState.Patrol && patrolPoints != null && patrolPoints.Length > 0 ? patrolPoints[patrolIndex] : lastKnown;
            Vector3 flat = destination - transform.position;
            flat.y = 0;
            bool move = alarmTarget != null || !combat || distance > 18;
            bool scripted = false;
            if (!combat && alarmTarget == null && alarmResponding && mission.Security != null && mission.Security.AlarmActive && authoredAlarm != null && authoredAlarm.Length > 0)
            {
                scripted = true; FollowCommands(authoredAlarm, ref alarmCommand, true, dt, out destination, out move);
            }
            else if (State == GuardState.Patrol && authoredPatrol != null && authoredPatrol.Length > 0)
            {
                scripted = true; FollowCommands(authoredPatrol, ref patrolCommand, false, dt, out destination, out move);
            }
            flat = destination - transform.position; flat.y = 0;
            if (!scripted && State == GuardState.Patrol && flat.magnitude < 1)
            {
                if (patrolWait == 0)
                    patrolWait = Time.time + Random.Range(1.5f, 3.5f);
                if (Time.time >= patrolWait && patrolPoints != null && patrolPoints.Length > 0)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    patrolWait = 0;
                    nextPath = 0;
                }
                move = false;
            }
            else if (!scripted && !combat && alarmTarget == null && State != GuardState.Patrol && flat.magnitude < 1.3f)
            {
                State = GuardState.Search;
                if (searchUntil == 0)
                    searchUntil = Time.time + 6;
                transform.Rotate(0, 35 * dt, 0);
                move = false;
                if (Time.time >= searchUntil)
                {
                    alertUntil = 0;
                    searchUntil = 0;
                }
            }
            if (move)
                MoveTowards(destination, dt);
            else
                stuck = 0;
            gravity = controller.isGrounded ? -2 : Mathf.Max(-35, gravity - 17 * dt);
            controller.Move(Vector3.up * gravity * dt);
            if (Vector3.Distance(lastPosition, transform.position) < .01f && move)
                stuck += dt;
            else
                stuck = 0;
            lastPosition = transform.position;
            if (stuck > 1.5f)
            {
                nextPath = 0;
                if (State == GuardState.Patrol && patrolPoints != null && patrolPoints.Length > 0)
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                stuck = 0;
            }
        }
        void FollowCommands(GuardPatrolCommand[] commands, ref int index, bool alarm, float dt, out Vector3 destination, out bool move)
        {
            destination = transform.position; move = false;
            if (index >= commands.Length || (!alarm && patrolFinished)) return;
            var command = commands[index];
            switch (command.kind)
            {
                case 1:
                    if (commandWait == 0) commandWait = Time.time + command.seconds;
                    if (Time.time >= commandWait) { commandWait = 0; index++; }
                    break;
                case 2:
                case 3:
                case 4:
                    destination = command.position; patrolRunning = command.kind == 3;
                    Vector3 delta = destination - transform.position; delta.y = 0;
                    if (delta.magnitude < .35f) { index++; nextPath = 0; } else move = true;
                    break;
                case 5:
                    TurnTowards(command.position, dt);
                    Vector3 facing = command.position - transform.position; facing.y = 0;
                    if (facing.sqrMagnitude < .01f || Vector3.Angle(transform.forward, facing) < 3) index++;
                    break;
                case 6: index++; break;
                case 7: if (!alarm) patrolFinished = true; index = commands.Length; break;
                default: index++; break;
            }
            if (index >= commands.Length && commands.Length > 0 && ArrayEnd(commands) >= 0) index = ArrayEnd(commands) + 1;
        }
        static int ArrayEnd(GuardPatrolCommand[] commands) { for (int i = 0; i < commands.Length; i++) if (commands[i].kind == 6) return i; return -1; }
        void MoveTowards(Vector3 destination, float dt)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0;
            if (delta.magnitude < .6f)
                return;
            Vector3 heading = delta.normalized;
            // Open a door in our way rather than firing or pushing against it.
            if (Physics.Raycast(transform.position + Vector3.up, heading, out var obstruction, 1.8f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore))
            {
                var door = obstruction.collider.GetComponentInParent<InteractiveDoor>();
                if (door != null && !door.IsOpen)
                {
                    door.SetOpen(true);
                    if (door.partner != null)
                        door.partner.SetOpen(true);
                    nextPath = 0;
                }
            }
            bool direct = Mathf.Abs(destination.y - transform.position.y) < 1.5f && !Physics.CapsuleCast(transform.position + Vector3.up * .38f, transform.position + Vector3.up * 1.5f, .28f, heading, delta.magnitude, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore);
            Vector3 step = destination;
            if (!direct)
            {
                if (navigation == null)
                    return;
                if (Time.time >= nextPath)
                {
                    path = authoredNavigation != null ? authoredNavigation.FindPath(graphId, transform.position, destination) : navigation.FindPath(transform.position, destination);
                    pathIndex = 0;
                    nextPath = Time.time + Random.Range(1.2f, 1.8f);
                } while (pathIndex < path.Count && Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(path[pathIndex].x, path[pathIndex].z)) < .4f)
                    pathIndex++;
                if (pathIndex >= path.Count)
                    return;
                step = path[pathIndex];
            }
            Vector3 direction = step - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f)
                return;
            Vector3 routeDirection = direction.normalized;
            if (Physics.Raycast(transform.position + Vector3.up, routeDirection, out var panelHit, 1.4f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore))
            {
                var panel = panelHit.collider.GetComponentInParent<InteractiveDoor>();
                if (panel != null && panel.Fraction < .98f) { panel.SetOpen(true); if (panel.partner != null) panel.partner.SetOpen(true); TurnTowards(step, dt); return; }
            }
            TurnTowards(step, dt);
            float speed = State == GuardState.Patrol && !patrolRunning ? 1.55f : 3.15f;
            // A local separation force avoids guards stacking in a single point.
            Vector3 separation = Vector3.zero;
            foreach (var c in Physics.OverlapSphere(transform.position + Vector3.up, .7f, 1 << DemoWorld.GuardLayer, QueryTriggerInteraction.Ignore))
            {
                var other = c.GetComponent<EnemyGuard>();
                if (other != null && other != this)
                {
                    Vector3 away = transform.position - other.transform.position;
                    away.y = 0;
                    separation += away.normalized * .35f;
                }
            }
            if (Physics.CheckSphere(transform.position + Vector3.up, .7f, 1 << DemoWorld.WorldLayer, QueryTriggerInteraction.Ignore)) separation = Vector3.zero;
            float facing = Vector3.Dot(transform.forward, direction.normalized);
            if (facing < .7f) return;
            float distanceToMove = Mathf.Min(direction.magnitude, speed * dt * facing);
            controller.Move((direction.normalized + separation).normalized * distanceToMove);
        }
        void Shoot(Vector3 point)
        {
            rounds--;
            burst++;
            nextShot = Time.time + (burst < 3 ? .14f : Random.Range(1.2f, 1.9f));
            if (burst >= 3)
                burst = 0;
            Vector3 eye = transform.position + Vector3.up * 1.4f, aim = (point - eye).normalized;
            float spread = 2.3f + Vector3.Distance(transform.position, target.position) * .025f;
            aim = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0) * aim;
            Vector3 end = eye + aim * fireRange;
            if (Physics.Raycast(eye, aim, out var hit, fireRange, (1 << DemoWorld.WorldLayer) | (1 << DemoWorld.PlayerLayer) | (1 << DemoWorld.GuardLayer), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                hit.collider.GetComponentInParent<Health>()?.TakeDamage(damage, Team.Guard, eye);
            }
            WeaponController.DrawTracer(eye + transform.forward * .45f, end, new Color(1, .3f, .15f), .07f);
            presentation?.Shot();
        }
        void TurnTowards(Vector3 point, float dt)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 220 * dt);
        }
        void OnDeath(Health _)
        {
            State = GuardState.Dead;
            controller.enabled = false;
            alarmTarget = null;
            HasSight = false;
            mission?.GuardDown();
        }
    }
}
