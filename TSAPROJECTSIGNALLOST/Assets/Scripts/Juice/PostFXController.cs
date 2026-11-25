using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SignalLost
{
    /// <summary>
    /// Runtime control of the Global Volume: Bloom on/off from settings, and a depth-of-field blur behind
    /// the pause menu. Works on a runtime copy of the profile so the asset on disk never changes.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class PostFXController : MonoBehaviour
    {
        public static PostFXController I { get; private set; }

        private Volume volume;
        private Bloom bloom;
        private DepthOfField blur;
        private Vignette vignette;
        private float baseVignette = 0.2f;
        private float blurAmount;
        private float blurTarget;

        private void Awake()
        {
            I = this;
            volume = GetComponent<Volume>();
            VolumeProfile p = volume.profile;
            if (p != null)
            {
                p.TryGet(out bloom);
                p.TryGet(out blur);
                p.TryGet(out vignette);
                if (vignette != null) baseVignette = vignette.intensity.value;
            }
        }

        private void OnEnable()
        {
            EventBus.PauseChanged += OnPause;
        }

        private void OnDisable()
        {
            EventBus.PauseChanged -= OnPause;
        }

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            if (bloom != null) bloom.active = GameSettings.S.bloom;
        }

        private void OnPause(bool paused)
        {
            blurTarget = paused ? 1f : 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(blurAmount, blurTarget)) return;
            blurAmount = Mathf.MoveTowards(blurAmount, blurTarget, Time.unscaledDeltaTime * 4f);
            if (blur != null)
            {
                blur.active = blurAmount > 0.01f;
                blur.gaussianMaxRadius.value = Mathf.Lerp(0.5f, 1.5f, blurAmount);
                blur.gaussianStart.value = Mathf.Lerp(30f, 0f, blurAmount);
                blur.gaussianEnd.value = Mathf.Lerp(60f, 4f, blurAmount);
            }
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(baseVignette, 0.45f, blurAmount);
        }
    }
}
