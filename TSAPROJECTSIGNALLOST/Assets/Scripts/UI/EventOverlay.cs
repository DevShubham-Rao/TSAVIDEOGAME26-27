using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Event UI: a dark vignette dims the world (blobs stay visible), a big timer bar sits at the top, and
    /// puzzle elements glow at full brightness. "SOLVED!" or "CLOSE ONE!" (never "FAILED").
    /// </summary>
    public class EventOverlay : MonoBehaviour
    {
        public static EventOverlay I { get; private set; }

        private RectTransform root;
        private CanvasGroup group;
        private Image vignette;
        private Image edgePulse;
        private Image flash;
        private RectTransform top;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI instructionText;
        private TextMeshProUGUI counterText;
        private Image timerFill;
        private TextMeshProUGUI timerText;
        private TextMeshProUGUI stampText;
        private TextMeshProUGUI warningText;
        private RectTransform widgets;
        private RectTransform oxygen;
        private Image oxygenFill;
        private Image shieldRing;
        private TextMeshProUGUI shieldCount;
        private RectTransform shieldButton;
        private readonly List<Image> nodeImages = new List<Image>();
        private readonly List<float> nodeLit = new List<float>();
        private readonly List<Color> nodeColors = new List<Color>();
        private readonly List<WorldButton> worldButtons = new List<WorldButton>();
        private bool edgeOn;
        private Color edgeColor;
        private float flashAlpha;
        private Color flashColor;

        public float TimerFraction { get; private set; }

        private class WorldButton
        {
            public RectTransform Rect;
            public Vector3 World;
        }

        private void Awake()
        {
            I = this;
            root = (RectTransform)transform;
            group = UIFactory.Group(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Build();
        }

        private void Build()
        {
            vignette = UIFactory.Image(root, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0.03f, 0.85f), false);
            UIFactory.Stretch(vignette.rectTransform, -200f, -200f, -200f, -200f);
            Image dim = UIFactory.Image(vignette.transform, "Dim", UISprites.Square, new Color(0f, 0.01f, 0.05f, 0.35f), false);
            UIFactory.Stretch(dim.rectTransform);
            edgePulse = UIFactory.Image(root, "EdgePulse", UISprites.Vignette, Color.clear, false);
            UIFactory.Stretch(edgePulse.rectTransform);
            flash = UIFactory.Image(root, "Flash", UISprites.Square, Color.clear, false);
            UIFactory.Stretch(flash.rectTransform);

            top = UIFactory.Place(UIFactory.Rect("Top", root), UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -200f), new Vector2(1000f, 220f));
            titleText = UIFactory.GlowText(top, "Title", "", 64f, Color.white);
            UIFactory.Place(titleText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(1000f, 76f));
            Image bar = UIFactory.Bar(top, "Timer", Palette.Orange, out timerFill);
            UIFactory.Place(bar.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -82f), new Vector2(940f, 34f));
            timerFill.fillAmount = 1f;
            timerText = UIFactory.OutlineText(bar.transform, "Seconds", "", 24f, Color.white);
            UIFactory.Stretch(timerText.rectTransform);
            instructionText = UIFactory.OutlineText(top, "Instructions", "", 34f, Palette.TextSoft);
            UIFactory.Place(instructionText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -124f), new Vector2(1200f, 46f));
            counterText = UIFactory.OutlineText(top, "Counter", "", 40f, Palette.Cyan);
            UIFactory.Place(counterText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -170f), new Vector2(800f, 50f));

            widgets = UIFactory.Rect("Widgets", root);
            UIFactory.Stretch(widgets);

            oxygen = UIFactory.Place(UIFactory.Rect("Oxygen", root), UIFactory.MidRight, UIFactory.MidRight, new Vector2(-660f, 0f), new Vector2(90f, 440f));
            Image obar = UIFactory.Bar(oxygen, "Bar", Palette.Blue, out oxygenFill, true);
            UIFactory.Place(obar.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -20f), new Vector2(48f, 380f));
            TextMeshProUGUI ol = UIFactory.OutlineText(oxygen, "Label", "O2", 34f, Palette.Blue);
            UIFactory.Place(ol.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, 10f), new Vector2(120f, 44f));
            oxygen.gameObject.SetActive(false);

            warningText = UIFactory.OutlineText(root, "Warning", "", 110f, Palette.Orange);
            UIFactory.Place(warningText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 60f), new Vector2(1700f, 160f));
            warningText.enableWordWrapping = false;
            stampText = UIFactory.OutlineText(root, "Stamp", "", 160f, Palette.Green);
            UIFactory.Place(stampText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 40f), new Vector2(1600f, 220f));
            stampText.enableWordWrapping = false;
            stampText.gameObject.SetActive(false);
        }

        // ---------------- lifecycle ----------------

        public void Show(EventDef def)
        {
            ClearWidgets();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            top.gameObject.SetActive(false);
            titleText.text = def.name.ToUpperInvariant();
            instructionText.text = "";
            counterText.text = "";
            timerFill.fillAmount = 1f;
            timerText.text = "";
            stampText.gameObject.SetActive(false);
            Juice.Run(Juice.Tween(0.3f, k => vignette.color = new Color(0f, 0f, 0.03f, 0.85f * k)));
        }

        public void ShowWarning(string text, Color tint)
        {
            warningText.gameObject.SetActive(true);
            warningText.text = string.IsNullOrEmpty(text) ? "WARNING!" : text;
            warningText.color = tint;
            Juice.StampIn(warningText.transform, 0.3f);
            Flash(tint, 0.12f);
        }

        public void BeginPuzzle(EventDef def)
        {
            warningText.gameObject.SetActive(false);
            top.gameObject.SetActive(true);
            Juice.StampIn(top, 0.3f);
            timerFill.color = Palette.Hex(def.tint);
        }

        public void Hide()
        {
            ClearWidgets();
            oxygen.gameObject.SetActive(false);
            edgeOn = false;
            group.blocksRaycasts = false;
            group.interactable = false;
            Juice.Run(Juice.Tween(0.3f, k => { if (group != null) group.alpha = 1f - k; }));
        }

        public void SetTimer(float frac, float secondsLeft)
        {
            TimerFraction = frac;
            timerFill.fillAmount = frac;
            timerText.text = secondsLeft.ToString("0.0") + "s";
        }

        public void SetInstructions(string s)
        {
            instructionText.text = s ?? "";
        }

        public void SetCounter(string s)
        {
            counterText.text = s ?? "";
            Juice.PunchScale(counterText.transform, 1.2f, 0.15f);
        }

        public void Stamp(string text, Color color)
        {
            stampText.gameObject.SetActive(true);
            stampText.text = text;
            stampText.color = color;
            Juice.StampIn(stampText.transform, 0.4f);
            ClearWidgets();
            if (text == "SOLVED!") VFXPlayer.ConfettiScreen(80);
        }

        public void EdgePulse(Color c, bool on)
        {
            edgeOn = on;
            edgeColor = c;
        }

        public void Flash(Color c, float alpha)
        {
            if (GameSettings.ReduceFlashing) return;
            flashColor = c;
            flashAlpha = alpha;
        }

        public void SetOxygen(float frac, bool visible)
        {
            oxygen.gameObject.SetActive(visible);
            oxygenFill.fillAmount = frac;
            oxygenFill.color = Color.Lerp(Palette.Red, Palette.Blue, frac);
        }

        // ---------------- widgets ----------------

        public void ClearWidgets()
        {
            foreach (Transform c in widgets) Destroy(c.gameObject);
            nodeImages.Clear();
            nodeLit.Clear();
            nodeColors.Clear();
            worldButtons.Clear();
            shieldButton = null;
        }

        public void CreateShieldButton(Action onPress)
        {
            Button b = UIFactory.Button(widgets, "Shield", null, 0f, () => onPress(), new Color(0.3f, 0.12f, 0.02f, 0.95f), Palette.Orange);
            shieldButton = (RectTransform)b.transform;
            UIFactory.Place(shieldButton, UIFactory.Center, UIFactory.Center, new Vector2(0f, -150f), new Vector2(330f, 330f));
            Image img = b.GetComponent<Image>();
            img.sprite = UISprites.Circle;
            img.type = Image.Type.Simple;
            Transform border = b.transform.Find("Border");
            if (border != null) Destroy(border.gameObject);
            shieldRing = UIFactory.Image(shieldButton, "Ring", UISprites.Ring, Palette.Orange, false);
            UIFactory.Stretch(shieldRing.rectTransform, -16f, -16f, -16f, -16f);
            shieldRing.type = Image.Type.Filled;
            shieldRing.fillMethod = Image.FillMethod.Radial360;
            shieldRing.fillOrigin = (int)Image.Origin360.Top;
            shieldRing.fillAmount = 0f;
            Image icon = UIFactory.Icon(shieldButton, "shield", Palette.Gold, 140f);
            UIFactory.Place(icon.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 34f), new Vector2(140f, 140f));
            TextMeshProUGUI label = UIFactory.OutlineText(shieldButton, "Label", "SHIELD", 52f, Color.white);
            UIFactory.Place(label.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -72f), new Vector2(300f, 64f));
            shieldCount = UIFactory.OutlineText(shieldButton, "Count", "", 30f, Palette.Gold);
            UIFactory.Place(shieldCount.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -120f), new Vector2(300f, 40f));
        }

        public void SetShieldProgress(float frac, int remaining)
        {
            if (shieldButton == null) return;
            shieldRing.fillAmount = frac;
            shieldCount.text = remaining > 0 ? remaining + " TO GO" : "SHIELDED!";
            Juice.PunchScale(shieldButton, 1.08f, 0.1f);
        }

        public void CreateNodes(int count, Action<int> onTap)
        {
            float radius = 230f;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / count;
                Vector2 pos = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius - 120f);
                Button b = UIFactory.Button(widgets, "Node" + i, null, 0f, () => onTap(index), new Color(0.05f, 0.1f, 0.2f, 1f), Palette.Cyan);
                RectTransform rt = (RectTransform)b.transform;
                UIFactory.Place(rt, UIFactory.Center, UIFactory.Center, pos, new Vector2(130f, 130f));
                Image img = b.GetComponent<Image>();
                img.sprite = UISprites.HexFill;
                img.type = Image.Type.Simple;
                Transform border = b.transform.Find("Border");
                if (border != null)
                {
                    Image bi = border.GetComponent<Image>();
                    bi.sprite = UISprites.HexOutline;
                    bi.type = Image.Type.Simple;
                }
                Image glow = UIFactory.Image(rt, "Glow", UISprites.Glow, Color.clear, false);
                UIFactory.Place(glow.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(260f, 260f));
                glow.transform.SetAsFirstSibling();
                Image spark = UIFactory.Icon(rt, "spark", Palette.Cyan, 60f);
                UIFactory.Place(spark.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(60f, 60f));
                nodeImages.Add(glow);
                nodeLit.Add(0f);
                nodeColors.Add(Palette.Cyan);
            }
            // Wires between neighbours.
            for (int i = 0; i < count; i++)
            {
                RectTransform a = (RectTransform)nodeImages[i].transform.parent;
                RectTransform b = (RectTransform)nodeImages[(i + 1) % count].transform.parent;
                Image wire = UIFactory.Image(widgets, "Wire", UISprites.Square, Palette.WithAlpha(Palette.Cyan, 0.25f), false);
                Vector2 mid = (a.anchoredPosition + b.anchoredPosition) * 0.5f;
                Vector2 d = b.anchoredPosition - a.anchoredPosition;
                UIFactory.Place(wire.rectTransform, UIFactory.Center, UIFactory.Center, mid, new Vector2(d.magnitude, 6f));
                wire.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                wire.transform.SetAsFirstSibling();
            }
        }

        public void LightNode(int i, Color c, float seconds)
        {
            if (i < 0 || i >= nodeImages.Count) return;
            nodeLit[i] = seconds;
            nodeColors[i] = c;
            Juice.PunchScale(nodeImages[i].transform.parent, 1.18f, 0.2f);
        }

        public void CreateWorldButton(Vector3 world, Action onClick)
        {
            WorldButton wb = new WorldButton { World = world };
            Button b = UIFactory.Button(widgets, "Leak", null, 0f, null, new Color(0.3f, 0.05f, 0.05f, 0.6f), Palette.Red);
            wb.Rect = (RectTransform)b.transform;
            wb.Rect.sizeDelta = new Vector2(110f, 110f);
            Image img = b.GetComponent<Image>();
            img.sprite = UISprites.Circle;
            img.type = Image.Type.Simple;
            Transform border = b.transform.Find("Border");
            if (border != null) Destroy(border.gameObject);
            Image glow = UIFactory.Image(wb.Rect, "Glow", UISprites.Glow, new Color(1f, 0.5f, 0.2f, 0.8f), false);
            UIFactory.Place(glow.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(220f, 220f));
            Image spark = UIFactory.Icon(wb.Rect, "spark", Palette.Orange, 80f);
            UIFactory.Place(spark.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(80f, 80f));
            b.onClick.AddListener(() =>
            {
                onClick();
                worldButtons.Remove(wb);
                Destroy(wb.Rect.gameObject);
            });
            worldButtons.Add(wb);
            PositionWorldButton(wb);
            Juice.StampIn(wb.Rect, 0.3f);
        }

        private void PositionWorldButton(WorldButton wb)
        {
            Camera cam = GameManager.I.Cam;
            Vector3 sp = cam.WorldToScreenPoint(wb.World);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out Vector2 local);
            wb.Rect.anchorMin = wb.Rect.anchorMax = UIFactory.Center;
            wb.Rect.anchoredPosition = local;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            float pulse = GameSettings.ReduceFlashing ? 0.15f : 0.2f + 0.15f * Mathf.Sin(t * 8f);
            if (edgeOn) edgePulse.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, pulse);
            else edgePulse.color = Color.Lerp(edgePulse.color, Color.clear, dt * 6f);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.2f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);

            for (int i = 0; i < nodeImages.Count; i++)
            {
                nodeLit[i] -= dt;
                Color c = nodeColors[i];
                nodeImages[i].color = nodeLit[i] > 0f ? new Color(c.r, c.g, c.b, 0.95f) : new Color(c.r, c.g, c.b, 0.08f);
            }
            for (int i = 0; i < worldButtons.Count; i++)
            {
                PositionWorldButton(worldButtons[i]);
                worldButtons[i].Rect.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * 10f + i));
            }
        }
    }
}
