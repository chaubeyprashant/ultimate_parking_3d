using UnityEngine;
using UltimateCarParking.Save;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Cars
{
    public enum UpgradeKind { Engine, Brakes, Steering }

    /// <summary>
    /// Per-car upgrades bought in the garage with coins, three levels each, stored in <see cref="SaveData.carUpgrades"/>.
    /// Engine: more drive torque. Brakes: more brake torque. Steering: faster steering and a tighter lock (a smaller
    /// turning circle, which matters most in tight bays). Applied on top of the car's own handling when it spawns.
    /// </summary>
    public static class CarUpgrades
    {
        public const int MaxLevel = 3;
        private static readonly int[] Prices = { 250, 500, 900 };

        public static int Level(SaveData save, CarData car, UpgradeKind kind)
        {
            CarUpgradeState u = car != null ? save?.FindUpgrades(car.CarId) : null;
            if (u == null) return 0;
            return Mathf.Clamp(kind switch { UpgradeKind.Engine => u.engine, UpgradeKind.Brakes => u.brakes, _ => u.steering }, 0, MaxLevel);
        }

        /// <summary>Price of the next level (0 when maxed).</summary>
        public static int NextPrice(SaveData save, CarData car, UpgradeKind kind)
        {
            int l = Level(save, car, kind);
            return l >= MaxLevel ? 0 : Prices[l];
        }

        public static bool CanUpgrade(SaveData save, CarData car, UpgradeKind kind) =>
            save != null && car != null && GarageService.IsOwned(save, car) && Level(save, car, kind) < MaxLevel &&
            save.coins >= NextPrice(save, car, kind);

        /// <summary>Buys the next level if the car is owned and affordable. Caller saves.</summary>
        public static bool TryUpgrade(SaveData save, CarData car, UpgradeKind kind)
        {
            if (!CanUpgrade(save, car, kind)) return false;
            save.coins -= NextPrice(save, car, kind);
            CarUpgradeState u = save.GetOrAddUpgrades(car.CarId);
            switch (kind)
            {
                case UpgradeKind.Engine: u.engine++; break;
                case UpgradeKind.Brakes: u.brakes++; break;
                default: u.steering++; break;
            }
            return true;
        }

        /// <summary>The car's handling with its upgrades applied (a copy; the asset is untouched).</summary>
        public static VehicleHandling Upgraded(VehicleHandling stock, int engine, int brakes, int steering)
        {
            VehicleHandling h = JsonUtility.FromJson<VehicleHandling>(JsonUtility.ToJson(stock));
            h.motorTorque *= 1f + 0.1f * engine;
            h.reverseTorque *= 1f + 0.1f * engine;
            h.brakeTorque *= 1f + 0.15f * brakes;
            h.handbrakeTorque *= 1f + 0.1f * brakes;
            h.steerSpeed *= 1f + 0.12f * steering;
            h.maxSteerAngle = Mathf.Min(45f, h.maxSteerAngle + 1.5f * steering);
            return h;
        }

        public static VehicleHandling Upgraded(SaveData save, CarData car) =>
            car == null ? null : Upgraded(car.Handling, Level(save, car, UpgradeKind.Engine), Level(save, car, UpgradeKind.Brakes),
                Level(save, car, UpgradeKind.Steering));

        public static string Name(UpgradeKind k) => k switch { UpgradeKind.Engine => "ENGINE", UpgradeKind.Brakes => "BRAKES", _ => "STEERING" };
    }
}
