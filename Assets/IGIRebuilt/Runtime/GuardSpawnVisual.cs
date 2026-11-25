using UnityEngine;
namespace IGIRebuilt
{
    public sealed class GuardSpawnVisual : MonoBehaviour
    {
        public GameObject visualPrefab;
        public int sourceId, graphId;
        public GuardPatrolCommand[] patrol, alarm;
    }
}
