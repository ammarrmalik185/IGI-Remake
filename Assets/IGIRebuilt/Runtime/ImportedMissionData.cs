using System;
using UnityEngine;

namespace IGIRebuilt
{
    // Original campaign data is retained separately from the prototype's mission rules.
    public sealed class ImportedMissionData : ScriptableObject
    {
        public int number;
        public string title;
        [TextArea(4, 16)] public string briefing;
        [TextArea] public string[] objectives;
        public string source;
        public ImportedPlacement[] placements;
        public string[] modelPaths;
        public Vector3 origin;
        public string limitations = "Reference scene: original QVM gameplay and terrain are not executed by the prototype.";
    }

    [Serializable]
    public sealed class ImportedPlacement
    {
        public int id;
        public string type, name, model;
        public Vector3 position, rotation;
    }

}
