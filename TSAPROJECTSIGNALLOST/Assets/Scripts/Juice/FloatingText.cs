using TMPro;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Pooled "+X" text: rises 60 px over 0.6 s and fades (unscaled time).</summary>
    public class FloatingText : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private RectTransform rt;
        private Vector2 start;
        private float t;
        private float duration;
        private float rise;
        private Color color;
        private float baseSize = 40f;
        private float sizeMult = 1f;

        public void Bind(TextMeshProUGUI text)
        {
            label = text;
            rt = (RectTransform)transform;
            baseSize = text.fontSize;
        }

        public void Play(Vector2 anchored, string text, Color c, float size)
        {
            BalanceData b = Balance.B;
            rise = b != null ? b.juice.floatTextRise : 60f;
            duration = b != null ? b.juice.floatTextTime : 0.6f;
            if (size > 1.5f) duration *= 1.6f;
            start = anchored + new Vector2(Random.Range(-14f, 14f), 0f);
            rt.anchoredPosition = start;
            label.text = text;
            color = c;
            label.color = c;
            sizeMult = size;
            label.fontSize = baseSize * size;
            t = 0f;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = start + new Vector2(0f, rise * Juice.EaseOutCubic(k));
            float pop = k < 0.15f ? Mathf.Lerp(0f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.15f) / 0.2f));
            transform.localScale = Vector3.one * pop;
            float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            label.color = new Color(color.r, color.g, color.b, alpha);
            if (k >= 1f && FloatingTextLayer.I != null) FloatingTextLayer.I.Release(this);
        }
    }
}
