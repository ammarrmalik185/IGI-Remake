using UnityEngine;

namespace IGIRebuilt
{
    [CreateAssetMenu(menuName = "IGI Rebuilt/Game configuration", fileName = "GameConfiguration")]
    public sealed class GameConfiguration : ScriptableObject
    {
        public string missionName = "OPERATION FIRST LIGHT";
        public bool captureTruck;
        [TextArea] public string briefing;
        public WeaponDefinition[] weapons;
        [Header("Optional replacements; leave empty for the built-in training compound")]
        public GameObject environmentPrefab;
        public GameObject enemyVisualPrefab;
        public Vector3 enemyVisualRotation;
        public float enemyHeight = 1.8f;
        [Header("Mission tuning")]
        [Range(1, 64)] public int enemyCount = 6;
        public float enemyHealth = 100;
        public float enemyDamage = 8;
        public float enemySightRange = 27;
    }
}
