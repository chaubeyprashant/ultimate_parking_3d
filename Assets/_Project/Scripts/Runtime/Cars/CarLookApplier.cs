using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UltimateCarParking.Cars
{
    /// <summary>
    /// Applies a <see cref="CarLook"/> to a spawned car without creating materials: every change is a MaterialPropertyBlock
    /// on the renderer's material slot, so the shared materials (also used by parked scenery cars) stay untouched and
    /// "factory" simply clears the block. Surfaces are found by material name: body paint, glass, wheels, number plates.
    /// Options a model cannot show (no glass slot, no wheel slot…) are reported by <see cref="Supports"/> and greyed out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarLookApplier : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private struct Slot { public Renderer r; public int index; public Material mat; }
        private readonly List<Slot> body = new List<Slot>(), glass = new List<Slot>(), wheels = new List<Slot>(), plates = new List<Slot>();
        private MaterialPropertyBlock block;
        private TextMeshPro plateText;
        private bool scanned;

        public int BodySlots { get { Scan(); return body.Count; } }
        public int GlassSlots { get { Scan(); return glass.Count; } }
        public int WheelSlots { get { Scan(); return wheels.Count; } }
        public string PlateShown => plateText != null && plateText.gameObject.activeSelf ? plateText.text : "";
        public CarLook Current { get; private set; } = new CarLook();

        public bool Supports(CustomSlot slot)
        {
            Scan();
            return slot switch
            {
                CustomSlot.Paint or CustomSlot.Finish => body.Count > 0,
                CustomSlot.Tint => glass.Count > 0,
                CustomSlot.Rims => wheels.Count > 0,
                _ => true,
            };
        }

        public static CarLookApplier On(GameObject car) => car.TryGetComponent(out CarLookApplier a) ? a : car.AddComponent<CarLookApplier>();

        private void Scan()
        {
            if (scanned) return;
            scanned = true;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) || r.GetComponent<TextMeshPro>() != null) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    var s = new Slot { r = r, index = i, mat = m };
                    if (n.Contains("numberplate")) plates.Add(s);
                    else if (n.Contains("glass") && (n.Contains("transp") || !n.Contains("light"))) glass.Add(s);
                    else if (n.Contains("tire") || n.Contains("wheel") || n.Contains("wheek")) wheels.Add(s);
                    else if ((n.Contains("body") && !n.Contains("bottom")) || n.EndsWith("__basecolor") || n.Contains("bloody_red")) body.Add(s);
                }
            }
        }

        public void Apply(CarLook look)
        {
            Scan();
            Current = look != null ? look.Clone() : new CarLook();
            block ??= new MaterialPropertyBlock();
            CarLook l = Current;

            // Paint + finish on the body.
            foreach (Slot s in body)
            {
                block.Clear();
                if (l.paint >= 0)
                {
                    block.SetColor(BaseColorId, CustomizationCatalog.Paints[Mathf.Clamp(l.paint, 0, CustomizationCatalog.Paints.Length - 1)].color);
                    if (s.mat.HasProperty(BaseMapId) && s.mat.GetTexture(BaseMapId) != null) block.SetTexture(BaseMapId, Texture2D.whiteTexture);
                }
                if (l.finish > 0)
                {
                    var f = CustomizationCatalog.FinishValues[Mathf.Clamp(l.finish, 0, 3)];
                    block.SetFloat(MetallicId, f.metallic);
                    block.SetFloat(SmoothnessId, f.smoothness);
                }
                Set(s, l.paint >= 0 || l.finish > 0);
            }

            // Window tint: darken the glass colour, keep its transparency rule.
            foreach (Slot s in glass)
            {
                block.Clear();
                if (l.tint > 0)
                {
                    Color c = s.mat.HasProperty(BaseColorId) ? s.mat.GetColor(BaseColorId) : Color.white;
                    float d = CustomizationCatalog.Tints[Mathf.Clamp(l.tint, 0, 3)].darkness;
                    block.SetColor(BaseColorId, new Color(c.r * (1f - d), c.g * (1f - d), c.b * (1f - d), Mathf.Lerp(c.a, 0.95f, d)));
                }
                Set(s, l.tint > 0);
            }

            // Rims: a multiply tint over the wheel texture (rubber stays black, the rim takes the colour).
            foreach (Slot s in wheels)
            {
                block.Clear();
                if (l.rims >= 0)
                {
                    var rim = CustomizationCatalog.Rims[Mathf.Clamp(l.rims, 0, CustomizationCatalog.Rims.Length - 1)];
                    Color c = s.mat.HasProperty(BaseColorId) ? s.mat.GetColor(BaseColorId) : Color.white;
                    block.SetColor(BaseColorId, new Color(c.r * rim.tint.r, c.g * rim.tint.g, c.b * rim.tint.b, c.a));
                    block.SetFloat(MetallicId, rim.metallic);
                }
                Set(s, l.rims >= 0);
            }

            ApplyPlate(l.plate);
            if (TryGetComponent(out Vehicle.VehicleLights lights))
                lights.HeadlightColor = CustomizationCatalog.Headlights[Mathf.Clamp(l.headlights, 0, 3)].color;
        }

        private void Set(Slot s, bool on)
        {
            if (on) s.r.SetPropertyBlock(block, s.index);
            else { block.Clear(); s.r.SetPropertyBlock(block, s.index); }
        }

        /// <summary>Custom plate: text on the rear of the car, at the model's plate (or the rear bumper's middle).</summary>
        private void ApplyPlate(string text)
        {
            if (string.IsNullOrEmpty(text)) { if (plateText != null) plateText.gameObject.SetActive(false); return; }
            if (plateText == null)
            {
                var go = new GameObject("CustomPlate");
                go.transform.SetParent(transform, false);
                plateText = go.AddComponent<TextMeshPro>();
                plateText.font = TMP_Settings.defaultFontAsset;
                plateText.alignment = TextAlignmentOptions.Center;
                plateText.fontSize = 1.6f;
                plateText.color = new Color(0.08f, 0.08f, 0.1f);
                plateText.rectTransform.sizeDelta = new Vector2(0.52f, 0.12f);
                plateText.enableAutoSizing = true; plateText.fontSizeMin = 0.4f; plateText.fontSizeMax = 1.6f;
                var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
                back.name = "PlateBack";
                DestroyImmediate(back.GetComponent<Collider>());
                back.transform.SetParent(go.transform, false);
                back.transform.localPosition = new Vector3(0f, 0f, 0.004f);
                back.transform.localScale = new Vector3(0.54f, 0.13f, 1f);
                back.GetComponent<Renderer>().sharedMaterial = PlateMaterial();
                back.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            plateText.gameObject.SetActive(true);
            plateText.text = text;
            // Rear plate position: the plate slot's rear end if the model has one, else the middle of the rear bumper.
            Bounds b = LocalBounds(out float plateY);
            var t = plateText.transform;
            t.localPosition = new Vector3(0f, plateY, b.min.z - 0.012f);
            t.localRotation = Quaternion.Euler(0f, 0f, 0f);   // faces -Z (backwards), readable from behind
        }

        private Bounds LocalBounds(out float plateY)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
            foreach (Renderer r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponent<TextMeshPro>() != null || r.name == "PlateBack") continue;
                Bounds lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = transform.InverseTransformPoint(r.transform.TransformPoint(new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            plateY = Mathf.Lerp(b.min.y, b.max.y, 0.32f);
            if (plates.Count > 0)
            {
                var mf = plates[0].r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && plates[0].index < mf.sharedMesh.subMeshCount)
                {
                    Bounds sb = mf.sharedMesh.GetSubMesh(plates[0].index).bounds;
                    plateY = transform.InverseTransformPoint(plates[0].r.transform.TransformPoint(sb.center)).y;
                }
            }
            return b;
        }

        private static Material plateMat;
        /// <summary>Shared plate background (a material asset in Resources, so its shader ships with the build).</summary>
        private static Material PlateMaterial() => plateMat != null ? plateMat : (plateMat = Resources.Load<Material>("Customization/PlateBack"));
    }
}
