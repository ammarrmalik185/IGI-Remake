using UnityEngine;

namespace IGIRebuilt
{
    public enum BulletSurfaceKind { Solid, Fence, Glass }
    public sealed class ImportedSurfaceData : ScriptableObject
    {
        public int[] triangleKinds;
    }
}
