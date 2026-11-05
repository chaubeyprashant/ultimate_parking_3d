using System;
using UnityEngine;

namespace UltimateCarParking.Environment
{
    /// <summary>One reusable module (cone, barrier, pillar, wall, parked car, line…) placed in a level.</summary>
    [Serializable]
    public struct PropPlacement
    {
        public GameObject prefab;
        public Vector3 position;
        [Tooltip("Degrees around Y.")]
        public float yaw;
        [Tooltip("Local scale; (0,0,0) means (1,1,1).")]
        public Vector3 scale;
        [Tooltip("Colour variant for modules with a PaintVariant (parked cars, containers).")]
        public int variant;

        public PropPlacement(GameObject prefab, Vector3 position, float yaw = 0f, Vector3 scale = default, int variant = 0)
        {
            this.prefab = prefab;
            this.position = position;
            this.yaw = yaw;
            this.scale = scale;
            this.variant = variant;
        }

        public Vector3 EffectiveScale => scale == Vector3.zero ? Vector3.one : scale;
    }
}
