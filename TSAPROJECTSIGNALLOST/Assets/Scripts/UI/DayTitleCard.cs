using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// "DAY 3 / Signal at 30%" plus one line from whoever wrote last night's mail. Shown for a few seconds
    /// before every shift; a click (or any key) skips it.
    /// </summary>
    public class DayTitleCard : MonoBehaviour
    {
        public static DayTitleCard I { get; private set; }

        private CanvasGroup group;
        private RectTransform band;
        private TextMeshProUGUI dayText;
        private TextMeshProUGUI subText;
        private TextMeshProUGUI lineText;
        private bool skip;

        public bool IsShowing { get; private set; }

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
            Image dim = UIFactory.Image(root, "Dim", UISprites.Square, new Color(0f, 0.01f, 0.05f, 0.55f), true);
            UIFactory.Stretch(dim.rectTransform);
            Button catcher = dim.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.onClick.AddListener(() => skip = true);

            Image b = UIFactory.Image(root, "Band", UISprites.Square, new Color(0.02f, 0.04f, 0.1f, 1f), false);
            band = b.rectTransform;
            UIFactory.Place(band, UIFactory.Center, UIFactory.Center, new Vector2(0f, 40f), new Vector2(4000f, 330f));
            Image top = UIFactory.Image(band, "Top", UISprites.Square, Palette.Cyan, false);
            UIFactory.Place(top.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(4000f, 4f));
            Image bottom = UIFactory.Image(band, "Bottom", UISprites.Square, Palette.Cyan, false);
            UIFactory.Place(bottom.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, Vector2.zero, new Vector2(4000f, 4f));

            dayText = UIFactory.GlowText(band, "Day", "DAY 1", 150f, Color.white);
            UIFactory.Place(dayText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 70f), new Vector2(1400f, 170f));
            dayText.characterSpacing = 12f;
            subText = UIFactory.Text(band, "Sub", "", 40f, Palette.Cyan);
            UIFactory.Place(subText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -36f), new Vector2(1400f, 56f));
            lineText = UIFactory.Text(band, "Line", "", 32f, Palette.TextSoft);
            UIFactory.Place(lineText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -104f), new Vector2(1500f, 50f));
            TextMeshProUGUI hint = UIFactory.Text(root, "Hint", "click to start", 22f, Palette.Dim);
            UIFactory.Place(hint.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -170f), new Vector2(600f, 30f));
        }

        /// <summary>Debug: hides the card immediately without calling the done callback.</summary>
        public void Dismiss()
        {
            StopAllCoroutines();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            IsShowing = false;
        }

        public void Show(int day, string subtitle, string line, Action done)
        {
            StopAllCoroutines();
            StartCoroutine(Run(day, subtitle, line, done));
        }

        private IEnumerator Run(int day, string subtitle, string line, Action done)
        {
            IsShowing = true;
            skip = false;
            dayText.text = "DAY " + day;
            subText.text = subtitle ?? "";
            bool symbols = !string.IsNullOrEmpty(line) && line.IndexOf('▲') >= 0 || (line != null && line.IndexOf('●') >= 0) || (line != null && line.IndexOf('★') >= 0);
            lineText.text = "";
            lineText.color = symbols ? Palette.Magenta : Palette.TextSoft;
            group.blocksRaycasts = true;
            AudioManager.Play("whoosh", 0.8f, 0.7f);

            float t = 0f;
            float inTime = 0.3f;
            while (t < inTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / inTime);
                group.alpha = k;
                band.localScale = new Vector3(1f, Mathf.Lerp(0.05f, 1f, Juice.EaseOutBack(k)), 1f);
                yield return null;
            }
            band.localScale = Vector3.one;
            Juice.StampIn(dayText.transform, 0.35f);
            AudioManager.Play("level_up", 1f, 0.45f);

            float hold = Balance.B != null ? Balance.B.days.titleCardSeconds : 3f;
            float shown = 0f;
            float minShow = 0.5f;
            string full = line ?? "";
            while (shown < hold)
            {
                shown += Time.unscaledDeltaTime;
                int chars = Mathf.Min(full.Length, Mathf.FloorToInt(shown * 60f));
                lineText.text = full.Substring(0, chars);
                bool anyKey = InputReader.I != null && (InputReader.I.AnyKeyPressed);
                if ((skip || anyKey) && shown > minShow) break;
                yield return null;
            }
            lineText.text = full;

            t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - t / 0.25f;
                yield return null;
            }
            group.alpha = 0f;
            group.blocksRaycasts = false;
            IsShowing = false;
            if (done != null) done();
        }
    }
}
