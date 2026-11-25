using System;
using UnityEngine;

namespace IGIRebuilt
{
    [Serializable] public class BuildingDoorway
    {
        public int slot;
        public Vector3 foot, normal;
        public float width, height;
    }
    public sealed class FirstBuildingInterior : MonoBehaviour
    {
        public BuildingDoorway[] doorways;
        public Transform[] rooms;
    }
}
