using UnityEngine;

namespace IGIRebuilt
{
    // Put this component on an environment prefab to replace the entire compound.
    // Spawn markers and objective markers stay editable independently of any art mesh.
    public sealed class MissionLayout : MonoBehaviour
    {
        public Transform playerSpawn;
        public Transform terminal;
        public Transform extraction;
        public Transform[] guardSpawns;
        public Vector3 navigationCentre;
        public Vector2 navigationSize = new Vector2(96, 88);
        public float minimumPlayerHeight = -12;
        public float viewDistance = 140;
    }
}
