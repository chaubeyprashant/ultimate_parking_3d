using System.Collections.Generic;
using UnityEngine;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Missions
{
    /// <summary>Everything an objective may use. Created by the <see cref="MissionRunner"/> once per attempt.</summary>
    public sealed class MissionContext
    {
        private readonly MissionRunner runner;
        private readonly Material zoneMaterial;
        private readonly Transform root;
        private readonly List<GameObject> spawned = new List<GameObject>();

        public VehicleController Vehicle { get; }
        public ParkingValidator Validator { get; }
        public MissionGauge Gauge => runner.Gauge;
        public ParkingSensorAudio SensorAudio { get; }
        /// <summary>The car's collision counter (hits on walls, barriers, other cars).</summary>
        public CollisionHandler Collisions => Vehicle != null ? Vehicle.GetComponent<CollisionHandler>() : null;

        internal MissionContext(MissionRunner runner, VehicleController vehicle, ParkingValidator validator, Material zoneMaterial,
            Transform root, ParkingSensorAudio sensorAudio)
        {
            this.runner = runner;
            Vehicle = vehicle;
            Validator = validator;
            this.zoneMaterial = zoneMaterial;
            this.root = root;
            SensorAudio = sensorAudio;
        }

        /// <summary>Creates a target bay owned by this attempt (destroyed with it).</summary>
        public ParkingZone CreateZone(string name, Vector3 centre, Quaternion rotation, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre, rotation);
            var zone = go.AddComponent<ParkingZone>();
            zone.Configure(size, zoneMaterial);
            spawned.Add(go);
            return zone;
        }

        /// <summary>Tells the HUD / guide which bay to point at.</summary>
        public void SetActiveZone(ParkingZone zone) => runner.SetActiveZone(zone);

        /// <summary>Short toast ("KERB STRIKE 1/2"). <paramref name="bad"/> = red flash.</summary>
        public void Notify(string message, bool bad) => runner.RaiseNotice(message, bad);

        internal void DestroySpawned()
        {
            foreach (GameObject go in spawned)
                if (go != null) Object.Destroy(go);
            spawned.Clear();
        }
    }
}
