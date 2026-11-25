using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Builds the code-driven uGUI: dark navy panels (#0B1026 at 85%) with 2 px neon cyan borders and
    /// rounded 10 px corners, chunky bold text, glowing accents.
    /// </summary>
    public static class UIFactory
    {
        private static Material glowMaterial;
        private static Material outlineMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            glowMaterial = null;
            outlineMaterial = null;
        }

        public static TMP_FontAsset Font
        {
            get { return TMP_Settings.defaultFontAsset; }
        }

        /// <summary>Soft cyan underlay glow (asset from GameAssets so the shader variant ships in builds).</summary>
        public static Material GlowMaterial
        {
            get
            {
                if (glowMaterial != null) return glowMaterial;
                GameAssets a = GameManager.I != null ? GameManager.I.Assets : null;
                if (a != null && a.tmpGlowMaterial != null) glowMaterial = a.tmpGlowMaterial;
                else if (Font != null)
                {
                    glowMaterial = new Material(Font.material);
                    glowMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    glowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.23f, 0.94f, 1f, 0.55f));
                    glowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.7f);
                    glowMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);
                }
                return glowMaterial;
            }
        }

        public static Material OutlineMaterial
        {
            get
            {
                if (outlineMaterial != null) return outlineMaterial;
                GameAssets a = GameManager.I != null ? GameManager.I.Assets : null;
                if (a != null && a.tmpOutlineMaterial != null) outlineMaterial = a.tmpOutlineMaterial;
                else if (Font != null)
                {
                    outlineMaterial = new Material(Font.material);
                    outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                    outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.03f, 0.1f, 1f));
                    outlineMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
                }
                return outlineMaterial;
            }
        }

        // ---------------- layout ----------------

        public static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 MidLeft = new Vector2(0f, 0.5f);
        public static readonly Vector2 MidRight = new Vector2(1f, 0.5f);

        // ---------------- graphics ----------------

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        /// <summary>Navy rounded panel with a cyan border. Returns the fill image (border is a child).</summary>
        public static Image Panel(Transform parent, string name, Color? fill = null, Color? border = null, bool raycast = true)
        {
            Image bg = Image(parent, name, UISprites.RoundedFill, fill ?? Palette.PanelNavy, raycast);
            bg.pixelsPerUnitMultiplier = 1.6f;
            UIContrast.Tag(bg, UIContrast.Role.PanelFill);
            if (border.HasValue && border.Value.a <= 0f) return bg;
            Image b = Image(bg.transform, "Border", UISprites.RoundedBorder, border ?? Palette.Cyan, false);
            b.pixelsPerUnitMultiplier = 1.6f;
            Stretch(b.rectTransform);
            UIContrast.Tag(b, UIContrast.Role.Border);
            return bg;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold)
        {
            RectTransform rt = Rect(name, parent);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            if (color == Palette.TextSoft || color == Palette.Dim) UIContrast.Tag(t, UIContrast.Role.SoftText);
            return t;
        }

        /// <summary>Lets keyboard / controller navigation reach this control.</summary>
        public static void EnableNav(Selectable s)
        {
            if (s == null) return;
            Navigation nav = s.navigation;
            nav.mode = Navigation.Mode.Automatic;
            s.navigation = nav;
        }

        /// <summary>
        /// Big menu button: hover/focus = scale 1.05 + glow + blip, click = punch + pop. Keyboard-navigable.
        /// </summary>
        public static Button MenuButton(Transform parent, string name, string label, float fontSize, Action onClick, Vector2 size, Color? border = null)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = size;
            Image glow = Image(rt, "Glow", UISprites.Glow, border ?? Palette.Cyan, false);
            Place(glow.rectTransform, Center, Center, Vector2.zero, size + new Vector2(150f, 120f));
            Image bg = Panel(rt, "Bg", new Color(0.05f, 0.09f, 0.2f, 1f), border ?? Palette.Cyan, true);
            Stretch(bg.rectTransform);
            // Hover / focus tint drawn over the opaque fill (the glow behind it only shows at the edges).
            Color hc = border ?? Palette.Cyan;
            Image highlight = Image(rt, "Highlight", UISprites.RoundedFill, new Color(hc.r, hc.g, hc.b, 0f), false);
            highlight.pixelsPerUnitMultiplier = 1.6f;
            Stretch(highlight.rectTransform);
            Button b = rt.gameObject.AddComponent<Button>();
            // Transition must be None before targetGraphic is set: otherwise the default ColorTint fades the
            // fill to the half-transparent "disabled" tint while the menu is still non-interactable, and with
            // no later transitions it stays see-through.
            b.transition = Selectable.Transition.None;
            b.targetGraphic = bg;
            bg.canvasRenderer.SetColor(Color.white);
            EnableNav(b);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (!string.IsNullOrEmpty(label))
            {
                TextMeshProUGUI t = Text(rt, "Label", label, fontSize, Color.white);
                Stretch(t.rectTransform, 14f, 2f, 14f, 2f);
                t.enableWordWrapping = false;
                t.enableAutoSizing = true;
                t.fontSizeMax = fontSize;
                t.fontSizeMin = Mathf.Max(10f, fontSize * 0.55f);
            }
            rt.gameObject.AddComponent<MenuButtonFX>().Init(glow, highlight);
            return b;
        }

        /// <summary>Slider with a fill bar and round handle. onRelease fires when the pointer lets go.</summary>
        public static Slider Slider(Transform parent, string name, Vector2 size, Action<float> onChange, Action onRelease = null)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = size;
            Image bg = Image(rt, "Background", UISprites.RoundedSmallFill, new Color(0.03f, 0.06f, 0.14f, 1f), true);
            Stretch(bg.rectTransform, 0f, 10f, 0f, 10f);
            UIContrast.Tag(bg, UIContrast.Role.PanelFill);
            RectTransform fillArea = Rect("Fill Area", rt);
            Stretch(fillArea, 6f, 12f, 20f, 12f);
            Image fill = Image(fillArea, "Fill", UISprites.RoundedSmallFill, Palette.Cyan, false);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);
            RectTransform handleArea = Rect("Handle Slide Area", rt);
            Stretch(handleArea, 14f, 0f, 14f, 0f);
            Image handle = Image(handleArea, "Handle", UISprites.Circle, Color.white, true);
            handle.rectTransform.sizeDelta = new Vector2(size.y, 0f);
            Slider s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            ColorBlock cb = s.colors;
            cb.highlightedColor = Palette.Cyan;
            cb.selectedColor = Palette.Cyan;
            cb.pressedColor = Palette.Gold;
            s.colors = cb;
            EnableNav(s);
            if (onChange != null) s.onValueChanged.AddListener(v => onChange(v));
            SliderHook hook = rt.gameObject.AddComponent<SliderHook>();
            hook.OnRelease = onRelease;
            return s;
        }

        private static TMP_DefaultControls.Resources ControlResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = UISprites.RoundedSmallFill,
                background = UISprites.RoundedSmallFill,
                inputField = UISprites.RoundedSmallFill,
                knob = UISprites.Circle,
                checkmark = UISprites.Star,
                dropdown = UISprites.Triangle,
                mask = UISprites.RoundedSmallFill,
            };
        }

        /// <summary>TextMeshPro dropdown styled for the dark UI.</summary>
        public static TMP_Dropdown Dropdown(Transform parent, string name, Vector2 size)
        {
            GameObject go = TMP_DefaultControls.CreateDropdown(ControlResources());
            go.name = name;
            SetLayer(go.transform, 5);
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            TMP_Dropdown d = go.GetComponent<TMP_Dropdown>();
            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.09f, 0.2f, 1f);
            UIContrast.Tag(bg, UIContrast.Role.PanelFill);
            if (d.captionText != null)
            {
                d.captionText.color = Color.white;
                d.captionText.fontSize = 24f;
                d.captionText.fontStyle = FontStyles.Bold;
            }
            if (d.itemText != null)
            {
                d.itemText.color = Color.white;
                d.itemText.fontSize = 22f;
            }
            Transform arrow = rt.Find("Arrow");
            if (arrow != null)
            {
                arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
                arrow.GetComponent<Image>().color = Palette.Cyan;
            }
            if (d.template != null)
            {
                Image tbg = d.template.GetComponent<Image>();
                if (tbg != null) tbg.color = new Color(0.03f, 0.05f, 0.12f, 0.98f);
                d.template.sizeDelta = new Vector2(d.template.sizeDelta.x, 300f);
                Transform itemBg = d.template.Find("Viewport/Content/Item/Item Background");
                if (itemBg != null) itemBg.GetComponent<Image>().color = new Color(0.1f, 0.2f, 0.35f, 1f);
                Transform check = d.template.Find("Viewport/Content/Item/Item Checkmark");
                if (check != null) check.GetComponent<Image>().color = Palette.Gold;
            }
            EnableNav(d);
            return d;
        }

        public static TMP_InputField InputField(Transform parent, string name, string placeholder, Vector2 size)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(ControlResources());
            go.name = name;
            SetLayer(go.transform, 5);
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            TMP_InputField f = go.GetComponent<TMP_InputField>();
            go.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.1f, 1f);
            if (f.textComponent != null)
            {
                f.textComponent.color = Color.white;
                f.textComponent.fontSize = 26f;
                f.textComponent.fontStyle = FontStyles.Bold;
            }
            TextMeshProUGUI ph = f.placeholder as TextMeshProUGUI;
            if (ph != null)
            {
                ph.text = placeholder;
                ph.color = Palette.Dim;
                ph.fontSize = 24f;
            }
            EnableNav(f);
            return f;
        }

        public static TextMeshProUGUI GlowText(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI t = Text(parent, name, text, size, color, align);
            if (GlowMaterial != null) t.fontSharedMaterial = GlowMaterial;
            return t;
        }

        public static TextMeshProUGUI OutlineText(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI t = Text(parent, name, text, size, color, align);
            if (OutlineMaterial != null) t.fontSharedMaterial = OutlineMaterial;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, float fontSize, Action onClick,
            Color? fill = null, Color? border = null, Color? textColor = null)
        {
            Image bg = Panel(parent, name, fill ?? new Color(0.07f, 0.11f, 0.24f, 0.95f), border ?? Palette.Cyan, true);
            Button b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.35f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            Navigation nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<UIButtonJuice>();
            if (!string.IsNullOrEmpty(label))
            {
                TextMeshProUGUI t = Text(bg.transform, "Label", label, fontSize, textColor ?? Color.white);
                Stretch(t.rectTransform, 10f, 2f, 10f, 2f);
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Overflow;
                // Shrink long labels to fit instead of cutting them off.
                t.enableAutoSizing = true;
                t.fontSizeMax = fontSize;
                t.fontSizeMin = Mathf.Max(10f, fontSize * 0.55f);
            }
            return b;
        }

        public static Button IconButton(Transform parent, string name, string icon, float size, Action onClick, Color iconColor)
        {
            Button b = Button(parent, name, null, 0f, onClick);
            Image ic = Image(b.transform, "Icon", UISprites.Icon(icon), iconColor, false);
            Place(ic.rectTransform, Center, Center, Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
            return b;
        }

        /// <summary>A filled progress bar. Returns the background; fill is the out param.</summary>
        public static Image Bar(Transform parent, string name, Color fillColor, out Image fill, bool vertical = false)
        {
            Image bg = Image(parent, name, UISprites.RoundedSmallFill, new Color(0.02f, 0.04f, 0.1f, 0.9f), false);
            bg.pixelsPerUnitMultiplier = 1f;
            Image border = Image(bg.transform, "Border", UISprites.RoundedBorder, Palette.WithAlpha(Palette.Cyan, 0.6f), false);
            border.pixelsPerUnitMultiplier = 2.4f;
            UIContrast.Tag(border, UIContrast.Role.Border);
            Stretch(border.rectTransform, -2f, -2f, -2f, -2f);
            fill = Image(bg.transform, "Fill", UISprites.Square, fillColor, false);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = vertical ? UnityEngine.UI.Image.FillMethod.Vertical : UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform, 3f, 3f, 3f, 3f);
            return bg;
        }

        public static Image Icon(Transform parent, string iconId, Color color, float size)
        {
            Image img = Image(parent, "Icon_" + iconId, UISprites.Icon(iconId), color, false);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            return img;
        }

        public static CanvasGroup Group(GameObject go)
        {
            CanvasGroup g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            return g;
        }

        /// <summary>Fades a CanvasGroup in or out (unscaled) and toggles raycast blocking.</summary>
        public static Coroutine Fade(CanvasGroup g, bool show, float duration = 0.2f)
        {
            if (g == null) return null;
            g.blocksRaycasts = show;
            g.interactable = show;
            float from = g.alpha;
            float to = show ? 1f : 0f;
            return Juice.Run(Juice.Tween(duration, k => { if (g != null) g.alpha = Mathf.Lerp(from, to, k); }));
        }

        /// <summary>Little UI starburst for purchases and skill buys.</summary>
        public static void Burst(RectTransform at, Color color, int count = 10)
        {
            if (at == null) return;
            Transform canvas = at.GetComponentInParent<Canvas>() != null ? at.GetComponentInParent<Canvas>().transform : at.parent;
            for (int i = 0; i < count; i++) Juice.Run(BurstOne(canvas, at, color, i, count));
        }

        private static IEnumerator BurstOne(Transform canvas, RectTransform at, Color color, int i, int count)
        {
            Image img = Image(canvas, "Burst", UISprites.Star, color, false);
            RectTransform rt = img.rectTransform;
            rt.position = at.position;
            float size = UnityEngine.Random.Range(14f, 28f);
            rt.sizeDelta = new Vector2(size, size);
            float ang = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector3 dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            float dist = UnityEngine.Random.Range(60f, 140f) * rt.lossyScale.x;
            Vector3 start = rt.position;
            float t = 0f;
            const float d = 0.45f;
            while (t < d && rt != null)
            {
                float k = t / d;
                rt.position = start + dir * dist * Juice.EaseOutCubic(k);
                rt.localRotation = Quaternion.Euler(0f, 0f, k * 360f);
                img.color = new Color(color.r, color.g, color.b, 1f - k);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
        }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }
    }
}
