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

        private void Awake() { Current = maximum; }
        public void ResetHealth(float value)
        {
            maximum = Mathf.Max(1, value);
            Current = maximum;
        }
        public bool TakeDamage(float damage, Team attacker)
        {
            if (IsDead || attacker == team || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            float taken = Mathf.Min(Current, damage);
            Current -= taken;
            Damaged?.Invoke(taken);
            if (IsDead) Died?.Invoke(this);
            return true;
        }
    }
}
