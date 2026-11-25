using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Tabbed settings: Audio, Graphics, Gameplay, Accessibility, Data. Every control applies instantly
    /// (live preview) and is saved to settings.json. "Restore Defaults" resets only the open tab.
    /// Fully usable with mouse, keyboard and controller.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        public static SettingsScreen I { get; private set; }

        private static readonly string[] TabIds = { "audio", "graphics", "gameplay", "accessibility", "data" };
        private static readonly string[] TabNames = { "AUDIO", "GRAPHICS", "GAMEPLAY", "ACCESSIBILITY", "DATA" };

        private const float RowHeight = 70f;
        private const float LabelWidth = 430f;
        private const float ControlWidth = 660f;

        private CanvasGroup group;
        private RectTransform panel;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<RectTransform> pages = new List<RectTransform>();
        private readonly List<Selectable> firstControls = new List<Selectable>();
        private readonly List<Action> refreshers = new List<Action>();
        private Button restoreButton;
        private Button backButton;
        private int currentTab;
        private bool open;
        private Action onClose;
        private TMP_Dropdown resolutionDropdown;
        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private TMP_InputField resetInput;
        private Button resetConfirm;
        private CanvasGroup resetStep2;
        private float lastSfxSample;
        private Slider uiScaleSlider;
        private SliderHook uiScaleHook;
        private float pendingUiScale = -1f;

        public bool IsOpen { get { return open; } }

        /// <summary>True while a dropdown list or the reset text field should get Esc instead of the screen.</summary>
        public bool BlocksEscape
        {
            get
            {
                if (resetInput != null && resetInput.isFocused) return true;
                return GameObject.Find("Dropdown List") != null;
            }
        }

        private void Awake()
        {
            I = this;
            group = UIFactory.Group(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Build();
        }

        private void OnEnable()
        {
            EventBus.SettingsChanged += OnSettingsChanged;
        }

        private void OnDisable()
        {
            EventBus.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged()
        {
            if (open) RefreshAll();
        }

        // ---------------- build ----------------

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            Image dim = UIFactory.Image(root, "Dim", UISprites.Square, new Color(0f, 0.01f, 0.05f, 0.75f), true);
            UIFactory.Stretch(dim.rectTransform);
            UIContrast.Tag(dim, UIContrast.Role.Backdrop);
            Image p = UIFactory.Panel(root, "Panel", new Color(0.03f, 0.05f, 0.13f, 1f));
            panel = p.rectTransform;
            UIFactory.Place(panel, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(1280f, 900f));
            TextMeshProUGUI title = UIFactory.GlowText(panel, "Title", "SETTINGS", 56f, Color.white);
            UIFactory.Place(title.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -20f), new Vector2(800f, 70f));

            for (int i = 0; i < TabIds.Length; i++)
            {
                int index = i;
                Button b = UIFactory.MenuButton(panel, "Tab_" + TabIds[i], TabNames[i], 24f, () => SelectTab(index, true), new Vector2(232f, 60f));
                UIFactory.Place((RectTransform)b.transform, UIFactory.TopCenter, UIFactory.Center, new Vector2(-492f + i * 246f, -128f), new Vector2(232f, 60f));
                tabButtons.Add(b);
                RectTransform page = UIFactory.Rect("Page_" + TabIds[i], panel);
                page.anchorMin = new Vector2(0f, 1f);
                page.anchorMax = new Vector2(1f, 1f);
                page.pivot = new Vector2(0.5f, 1f);
                page.anchoredPosition = new Vector2(0f, -180f);
                page.sizeDelta = new Vector2(-60f, 600f);
                VerticalLayoutGroup v = page.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 10f;
                v.childControlHeight = false;
                v.childControlWidth = true;
                v.childForceExpandHeight = false;
                v.childForceExpandWidth = true;
                pages.Add(page);
                firstControls.Add(null);
            }

            BuildAudio(pages[0]);
            BuildGraphics(pages[1]);
            BuildGameplay(pages[2]);
            BuildAccessibility(pages[3]);
            BuildData(pages[4]);

            restoreButton = UIFactory.MenuButton(panel, "Restore", "RESTORE DEFAULTS", 24f, RestoreDefaults, new Vector2(360f, 66f), Palette.Gold);
            UIFactory.Place((RectTransform)restoreButton.transform, UIFactory.BottomLeft, UIFactory.Center, new Vector2(220f, 56f), new Vector2(360f, 66f));
            backButton = UIFactory.MenuButton(panel, "Back", "BACK", 28f, Close, new Vector2(300f, 66f));
            UIFactory.Place((RectTransform)backButton.transform, UIFactory.BottomRight, UIFactory.Center, new Vector2(-190f, 56f), new Vector2(300f, 66f));
            SelectTab(0, false);
        }

        private RectTransform Row(RectTransform page, string label, string hint = null)
        {
            RectTransform row = UIFactory.Rect("Row_" + label, page);
            row.sizeDelta = new Vector2(0f, RowHeight);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = RowHeight;
            Image bg = UIFactory.Image(row, "Bg", UISprites.RoundedSmallFill, new Color(0.06f, 0.1f, 0.2f, 0.55f), false);
            UIFactory.Stretch(bg.rectTransform);
            TextMeshProUGUI l = UIFactory.Text(row, "Label", label, 28f, Color.white, TextAlignmentOptions.Left);
            l.rectTransform.anchorMin = new Vector2(0f, 0f);
            l.rectTransform.anchorMax = new Vector2(0f, 1f);
            l.rectTransform.pivot = new Vector2(0f, 0.5f);
            l.rectTransform.anchoredPosition = new Vector2(24f, hint != null ? 10f : 0f);
            l.rectTransform.sizeDelta = new Vector2(LabelWidth, 0f);
            l.enableWordWrapping = false;
            if (hint != null)
            {
                TextMeshProUGUI h = UIFactory.Text(row, "Hint", hint, 17f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Normal);
                h.rectTransform.anchorMin = new Vector2(0f, 0f);
                h.rectTransform.anchorMax = new Vector2(0f, 0f);
                h.rectTransform.pivot = new Vector2(0f, 0f);
                h.rectTransform.anchoredPosition = new Vector2(26f, 4f);
                h.rectTransform.sizeDelta = new Vector2(LabelWidth, 24f);
                h.enableWordWrapping = false;
            }
            return row;
        }

        private static RectTransform ControlArea(RectTransform row)
        {
            RectTransform c = UIFactory.Rect("Control", row);
            c.anchorMin = new Vector2(1f, 0.5f);
            c.anchorMax = new Vector2(1f, 0.5f);
            c.pivot = new Vector2(1f, 0.5f);
            c.anchoredPosition = new Vector2(-20f, 0f);
            c.sizeDelta = new Vector2(ControlWidth, RowHeight - 14f);
            return c;
        }

        private void First(RectTransform page, Selectable s)
        {
            int i = pages.IndexOf(page);
            if (i >= 0 && firstControls[i] == null) firstControls[i] = s;
        }

        private Slider AddSlider(RectTransform page, string label, Func<float> get, Action<float> set, float min, float max,
            Func<float, string> format, Action onRelease = null, string hint = null)
        {
            RectTransform c = ControlArea(Row(page, label, hint));
            TextMeshProUGUI value = UIFactory.Text(c, "Value", "", 26f, Palette.Cyan, TextAlignmentOptions.Right);
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            value.rectTransform.pivot = new Vector2(1f, 0.5f);
            value.rectTransform.sizeDelta = new Vector2(120f, 50f);
            bool refreshing = false;
            Slider s = UIFactory.Slider(c, "Slider", new Vector2(ControlWidth - 140f, 40f), v =>
            {
                if (refreshing) return;
                set(v);
                value.text = format(v);
            }, onRelease);
            RectTransform srt = (RectTransform)s.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            s.minValue = min;
            s.maxValue = max;
            refreshers.Add(() =>
            {
                refreshing = true;
                float v = get();
                s.SetValueWithoutNotify(v);
                value.text = format(v);
                refreshing = false;
            });
            First(page, s);
            return s;
        }

        private Button SmallButton(RectTransform parent, string label, Action onClick, Vector2 size)
        {
            Button b = UIFactory.MenuButton(parent, label, label, 24f, onClick, size);
            return b;
        }

        private void AddToggle(RectTransform page, string label, Func<bool> get, Action<bool> set, string hint = null)
        {
            RectTransform c = ControlArea(Row(page, label, hint));
            TextMeshProUGUI text = null;
            Image fill = null;
            Button b = SmallButton(c, "Toggle", () => set(!get()), new Vector2(220f, 54f));
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            text = b.GetComponentInChildren<TextMeshProUGUI>();
            fill = b.transform.Find("Bg").GetComponent<Image>();
            refreshers.Add(() =>
            {
                bool on = get();
                text.text = on ? "ON" : "OFF";
                text.color = on ? Color.white : Palette.TextSoft;
                fill.color = on ? new Color(0.1f, 0.45f, 0.3f, 1f) : new Color(0.08f, 0.1f, 0.16f, 1f);
            });
            First(page, b);
        }

        private void AddChoice(RectTransform page, string label, string[] options, Func<int> get, Action<int> set, string hint = null)
        {
            RectTransform c = ControlArea(Row(page, label, hint));
            float gap = 10f;
            float w = (ControlWidth - gap * (options.Length - 1)) / options.Length;
            List<Image> fills = new List<Image>();
            List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Button b = SmallButton(c, options[i], () => set(index), new Vector2(w, 54f));
                RectTransform rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(i * (w + gap), 0f);
                fills.Add(b.transform.Find("Bg").GetComponent<Image>());
                labels.Add(b.GetComponentInChildren<TextMeshProUGUI>());
                if (i == 0) First(page, b);
            }
            refreshers.Add(() =>
            {
                int sel = get();
                for (int i = 0; i < fills.Count; i++)
                {
                    bool on = i == sel;
                    fills[i].color = on ? new Color(0.1f, 0.42f, 0.55f, 1f) : new Color(0.05f, 0.08f, 0.17f, 1f);
                    labels[i].color = on ? Color.white : Palette.TextSoft;
                }
            });
        }

        private void AddButtonRow(RectTransform page, string label, string buttonText, Action onClick, string hint = null)
        {
            RectTransform c = ControlArea(Row(page, label, hint));
            Button b = SmallButton(c, buttonText, onClick, new Vector2(340f, 54f));
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            First(page, b);
        }

        private static void Set(Action<SettingsData> change)
        {
            if (SettingsManager.I != null) SettingsManager.I.Change(change);
        }

        private static string Pct(float v)
        {
            return Mathf.RoundToInt(v * 100f) + "%";
        }

        // ---------------- tabs ----------------

        private void BuildAudio(RectTransform page)
        {
            AddSlider(page, "Master", () => GameSettings.S.master, v => Set(s => s.master = v), 0f, 1f, Pct);
            AddSlider(page, "Music", () => GameSettings.S.music, v => Set(s => s.music = v), 0f, 1f, Pct);
            Slider sfx = AddSlider(page, "Sound Effects", () => GameSettings.S.sfx, v =>
            {
                Set(s => s.sfx = v);
                // Keyboard / controller changes have no "release": sample right away (rate-limited).
                SliderHook h = currentSfxHook;
                if ((h == null || !h.Dragging) && Time.unscaledTime - lastSfxSample > 0.2f)
                {
                    lastSfxSample = Time.unscaledTime;
                    AudioManager.Play("pop");
                }
            }, 0f, 1f, Pct, () => AudioManager.Play("pop"), "Release the slider to hear a pop");
            currentSfxHook = sfx.GetComponent<SliderHook>();
            RectTransform note = Row(page, "Mixer");
            TextMeshProUGUI info = UIFactory.Text(ControlArea(note), "Info", "Master > Music / SFX (dB, logarithmic)", 22f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Normal);
            UIFactory.Stretch(info.rectTransform);
        }

        private SliderHook currentSfxHook;

        private void BuildGraphics(RectTransform page)
        {
            AddChoice(page, "Display", new[] { "FULLSCREEN", "WINDOWED" }, () => GameSettings.S.fullscreen ? 0 : 1, i => Set(s => s.fullscreen = i == 0));

            RectTransform c = ControlArea(Row(page, "Resolution"));
            resolutionDropdown = UIFactory.Dropdown(c, "Resolution", new Vector2(ControlWidth, 54f));
            RectTransform drt = (RectTransform)resolutionDropdown.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.pivot = new Vector2(0f, 0.5f);
            drt.anchoredPosition = Vector2.zero;
            resolutionDropdown.onValueChanged.AddListener(i =>
            {
                if (i < 0 || i >= resolutions.Count) return;
                Vector2Int r = resolutions[i];
                Set(s =>
                {
                    s.resolutionWidth = r.x;
                    s.resolutionHeight = r.y;
                });
            });
            refreshers.Add(RefreshResolutions);

            AddToggle(page, "VSync", () => GameSettings.S.vsync, v => Set(s => s.vsync = v));
            List<int> fps = Balance.B != null ? Balance.B.graphics.fpsOptions : new List<int> { 30, 60, 120, -1 };
            string[] fpsLabels = new string[fps.Count];
            for (int i = 0; i < fps.Count; i++) fpsLabels[i] = fps[i] < 0 ? "UNLIMITED" : fps[i].ToString();
            AddChoice(page, "FPS Cap", fpsLabels, () => Mathf.Max(0, fps.IndexOf(GameSettings.S.fpsCap)), i => Set(s => s.fpsCap = fps[i]), "Used when VSync is off");
            AddChoice(page, "Quality", new[] { "LOW", "MEDIUM", "HIGH" }, () => GameSettings.S.quality, i => Set(s => s.quality = i));
            AddToggle(page, "Bloom", () => GameSettings.S.bloom, v => Set(s => s.bloom = v));
            AddChoice(page, "Particle Density", new[] { "LOW", "MED", "HIGH" }, () => GameSettings.S.particleDensity, i => Set(s => s.particleDensity = i));
        }

        private void RefreshResolutions()
        {
            resolutions.Clear();
            HashSet<long> seen = new HashSet<long>();
            foreach (Resolution r in Screen.resolutions)
            {
                long key = ((long)r.width << 32) | (uint)r.height;
                if (seen.Add(key)) resolutions.Add(new Vector2Int(r.width, r.height));
            }
            Vector2Int current = new Vector2Int(GameSettings.S.resolutionWidth, GameSettings.S.resolutionHeight);
            if (!resolutions.Contains(current)) resolutions.Add(current);
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            List<string> labels = new List<string>();
            foreach (Vector2Int r in resolutions) labels.Add(r.x + " x " + r.y);
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, resolutions.IndexOf(current)));
            resolutionDropdown.RefreshShownValue();
        }

        private void BuildGameplay(RectTransform page)
        {
            AddSlider(page, "Screen Shake", () => GameSettings.S.shake, v => Set(s => s.shake = v), 0f, 1f, Pct, () => Juice.Shake(0.15f, 0.25f));
            AddChoice(page, "Number Format", new[] { "SUFFIX  1.2M", "SCIENTIFIC  1.2e6" }, () => GameSettings.Scientific ? 1 : 0,
                i => Set(s => s.notation = i == 1 ? "scientific" : "suffix"));
            AddChoice(page, "Vacuum", new[] { "HOLD", "TOGGLE" }, () => GameSettings.VacuumToggle ? 1 : 0, i => Set(s => s.vacuumMode = i == 1 ? "toggle" : "hold"),
                "Toggle: right-click to start, right-click again to drop");
            AddChoice(page, "Mail Text Speed", new[] { "SLOW", "NORMAL", "INSTANT" }, () => GameSettings.S.mailSpeed, i => Set(s => s.mailSpeed = i));
            AddToggle(page, "Tutorial Hints", () => GameSettings.S.tutorialHints, v => Set(s => s.tutorialHints = v), "One-time tips from Commander Hale");
            AddButtonRow(page, "Tutorial", "REPLAY TUTORIAL", ReplayTutorial);
        }

        private void BuildAccessibility(RectTransform page)
        {
            AddToggle(page, "Colorblind Mode", () => GameSettings.S.colorblind, v => Set(s => s.colorblind = v), "Shapes on blobs and their chutes");
            RectTransform previewRow = Row(page, "Shapes");
            RectTransform pc = ControlArea(previewRow);
            BlobColor[] cols = { BlobColor.Red, BlobColor.Blue, BlobColor.Green, BlobColor.Gold };
            for (int i = 0; i < cols.Length; i++)
            {
                Image dot = UIFactory.Image(pc, "Blob", UISprites.Circle, Palette.ForBlob(cols[i]), false);
                RectTransform dr = dot.rectTransform;
                dr.anchorMin = dr.anchorMax = new Vector2(0f, 0.5f);
                dr.pivot = new Vector2(0f, 0.5f);
                dr.anchoredPosition = new Vector2(i * 160f, 0f);
                dr.sizeDelta = new Vector2(54f, 54f);
                Texture2D tex = UISprites.GlyphTexture(ShapeGlyph.ShapeFor(cols[i]));
                Image glyph = UIFactory.Image(dr, "Glyph", Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)), Color.white, false);
                UIFactory.Stretch(glyph.rectTransform, 6f, 6f, 6f, 6f);
                TextMeshProUGUI n = UIFactory.Text(dr, "Name", cols[i].ToString().ToUpperInvariant(), 18f, Palette.TextSoft, TextAlignmentOptions.Left);
                n.rectTransform.anchorMin = n.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                n.rectTransform.pivot = new Vector2(0f, 0.5f);
                n.rectTransform.anchoredPosition = new Vector2(8f, 0f);
                n.rectTransform.sizeDelta = new Vector2(90f, 30f);
                refreshers.Add(() => glyph.enabled = GameSettings.Colorblind);
            }
            AddToggle(page, "Reduce Flashing", () => GameSettings.S.reduceFlashing, v => Set(s => s.reduceFlashing = v), "No full-screen flashes, softer Golden effects");
            uiScaleSlider = AddSlider(page, "UI Scale", () => pendingUiScale > 0f ? pendingUiScale : GameSettings.S.uiScale, v =>
            {
                pendingUiScale = v;
                // Mouse drags apply on release (so the slider doesn't move under the cursor); keys apply now.
                if (uiScaleHook == null || !uiScaleHook.Dragging) CommitUiScale();
            }, Balance.B != null ? Balance.B.graphics.uiScaleMin : 0.8f, Balance.B != null ? Balance.B.graphics.uiScaleMax : 1.3f, Pct, CommitUiScale);
            uiScaleHook = uiScaleSlider.GetComponent<SliderHook>();
            AddToggle(page, "High-Contrast UI", () => GameSettings.S.highContrast, v => Set(s => s.highContrast = v));
        }

        private void CommitUiScale()
        {
            if (pendingUiScale <= 0f) return;
            float v = Mathf.Round(pendingUiScale * 20f) / 20f;
            pendingUiScale = -1f;
            Set(s => s.uiScale = v);
            RefreshAll();
        }

        private void BuildData(RectTransform page)
        {
            AddButtonRow(page, "Reset Save", "RESET SAVE...", () =>
            {
                UIFactory.Fade(resetStep2, true, 0.15f);
                resetInput.text = "";
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resetInput.gameObject);
                resetInput.ActivateInputField();
            }, "Erases progress. Settings are kept.");

            RectTransform row = Row(page, "Type RESET to confirm");
            resetStep2 = UIFactory.Group(row.gameObject);
            resetStep2.alpha = 0f;
            resetStep2.blocksRaycasts = false;
            resetStep2.interactable = false;
            RectTransform c = ControlArea(row);
            resetInput = UIFactory.InputField(c, "ResetInput", "Type RESET", new Vector2(300f, 54f));
            RectTransform irt = (RectTransform)resetInput.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            resetInput.characterLimit = 5;
            resetInput.onValueChanged.AddListener(t => resetConfirm.interactable = t.Trim().ToUpperInvariant() == "RESET");
            resetConfirm = UIFactory.MenuButton(c, "ConfirmReset", "ERASE EVERYTHING", 22f, () =>
            {
                if (resetInput.text.Trim().ToUpperInvariant() != "RESET") return;
                AudioManager.Play("crunch");
                if (GameManager.I != null) GameManager.I.ResetSave();
            }, new Vector2(330f, 54f), Palette.Red);
            RectTransform crt = (RectTransform)resetConfirm.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            resetConfirm.interactable = false;

            RectTransform vrow = Row(page, "Version");
            TextMeshProUGUI ver = UIFactory.Text(ControlArea(vrow), "Version", "", 24f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Normal);
            UIFactory.Stretch(ver.rectTransform);
            refreshers.Add(() => ver.text = "Signal Lost " + Application.version + "   |   Unity " + Application.unityVersion);
            RectTransform frow = Row(page, "Files");
            TextMeshProUGUI files = UIFactory.Text(ControlArea(frow), "Files", "save.json (progress) and settings.json (settings) are separate", 20f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Normal);
            UIFactory.Stretch(files.rectTransform);
        }

        // ---------------- behaviour ----------------

        private void SelectTab(int index, bool sound)
        {
            currentTab = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].gameObject.SetActive(i == currentTab);
                Image fill = tabButtons[i].transform.Find("Bg").GetComponent<Image>();
                fill.color = i == currentTab ? new Color(0.1f, 0.42f, 0.55f, 1f) : new Color(0.05f, 0.09f, 0.2f, 0.96f);
            }
            restoreButton.gameObject.SetActive(TabIds[currentTab] != "data");
            if (resetStep2 != null && TabIds[currentTab] != "data")
            {
                resetStep2.alpha = 0f;
                resetStep2.blocksRaycasts = false;
                resetStep2.interactable = false;
            }
            if (sound) AudioManager.Play("whoosh", 1.4f, 0.35f);
            RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (Action r in refreshers) r();
        }

        public void Open(Action closed = null)
        {
            onClose = closed;
            open = true;
            if (SettingsManager.I != null) SettingsManager.I.SyncDisplayFromWindow();
            transform.SetAsLastSibling();
            if (ConfirmDialog.I != null) ConfirmDialog.I.transform.SetAsLastSibling();
            SelectTab(currentTab, false);
            UIFactory.Fade(group, true, 0.15f);
            panel.localScale = Vector3.one * 0.94f;
            Juice.Run(Juice.Tween(0.2f, k => panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, Juice.EaseOutBack(k))));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(tabButtons[currentTab].gameObject);
        }

        public void Close()
        {
            if (!open) return;
            CommitUiScale();
            open = false;
            UIFactory.Fade(group, false, 0.12f);
            if (SettingsManager.I != null) SettingsManager.I.SaveNow();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Action cb = onClose;
            onClose = null;
            if (cb != null) cb();
        }

        private void RestoreDefaults()
        {
            if (SettingsManager.I == null) return;
            SettingsManager.I.RestoreDefaults(TabIds[currentTab]);
            pendingUiScale = -1f;
            RefreshAll();
            AudioManager.Play("skill", 1.2f, 0.6f);
            Juice.PunchScale(panel, 1.02f, 0.15f);
        }

        private void ReplayTutorial()
        {
            if (TutorialDirector.I == null) return;
            TutorialDirector.I.Replay();
            bool playing = GameManager.I != null && (GameManager.I.State == GameState.Playing);
            Close();
            if (playing && PauseMenu.I != null && PauseMenu.I.IsOpen) PauseMenu.I.Close();
            if (HUD.I != null && !playing) HUD.I.Toast("TUTORIAL WILL PLAY WHEN YOU START", Palette.Cyan);
        }

        private void Update()
        {
            if (!open) return;
            // Tab / LB / RB style quick switching with the keyboard's Q and E is not needed; arrows reach the tabs.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && !BlocksEscape)
            {
                Selectable first = firstControls[currentTab];
                EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : tabButtons[currentTab].gameObject);
            }
        }
    }
}
