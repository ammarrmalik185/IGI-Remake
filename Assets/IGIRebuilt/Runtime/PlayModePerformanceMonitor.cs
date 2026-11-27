#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace IGIRebuilt
{
    // Capture the user's actual editor session; an isolated batch renderer cannot reproduce every UI stall.
    public sealed class PlayModePerformanceMonitor : MonoBehaviour
    {
        public MissionDirector mission;
        [Serializable] private sealed class Sample
        {
            public string utc;
            public float playSeconds, fps, worstFrameMs;
            public int framesOver50Ms, collections, width, height, targetFrameRate;
            public long allocatedMB, monoMB;
        }
        private string path;
        private float warmup, total, elapsed, worst;
        private int frames, slow, collections;
        private void Start()
        {
            path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/playmode-performance.jsonl"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "");
            collections = GC.CollectionCount(0);
        }
        private void Update()
        {
            if (mission == null || mission.InputBlocked || path == null) return;
            float dt = Time.unscaledDeltaTime;
            if (warmup < 10) { warmup += dt; return; }
            total += dt; elapsed += dt; frames++; worst = Mathf.Max(worst, dt);
            if (dt > .05f) slow++;
            if (elapsed < 30) return;
            var sample = new Sample {
                utc = DateTime.UtcNow.ToString("O"), playSeconds = total, fps = frames / elapsed, worstFrameMs = worst * 1000,
                framesOver50Ms = slow, collections = GC.CollectionCount(0) - collections, width = Screen.width, height = Screen.height,
                targetFrameRate = Application.targetFrameRate, allocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024), monoMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024)
            };
            File.AppendAllText(path, JsonUtility.ToJson(sample) + Environment.NewLine);
            elapsed = worst = 0; frames = slow = 0; collections = GC.CollectionCount(0);
        }
    }
}
#endif
