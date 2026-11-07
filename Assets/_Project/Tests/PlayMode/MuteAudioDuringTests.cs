using NUnit.Framework;
using UnityEngine;

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Runs once around every PlayMode test in this namespace: silences all audio so headless test runs don't play
    /// the game's music and engine sounds through the developer's speakers. Audio logic still runs (and is tested).
    /// </summary>
    [SetUpFixture]
    public class MuteAudioDuringTests
    {
        [OneTimeSetUp]
        public void Mute() => AudioListener.volume = 0f;

        [OneTimeTearDown]
        public void Restore() => AudioListener.volume = 1f;
    }
}
