using NUnit.Framework;

/// <summary>Assembly-wide: tests exercise real progression, so the testing "unlock all" switch is off.</summary>
[SetUpFixture]
public class TestingUnlocksOff
{
    [OneTimeSetUp]
    public void Off() => UltimateCarParking.Core.TestingUnlocks.UnlockAll = false;

    [OneTimeTearDown]
    public void Restore() => UltimateCarParking.Core.TestingUnlocks.UnlockAll = UltimateCarParking.Core.TestingUnlocks.DefaultUnlockAll;
}
