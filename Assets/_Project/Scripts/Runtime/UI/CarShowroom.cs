using System.Collections;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// 3D preview of a car on a slowly turning platform. Spawns the car's model prefab with physics and
    /// driving scripts switched off, painted with its CarData paint. Switching cars slides the old one out.
    /// </summary>
    public sealed class CarShowroom : MonoBehaviour
    {
        [SerializeField] private Transform turntable;
        [SerializeField] private float spinDegreesPerSecond = 14f;
        [SerializeField, Min(0.05f)] private float slideSeconds = 0.45f;
        [SerializeField] private float slideDistance = 7f;

        private GameObject current;
        private float manualSpin;
        private float spinVelocity;

        public GameObject CurrentPreview => current;

        /// <summary>Adds drag rotation (degrees); decays smoothly.</summary>
        public void AddSpin(float degrees) => spinVelocity += degrees;

        public void Show(CarData car, int direction = 0)
        {
            if (turntable == null) { Debug.LogError("[CarShowroom] No turntable assigned.", this); return; }
            if (car == null || car.ModelPrefab == null) return;

            GameObject previous = current;
            current = Spawn(car);
            if (direction == 0 || !isActiveAndEnabled)
            {
                if (previous != null) Destroy(previous);
                return;
            }
            StartCoroutine(Slide(previous, Vector3.zero, Vector3.left * slideDistance * direction, destroyAfter: true));
            StartCoroutine(Slide(current, Vector3.right * slideDistance * direction, Vector3.zero, destroyAfter: false));
        }

        private GameObject Spawn(CarData car)
        {
            GameObject go = Instantiate(car.ModelPrefab, turntable);
            go.name = $"Preview_{car.CarId}";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is Cars.CarPaint)) mb.enabled = false;
            if (go.TryGetComponent(out Rigidbody rb))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            foreach (WheelCollider wc in go.GetComponentsInChildren<WheelCollider>(true)) wc.enabled = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (car.PaintMaterial != null && go.TryGetComponent(out CarPaint paint)) paint.Apply(car.PaintMaterial);
            return go;
        }

        private IEnumerator Slide(GameObject go, Vector3 from, Vector3 to, bool destroyAfter)
        {
            if (go == null) yield break;
            for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
            {
                if (go == null) yield break;
                go.transform.localPosition = Vector3.LerpUnclamped(from, to, UiMotion.EaseInOutCubic(t / slideSeconds));
                yield return null;
            }
            if (go == null) yield break;
            go.transform.localPosition = to;
            if (destroyAfter) Destroy(go);
        }

        private void Update()
        {
            if (turntable == null) return;
            manualSpin = spinVelocity;
            spinVelocity = Mathf.Lerp(spinVelocity, 0f, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            turntable.Rotate(0f, (spinDegreesPerSecond + manualSpin) * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }
}
