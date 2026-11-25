using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Edge HUD: Data counter (with today's earnings) top center, Day badge + shift clock + Signal top left,
    /// pause + buffs top right, combo meter on the left edge, Compressor gauge in the world. Mail and the
    /// skill tree live on the end-of-day screen, so they have no HUD buttons (mail shows only in the finale).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I { get; private set; }

        private RectTransform root;
        private RectTransform counterBlock;
        private TextMeshProUGUI dataText;
        private TextMeshProUGUI perSecText;
        private RectTransform coinIcon;
        private Image coinSparkle;
        private TextMeshProUGUI levelText;
        private Image levelBadge;
        private Image xpFill;
        private TextMeshProUGUI xpLabel;
        private Image signalFill;
        private TextMeshProUGUI signalLabel;
        private RectTransform comboBlock;
        private Image comboFill;
        private Image comboBg;
        private TextMeshProUGUI comboMultText;
        private TextMeshProUGUI comboCountText;
        private Button pauseButton;
        private RectTransform topLeft;
        private CanvasGroup hudGroup;
        private readonly List<SlideItem> slideItems = new List<SlideItem>();
        private bool hudShown = true;
        private Coroutine slideRoutine;

        private class SlideItem
        {
            public RectTransform Rect;
            public Vector2 Base;
            public Vector2 HiddenOffset;
        }

        /// <summary>Screen-space targets for the tutorial spotlight.</summary>
        public RectTransform MailButtonRect { get { return mailButton != null ? (RectTransform)mailButton.transform : null; } }
        public RectTransform ClockRect { get { return xpFill != null ? (RectTransform)xpFill.transform.parent : null; } }
        public RectTransform PauseButtonRect { get { return pauseButton != null ? (RectTransform)pauseButton.transform : null; } }
        private Button mailButton;
        private Image mailBadge;
        private TextMeshProUGUI mailBadgeText;
        private RectTransform buffRow;
        private RectTransform compressorGauge;
        private Image compressorFill;
        private TextMeshProUGUI compressorText;
        private TextMeshProUGUI milestoneText;
        private Image screenTint;
        private TextMeshProUGUI toastText;
        private TextMeshProUGUI finaleHint;

        private double displayed;
        private double tweenFrom;
        private double tweenTo;
        private float tweenT = 1f;
        private float perSecTimer;
        private float lastBump;
        private readonly List<BuffIconView> buffViews = new List<BuffIconView>();
        private Coroutine toastRoutine;
        private Coroutine milestoneRoutine;

        private class BuffIconView
        {
            public RectTransform Root;
            public Image Ring;
            public Image Icon;
            public TextMeshProUGUI Label;
            public ActiveBuff Buff;
        }

        public Vector2 CoinTargetScreen
        {
            get
            {
                if (coinIcon == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.92f);
                return RectTransformUtility.WorldToScreenPoint(null, coinIcon.position);
            }
        }

        private void Awake()
        {
            I = this;
            root = (RectTransform)transform;
            hudGroup = UIFactory.Group(gameObject);
            Build();
            RegisterSlides();
            SetShown(false, true);
        }

        private void OnEnable()
        {
            EventBus.CoinLanded += OnCoinLanded;
            EventBus.DataSpent += OnDataSpent;
            EventBus.ComboMilestone += OnMilestone;
            EventBus.BuffsChanged += RebuildBuffs;
            EventBus.MailReceived += OnMail;
            EventBus.InboxChanged += RefreshMail;
            EventBus.GoldenPopped += OnGolden;
            EventBus.DayStarted += OnDayStarted;
            EventBus.GameReady += OnReady;
            EventBus.GameplayStarted += OnGameplayStarted;
            EventBus.ReturnedToMenu += OnReturnedToMenu;
        }

        private void OnDisable()
        {
            EventBus.CoinLanded -= OnCoinLanded;
            EventBus.DataSpent -= OnDataSpent;
            EventBus.ComboMilestone -= OnMilestone;
            EventBus.BuffsChanged -= RebuildBuffs;
            EventBus.MailReceived -= OnMail;
            EventBus.InboxChanged -= RefreshMail;
            EventBus.GoldenPopped -= OnGolden;
            EventBus.DayStarted -= OnDayStarted;
            EventBus.GameReady -= OnReady;
            EventBus.GameplayStarted -= OnGameplayStarted;
            EventBus.ReturnedToMenu -= OnReturnedToMenu;
        }

        private void OnGameplayStarted()
        {
            // The intro cutscene (or a resumed end-of-day screen) keeps the HUD hidden.
            if ((CutscenePlayer.I != null && CutscenePlayer.I.IsPlaying) || (EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen)) return;
            SetShown(true, false);
        }

        private void OnReturnedToMenu()
        {
            SetShown(false, false);
        }

        private void RegisterSlides()
        {
            AddSlide(counterBlock, new Vector2(0f, 320f));
            AddSlide(topLeft, new Vector2(-760f, 0f));
            AddSlide((RectTransform)pauseButton.transform, new Vector2(0f, 240f));
            AddSlide((RectTransform)mailButton.transform, new Vector2(0f, 240f));
            AddSlide(buffRow, new Vector2(760f, 0f));
            AddSlide(comboBlock, new Vector2(-320f, 0f));
        }

        private void AddSlide(RectTransform rt, Vector2 offset)
        {
            if (rt == null) return;
            slideItems.Add(new SlideItem { Rect = rt, Base = rt.anchoredPosition, HiddenOffset = offset });
        }

        /// <summary>Slides the HUD in from (or out to) the screen edges. Hidden behind the main menu.</summary>
        public void SetShown(bool show, bool instant)
        {
            if (hudShown == show && !instant) return;
            hudShown = show;
            hudGroup.blocksRaycasts = show;
            hudGroup.interactable = show;
            if (slideRoutine != null) Juice.Stop(slideRoutine);
            if (instant)
            {
                foreach (SlideItem s in slideItems) s.Rect.anchoredPosition = s.Base + (show ? Vector2.zero : s.HiddenOffset);
                hudGroup.alpha = show ? 1f : 0f;
                return;
            }
            slideRoutine = Juice.Run(Slide(show));
        }

        private System.Collections.IEnumerator Slide(bool show)
        {
            float d = Balance.B != null ? Balance.B.menu.hudSlideSeconds : 0.5f;
            float t = 0f;
            if (show) hudGroup.alpha = 1f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < slideItems.Count; i++)
                {
                    SlideItem s = slideItems[i];
                    float k = Mathf.Clamp01((t - i * 0.03f) / Mathf.Max(0.05f, d - 0.18f));
                    float e = show ? 1f - Juice.EaseOutBack(k) : k * k;
                    s.Rect.anchoredPosition = s.Base + s.HiddenOffset * e;
                }
                yield return null;
            }
            foreach (SlideItem s in slideItems) s.Rect.anchoredPosition = s.Base + (show ? Vector2.zero : s.HiddenOffset);
            if (!show) hudGroup.alpha = 0f;
        }

        // ---------------- build ----------------

        private void Build()
        {
            // Screen tint for combo milestones / golden flashes (never blocks clicks).
            screenTint = UIFactory.Image(root, "ScreenTint", UISprites.Vignette, new Color(1f, 1f, 1f, 0f), false);
            UIFactory.Stretch(screenTint.rectTransform);

            // Top center: Data counter.
            counterBlock = UIFactory.Place(UIFactory.Rect("DataCounter", root), UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -10f), new Vector2(900f, 170f));
            Image glow = UIFactory.Image(counterBlock, "Glow", UISprites.Glow, new Color(0.23f, 0.94f, 1f, 0.16f), false);
            UIFactory.Place(glow.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 10f), new Vector2(900f, 240f));
            Image coin = UIFactory.Icon(counterBlock, "coin", Palette.Gold, 78f);
            coinIcon = coin.rectTransform;
            UIFactory.Place(coinIcon, UIFactory.TopCenter, UIFactory.Center, new Vector2(-330f, -62f), new Vector2(78f, 78f));
            coinSparkle = UIFactory.Image(coinIcon, "Sparkle", UISprites.Star, new Color(1f, 1f, 1f, 0f), false);
            UIFactory.Place(coinSparkle.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(22f, 22f), new Vector2(34f, 34f));
            dataText = UIFactory.GlowText(counterBlock, "Data", "0", 96f, Color.white);
            UIFactory.Place(dataText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(20f, -4f), new Vector2(700f, 120f));
            dataText.enableWordWrapping = false;
            perSecText = UIFactory.Text(counterBlock, "PerSec", "0 / sec", 30f, Palette.TextSoft);
            UIFactory.Place(perSecText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(20f, -116f), new Vector2(600f, 44f));

            // Top left: Day badge, shift clock, Signal bar.
            RectTransform tl = UIFactory.Place(UIFactory.Rect("TopLeft", root), UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -20f), new Vector2(620f, 150f));
            topLeft = tl;
            levelBadge = UIFactory.Image(tl, "LevelBadge", UISprites.HexFill, new Color(0.07f, 0.11f, 0.24f, 0.95f), false);
            UIFactory.Place(levelBadge.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, Vector2.zero, new Vector2(124f, 124f));
            Image badgeRim = UIFactory.Image(levelBadge.transform, "Rim", UISprites.HexOutline, Palette.Cyan, false);
            UIFactory.Stretch(badgeRim.rectTransform);
            TextMeshProUGUI lvSmall = UIFactory.Text(levelBadge.transform, "Lv", "DAY", 18f, Palette.TextSoft);
            UIFactory.Place(lvSmall.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 26f), new Vector2(110f, 26f));
            levelText = UIFactory.GlowText(levelBadge.transform, "Level", "1", 54f, Color.white);
            UIFactory.Place(levelText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -10f), new Vector2(110f, 64f));

            Image xpBar = UIFactory.Bar(tl, "ShiftClock", Palette.Cyan, out xpFill);
            UIFactory.Place(xpBar.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(140f, -22f), new Vector2(340f, 24f));
            xpLabel = UIFactory.Text(tl, "ShiftLabel", "SHIFT", 20f, Palette.TextSoft, TextAlignmentOptions.Left);
            UIFactory.Place(xpLabel.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(142f, 6f), new Vector2(340f, 26f));
            Image sigBar = UIFactory.Bar(tl, "SignalBar", Palette.Green, out signalFill);
            UIFactory.Place(sigBar.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(140f, -84f), new Vector2(340f, 24f));
            signalLabel = UIFactory.Text(tl, "SignalLabel", "SIGNAL STRENGTH 0%", 20f, Palette.TextSoft, TextAlignmentOptions.Left);
            UIFactory.Place(signalLabel.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(142f, -56f), new Vector2(340f, 26f));

            // Top right: pause button (Settings live in the pause menu), mail, buffs.
            pauseButton = UIFactory.IconButton(root, "Pause", "pause", 76f, () => { if (PauseMenu.I != null) PauseMenu.I.Open(); }, Palette.TextSoft);
            UIFactory.Place((RectTransform)pauseButton.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-24f, -24f), new Vector2(76f, 76f));
            TextMeshProUGUI pauseKey = UIFactory.Text(pauseButton.transform, "Key", "P", 18f, Palette.TextSoft);
            UIFactory.Place(pauseKey.rectTransform, UIFactory.BottomCenter, UIFactory.TopCenter, new Vector2(0f, -2f), new Vector2(60f, 24f));
            mailButton = UIFactory.IconButton(root, "Mail", "mail", 92f, () => { if (MailUI.I != null) MailUI.I.Toggle(); }, Palette.Cyan);
            UIFactory.Place((RectTransform)mailButton.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-116f, -18f), new Vector2(92f, 88f));
            TextMeshProUGUI mailKey = UIFactory.Text(mailButton.transform, "Key", "M", 18f, Palette.TextSoft);
            UIFactory.Place(mailKey.rectTransform, UIFactory.BottomCenter, UIFactory.TopCenter, new Vector2(0f, -2f), new Vector2(60f, 24f));
            mailBadge = UIFactory.Image(mailButton.transform, "Badge", UISprites.Circle, Palette.Red, false);
            UIFactory.Place(mailBadge.rectTransform, UIFactory.TopRight, UIFactory.Center, new Vector2(-6f, -6f), new Vector2(38f, 38f));
            mailBadgeText = UIFactory.Text(mailBadge.transform, "Count", "1", 24f, Color.white);
            UIFactory.Stretch(mailBadgeText.rectTransform);
            mailBadge.gameObject.SetActive(false);
            mailButton.gameObject.SetActive(false);

            buffRow = UIFactory.Place(UIFactory.Rect("Buffs", root), UIFactory.TopRight, UIFactory.TopRight, new Vector2(-24f, -132f), new Vector2(560f, 110f));
            HorizontalLayoutGroup h = buffRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.UpperRight;
            h.spacing = 10f;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.reverseArrangement = true;

            // Left edge: combo meter.
            comboBlock = UIFactory.Place(UIFactory.Rect("Combo", root), UIFactory.MidLeft, UIFactory.MidLeft, new Vector2(30f, -20f), new Vector2(150f, 440f));
            comboBg = UIFactory.Bar(comboBlock, "ComboBar", Color.white, out comboFill, true);
            UIFactory.Place(comboBg.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(-40f, 0f), new Vector2(38f, 340f));
            comboMultText = UIFactory.OutlineText(comboBlock, "Mult", "x1", 40f, Color.white);
            UIFactory.Place(comboMultText.rectTransform, UIFactory.TopCenter, UIFactory.BottomCenter, new Vector2(-10f, -44f), new Vector2(170f, 50f));
            comboCountText = UIFactory.Text(comboBlock, "Count", "COMBO 0", 20f, Palette.TextSoft);
            UIFactory.Place(comboCountText.rectTransform, UIFactory.BottomCenter, UIFactory.TopCenter, new Vector2(-10f, 44f), new Vector2(170f, 30f));

            // World-anchored Compressor gauge.
            compressorGauge = UIFactory.Place(UIFactory.Rect("CompressorGauge", root), UIFactory.BottomLeft, UIFactory.Center, Vector2.zero, new Vector2(170f, 56f));
            Image cbar = UIFactory.Bar(compressorGauge, "Fill", Palette.Cyan, out compressorFill);
            UIFactory.Place(cbar.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, Vector2.zero, new Vector2(150f, 16f));
            compressorText = UIFactory.OutlineText(compressorGauge, "Text", "0 / 10", 26f, Color.white);
            UIFactory.Place(compressorText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, 6f), new Vector2(200f, 36f));

            // Center: combo milestone + toast + finale hint.
            milestoneText = UIFactory.OutlineText(root, "Milestone", "", 120f, Palette.Gold);
            UIFactory.Place(milestoneText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 120f), new Vector2(1400f, 180f));
            milestoneText.enableWordWrapping = false;
            milestoneText.gameObject.SetActive(false);
            toastText = UIFactory.OutlineText(root, "Toast", "", 56f, Palette.Cyan);
            UIFactory.Place(toastText.rectTransform, UIFactory.TopCenter, UIFactory.Center, new Vector2(0f, -250f), new Vector2(1400f, 80f));
            toastText.gameObject.SetActive(false);
            finaleHint = UIFactory.GlowText(root, "FinaleHint", "A STRANGE SIGNAL... CHECK YOUR MAIL  [M]", 42f, Palette.Magenta);
            UIFactory.Place(finaleHint.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 190f), new Vector2(1400f, 70f));
            finaleHint.gameObject.SetActive(false);
        }

        // ---------------- events ----------------

        private void OnReady()
        {
            displayed = GameManager.I.DisplayData;
            tweenFrom = tweenTo = displayed;
            tweenT = 1f;
            RebuildBuffs();
            RefreshMail();
        }

        private void OnCoinLanded(double amount)
        {
            RetargetCounter();
            // Tiny bump per landing, rate-limited so a 40-coin shower stays readable.
            if (Time.unscaledTime - lastBump < 0.06f) return;
            lastBump = Time.unscaledTime;
            Juice.PunchScale(dataText.transform, 1.05f, 0.12f);
            coinIcon.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
        }

        private void OnDataSpent()
        {
            RetargetCounter();
        }

        private void RetargetCounter()
        {
            double target = GameManager.I.DisplayData;
            if (System.Math.Abs(target - tweenTo) < 1e-9) return;
            tweenFrom = displayed;
            tweenTo = target;
            tweenT = 0f;
        }

        private void OnMilestone(int combo)
        {
            AudioManager.Play("arpeggio");
            if (milestoneRoutine != null) Juice.Stop(milestoneRoutine);
            milestoneRoutine = Juice.Run(MilestoneRoutine(combo));
        }

        private System.Collections.IEnumerator MilestoneRoutine(int combo)
        {
            milestoneText.gameObject.SetActive(true);
            Color c = ComboColor(combo);
            milestoneText.text = "x" + combo + " COMBO!!";
            milestoneText.color = c;
            float tint = GameSettings.ReduceFlashing ? 0f : 0.3f;
            screenTint.color = new Color(c.r, c.g, c.b, tint);
            Juice.StampIn(milestoneText.transform, 0.35f);
            float t = 0f;
            while (t < 1.1f)
            {
                t += Time.unscaledDeltaTime;
                screenTint.color = new Color(c.r, c.g, c.b, Mathf.Lerp(tint, 0f, t / 0.6f));
                if (t > 0.8f) milestoneText.color = new Color(c.r, c.g, c.b, 1f - (t - 0.8f) / 0.3f);
                yield return null;
            }
            milestoneText.gameObject.SetActive(false);
            screenTint.color = Color.clear;
        }

        private void OnGolden()
        {
            Juice.PunchScale(coinIcon, 1.5f, 0.3f);
            if (GameSettings.ReduceFlashing) return;
            Juice.Run(Juice.Tween(0.4f, k => screenTint.color = new Color(1f, 0.82f, 0.23f, 0.22f * (1f - k))));
        }

        private void OnDayStarted(int day)
        {
            Juice.PunchScale(levelBadge.transform, 1.35f, 0.35f);
        }

        private void OnMail(MailEntry e)
        {
            BalanceData b = Balance.B;
            Juice.Bounce((RectTransform)mailButton.transform, b.juice.mailBounces, b.juice.mailBounceTime);
            RefreshMail();
        }

        private void RefreshMail()
        {
            if (MailDirector.I == null) return;
            int n = MailDirector.I.UnreadCount;
            mailBadge.gameObject.SetActive(n > 0);
            mailBadgeText.text = n > 9 ? "9+" : n.ToString();
        }

        public void Toast(string text, Color color)
        {
            if (toastRoutine != null) Juice.Stop(toastRoutine);
            toastRoutine = Juice.Run(ToastRoutine(text, color));
        }

        private System.Collections.IEnumerator ToastRoutine(string text, Color color)
        {
            toastText.gameObject.SetActive(true);
            toastText.text = text;
            toastText.color = color;
            Juice.StampIn(toastText.transform, 0.3f);
            yield return new WaitForSecondsRealtime(1.8f);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                toastText.color = new Color(color.r, color.g, color.b, 1f - t / 0.4f);
                yield return null;
            }
            toastText.gameObject.SetActive(false);
        }

        private void RebuildBuffs()
        {
            if (BuffSystem.I == null) return;
            List<ActiveBuff> active = BuffSystem.I.Active;
            while (buffViews.Count < active.Count) buffViews.Add(MakeBuffView());
            for (int i = 0; i < buffViews.Count; i++)
            {
                bool on = i < active.Count;
                buffViews[i].Root.gameObject.SetActive(on);
                if (!on) continue;
                ActiveBuff b = active[i];
                bool isNew = buffViews[i].Buff != b;
                buffViews[i].Buff = b;
                buffViews[i].Icon.sprite = UISprites.Icon(b.id);
                buffViews[i].Icon.color = BuffColor(b.id);
                buffViews[i].Ring.color = BuffColor(b.id);
                buffViews[i].Label.text = b.label;
                if (isNew) Juice.PunchScale(buffViews[i].Root, 1.4f, 0.35f);
            }
        }

        private BuffIconView MakeBuffView()
        {
            BuffIconView v = new BuffIconView();
            v.Root = UIFactory.Rect("Buff", buffRow);
            v.Root.sizeDelta = new Vector2(100f, 112f);
            Image bg = UIFactory.Image(v.Root, "Bg", UISprites.Circle, new Color(0.04f, 0.07f, 0.16f, 0.9f), false);
            UIFactory.Place(bg.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(70f, 70f));
            v.Ring = UIFactory.Image(bg.transform, "Ring", UISprites.Ring, Palette.Cyan, false);
            UIFactory.Stretch(v.Ring.rectTransform);
            v.Ring.type = Image.Type.Filled;
            v.Ring.fillMethod = Image.FillMethod.Radial360;
            v.Ring.fillOrigin = (int)Image.Origin360.Top;
            v.Ring.fillClockwise = false;
            v.Icon = UIFactory.Image(bg.transform, "Icon", UISprites.Icon("all_data"), Color.white, false);
            UIFactory.Place(v.Icon.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(38f, 38f));
            v.Label = UIFactory.Text(v.Root, "Label", "", 15f, Palette.TextSoft);
            UIFactory.Place(v.Label.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, -6f), new Vector2(98f, 40f));
            v.Label.enableWordWrapping = true;
            v.Label.enableAutoSizing = true;
            v.Label.fontSizeMin = 10f;
            v.Label.fontSizeMax = 15f;
            v.Label.alignment = TextAlignmentOptions.Top;
            return v;
        }

        public static Color BuffColor(string id)
        {
            switch (id)
            {
                case "all_data": return Palette.Gold;
                case "click_data": return Palette.Red;
                case "crunch_payout": return Palette.Cyan;
                case "drone_speed": return Palette.Blue;
                case "extra_drones": return Palette.Blue;
                case "sort_mult": return Palette.Green;
                case "xp_mult": return Palette.Magenta;
                case "combo_window": return Palette.Magenta;
                case "luck": return Palette.Green;
                case "golden_chance": return Palette.Gold;
                case "golden_hour": return Palette.Gold;
                default: return Palette.Cyan;
            }
        }

        public static Color ComboColor(int combo)
        {
            if (combo >= 50) return Palette.Gold;
            if (combo >= 25) return Palette.Magenta;
            if (combo >= 10) return Palette.Cyan;
            return Color.white;
        }

        // ---------------- per frame ----------------

        private void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.IsReady) return;
            float dt = Time.unscaledDeltaTime;

            // Data counter tweens to the new value over 0.3 s, never jumping.
            if (tweenT < 1f)
            {
                tweenT = Mathf.Min(1f, tweenT + dt / Mathf.Max(0.01f, Balance.B.juice.counterTween));
                float e = Juice.EaseOutCubic(tweenT);
                displayed = tweenFrom + (tweenTo - tweenFrom) * e;
            }
            else if (System.Math.Abs(displayed - gm.DisplayData) > 0.5d)
            {
                RetargetCounter();
            }
            dataText.text = "<mspace=0.6em>" + NumberFormat.Format(displayed) + "</mspace>";

            perSecTimer -= dt;
            if (perSecTimer <= 0f)
            {
                perSecTimer = 0.25f;
                double today = DayManager.I != null ? DayManager.I.Report.dataEarned : 0d;
                perSecText.text = "TODAY  +" + NumberFormat.Format(today) + " Data";
            }
            // Keep the coin icon snug against the left edge of the number.
            float coinX = 20f - dataText.preferredWidth * 0.5f - 52f;
            Vector2 cp = coinIcon.anchoredPosition;
            coinIcon.anchoredPosition = new Vector2(Mathf.Lerp(cp.x, coinX, 1f - Mathf.Exp(-12f * dt)), cp.y);

            // Gold Detector: the coin icon sparkles more with each level.
            int gold = SkillTree.I.TierCount("golden_chance");
            float sparkle = gold > 0 ? Mathf.Clamp01(0.25f + gold * 0.05f) * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * (2f + gold * 0.2f))) : 0f;
            coinSparkle.color = new Color(1f, 1f, 1f, sparkle);
            coinSparkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 90f);
            coinIcon.localRotation = Quaternion.Slerp(coinIcon.localRotation, Quaternion.identity, dt * 8f);

            // Day / shift clock / Signal.
            DayManager dm = DayManager.I;
            if (dm != null)
            {
                levelText.text = dm.Day.ToString();
                float left = dm.Phase == DayPhase.Shift ? dm.TimeLeft : (dm.Phase == DayPhase.TitleCard || dm.Phase == DayPhase.Cutscene ? dm.ShiftLength : 0f);
                float frac = dm.ShiftLength > 0f ? Mathf.Clamp01(left / dm.ShiftLength) : 0f;
                xpFill.fillAmount = Mathf.Lerp(xpFill.fillAmount, frac, dt * 10f);
                bool urgent = dm.Phase == DayPhase.LastCall || (dm.Phase == DayPhase.Shift && left <= 10f);
                xpFill.color = urgent ? Color.Lerp(Palette.Orange, Palette.Red, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : Palette.Cyan;
                int secs = Mathf.CeilToInt(left);
                string clock = (secs / 60) + ":" + (secs % 60).ToString("00");
                xpLabel.text = dm.Phase == DayPhase.LastCall ? "LAST CALL!" : (dm.Phase == DayPhase.Night ? "SHIFT OVER" : "SHIFT  " + clock + (dm.Lucky ? "   LUCKY SHIFT" : ""));
                xpLabel.color = urgent ? Palette.Orange : Palette.TextSoft;
            }
            float sig = (float)GameStats.SignalStrength;
            signalFill.fillAmount = Mathf.Lerp(signalFill.fillAmount, sig, dt * 4f);
            signalLabel.text = "SIGNAL STRENGTH " + Mathf.RoundToInt(sig * 100f) + "%";

            // Combo meter: fills with the remaining window and changes color by tier.
            ComboTracker ct = ComboTracker.I;
            int combo = ct.Combo;
            Color cc = ComboColor(combo);
            comboFill.fillAmount = ct.TimeLeft01;
            comboFill.color = cc;
            comboMultText.text = "x" + ct.Mult.ToString("0.00");
            comboMultText.color = cc;
            comboCountText.text = "COMBO " + combo;
            float width = 38f + 3f * SkillTree.I.TierCount("combo_window");
            comboBg.rectTransform.sizeDelta = new Vector2(width, 340f);

            // Mail only has a HUD button during the finale (it lives on the end-of-day screen otherwise).
            bool finale = gm.State == GameState.Finale;
            if (mailButton.gameObject.activeSelf != finale) mailButton.gameObject.SetActive(finale);

            // Buff countdown rings.
            foreach (BuffIconView v in buffViews)
            {
                if (v.Buff == null || !v.Root.gameObject.activeSelf) continue;
                v.Ring.fillAmount = v.Buff.total > 0f ? Mathf.Clamp01(v.Buff.remaining / v.Buff.total) : 0f;
            }

            UpdateCompressorGauge();
            finaleHint.gameObject.SetActive(gm.State == GameState.Finale);
            if (gm.State == GameState.Finale) finaleHint.alpha = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f);
        }

        private void UpdateCompressorGauge()
        {
            Compressor c = Compressor.I;
            Camera cam = GameManager.I.Cam;
            if (c == null || cam == null)
            {
                compressorGauge.gameObject.SetActive(false);
                return;
            }
            Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * 2.6f * c.transform.localScale.y);
            GameState st = GameManager.I.State;
            bool inDay = DayManager.I == null || DayManager.I.InShiftOrLastCall;
            bool show = sp.z > 0f && hudShown && inDay && (st == GameState.Playing || st == GameState.Finale);
            compressorGauge.gameObject.SetActive(show);
            if (!show) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out Vector2 local);
            compressorGauge.anchorMin = compressorGauge.anchorMax = UIFactory.Center;
            compressorGauge.anchoredPosition = local;
            compressorFill.fillAmount = c.Fill01;
            compressorFill.color = Color.Lerp(Palette.Cyan, Palette.Magenta, c.Fill01);
            compressorText.text = c.Crunching ? "CRUNCH!" : c.Count + " / " + c.Capacity;
        }
    }
}
