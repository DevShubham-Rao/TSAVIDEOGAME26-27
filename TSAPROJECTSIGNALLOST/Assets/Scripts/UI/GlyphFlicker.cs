using UnityEngine;

namespace SignalLost
{
    /// <summary>Garbled, flickering wobble for ??? glyphs.</summary>
    public class GlyphFlicker : MonoBehaviour
    {
        private int index;
        private CanvasGroup group;

        public void Init(int i)
        {
            index = i;
            group = UIFactory.Group(gameObject);
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float wobble = Mathf.Sin(t * 3f + index * 0.9f);
            transform.localScale = Vector3.one * (1f + 0.08f * wobble);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f + index) * 6f);
            if (group != null)
            {
                bool glitch = Mathf.PerlinNoise(t * 6f, index * 3.1f) > 0.82f;
                group.alpha = glitch ? 0.25f : 1f;
            }
        }
    }
}
