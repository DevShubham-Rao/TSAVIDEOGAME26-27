using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Runs Juice coroutines and owns Time.timeScale: pause (0), hitstop (0.05) and slow-mo all resolve
    /// here every frame so they never fight each other. UI always animates on unscaled time.
    /// </summary>
    public class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost instance;

        private float hitstopUntil;
        private float hitstopScale = 0.05f;
        private float nextHitstopAllowed;
        private float slowUntil;
        private float slowScale = 1f;

        /// <summary>True while the pause menu is open: gameplay time is frozen.</summary>
        public static bool Paused { get; private set; }

        public static CoroutineHost I
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("[Juice]");
                    instance = go.AddComponent<CoroutineHost>();
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Paused = false;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                Paused = false;
                Time.timeScale = 1f;
            }
        }

        public static void SetPaused(bool paused)
        {
            Paused = paused;
            I.Resolve();
        }

        /// <summary>Freezes time to a crawl for a few frames. Timed in unscaled time; rate-limited.</summary>
        public void Hitstop(float duration, float scale, float cooldown)
        {
            float now = Time.unscaledTime;
            if (Paused || now < nextHitstopAllowed) return;
            nextHitstopAllowed = now + cooldown;
            hitstopScale = scale;
            hitstopUntil = Mathf.Max(hitstopUntil, now + duration);
            Resolve();
        }

        /// <summary>Slow motion for a moment (tutorial highlights such as the first Golden blob).</summary>
        public void SlowMo(float seconds, float scale)
        {
            slowScale = Mathf.Clamp(scale, 0.05f, 1f);
            slowUntil = Mathf.Max(slowUntil, Time.unscaledTime + seconds);
            Resolve();
        }

        private void Resolve()
        {
            float now = Time.unscaledTime;
            float scale = 1f;
            if (Paused) scale = 0f;
            else if (now < hitstopUntil) scale = hitstopScale;
            else if (now < slowUntil) scale = slowScale;
            if (!Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = scale;
        }

        private void Update()
        {
            Resolve();
        }
    }
}
