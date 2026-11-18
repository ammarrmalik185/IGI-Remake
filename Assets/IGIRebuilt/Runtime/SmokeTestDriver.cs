using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace IGIRebuilt
{
    // Opt-in verification for an actual player build; normal play never runs this.
    public sealed class SmokeTestDriver : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "--smoke-test");
            if (flag < 0) yield break;
            yield return null;
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            var mission = bootstrap != null ? bootstrap.Mission : null;
            bool valid = mission != null && mission.player != null && mission.weapon != null && mission.weapon.Active != null && mission.GuardsRemaining > 0;
            if (!valid) { Debug.LogError("IGI_REBUILT_SMOKE_FAILED: bootstrap"); Application.Quit(2); yield break; }
            mission.SetPaused(false);
            if (mission.CapturingTruck) mission.player.GetComponent<Health>().ResetHealth(10000);
            int startFrame = Time.frameCount;
            yield return new WaitForSeconds(1);
            string output = flag + 1 < args.Length && !args[flag + 1].StartsWith("-") ? args[flag + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(output);
            if (mission.CapturingTruck) { valid &= VerifyWallFaces(output); valid &= VerifyBuildingCollision(output); }
            CaptureView(mission.player.eye, Path.Combine(output, "first-person.png"));
            if (mission.CapturingTruck)
            {
                bool grounded = Physics.Raycast(mission.player.transform.position + Vector3.up * 0.5f, Vector3.down, 2, 1 << DemoWorld.WorldLayer);
                valid &= grounded; Debug.Log("SPAWN_GROUND: " + grounded + " at " + mission.player.transform.position);
                var overview = new GameObject("Overview verification camera").AddComponent<Camera>();
                overview.transform.position = new Vector3(180, 160, 110); overview.transform.LookAt(new Vector3(0, 0, -75));
                overview.farClipPlane = 2000; overview.clearFlags = CameraClearFlags.SolidColor; overview.backgroundColor = mission.player.eye.backgroundColor;
                overview.cullingMask = ~(1 << DemoWorld.PlayerLayer);
                CaptureView(overview, Path.Combine(output, "trainyard-overview.png")); Destroy(overview.gameObject);
                valid &= !mission.CollectIntel();
            }
            yield return new WaitForEndOfFrame();
            // Verify each complete imported prefab in the running player, including idle input stability.
            for (int i = 0; i < mission.weapon.definitions.Length; i++)
            {
                mission.weapon.Equip(i);
                WeaponDefinition selected = mission.weapon.Active;
                yield return new WaitForSeconds(0.3f);
                valid &= mission.weapon.Active == selected;
                CaptureView(mission.player.eye, Path.Combine(output, "weapon-" + (i + 1) + ".png"));
                yield return new WaitForEndOfFrame();
            }
            mission.weapon.Equip(0);
            yield return new WaitForSeconds(0.3f);
            int startAmmo = mission.weapon.Ammo.Loaded;
            bool fired = mission.weapon.Fire();
            valid &= fired && mission.weapon.Ammo.Loaded == startAmmo - 1;
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false;
            if (mission.CapturingTruck)
            {
                mission.player.enabled = false;
                mission.player.transform.position = mission.terminal.position + mission.terminal.forward * 1.8f - Vector3.up * 0.9f;
                mission.player.eye.transform.LookAt(mission.terminal.position);
                Physics.SyncTransforms();
                CaptureView(mission.player.eye, Path.Combine(output, "truck-objective.png"));
                bool interaction = mission.CanInteract && mission.CollectIntel() && mission.Progress.Complete;
                valid &= interaction; Debug.Log("TRUCK_CAPTURE: " + interaction);
            }
            else
            {
                valid &= mission.CollectIntel() && mission.Progress.HasIntel;
                mission.player.transform.position = mission.extraction.position;
                controller.enabled = true;
            }
            yield return null;
            valid &= mission.Progress.Complete && Time.frameCount > startFrame;
            CaptureView(mission.player.eye, Path.Combine(output, "mission-complete.png"));
            yield return new WaitForEndOfFrame();
            string result = "{\"passed\":" + (valid ? "true" : "false") + ",\"framesAdvanced\":" + (Time.frameCount - startFrame) + ",\"guards\":" + mission.GuardsRemaining + ",\"weapons\":" + mission.weapon.definitions.Length + "}";
            File.WriteAllText(Path.Combine(output, "smoke-result.json"), result);
            Debug.Log(valid ? "IGI_REBUILT_SMOKE_PASSED" : "IGI_REBUILT_SMOKE_FAILED");
            yield return new WaitForSecondsRealtime(0.25f);
            Application.Quit(valid ? 0 : 2);
        }
        // Explicit rendering also works when Windows suppresses the hidden player swap chain.
        // Captures show scene geometry and weapons; the immediate-mode HUD is excluded.
        private static bool VerifyWallFaces(string output)
        {
            Material source = null;
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.mainTexture != null && material.mainTexture.name == "315_01_1") source = material;
            if (source == null) { Debug.LogError("WALL_FACES_FAILED: source wall material missing"); return false; }
            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.transform.position = new Vector3(0, 3000, 0);
            wall.transform.localScale = Vector3.one * 4;
            wall.GetComponent<Renderer>().sharedMaterial = source;
            var camera = new GameObject("Wall rendering verification").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.farClipPlane = 10; camera.orthographic = true; camera.orthographicSize = 1;
            bool valid = true;
            for (int side = -1; side <= 1; side += 2)
            {
                camera.transform.position = wall.transform.position + Vector3.forward * (side * 3);
                camera.transform.LookAt(wall.transform.position);
                Color pixel = CaptureView(camera, Path.Combine(output, side < 0 ? "wall-front.png" : "wall-back.png"));
                valid &= Vector3.Distance(new Vector3(pixel.r, pixel.g, pixel.b), new Vector3(1, 0, 1)) > 0.2f;
            }
            Destroy(wall); Destroy(camera.gameObject);
            Debug.Log(valid ? "WALL_FACES_PASSED: imported wall visible from both sides" : "WALL_FACES_FAILED");
            return valid;
        }
        private static bool VerifyBuildingCollision(string output)
        {
            int tested = 0; bool valid = true;
            foreach (var info in FindObjectsByType<ImportedModelInfo>(FindObjectsSortMode.None))
            {
                if (!info.name.StartsWith("1118 ") && !info.name.StartsWith("1111 ") && !info.name.StartsWith("1106 ")) continue;
                var source = info.GetComponent<MeshCollider>();
                if (source == null) { valid = false; continue; }
                var fixture = new GameObject("Native wall collision verification"); fixture.layer = DemoWorld.WorldLayer;
                fixture.transform.position = new Vector3(0, 4500, 0);
                var solid = fixture.AddComponent<MeshCollider>(); solid.sharedMesh = source.sharedMesh;
                var probe = new GameObject("Character wall sweep"); probe.layer = DemoWorld.PlayerLayer;
                var capsule = probe.AddComponent<CharacterController>(); capsule.enabled = false;
                capsule.height = 1.8f; capsule.radius = 0.32f; capsule.center = Vector3.up * 0.9f; capsule.skinWidth = 0.04f;
                Physics.SyncTransforms();
                var vertices = solid.sharedMesh.vertices; var triangles = solid.sharedMesh.triangles; bool found = false;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    if (cross.magnitude < 3 || Mathf.Abs(cross.normalized.y) > 0.05f) continue;
                    Vector3 centre = fixture.transform.position + (a + b + c) / 3, normal = cross.normalized;
                    bool clear = true;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 start = centre + normal * (0.9f * side);
                        clear &= !Physics.CheckCapsule(start + Vector3.up * 0.58f, start - Vector3.up * 0.58f, 0.32f, 1 << DemoWorld.WorldLayer);
                    }
                    if (!clear) continue;
                    found = true;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        capsule.enabled = false; probe.transform.position = centre + normal * (0.9f * side) - Vector3.up * 0.9f;
                        capsule.enabled = true; Physics.SyncTransforms();
                        for (int step = 0; step < 18; step++) capsule.Move(-normal * (side * 0.1f));
                        valid &= Vector3.Dot(probe.transform.position + Vector3.up * 0.9f - centre, normal) * side > 0.2f;
                    }
                    break;
                }
                valid &= found; tested++; Debug.Log("BUILDING_WALL_TEST: " + info.name + " candidate=" + found + " passed=" + valid);
                probe.SetActive(false); fixture.SetActive(false); Destroy(probe); Destroy(fixture);
                if (info.name.StartsWith("1111 "))
                {
                    var camera = new GameObject("Interior verification").AddComponent<Camera>();
                    camera.transform.position = info.transform.TransformPoint(new Vector3(0, 1.65f, 0));
                    camera.transform.rotation = info.transform.rotation; camera.farClipPlane = 100;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
                    CaptureView(camera, Path.Combine(output, "building-interior.png")); Destroy(camera.gameObject);
                    foreach (var material in info.GetComponent<Renderer>().sharedMaterials) valid &= material.GetInt("_Cull") == 2;
                }
            }
            valid &= tested == 3;
            Debug.Log(valid ? "BUILDING_COLLISION_PASSED: character blocked from both sides of three native building wall meshes; interior uses authored faces" : "BUILDING_COLLISION_FAILED: tested=" + tested);
            return valid;
        }
        private static Color CaptureView(Camera camera, string path)
        {
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return pixels.GetPixel(640, 360);
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                target.Release();
                Destroy(target);
                Destroy(pixels);
            }
        }
    }
}
