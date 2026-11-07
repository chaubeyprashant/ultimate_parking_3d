using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Map pin for one country: flag in a frame above a location dot, tour number and name tag, lock overlay,
    /// completed check and a lime selection ring with a short pop. The name tag only shows when zoomed in or selected.
    /// </summary>
    public sealed class CountryMarker : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RectTransform pin;
        [SerializeField] private Image frame;
        [SerializeField] private Image flag;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private GameObject checkBadge;
        [SerializeField] private Image selectionRing;
        [SerializeField] private Image dot;
        [SerializeField] private GameObject nameTag;
        [SerializeField] private TMP_Text nameLabel;

        public CountryData Country { get; private set; }
        public CountryState State { get; private set; }
        public bool Selected { get; private set; }
        public bool NameVisible => nameTag != null && nameTag.activeSelf;
        public Button Button => button;

        public void Bind(CountryData country, int number, Action<CountryMarker> onClick)
        {
            Country = country;
            name = "Marker_" + country.CountryId;
            if (flag != null) flag.sprite = country.Flag;
            if (nameLabel != null) nameLabel.text = $"<color=#B6FF2E>{number:00}</color>  {country.DisplayName.ToUpperInvariant()}";
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => onClick(this)); }
        }

        public void SetState(CountryState state)
        {
            State = state;
            bool locked = state == CountryState.Locked;
            if (lockOverlay != null) lockOverlay.SetActive(locked);
            if (checkBadge != null) checkBadge.SetActive(state == CountryState.Completed);
            if (frame != null) frame.color = locked ? UiPalette.SurfaceRaised : state == CountryState.Completed ? UiPalette.Lime : UiPalette.TextPrimary;
            if (dot != null) dot.color = locked ? UiPalette.Locked : UiPalette.Lime;
        }

        public void SetSelected(bool selected)
        {
            bool changed = selected != Selected;
            Selected = selected;
            if (selectionRing != null) selectionRing.enabled = selected;
            if (selected) transform.SetAsLastSibling();          // draw above neighbours
            if (changed && selected && isActiveAndEnabled) StartCoroutine(Pop());
            else if (pin != null && !selected) pin.localScale = Vector3.one;
        }

        public void SetNameVisible(bool visible)
        {
            if (nameTag != null && nameTag.activeSelf != visible) nameTag.SetActive(visible);
        }

        private IEnumerator Pop()
        {
            if (pin == null) yield break;
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.22f;
                pin.localScale = Vector3.one * (1f + 0.28f * Mathf.Sin(k * Mathf.PI) + 0.12f * k);
                yield return null;
            }
            pin.localScale = Vector3.one * 1.12f;
        }
    }
}
