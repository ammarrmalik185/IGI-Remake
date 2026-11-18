using System;
using NUnit.Framework;
using UnityEngine;

namespace IGIRebuilt.Tests
{
    public sealed class TrainyardTests
    {
        [Test]
        public void TruckRequiresProximityFacingAndAnUnobstructedInteraction()
        {
            using (var scene = new TruckTestScene())
            {
                scene.target.position += Vector3.forward * 8;
                Assert.IsFalse(scene.mission.CollectIntel());
                scene.target.position -= Vector3.forward * 8;
                scene.camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                Assert.IsFalse(scene.mission.CollectIntel());
                scene.camera.transform.rotation = Quaternion.identity;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    wall.layer = DemoWorld.WorldLayer;
                    wall.transform.position = scene.camera.transform.position + Vector3.forward;
                    wall.transform.localScale = new Vector3(2, 2, 0.2f);
                    Physics.SyncTransforms();
                    Assert.IsFalse(scene.mission.CollectIntel());
                    wall.GetComponent<Collider>().enabled = false;
                    Physics.SyncTransforms();
                    Assert.IsTrue(scene.mission.CollectIntel());
                    Assert.IsTrue(scene.mission.Progress.Complete);
                    Assert.IsFalse(scene.mission.CollectIntel());
                }
                finally { UnityEngine.Object.DestroyImmediate(wall); }
            }
        }
        [Test]
        public void ADeadPlayerCannotSecureTheTruck()
        {
            using (var scene = new TruckTestScene())
            {
                scene.health.TakeDamage(100, Team.Guard);
                Assert.IsFalse(scene.mission.CollectIntel());
                Assert.IsFalse(scene.mission.Progress.Complete);
            }
        }
        private sealed class TruckTestScene : IDisposable
        {
            public readonly GameObject root = new GameObject("Truck objective test");
            public readonly Transform target;
            public readonly Camera camera;
            public readonly Health health;
            public readonly MissionDirector mission;
            private readonly GameConfiguration config;
            public TruckTestScene()
            {
                root.transform.position = Vector3.up * 1000;
                var player = new GameObject("Player"); player.transform.SetParent(root.transform, false);
                var motor = player.AddComponent<FirstPersonMotor>(); health = player.GetComponent<Health>(); health.team = Team.Player; health.ResetHealth(100);
                var eye = new GameObject("Eye"); eye.transform.SetParent(player.transform, false); eye.transform.localPosition = Vector3.up * 1.7f;
                camera = eye.AddComponent<Camera>(); motor.eye = camera;
                target = new GameObject("Truck door").transform; target.SetParent(root.transform, false); target.position = camera.transform.position + Vector3.forward * 2;
                config = ScriptableObject.CreateInstance<GameConfiguration>(); config.captureTruck = true;
                mission = root.AddComponent<MissionDirector>(); mission.configuration = config; mission.player = motor; mission.terminal = target; mission.extraction = target;
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
