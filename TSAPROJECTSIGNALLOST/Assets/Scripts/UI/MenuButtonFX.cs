using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Menu button juice: hover or keyboard focus = scale 1.05 + glow + soft blip; click/submit = punch
    /// scale + pop sound. Hovering also moves keyboard focus so mouse and keyboard never disagree.
    /// Runs on unscaled time so it works while the game is paused.
    /// </summary>
    public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerClickHandler, ISubmitHandler
    {
        private Image glow;
        private Image highlight;
        private Selectable selectable;
        private bool hovered;
        private bool selected;
        private float current = 1f;
        private float punch;

        public void Init(Image glowImage, Image highlightImage = null)
        {
            glow = glowImage;
            highlight = highlightImage;
            selectable = GetComponent<Selectable>();
            if (glow != null) glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            if (EventSystem.current != null && (selectable == null || selectable.interactable))
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovered = false;
        }

        public void OnSelect(BaseEventData e)
        {
            if (!selected) AudioManager.Play("blip", Random.Range(0.95f, 1.05f), 0.5f);
            selected = true;
        }

        public void OnDeselect(BaseEventData e)
        {
            selected = false;
        }

        public void OnPointerClick(PointerEventData e)
        {
            Clicked();
        }

        public void OnSubmit(BaseEventData e)
        {
            Clicked();
        }

        private void Clicked()
        {
            if (selectable != null && !selectable.interactable) return;
            punch = 1f;
            AudioManager.Play("pop", 1.25f, 0.7f);
        }

        private void OnDisable()
        {
            hovered = false;
            selected = false;
            punch = 0f;
            current = 1f;
            transform.localScale = Vector3.one;
            if (highlight != null) highlight.color = new Color(highlight.color.r, highlight.color.g, highlight.color.b, 0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            MenuConfig m = Balance.B != null ? Balance.B.menu : null;
            float hoverScale = m != null ? m.hoverScale : 1.05f;
            float clickPunch = m != null ? m.clickPunch : 1.15f;
            bool lit = (hovered || selected) && (selectable == null || selectable.interactable);
            current = Mathf.Lerp(current, lit ? hoverScale : 1f, 1f - Mathf.Exp(-18f * dt));
            punch = Mathf.MoveTowards(punch, 0f, dt / 0.15f);
            float p = 1f + (clickPunch - 1f) * Mathf.Sin(punch * Mathf.PI);
            transform.localScale = Vector3.one * current * p;
            if (glow != null)
            {
                Color c = glow.color;
                c.a = Mathf.MoveTowards(c.a, lit ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 0f, dt * 6f);
                glow.color = c;
            }
            if (highlight != null)
            {
                Color h = highlight.color;
                h.a = Mathf.MoveTowards(h.a, lit ? 0.22f : 0f, dt * 2.5f);
                highlight.color = h;
            }
        }
    }
}
