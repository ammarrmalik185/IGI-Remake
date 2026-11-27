using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    [InitializeOnLoad]
    public static class CameraEffectsValidation
    {
        static IEnumerator routine;
        static double started;
        static CameraEffectsValidation()
        {
            if (!SessionState.GetBool("IGI_CAMERA_EFFECTS_TEST", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Update;
        }
        public static void Run()
        {
            TrainyardSecuritySetup.UpdateCameraEffects();
            EditorSceneManager.OpenScene(TrainyardSetup.ScenePath);
            SessionState.SetBool("IGI_CAMERA_EFFECTS_TEST", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
        }
        static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Camera effects validation timed out");
                if (!EditorApplication.isPlaying) return;
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (bootstrap == null || bootstrap.Mission == null) return;
                if (routine == null) routine = Checks(bootstrap.Mission);
                if (routine.MoveNext()) return;
                SessionState.SetBool("IGI_CAMERA_EFFECTS_TEST", false);
                Debug.Log("CAMERA_EFFECTS_VALIDATION_PASS: correct right/left sweep direction and endpoints, forward-only sweep and reversals, upright pan axis, four-second detection, accelerating beeps, sight lock, imported bindings, occlusion, pause, camera shutdown, one-shot destruction burst and cleanup");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                SessionState.SetBool("IGI_CAMERA_EFFECTS_TEST", false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        static IEnumerator Checks(MissionDirector mission)
        {
            mission.SetPaused(false);
            foreach (var guard in UnityEngine.Object.FindObjectsByType<EnemyGuard>(FindObjectsSortMode.None)) guard.enabled = false;
            var network = mission.Security;
            Require(network.cameras.Length == 5, "Expected five cameras");
            foreach (var camera in network.cameras)
            {
                camera.Initialize();
                camera.enabled = false;
                Require(camera.detectionClip != null && camera.detectionClip.name == "camera_beep", "Imported camera beep missing");
                Require(camera.destructionClip != null && camera.destructionClip.name == "explo_camera", "Imported destruction sound missing");
                Require(camera.destructionEffect != null, "Camera explosion prefab missing");
                foreach (var renderer in camera.destructionEffect.GetComponentsInChildren<ParticleSystemRenderer>())
                    Require(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported && renderer.sharedMaterial.mainTexture != null, "Imported particle material unavailable");
            }
            var controller = mission.player.GetComponent<CharacterController>();
            mission.player.enabled = false;
            controller.enabled = false;
            mission.player.transform.position = new Vector3(1000, 100, 1010);
            controller.enabled = true;
            Physics.SyncTransforms();
            foreach (var camera in network.cameras)
            {
                Vector3 initial = camera.head.forward;
                Vector3 previous = initial;
                bool movedLeft = false, movedRight = false;
                float minAngle = 0, maxAngle = 0;
                for (int step = 0; step < 1600; step++)
                {
                    camera.Tick(.05f);
                    Vector3 forward = camera.head.forward;
                    Require(Vector3.Dot(Vector3.ProjectOnPlane(initial, Vector3.up).normalized, Vector3.ProjectOnPlane(forward, Vector3.up).normalized) >= .499f, "Camera swept outside its forward sector: " + camera.name);
                    Require(Mathf.Abs(initial.y - forward.y) < .0001f, "Camera pan changed authored pitch: " + camera.name);
                    float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(previous, Vector3.up), Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.up);
                    float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(initial, Vector3.up), Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.up);
                    if (step == 0 && camera.speed > 0 && camera.right > 0)
                        Require(turn > 0, "Camera must begin by panning right, but panned left: " + camera.name);
                    Require(angle >= -Mathf.Min(camera.left, camera.sweepLimit) - .01f && angle <= Mathf.Min(camera.right, camera.sweepLimit) + .01f,
                        "Camera moved to the wrong side of its authored sweep limits: " + camera.name);
                    minAngle = Mathf.Min(minAngle, angle);
                    maxAngle = Mathf.Max(maxAngle, angle);
                    movedLeft |= turn < -.01f;
                    movedRight |= turn > .01f;
                    previous = forward;
                }
                if (camera.speed > 0 && camera.left + camera.right > 0)
                {
                    Require(movedLeft && movedRight, "Scanning camera did not reverse: " + camera.name);
                    Require(Mathf.Abs(minAngle + Mathf.Min(camera.left, camera.sweepLimit)) < .01f && Mathf.Abs(maxAngle - Mathf.Min(camera.right, camera.sweepLimit)) < .01f,
                        "Camera did not reach its correct left and right endpoints: " + camera.name);
                    Debug.Log("CAMERA_SWEEP_DIRECTION_PASS: " + camera.name + " left=" + minAngle + " right=" + maxAngle);
                }
                else
                    Require(Vector3.Angle(initial, camera.head.forward) < .01f, "Stationary authored camera moved");
            }
            var subject = network.cameras[0];
            subject.transform.SetPositionAndRotation(new Vector3(1000, 100, 1000), Quaternion.identity);
            subject.head.localRotation = Quaternion.identity;
            Physics.SyncTransforms();
            var source = subject.head.GetComponent<AudioSource>();
            Require(source != null && source.spatialBlend == 1, "Camera audio must be positional");
            subject.Tick(.2f);
            Require(subject.Detection > 0 && source.isPlaying, "Visible player did not start warning beep");
            mission.SetPaused(true);
            float detection = subject.Detection;
            subject.Tick(1);
            Require(source.mute && subject.Detection == detection, "Camera warning did not respect pause");
            mission.SetPaused(false);
            subject.Tick(.01f);
            Require(!source.mute, "Warning remained muted after resume");
            subject.ClearDetection();
            var countdown = typeof(SecurityCamera).GetField("nextBeep", BindingFlags.Instance | BindingFlags.NonPublic);
            var beepTimes = new List<float>();
            for (int step = 0; step < 195; step++)
            {
                float before = (float)countdown.GetValue(subject);
                subject.Tick(.02f);
                float after = (float)countdown.GetValue(subject);
                if (after > before - .019f) beepTimes.Add((step + 1) * .02f);
                Require(Quaternion.Angle(subject.head.localRotation, Quaternion.identity) < .01f, "Camera did not hold its view while detecting");
            }
            Require(subject.Detection > .97f && subject.Detection < 1 && !network.AlarmActive, "Camera should require four seconds to confirm a fresh sighting");
            Require(beepTimes.Count >= 8, "Expected an accelerating series of warning beeps");
            float firstGap = beepTimes[1] - beepTimes[0];
            float lastGap = beepTimes[beepTimes.Count - 1] - beepTimes[beepTimes.Count - 2];
            Require(firstGap > .6f && lastGap < .2f, "Beep cadence did not accelerate toward full detection");
            subject.Tick(.12f);
            Require(network.AlarmActive && subject.Detection == 1, "Four-second detection did not raise the alarm");
            Debug.Log("CAMERA_CADENCE_PASS: first gap " + firstGap + "s, final gap " + lastGap + "s");
            network.Silence();
            subject.left = subject.right = 0;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = DemoWorld.WorldLayer;
            wall.transform.position = new Vector3(1000, 101, 1005);
            wall.transform.localScale = new Vector3(10, 10, 1);
            Physics.SyncTransforms();
            subject.Tick(2);
            Require(subject.Detection == 0 && !source.isPlaying, "Occluded camera kept warning");
            UnityEngine.Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            subject.Tick(.2f);
            Require(source.isPlaying, "Camera failed to detect again");
            network.DisableCameras();
            Require(subject.Detection == 0 && !source.isPlaying, "Hacking did not stop camera warning");
            subject.GetComponent<Health>().TakeDamage(1000, Team.Player);
            Require(subject.Destroyed && !subject.indicator.enabled && source.isPlaying, "Destroyed camera did not explode and switch off");
            var effects = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            Require(effects.Length == 2, "Expected one fireball and smoke burst");
            Require(Vector3.Distance(effects[0].transform.position, subject.head.position) < 2, "Explosion did not spawn at camera");
            yield return null;
            Require(effects.All(effect => effect.particleCount > 0), "Explosion particles did not emit");
            subject.GetComponent<Health>().TakeDamage(1000, Team.Player);
            Require(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length == 2, "Repeated hits spawned duplicate explosions");
            float end = Time.time + 3.2f;
            while (Time.time < end) yield return null;
            Require(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length == 0, "Explosion did not clean up");
        }
    }
}
