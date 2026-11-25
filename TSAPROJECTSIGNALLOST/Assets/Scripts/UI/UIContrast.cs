using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// High-contrast UI: tagged graphics switch to solid black panels, bright yellow borders and pure
    /// white secondary text when the Accessibility setting is on, and restore their colors when it is off.
    /// </summary>
    public class UIContrast : MonoBehaviour
    {
        public enum Role { PanelFill, Border, SoftText, Backdrop }

        private static readonly List<UIContrast> all = new List<UIContrast>();

        private Role role;
        private Graphic graphic;
        private Color original;
        private bool ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        public static void Tag(Graphic g, Role role)
        {
            if (g == null) return;
            UIContrast c = g.gameObject.GetComponent<UIContrast>();
            if (c == null) c = g.gameObject.AddComponent<UIContrast>();
            c.role = role;
            c.graphic = g;
            c.original = g.color;
            c.ready = true;
            c.Apply();
        }

        public static void ApplyAll()
        {
            for (int i = 0; i < all.Count; i++) all[i].Apply();
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            Apply();
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        public void Apply()
        {
            if (!ready || graphic == null) return;
            if (!GameSettings.HighContrast)
            {
                graphic.color = original;
                return;
            }
            switch (role)
            {
                case Role.PanelFill: graphic.color = new Color(0f, 0f, 0f, Mathf.Max(original.a, 0.97f)); break;
                case Role.Border: graphic.color = new Color(1f, 0.92f, 0.25f, 1f); break;
                case Role.SoftText: graphic.color = Color.white; break;
                case Role.Backdrop: graphic.color = new Color(0f, 0f, 0f, Mathf.Max(original.a, 0.9f)); break;
            }
        }
    }
}
