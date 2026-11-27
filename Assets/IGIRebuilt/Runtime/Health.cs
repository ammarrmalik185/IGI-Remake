using System;
using UnityEngine;

namespace IGIRebuilt
{
    public enum Team { Player, Guard }

    public sealed class Health : MonoBehaviour
    {
        public Team team;
        [Min(1)] public float maximum = 100;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0;
        public event Action<Health> Died;
        public event Action<float> Damaged;
        public event Action<Vector3?> Attacked;

        private void Awake() { Current = maximum; }
        public void ResetHealth(float value)
        {
            maximum = Mathf.Max(1, value);
            Current = maximum;
        }
        public bool TakeDamage(float damage, Team attacker, Vector3? origin = null)
        {
            if (IsDead || attacker == team || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            Attacked?.Invoke(origin);
            float taken = Mathf.Min(Current, damage);
            Current -= taken;
            Damaged?.Invoke(taken);
            if (IsDead) Died?.Invoke(this);
            return true;
        }
        public bool Heal(float amount)
        {
            if (IsDead || Current >= maximum || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            Current = Mathf.Min(maximum, Current + amount);
            return true;
        }
    }
}
