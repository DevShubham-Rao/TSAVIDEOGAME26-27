using UnityEngine;

namespace SignalLost
{
    /// <summary>One active shake at a time so chaos never gets nauseating. Scaled by the Screen Shake intensity setting.</summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake I { get; private set; }

        private Vector3 basePosition;
        private float amplitude;
        private float duration;
        private float elapsed;
        private float seed;
        private bool active;

        private void Awake()
        {
            I = this;
            basePosition = transform.localPosition;
            seed = Random.value * 100f;
        }

        /// <summary>Call after moving the camera rig on purpose (endings).</summary>
        public void Rebase()
        {
            basePosition = transform.localPosition;
        }

        public void Shake(float amp, float dur)
        {
            amp *= GameSettings.ShakeIntensity;
            if (amp <= 0.0001f) return;
            // Only one shake at a time: a stronger shake replaces a weaker one, weaker ones are ignored.
            float currentStrength = active ? amplitude * (1f - elapsed / Mathf.Max(0.001f, duration)) : 0f;
            if (amp < currentStrength) return;
            amplitude = amp;
            duration = dur;
            elapsed = 0f;
            active = true;
        }

        private void LateUpdate()
        {
            if (!active) return;
            elapsed += Time.unscaledDeltaTime;
            float k = 1f - elapsed / Mathf.Max(0.001f, duration);
            if (k <= 0f || GameSettings.ShakeIntensity <= 0f)
            {
                active = false;
                transform.localPosition = basePosition;
                return;
            }
            float t = Time.unscaledTime * 38f;
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(seed, t) - 0.5f,
                Mathf.PerlinNoise(seed + 13f, t) - 0.5f,
                Mathf.PerlinNoise(seed + 27f, t) - 0.5f) * 2f * amplitude * k * k;
            transform.localPosition = basePosition + offset;
        }
    }
}
