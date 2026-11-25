using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [Serializable] internal class LadderAttachment
    {
        public int parentId; public string modelPath;
        public Vector3 position, foot, head, forward, up;
    }
    [Serializable] internal class LadderAttachments { public LadderAttachment[] ladders; }
    internal static class TrainyardLadderSetup
    {
        public static void Add(Dictionary<int, GameObject> parents)
        {
            var data = JsonUtility.FromJson<LadderAttachments>(File.ReadAllText("Assets/IGIRebuilt/Trainyard/TrainyardLadders.json"));
            foreach (var item in data.ladders)
            {
                if (!parents.TryGetValue(item.parentId, out var parent)) throw new Exception("Missing ladder building " + item.parentId);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.modelPath);
                if (prefab == null) throw new Exception("Missing ladder model " + item.modelPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Ladder " + item.parentId + " - " + prefab.name;
                instance.transform.SetParent(parent.transform, false);
                instance.transform.localPosition = item.position;
                instance.transform.localRotation = Quaternion.LookRotation(item.forward, item.up);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer = DemoWorld.WorldLayer;
                var ladder = instance.AddComponent<ClimbableLadder>();
                Vector3 foot = instance.transform.TransformPoint(item.foot), head = instance.transform.TransformPoint(item.head);
                Physics.SyncTransforms(); bool ready = false; string diagnosis = "";
                bool fallback = false; Vector3 fallbackLow = default, fallbackHigh = default, fallbackBase = default, fallbackRoof = default, fallbackOut = default;
                foreach (float sign in new[] { 1f, -1f })
                {
                    Vector3 outward = instance.transform.forward * sign; outward.y = 0; outward.Normalize();
                    foreach (float standoff in new[] { 0.37f, 0.45f, 0.58f, 0.78f, 0.98f })
                    {
                        Vector3 low = foot + outward * standoff;
                        if (!Physics.Raycast(low + Vector3.up * 1.7f, Vector3.down, out var ground, 4, 1 << DemoWorld.WorldLayer)) continue;
                        low.y = ground.point.y + 0.08f;
                        Vector3 lowExit = low + outward * 0.35f;
                        if (!Physics.Raycast(lowExit + Vector3.up, Vector3.down, out var baseGround, 3, 1 << DemoWorld.WorldLayer)) continue;
                        lowExit.y = baseGround.point.y + 0.08f;
                        foreach (float inset in new[] { 0.85f, 1.25f, 1.65f })
                        {
                            Vector3 roof = head - outward * inset;
                            if (!Physics.Raycast(roof + Vector3.up * 0.8f, Vector3.down, out var landing, 3.2f, 1 << DemoWorld.WorldLayer)) { diagnosis += " no roof " + roof; continue; }
                            if (landing.normal.y < 0.6f || Mathf.Abs(landing.point.y - head.y) > 2.1f) { diagnosis += " unsuitable roof " + landing.point; continue; }
                            roof.y = landing.point.y + 0.08f;
                            float lift = 0.6f;
                            if (Physics.Raycast(roof + Vector3.up * 0.1f, Vector3.up, out var ceiling, 3, 1 << DemoWorld.WorldLayer)) lift = Mathf.Clamp(ceiling.distance - 1.9f, 0.08f, 0.6f);
                            Vector3 high = head + outward * standoff; high.y = Mathf.Max(head.y, roof.y + lift);
                            bool standingClear = ClimbableLadder.ClearBody(lowExit) && ClimbableLadder.ClearBody(roof);
                            bool clear = standingClear;
                            for (int step = 0; clear && step <= 80; step++) clear &= ClimbableLadder.ClearBody(Vector3.Lerp(low, high, step / 80f));
                            Vector3 aboveRoof = new Vector3(roof.x, high.y, roof.z);
                            for (int step = 0; clear && step <= 16; step++) clear &= ClimbableLadder.ClearBody(Vector3.Lerp(high, aboveRoof, step / 16f));
                            if (!clear)
                            {
                                if (standingClear && !fallback) { fallback = true; fallbackLow = low; fallbackHigh = high; fallbackBase = lowExit; fallbackRoof = roof; fallbackOut = outward; }
                                diagnosis += " blocked path " + low + " -> " + high + " -> " + roof; continue;
                            }
                            ladder.bottom = instance.transform.InverseTransformPoint(low);
                            ladder.top = instance.transform.InverseTransformPoint(high);
                            ladder.bottomExit = instance.transform.InverseTransformPoint(lowExit);
                            ladder.topExit = instance.transform.InverseTransformPoint(roof);
                            ladder.outward = instance.transform.InverseTransformDirection(outward);
                            ready = true; break;
                        }
                        if (ready) break;
                    }
                    if (ready) break;
                }
                if (!ready && fallback)
                {
                    // Original ladder movement passes through authored hatches even when the
                    // coarse player-collision hull covers them. Only the fixed route bypasses it.
                    ladder.bottom = instance.transform.InverseTransformPoint(fallbackLow);
                    ladder.top = instance.transform.InverseTransformPoint(fallbackHigh);
                    ladder.bottomExit = instance.transform.InverseTransformPoint(fallbackBase);
                    ladder.topExit = instance.transform.InverseTransformPoint(fallbackRoof);
                    ladder.outward = instance.transform.InverseTransformDirection(fallbackOut);
                    ladder.scriptedPassage = true; ready = true;
                }
                if (!ready) throw new Exception("No clear climb and roof exit for " + instance.name + " foot=" + foot + " head=" + head + diagnosis);
                // Attachment coordinates put some rung planes behind the rendered wall skin.
                // Project the visual out to that skin without moving the tested traversal route.
                var parentMesh = parent.GetComponent<MeshFilter>();
                if (parentMesh != null && parentMesh.sharedMesh != null)
                {
                    var surface = new GameObject("Ladder wall alignment probe"); surface.layer = 2;
                    surface.transform.SetPositionAndRotation(parent.transform.position, parent.transform.rotation);
                    surface.transform.localScale = parent.transform.lossyScale;
                    var wall = surface.AddComponent<MeshCollider>(); wall.sharedMesh = parentMesh.sharedMesh;
                    Physics.SyncTransforms(); float projection = float.NegativeInfinity;
                    for (int sample = 1; sample <= 8; sample++)
                    {
                        Vector3 reference = Vector3.Lerp(foot, head, sample / 10f);
                        if (wall.Raycast(new Ray(reference + ladder.Outward * 2, -ladder.Outward), out var hit, 4))
                            projection = Mathf.Max(projection, Vector3.Dot(hit.point - reference, ladder.Outward));
                    }
                    UnityEngine.Object.DestroyImmediate(surface);
                    if (!float.IsNegativeInfinity(projection))
                    {
                        Vector3 low = ladder.Bottom, high = ladder.Top, baseExit = ladder.BottomExit, roofExit = ladder.TopExit;
                        float shift = Mathf.Clamp(projection + 0.16f, 0.08f, 1.25f);
                        instance.transform.position += ladder.Outward * shift;
                        ladder.bottom = instance.transform.InverseTransformPoint(low);
                        ladder.top = instance.transform.InverseTransformPoint(high);
                        ladder.bottomExit = instance.transform.InverseTransformPoint(baseExit);
                        ladder.topExit = instance.transform.InverseTransformPoint(roofExit);
                        Debug.Log("LADDER_VISUAL_OFFSET: " + instance.name + " " + shift);
                    }
                }
                Debug.Log("LADDER_READY: " + instance.name + " bottom=" + ladder.BottomExit + " roof=" + ladder.TopExit);
            }
            Debug.Log("TRAINYARD_LADDERS_READY: " + data.ladders.Length);
        }
    }
}
