using UnityEngine;

namespace IGIRebuilt
{
    public enum WeaponAction { Hitscan, Rocket, Grenade, Flashbang, ProximityMine, Binoculars, Medipack }
    [CreateAssetMenu(menuName = "IGI Rebuilt/Weapon", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string displayName = "Rifle";
        [Header("Replace this prefab with your own complete gun model")]
        public GameObject visualPrefab;
        public Vector3 viewPosition = new Vector3(0.21f, -0.2f, 0.57f);
        public Vector3 viewRotation;
        [Min(0.05f)] public float modelLength = 0.85f;
        [Header("Gameplay")]
        [Min(1)] public float damage = 34f;
        [Min(1)] public int magazineSize = 30;
        [Min(0)] public int reserveAmmo = 150;
        [Min(0.05f)] public float secondsPerShot = 0.12f;
        [Min(0.1f)] public float reloadSeconds = 2.1f;
        public bool automatic = true;
        public bool melee;
        public bool dualWield;
        public WeaponAction action;
        [Range(1, 24)] public int pellets = 1;
        [Min(0)] public float noiseRadius = 100;
        [Min(0)] public float blastRadius = 7;
        [Min(0.1f)] public float projectileSpeed = 35;
        [Min(0.1f)] public float fuseSeconds = 3;
        public AudioClip explosionSound;
        public GameObject explosionEffect;
        [Min(1)] public float range = 150;
        [Min(0)] public float spreadDegrees = 0.5f;
        public float aimFieldOfView = 48;
        public bool canZoom;
        public bool scoped;
        [Range(50, 100)] public float viewHorizontalFieldOfView = 68;
        public Shader scopeShader;
        public Texture2D scopeMask, scopeHorizontal, scopeVertical;
        public float recoilDegrees = 1.4f;
        public AudioClip fireSound;
        public AudioClip reloadSound;
    }

    // Gameplay never depends on a particular mesh, its material count, or its source format.
    public sealed class MagazineState
    {
        public int Loaded { get; private set; }
        public int Reserve { get; private set; }
        public int Capacity { get; }
        public MagazineState(int capacity, int reserve)
        {
            Capacity = Mathf.Max(1, capacity);
            Loaded = Capacity;
            Reserve = Mathf.Max(0, reserve);
        }
        public bool Fire()
        {
            if (Loaded == 0) return false;
            Loaded--;
            return true;
        }
        public int Reload()
        {
            int transferred = Mathf.Min(Capacity - Loaded, Reserve);
            Loaded += transferred;
            Reserve -= transferred;
            return transferred;
        }
    }
}
