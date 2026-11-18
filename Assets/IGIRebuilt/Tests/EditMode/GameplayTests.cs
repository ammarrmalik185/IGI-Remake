using NUnit.Framework;
using UnityEngine;

namespace IGIRebuilt.Tests
{
    public sealed class GameplayTests
    {
        [Test]
        public void MagazineConservesAmmunitionAcrossShotsAndReload()
        {
            var ammo = new MagazineState(3, 2);
            Assert.IsTrue(ammo.Fire());
            Assert.IsTrue(ammo.Fire());
            Assert.AreEqual(2, ammo.Reload());
            Assert.AreEqual(3, ammo.Loaded);
            Assert.AreEqual(0, ammo.Reserve);
            for (int i = 0; i < 3; i++) Assert.IsTrue(ammo.Fire());
            Assert.IsFalse(ammo.Fire());
            Assert.AreEqual(0, ammo.Reload());
        }
        [Test]
        public void MissionRequiresIntelAndLivingPlayerBeforeExtraction()
        {
            var mission = new MissionProgress();
            Assert.IsFalse(mission.Extract(true, true));
            Assert.IsTrue(mission.AcquireIntel());
            Assert.IsFalse(mission.AcquireIntel());
            Assert.IsFalse(mission.Extract(false, true));
            Assert.IsFalse(mission.Extract(true, false));
            Assert.IsTrue(mission.Extract(true, true));
            Assert.IsFalse(mission.Extract(true, true));
        }
        [Test]
        public void FriendlyFireIsIgnoredAndDeathFiresOnlyOnce()
        {
            var go = new GameObject("Test health");
            try
            {
                var health = go.AddComponent<Health>();
                health.team = Team.Guard;
                health.ResetHealth(100);
                int deaths = 0;
                health.Died += _ => deaths++;
                Assert.IsFalse(health.TakeDamage(50, Team.Guard));
                Assert.AreEqual(100, health.Current);
                Assert.IsTrue(health.TakeDamage(150, Team.Player));
                Assert.IsFalse(health.TakeDamage(1, Team.Player));
                Assert.AreEqual(0, health.Current);
                Assert.AreEqual(1, deaths);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
