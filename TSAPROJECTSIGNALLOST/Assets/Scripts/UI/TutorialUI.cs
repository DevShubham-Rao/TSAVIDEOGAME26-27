using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Tutorial presentation: Commander Hale's comms box (portrait + typewriter line) bottom-left, a
    /// spotlight that darkens everything except the target, and a pulsing ring + bouncing arrow on it.
    /// Never blocks input except its own Skip button; positions are recomputed every frame (window resize safe).
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        public static TutorialUI I { get; private set; }

        private RectTransform root;
        private CanvasGroup rootGroup;
        private Image spotlight;
        private RectTransform ring;
        private Image ringImage;
        private RectTransform arrow;
        private Image arrowImage;
        private RectTransform comms;
        private CanvasGroup commsGroup;
        private Image portrait;
        private TextMeshProUGUI speakerText;
        private TextMeshProUGUI lineText;
        private TextMeshProUGUI progressText;
        private Button skipButton;
        private TextMeshProUGUI stamp;
        private Canvas canvas;

        private string mainLine = "";
        private string mainSpeaker = "hale";
        private float tempLineUntil;
        private float typed;
        private int lastTick;
        private bool commsVisible;
        private bool targetActive;
        private Vector2 targetScreen;
        private float targetRadius = 100f;
        private bool spotlightWanted;
        private float spotAlpha;
        private float ringAlpha;
        private float nudge;
        private Coroutine stampRoutine;

        private void Awake()
        {
            I = this;
            root = (RectTransform)transform;
            rootGroup = UIFactory.Group(gameObject);
            rootGroup.alpha = 0f;
            canvas = GetComponentInParent<Canvas>();
            Build();
        }

        private void Build()
        {
            spotlight = UIFactory.Image(root, "Spotlight", UISprites.SpotlightHole, new Color(0f, 0.01f, 0.05f, 0f), false);
            spotlight.rectTransform.anchorMin = spotlight.rectTransform.anchorMax = UIFactory.Center;

            ringImage = UIFactory.Image(root, "Ring", UISprites.Ring, new Color(1f, 0.85f, 0.25f, 0f), false);
            ring = ringImage.rectTransform;
            ring.anchorMin = ring.anchorMax = UIFactory.Center;
            arrowImage = UIFactory.Image(root, "Arrow", UISprites.Triangle, new Color(1f, 0.85f, 0.25f, 0f), false);
            arrow = arrowImage.rectTransform;
            arrow.anchorMin = arrow.anchorMax = UIFactory.Center;
            arrow.sizeDelta = new Vector2(56f, 56f);

            Image box = UIFactory.Panel(root, "Comms", new Color(0.02f, 0.05f, 0.11f, 1f), Palette.Cyan, true);
            comms = box.rectTransform;
            UIFactory.Place(comms, UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(196f, 24f), new Vector2(780f, 150f));
            commsGroup = UIFactory.Group(box.gameObject);
            commsGroup.alpha = 0f;
            Image frame = UIFactory.Image(comms, "PortraitFrame", UISprites.Circle, new Color(0.1f, 0.16f, 0.32f, 1f), false);
            UIFactory.Place(frame.rectTransform, UIFactory.MidLeft, UIFactory.MidLeft, new Vector2(14f, 0f), new Vector2(104f, 104f));
            portrait = UIFactory.Image(frame.transform, "Portrait", UISprites.Portrait("hale"), Color.white, false);
            UIFactory.Stretch(portrait.rectTransform, 5f, 5f, 5f, 5f);
            speakerText = UIFactory.Text(comms, "Speaker", "COMMANDER HALE", 18f, Palette.Cyan, TextAlignmentOptions.Left);
            UIFactory.Place(speakerText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(132f, -10f), new Vector2(400f, 26f));
            lineText = UIFactory.Text(comms, "Line", "", 30f, Color.white, TextAlignmentOptions.TopLeft);
            UIFactory.Place(lineText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(132f, -38f), new Vector2(620f, 104f));
            lineText.enableAutoSizing = true;
            lineText.fontSizeMin = 18f;
            lineText.fontSizeMax = 28f;
            progressText = UIFactory.Text(comms, "Progress", "", 22f, Palette.Gold, TextAlignmentOptions.Right);
            UIFactory.Place(progressText.rectTransform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-16f, 10f), new Vector2(120f, 30f));
            skipButton = UIFactory.Button(comms, "Skip", "SKIP", 18f, AskSkip, new Color(0.08f, 0.1f, 0.18f, 1f), Palette.Dim, Palette.TextSoft);
            UIFactory.Place((RectTransform)skipButton.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-10f, -10f), new Vector2(96f, 36f));
            UIFactory.EnableNav(skipButton);
            skipButton.gameObject.SetActive(false);

            stamp = UIFactory.OutlineText(root, "Stamp", "", 130f, Palette.Gold);
            UIFactory.Place(stamp.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 140f), new Vector2(1600f, 180f));
            stamp.enableWordWrapping = false;
            stamp.gameObject.SetActive(false);
        }

        // ---------------- API ----------------

        public void Say(string speaker, string text)
        {
            mainSpeaker = string.IsNullOrEmpty(speaker) ? "hale" : speaker;
            mainLine = text ?? "";
            tempLineUntil = 0f;
            ApplySpeaker(mainSpeaker);
            Restart(mainLine);
            if (!commsVisible)
            {
                commsVisible = true;
                Juice.StampIn(comms, 0.25f);
            }
            AudioManager.Play("mail", 1.3f, 0.35f);
        }

        /// <summary>A short line (nudge) that reverts to the main instruction afterwards.</summary>
        public void SayTemporary(string text, float seconds)
        {
            tempLineUntil = Time.unscaledTime + seconds;
            Restart(text);
            Juice.PunchScale(comms, 1.06f, 0.2f);
        }

        public void SetProgress(int done, int total)
        {
            progressText.text = total > 1 ? done + " / " + total : "";
            if (total > 1 && done > 0) Juice.PunchScale(progressText.transform, 1.4f, 0.2f);
        }

        public void HideComms()
        {
            commsVisible = false;
            progressText.text = "";
        }

        public void ShowSkip(bool show)
        {
            skipButton.gameObject.SetActive(show);
        }

        public void SetTarget(bool active, Vector2 screen, float radiusPx, bool withSpotlight)
        {
            targetActive = active;
            if (!active)
            {
                spotlightWanted = false;
                return;
            }
            targetScreen = screen;
            targetRadius = Mathf.Max(40f, radiusPx);
            spotlightWanted = withSpotlight;
        }

        public void Nudge()
        {
            nudge = 1f;
        }

        /// <summary>Step complete: stamp text, confetti, coins (the caller pays), sound. huge is the biggest.</summary>
        public void Celebrate(string level, string text)
        {
            if (level == "none") return;
            if (stampRoutine != null) Juice.Stop(stampRoutine);
            stampRoutine = Juice.Run(StampRoutine(text, level));
            switch (level)
            {
                case "huge":
                    VFXPlayer.ConfettiScreen(160);
                    Juice.Shake(0.18f, 0.4f);
                    AudioManager.Play("level_up", 1.1f);
                    AudioManager.Play("coin_shower");
                    break;
                case "big":
                    VFXPlayer.ConfettiScreen(80);
                    AudioManager.Play("solved");
                    break;
                default:
                    VFXPlayer.ConfettiScreen(30);
                    AudioManager.Play("sort_ding", 1.2f);
                    break;
            }
        }

        private System.Collections.IEnumerator StampRoutine(string text, string level)
        {
            stamp.gameObject.SetActive(true);
            stamp.text = string.IsNullOrEmpty(text) ? "NICE!" : text;
            stamp.fontSize = level == "huge" ? 170f : (level == "big" ? 140f : 110f);
            stamp.color = level == "huge" ? Palette.Gold : Palette.Cyan;
            Juice.StampIn(stamp.transform, 0.35f);
            yield return new WaitForSecondsRealtime(0.9f);
            float t = 0f;
            Color c = stamp.color;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                stamp.color = new Color(c.r, c.g, c.b, 1f - t / 0.3f);
                yield return null;
            }
            stamp.gameObject.SetActive(false);
        }

        public void ClearAll()
        {
            HideComms();
            SetTarget(false, Vector2.zero, 0f, false);
            ShowSkip(false);
        }

        private void AskSkip()
        {
            if (ConfirmDialog.I == null || TutorialDirector.I == null) return;
            ConfirmDialog.I.Show("SKIP THE TUTORIAL?", "You can replay it any time from\nSettings > Gameplay.", "SKIP", "KEEP GOING",
                () => TutorialDirector.I.Skip());
        }

        private void ApplySpeaker(string id)
        {
            SenderDef s = Balance.I != null ? Balance.I.Sender(id) : null;
            if (s == null) return;
            portrait.sprite = UISprites.Portrait(s.portrait);
            speakerText.text = (s.name ?? id).ToUpperInvariant();
            speakerText.color = Palette.Hex(s.color);
        }

        private void Restart(string text)
        {
            lineText.text = text;
            lineText.ForceMeshUpdate();
            lineText.maxVisibleCharacters = 0;
            typed = 0f;
            lastTick = 0;
        }

        // ---------------- per frame ----------------

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            GameManager gm = GameManager.I;
            bool visible = gm != null && gm.IsReady && gm.State == GameState.Playing;
            rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, visible ? 1f : 0f, dt * 6f);
            rootGroup.blocksRaycasts = visible;
            rootGroup.interactable = visible;

            // Comms box + typewriter (fast; never blocks input).
            commsGroup.alpha = Mathf.MoveTowards(commsGroup.alpha, commsVisible ? 1f : 0f, dt * 6f);
            commsGroup.blocksRaycasts = commsVisible;
            if (tempLineUntil > 0f && Time.unscaledTime > tempLineUntil)
            {
                tempLineUntil = 0f;
                Restart(mainLine);
            }
            int total = lineText.textInfo != null ? lineText.textInfo.characterCount : lineText.text.Length;
            if (lineText.maxVisibleCharacters < total)
            {
                float cps = Balance.B != null ? Balance.B.tutorial.textCharsPerSecond : 60f;
                typed += dt * cps;
                int shown = Mathf.Min(total, (int)typed);
                lineText.maxVisibleCharacters = shown;
                if (shown - lastTick >= 3)
                {
                    lastTick = shown;
                    AudioManager.PlayVaried("type", 0.9f, 1.15f, 0.25f);
                }
            }

            // Spotlight, ring and arrow follow the target in screen space.
            float spotTarget = targetActive && spotlightWanted ? (Balance.B != null ? Balance.B.tutorial.spotlightAlpha : 0.6f) : 0f;
            spotAlpha = Mathf.MoveTowards(spotAlpha, spotTarget, dt * 2.5f);
            ringAlpha = Mathf.MoveTowards(ringAlpha, targetActive ? 1f : 0f, dt * 5f);
            nudge = Mathf.MoveTowards(nudge, 0f, dt * 0.6f);
            spotlight.color = new Color(0f, 0.01f, 0.05f, spotAlpha);
            ringImage.color = new Color(1f, 0.85f, 0.25f, ringAlpha);
            arrowImage.color = new Color(1f, 0.85f, 0.25f, ringAlpha);
            if (ringAlpha <= 0.001f && spotAlpha <= 0.001f) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, targetScreen, null, out Vector2 local)) return;
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            float r = targetRadius / scale;
            spotlight.rectTransform.anchoredPosition = local;
            spotlight.rectTransform.sizeDelta = Vector2.one * r * UISprites.SpotlightHoleFraction;

            float t = Time.unscaledTime;
            float nudgeScale = Balance.B != null ? Balance.B.tutorial.nudgeScale : 1.6f;
            float pulse = 1f + 0.1f * Mathf.Sin(t * 6f) + nudge * (nudgeScale - 1f) * (0.5f + 0.5f * Mathf.Sin(t * 14f));
            ring.anchoredPosition = local;
            ring.sizeDelta = Vector2.one * r * 2.1f * pulse;

            // Arrow above the target, or below it when the target is near the top of the screen.
            float bounce = (14f + nudge * 26f) * Mathf.Abs(Mathf.Sin(t * 5f));
            float halfHeight = root.rect.height * 0.5f;
            bool below = local.y + r + 90f > halfHeight - 20f;
            float dir = below ? -1f : 1f;
            arrow.anchoredPosition = local + new Vector2(0f, dir * (r + 46f + bounce));
            arrow.localRotation = Quaternion.Euler(0f, 0f, below ? 0f : 180f);
            arrow.sizeDelta = Vector2.one * (56f + nudge * 24f);
        }
    }
}
