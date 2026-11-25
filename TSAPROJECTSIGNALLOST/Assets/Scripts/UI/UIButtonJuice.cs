using UnityEngine;
using UnityEngine.EventSystems;

namespace SignalLost
{
    /// <summary>Hover swell and press squish on every button (unscaled time, so it works during hitstop).</summary>
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float pressScale = 0.94f;
        [SerializeField] private bool deselectOnRelease = true;

        public bool DeselectOnRelease
        {
            get { return deselectOnRelease; }
            set { deselectOnRelease = value; }
        }

        private bool hovered;
        private bool pressed;
        private float current = 1f;

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e)
        {
            pressed = false;
            // HUD buttons must not keep keyboard focus, or Space (the SHIELD key) would re-click them.
            if (deselectOnRelease && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            current = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float target = pressed ? pressScale : (hovered ? hoverScale : 1f);
            if (Mathf.Abs(current - target) < 0.0005f) return;
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * current;
        }
    }
}
