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
            if (mission.CapturingTruck) { valid &= VerifyWallFaces(output); valid &= VerifyBuildingCollision(output); valid &= VerifyFirstBuilding(output); }
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
                valid &= VerifyLadders(mission, output);
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
        private static bool VerifyLadders(MissionDirector mission, string output)
        {
            var ladders = FindObjectsByType<ClimbableLadder>(FindObjectsSortMode.None);
            Array.Sort(ladders, (a, b) => a.BottomExit.sqrMagnitude.CompareTo(b.BottomExit.sqrMagnitude));
            bool valid = ladders.Length == 6;
            var motor = mission.player; var body = motor.GetComponent<CharacterController>();
            Vector3 original = motor.transform.position; Quaternion rotation = motor.transform.rotation, eye = motor.eye.transform.localRotation;
            motor.enabled = false;
            if (ladders.Length > 0) valid &= !motor.TryClimb(ladders[0]);
            for (int i = 0; i < ladders.Length; i++)
            {
                var ladder = ladders[i];
                body.enabled = false; motor.transform.position = ladder.BottomExit;
                Vector3 facing = ladder.Bottom - ladder.BottomExit; facing.y = 0;
                motor.transform.rotation = Quaternion.LookRotation(facing.normalized); motor.eye.transform.localRotation = Quaternion.identity;
                body.enabled = true; Physics.SyncTransforms();
                if (i == 0)
                {
                    var camera = new GameObject("Ladder verification camera").AddComponent<Camera>();
                    camera.transform.position = ladder.BottomExit + ladder.Outward * 0.2f + Vector3.up * 1.73f;
                    camera.transform.LookAt(ladder.Bottom + Vector3.up * 2.8f);
                    camera.nearClipPlane = 0.025f;
                    camera.fieldOfView = 78; camera.farClipPlane = 1000; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = motor.eye.backgroundColor;
                    CaptureView(camera, Path.Combine(output, "ladder-base.png")); Destroy(camera.gameObject);
                }
                bool up = motor.TryClimb(ladder);
                if (up)
                {
                    int ammo = mission.weapon.Ammo.Loaded;
                    valid &= !mission.weapon.Fire() && mission.weapon.Ammo.Loaded == ammo;
                    mission.SetPaused(true); Vector3 paused = motor.transform.position; motor.AdvanceClimb(1, 0.5f);
                    valid &= motor.transform.position == paused; mission.SetPaused(false);
                    for (int step = 0; step < 2000 && motor.IsClimbing; step++) motor.AdvanceClimb(1, 0.05f);
                    up = !motor.IsClimbing && body.enabled && Vector3.Distance(motor.transform.position, ladder.TopExit) < 0.12f
                        && Physics.Raycast(motor.transform.position + Vector3.up * 0.3f, Vector3.down, 0.8f, 1 << DemoWorld.WorldLayer);
                }
                if (i == 0) { motor.eye.transform.LookAt(motor.eye.transform.position - ladder.Outward * 20); CaptureView(motor.eye, Path.Combine(output, "ladder-roof.png")); }
                bool down = false;
                if (up)
                {
                    facing = ladder.Top - motor.transform.position; facing.y = 0;
                    motor.transform.rotation = Quaternion.LookRotation(facing.normalized); motor.eye.transform.localRotation = Quaternion.identity;
                    down = motor.TryClimb(ladder);
                    for (int step = 0; step < 2000 && motor.IsClimbing; step++) motor.AdvanceClimb(-1, 0.05f);
                    down &= !motor.IsClimbing && body.enabled && Vector3.Distance(motor.transform.position, ladder.BottomExit) < 0.12f;
                }
                Debug.Log("LADDER_TEST: " + ladder.name + " up=" + up + " down=" + down + " at=" + motor.transform.position + " roof=" + ladder.TopExit);
                valid &= up && down;
                if (motor.IsClimbing) { motor.LetGoLadder(); break; }
                if (i == 0 && down)
                {
                    facing = ladder.Bottom - motor.transform.position; facing.y = 0; motor.transform.rotation = Quaternion.LookRotation(facing.normalized);
                    bool release = motor.TryClimb(ladder);
                    for (int step = 0; step < 200 && motor.IsClimbing && motor.transform.position.y < ladder.Bottom.y + 1.2f; step++) motor.AdvanceClimb(1, 0.05f);
                    release &= motor.LetGoLadder() && !motor.IsClimbing && body.enabled;
                    valid &= release;
                }
            }
            if (motor.IsClimbing) motor.LetGoLadder();
            body.enabled = false; motor.transform.SetPositionAndRotation(original, rotation); motor.eye.transform.localRotation = eye;
            Physics.SyncTransforms(); body.enabled = true; motor.enabled = true;
            Debug.Log(valid ? "LADDER_TRAVERSAL_PASSED: six original ladders climb up/down, land on roofs, pause, release, and block shooting" : "LADDER_TRAVERSAL_FAILED");
            return valid;
        }
        private static bool VerifyFirstBuilding(string output)
        {
            var interior = FindFirstObjectByType<FirstBuildingInterior>();
            bool valid = interior != null && interior.rooms.Length == 3 && interior.doorways.Length == 2;
            if (!valid) { Debug.LogError("FIRST_BUILDING_FAILED: missing authored rooms or doorway metadata"); return false; }
            var probe = new GameObject("Doorway player sweep"); probe.layer = DemoWorld.PlayerLayer;
            var body = probe.AddComponent<CharacterController>(); body.enabled = false;
            body.height = 1.85f; body.center = Vector3.up * 0.925f; body.radius = 0.32f; body.stepOffset = 0.32f; body.skinWidth = 0.08f;
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (collider.gameObject.layer == DemoWorld.GuardLayer) Physics.IgnoreCollision(body, collider);
            foreach (var door in interior.doorways)
            {
                Vector3 foot = interior.transform.TransformPoint(door.foot) + Vector3.up * 0.08f;
                Vector3 normal = interior.transform.TransformDirection(door.normal).normalized;
                bool clear = true;
                for (int side = -1; side <= 1; side += 2)
                {
                    body.enabled = false; probe.transform.position = foot + normal * (side * 0.85f);
                    body.enabled = true; Physics.SyncTransforms();
                    for (int step = 0; step < 40; step++) body.Move(-normal * (side * 0.06f) + Vector3.down * 0.03f);
                    bool crossed = Vector3.Dot(probe.transform.position - foot, normal) * side < -0.6f;
                    clear &= crossed;
                    Debug.Log("FIRST_BUILDING_DOOR: slot=" + door.slot + " side=" + side + " crossed=" + crossed + " at=" + probe.transform.position);
                }
                valid &= clear;
                var camera = new GameObject("First building interior verification").AddComponent<Camera>();
                camera.transform.position = foot - normal * 1.4f + Vector3.up * 1.65f;
                camera.transform.rotation = Quaternion.LookRotation(-normal); camera.nearClipPlane = 0.025f;
                camera.fieldOfView = 78; camera.farClipPlane = 300; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.55f, 0.64f, 0.68f);
                CaptureView(camera, Path.Combine(output, door.slot == 4 ? "first-building-ladder-room.png" : "first-building-interior.png"));
                if (door.slot == 4) { camera.transform.rotation = Quaternion.LookRotation(normal); CaptureView(camera, Path.Combine(output, "first-building-doorway.png")); }
                Destroy(camera.gameObject);
            }
            probe.SetActive(false); Destroy(probe);
            Debug.Log(valid ? "FIRST_BUILDING_PASSED: three authored interiors and both doorways traversable in both directions" : "FIRST_BUILDING_FAILED");
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
                var weapons = camera.GetComponentInParent<WeaponController>();
                if (weapons != null && weapons.eye == camera) weapons.RenderView(target);
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
