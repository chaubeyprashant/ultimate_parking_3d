using NUnit.Framework;
using UltimateCarParking.DebugTools;

namespace UltimateCarParking.Tests
{
    public class PerfWindowTests
    {
        [Test]
        public void Empty_Window_Has_No_Frames()
        {
            Assert.AreEqual(0, new PerfWindow().Compute().Frames);
        }

        [Test]
        public void Steady_60fps_Reports_60_And_No_Hitches()
        {
            var w = new PerfWindow();
            for (int i = 0; i < 600; i++) w.Add(1f / 60f);
            PerfStats s = w.Compute();
            Assert.AreEqual(600, s.Frames);
            Assert.AreEqual(60f, s.AvgFps, 0.1f);
            Assert.AreEqual(16.67f, s.AvgMs, 0.05f);
            Assert.AreEqual(0, s.Over33Ms);
            Assert.AreEqual(0, s.Over50Ms);
            Assert.AreEqual(60f, s.OnePercentLowFps, 0.5f);
        }

        [Test]
        public void Hitches_Are_Counted_And_Pull_Down_The_One_Percent_Low()
        {
            var w = new PerfWindow();
            for (int i = 0; i < 490; i++) w.Add(1f / 60f);
            for (int i = 0; i < 6; i++) w.Add(0.060f);   // six 60 ms hitches
            for (int i = 0; i < 4; i++) w.Add(0.040f);   // four 40 ms frames
            PerfStats s = w.Compute();
            Assert.AreEqual(10, s.Over33Ms);
            Assert.AreEqual(6, s.Over50Ms);
            Assert.AreEqual(60f, s.WorstMs, 0.01f);
            Assert.Less(s.OnePercentLowFps, 20f, "1% low reflects the hitches");
            Assert.Greater(s.AvgFps, 55f, "the average hides them, which is why 1% low is reported");
        }

        [Test]
        public void P95_Is_The_Ninety_Fifth_Percentile_Frame()
        {
            var w = new PerfWindow();
            for (int i = 1; i <= 100; i++) w.Add(i / 1000f);   // 1..100 ms
            PerfStats s = w.Compute();
            Assert.AreEqual(96f, s.P95Ms, 1.01f);
            Assert.AreEqual(100f, s.WorstMs, 0.01f);
        }

        [Test]
        public void Reset_Clears_And_Capacity_Is_Respected()
        {
            var w = new PerfWindow(8);
            for (int i = 0; i < 20; i++) w.Add(0.016f);
            Assert.AreEqual(8, w.Count, "never grows past its buffer");
            w.Reset();
            Assert.AreEqual(0, w.Count);
            w.Add(0f); w.Add(-1f);
            Assert.AreEqual(0, w.Count, "ignores non-positive frame times");
        }
    }
}
