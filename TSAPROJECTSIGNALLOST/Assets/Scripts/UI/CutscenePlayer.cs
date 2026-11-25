using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// In-engine cutscenes from cutscenes.json: letterbox bars, camera shots around the station, a comms
    /// line per shot (portrait + typewriter) and a few world beats (lights out, alarm, Static flood, the
    /// Static freezing into one shape). Always skippable with the SKIP button, Esc or Enter.
    /// </summary>
    public class CutscenePlayer : MonoBehaviour
    {
        public static CutscenePlayer I { get; private set; }

        public bool IsPlaying { get; private set; }

        private CanvasGroup group;
        private RectTransform topBar;
        private RectTransform bottomBar;
        private Image dark;
        private Image flash;
        private RectTransform lineBox;
        private Image portrait;
        private TextMeshProUGUI speakerText;
        private TextMeshProUGUI lineText;
        private Button skipButton;
        private bool skipRequested;
        private bool advanceRequested;
        private TextMeshProUGUI clickHint;
        private Coroutine running;
        private Action onDone;

        private const float BarHeight = 150f;

        private void Awake()
        {
            I = this;
            group = UIFactory.Group(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Build();
        }

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            dark = UIFactory.Image(root, "Dark", UISprites.Square, new Color(0f, 0f, 0.02f, 0f), true);
            UIFactory.Stretch(dark.rectTransform);
            // Click anywhere: finish the line, then go to the next shot.
            Button clicker = dark.gameObject.AddComponent<Button>();
            clicker.transition = Selectable.Transition.None;
            clicker.onClick.AddListener(() => advanceRequested = true);
            flash = UIFactory.Image(root, "Flash", UISprites.Square, new Color(1f, 1f, 1f, 0f), false);
            UIFactory.Stretch(flash.rectTransform);

            Image tb = UIFactory.Image(root, "TopBar", UISprites.Square, Color.black, false);
            topBar = tb.rectTransform;
            UIFactory.Place(topBar, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, BarHeight), new Vector2(4000f, BarHeight));
            Image bb = UIFactory.Image(root, "BottomBar", UISprites.Square, Color.black, false);
            bottomBar = bb.rectTransform;
            UIFactory.Place(bottomBar, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, -BarHeight), new Vector2(4000f, BarHeight));

            lineBox = UIFactory.Place(UIFactory.Rect("Line", root), UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 22f), new Vector2(1300f, 110f));
            Image frame = UIFactory.Image(lineBox, "PortraitFrame", UISprites.Circle, new Color(0.1f, 0.16f, 0.32f, 1f), false);
            UIFactory.Place(frame.rectTransform, UIFactory.MidLeft, UIFactory.MidLeft, new Vector2(0f, 0f), new Vector2(96f, 96f));
            portrait = UIFactory.Image(frame.transform, "Portrait", UISprites.Portrait("hale"), Color.white, false);
            UIFactory.Stretch(portrait.rectTransform, 6f, 6f, 6f, 6f);
            speakerText = UIFactory.Text(lineBox, "Speaker", "", 22f, Palette.Cyan, TextAlignmentOptions.Left);
            UIFactory.Place(speakerText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(118f, -6f), new Vector2(1100f, 30f));
            lineText = UIFactory.Text(lineBox, "Text", "", 32f, Color.white, TextAlignmentOptions.TopLeft);
            UIFactory.Place(lineText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(118f, -38f), new Vector2(1180f, 72f));
            lineText.enableWordWrapping = true;

            clickHint = UIFactory.Text(root, "ClickHint", "CLICK TO CONTINUE", 22f, Palette.TextSoft, TextAlignmentOptions.Right);
            UIFactory.Place(clickHint.rectTransform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-40f, 20f), new Vector2(420f, 30f));
            clickHint.gameObject.SetActive(false);
            skipButton = UIFactory.Button(root, "Skip", "SKIP  >", 24f, Skip, new Color(0.05f, 0.07f, 0.14f, 1f), Palette.Dim, Palette.TextSoft);
            UIFactory.Place((RectTransform)skipButton.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-28f, -28f), new Vector2(150f, 54f));
        }

        public void Skip()
        {
            if (IsPlaying) skipRequested = true;
        }

        /// <summary>Debug: stops the cutscene at once and puts everything back.</summary>
        public void Abort()
        {
            if (!IsPlaying) return;
            if (running != null) StopCoroutine(running);
            StopAllCoroutines();
            running = null;
            lineBox.gameObject.SetActive(false);
            dark.color = new Color(0f, 0f, 0.02f, 0f);
            flash.color = new Color(1f, 1f, 1f, 0f);
            topBar.anchoredPosition = new Vector2(0f, BarHeight);
            bottomBar.anchoredPosition = new Vector2(0f, -BarHeight);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            if (BlobSpawner.I != null)
            {
                BlobSpawner.I.ReleaseFinale();
                BlobSpawner.I.ClearField();
            }
            if (CameraDirector.I != null) CameraDirector.I.EndCutscene();
            IsPlaying = false;
            onDone = null;
        }

        public void Play(string id, Action done)
        {
            CutsceneDef def = Balance.I.Cutscene(id);
            if (def == null || def.shots == null || def.shots.Count == 0)
            {
                if (done != null) done();
                return;
            }
            if (running != null) StopCoroutine(running);
            onDone = done;
            running = StartCoroutine(Run(def));
        }

        private void Update()
        {
            if (!IsPlaying || InputReader.I == null) return;
            if (InputReader.I.CancelPressed) Skip();
            if (InputReader.I.SpacePressed) advanceRequested = true;
        }

        private IEnumerator Run(CutsceneDef def)
        {
            IsPlaying = true;
            skipRequested = false;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            if (HUD.I != null) HUD.I.SetShown(false, false);
            if (Vacuum.I != null) Vacuum.I.DropAll();
            yield return Bars(true, 0.45f);

            foreach (CutsceneShot shot in def.shots)
            {
                if (skipRequested) break;
                if (CameraDirector.I != null) CameraDirector.I.SetShot(shot.pitch, shot.yaw, shot.distance, shot.lookHeight, shot.blendSeconds);
                DoAction(shot);
                if (!string.IsNullOrEmpty(shot.sfx)) AudioManager.Play(shot.sfx);
                ShowLine(shot.speaker, null);
                float t = 0f;
                string full = shot.line ?? "";
                advanceRequested = false;
                clickHint.gameObject.SetActive(false);
                while (!skipRequested)
                {
                    t += Time.unscaledDeltaTime;
                    bool typed = true;
                    if (full.Length > 0)
                    {
                        int chars = Mathf.Min(full.Length, Mathf.FloorToInt(t * 40f));
                        lineText.text = full.Substring(0, chars);
                        typed = chars >= full.Length;
                    }
                    if (advanceRequested)
                    {
                        advanceRequested = false;
                        if (!typed)
                        {
                            // First click finishes the typing.
                            t = full.Length / 40f + 0.01f;
                            continue;
                        }
                        if (full.Length > 0 && t > 0.25f) break;
                    }
                    if (full.Length == 0 && t >= shot.seconds) break;
                    clickHint.gameObject.SetActive(typed && full.Length > 0);
                    if (typed && full.Length > 0) clickHint.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                    yield return null;
                }
                clickHint.gameObject.SetActive(false);
                lineText.text = full;
            }

            // Clean up the world beats and hand control back.
            lineBox.gameObject.SetActive(false);
            Juice.Run(Juice.Tween(0.4f, k => dark.color = new Color(0f, 0f, 0.02f, dark.color.a * (1f - k))));
            if (BlobSpawner.I != null)
            {
                BlobSpawner.I.ReleaseFinale();
                BlobSpawner.I.ClearField();
            }
            if (CameraDirector.I != null) CameraDirector.I.EndCutscene();
            yield return Bars(false, 0.4f);
            dark.color = new Color(0f, 0f, 0.02f, 0f);
            flash.color = new Color(1f, 1f, 1f, 0f);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            if (HUD.I != null) HUD.I.SetShown(true, false);
            IsPlaying = false;
            running = null;
            Action cb = onDone;
            onDone = null;
            if (cb != null) cb();
        }

        private IEnumerator Bars(bool show, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Juice.EaseOutCubic(Mathf.Clamp01(t / duration));
                float y = show ? Mathf.Lerp(BarHeight, 0f, k) : Mathf.Lerp(0f, BarHeight, k);
                topBar.anchoredPosition = new Vector2(0f, y);
                bottomBar.anchoredPosition = new Vector2(0f, -y);
                yield return null;
            }
        }

        private void ShowLine(string speaker, string text)
        {
            bool has = !string.IsNullOrEmpty(speaker);
            lineBox.gameObject.SetActive(has);
            lineText.text = text ?? "";
            if (!has) return;
            SenderDef s = Balance.I.Sender(speaker);
            bool narrator = speaker == "narrator";
            portrait.transform.parent.gameObject.SetActive(!narrator);
            if (!narrator) portrait.sprite = UISprites.Portrait(s.portrait ?? speaker);
            speakerText.text = narrator ? "" : (s.name ?? speaker).ToUpperInvariant();
            speakerText.color = Palette.Hex(s.color);
            lineText.color = speaker == "static" ? Palette.Magenta : Color.white;
            lineText.rectTransform.anchoredPosition = new Vector2(narrator ? 0f : 118f, -38f);
        }

        private void DoAction(CutsceneShot shot)
        {
            if (string.IsNullOrEmpty(shot.action)) return;
            switch (shot.action)
            {
                case "dark":
                    Juice.Run(Juice.Tween(0.8f, k => dark.color = new Color(0f, 0f, 0.02f, Mathf.Lerp(0f, 0.65f, k))));
                    break;
                case "light":
                    Juice.Run(Juice.Tween(0.8f, k => dark.color = new Color(0f, 0f, 0.02f, Mathf.Lerp(0.65f, 0f, k))));
                    break;
                case "alarm":
                    AudioManager.Play("alarm");
                    if (!GameSettings.ReduceFlashing) Juice.Run(Juice.Tween(0.9f, k => flash.color = new Color(1f, 0.2f, 0.25f, 0.28f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)))));
                    break;
                case "flash":
                    if (!GameSettings.ReduceFlashing) Juice.Run(Juice.Tween(0.6f, k => flash.color = new Color(1f, 1f, 1f, 0.8f * (1f - k))));
                    AudioManager.Play("crunch", 0.7f, 0.8f);
                    Juice.Shake(0.2f, 0.4f);
                    break;
                case "burst":
                    if (BlobSpawner.I != null) VFXPlayer.Shockwave(BlobSpawner.I.AbsorbPoint, 6f, Palette.Magenta);
                    Juice.Shake(0.25f, 0.5f);
                    AudioManager.Play("whoosh", 0.6f);
                    break;
                case "flood":
                    StartCoroutine(Flood(Mathf.Max(6, shot.count)));
                    break;
                case "freeze_shape":
                    if (BlobSpawner.I != null) BlobSpawner.I.ArrangeFinale();
                    AudioManager.Play("arpeggio");
                    break;
                case "release":
                    if (BlobSpawner.I != null) BlobSpawner.I.ReleaseFinale();
                    break;
            }
        }

        /// <summary>Static pours out of the receiver and scatters over the moon.</summary>
        private IEnumerator Flood(int count)
        {
            BlobColor[] colors = { BlobColor.Red, BlobColor.Red, BlobColor.Blue, BlobColor.Green };
            Vector3 from = BlobSpawner.I.AbsorbPoint + Vector3.up * 1.5f;
            for (int i = 0; i < count && IsPlaying; i++)
            {
                Blob b = BlobSpawner.I.SpawnColor(colors[i % colors.Length], from, UnityEngine.Random.Range(0.7f, 1.1f), true);
                if (b != null) b.FlyTo(BlobSpawner.I.RandomInteriorPoint(), UnityEngine.Random.Range(0.6f, 1.2f), UnityEngine.Random.Range(1.5f, 3f), x => x.ReturnToDrift());
                if (i % 3 == 0) AudioManager.PlayVaried("pop", 0.6f, 0.9f, 0.3f);
                yield return new WaitForSecondsRealtime(0.06f);
            }
        }
    }
}
