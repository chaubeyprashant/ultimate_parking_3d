using NUnit.Framework;
using UltimateCarParking.Quality;

namespace UltimateCarParking.Tests
{
    public class MemoryPolicyTests
    {
        [Test]
        public void First_Warning_Cleans_Up_Now_In_A_Menu_And_Waits_While_Driving()
        {
            Assert.AreEqual(MemoryAction.CleanUpNow, new MemoryPolicy().OnLowMemory(10, playerIsDriving: false));
            Assert.AreEqual(MemoryAction.CleanUpWhenSafe, new MemoryPolicy().OnLowMemory(10, playerIsDriving: true));
        }

        [Test]
        public void A_Second_Warning_Within_Five_Minutes_Halves_Textures_Once()
        {
            var p = new MemoryPolicy();
            p.OnLowMemory(10, false);
            Assert.AreEqual(MemoryAction.HalveTextures, p.OnLowMemory(100, false));
            Assert.IsTrue(p.TexturesHalved);
            Assert.AreEqual(MemoryAction.CleanUpNow, p.OnLowMemory(120, false), "already halved: only clean up");
        }

        [Test]
        public void Warnings_Far_Apart_Do_Not_Add_Up()
        {
            var p = new MemoryPolicy();
            p.OnLowMemory(10, false);
            Assert.AreEqual(MemoryAction.CleanUpNow, p.OnLowMemory(10 + 400, false), "outside the five-minute window");
            Assert.IsFalse(p.TexturesHalved);
        }

        [Test]
        public void Never_Halves_Textures_Mid_Drive_It_Waits_For_A_Safe_Moment()
        {
            var p = new MemoryPolicy();
            p.OnLowMemory(10, true);
            Assert.AreEqual(MemoryAction.CleanUpWhenSafe, p.OnLowMemory(20, true), "texture change would hitch the drive");
            Assert.IsFalse(p.TexturesHalved);
            Assert.AreEqual(MemoryAction.HalveTextures, p.OnLowMemory(30, false));
        }

        [Test]
        public void Deferred_Cleanup_Runs_Exactly_Once()
        {
            var p = new MemoryPolicy();
            bool pending = true;
            Assert.IsTrue(p.TakeDeferredCleanup(ref pending));
            Assert.IsFalse(p.TakeDeferredCleanup(ref pending));
            Assert.IsFalse(pending);
        }

        [Test]
        public void Low_Tier_Uses_Half_Size_Textures_Others_Full_Size()
        {
            Assert.AreEqual(1, QualityPresets.For(QualityTier.Low).TextureMipLimit);
            Assert.AreEqual(0, QualityPresets.For(QualityTier.Mid).TextureMipLimit);
            Assert.AreEqual(0, QualityPresets.For(QualityTier.High).TextureMipLimit);
        }
    }
}
