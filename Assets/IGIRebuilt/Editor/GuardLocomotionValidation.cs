using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace IGIRebuilt.Editor
{
    public static class GuardLocomotionValidation
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Run()
        {
            Render(new[] { "Walk", "Run" }, "guard-locomotion-contact.png");
        }
        public static void RunCombat()
        {
            Render(new[] { "WalkFire", "RunFire" }, "guard-moving-fire-contact.png");
        }
        static void Render(string[] states, string filename)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = Color.white * .65f;
            var sun = new GameObject("Preview light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1; sun.transform.rotation = Quaternion.Euler(35, -30, 0);
            var camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.transform.position = new Vector3(3, 1.5f, 4); camera.transform.LookAt(new Vector3(0, .9f, 0)); camera.orthographic = true; camera.orthographicSize = 1.1f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .19f, .23f);
            const int width = 180, height = 240;
            var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
            var sheet = new Texture2D(width * 6, height * 6, TextureFormat.RGB24, false);
            var settings = AssetDatabase.LoadAssetAtPath<GuardPresentationSettings>("Assets/IGIRebuilt/Resources/GuardPresentationSettings.asset");
            int row = 0;
            foreach (var binding in settings.motions)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IGIImported/Models/missions/location0/level1/" + binding.model + ".igimodel");
                var parent = new GameObject("Preview actor");
                var visual = VisualFactory.Attach(prefab, parent.transform, Vector3.up * .9f, Vector3.zero, 1.8f, true);
                var player = visual.AddComponent<GuardAnimation>(); player.Initialize(binding.motion);
                var rifle = UnityEngine.Object.Instantiate(settings.rifle, player.WeaponSocket); rifle.transform.localPosition = Vector3.zero; rifle.transform.localRotation = Quaternion.identity; rifle.transform.localScale = Vector3.one;
                foreach (string name in states)
                {
                    var clip = Array.Find(binding.motion.clips, c => c.name == name);
                    Require(clip.metresPerSecond > .5f, "Missing native travel speed");
                    bool walking = name.StartsWith("Walk");
                    Require(walking ? clip.metresPerSecond < 2 : clip.metresPerSecond > 3, "Reversed gait mapping");
                    Require(clip.frames >= Mathf.CeilToInt(clip.duration * 30), "Motion undersampled");
                    float seam = 0;
                    for (int i = 0; i < binding.motion.vertexCount; i++) seam = Mathf.Max(seam, Vector3.Distance(clip.positions[i], clip.positions[(clip.frames - 1) * binding.motion.vertexCount + i]));
                    Require(seam < .0001f, "Visible loop seam");
                    player.Play(name); player.SetMovementSpeed(walking ? 1.55f : 3.15f); player.Advance(.2f);
                    float expected = (walking ? 1.55f : 3.15f) / (clip.metresPerSecond * visual.transform.lossyScale.z);
                    Require(Mathf.Abs(player.PlaybackRate - expected) < .01f, "Cadence does not match movement");
                    var filter = visual.GetComponentInChildren<MeshFilter>(); var before = filter.sharedMesh.vertices;
                    player.Advance(.1f); var after = filter.sharedMesh.vertices;
                    Require(before.Where((p, i) => Vector3.Distance(p, after[i]) > .005f).Any(), "Locomotion frozen");
                    for (int col = 0; col < 6; col++)
                    {
                        camera.Render(); RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, width, height), col * width, (5 - row) * height);
                        player.Advance((walking ? .5667f : clip.duration) / (6 * player.PlaybackRate));
                    }
                    row++;
                    player.SetMovementSpeed(0); player.Advance(.1f); before = filter.sharedMesh.vertices; player.Advance(.3f); after = filter.sharedMesh.vertices;
                    Require(before.Where((p, i) => Vector3.Distance(p, after[i]) > .0001f).Count() == 0, "Stationary actor keeps stepping");
                    Debug.Log($"LOCOMOTION_PASS: {binding.model} {name}, native {clip.metresPerSecond:F2} m/s, cadence {expected:F2}, loop seam {seam:F6}");
                }
                UnityEngine.Object.DestroyImmediate(parent);
            }
            sheet.Apply(); Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + filename, sheet.EncodeToPNG()); RenderTexture.active = null; camera.targetTexture = null; target.Release();
            Debug.Log("GUARD_LOCOMOTION_VALIDATION_PASS: six native gait clips, loop continuity, motion sampling, speed matching and stationary feet");
        }
    }
}
