using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SignalLost
{
    /// <summary>
    /// Every sound goes through AudioManager.Play("pop")-style calls. Placeholder clips are synthesized at
    /// startup; drop a real AudioClip with the same name into Clip Overrides to replace one.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private int voiceCount = 28;
        [SerializeField] private AudioClip[] clipOverrides = new AudioClip[0];
        [SerializeField] private AudioClip musicOverride;
        [SerializeField] private AudioClip musicDrumsOverride;
        [SerializeField] private float sfxBaseVolume = 0.8f;
        [SerializeField] private float musicBaseVolume = 0.45f;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private int nextVoice;
        private AudioSource vacuumSource;
        private AudioSource hissSource;
        private AudioSource musicSource;
        private AudioSource drumSource;
        private float vacuumTarget;
        private float drumTarget;
        private float musicDuck = 1f;
        private float coinWindowStart;
        private int coinsThisWindow;
        private AudioMixerGroup sfxGroup;
        private AudioMixerGroup musicGroup;
        private bool mixerActive;
        private float pauseDuck = 1f;

        /// <summary>Linear 0..1 slider value to mixer decibels (logarithmic).</summary>
        public static float LinearToDb(float v)
        {
            return v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
        }

        /// <summary>Volume applied on the sources themselves when no AudioMixer is available.</summary>
        private float SfxFactor { get { return mixerActive ? 1f : GameSettings.Sfx * GameSettings.Master; } }
        private float MusicFactor { get { return mixerActive ? 1f : GameSettings.Music * GameSettings.Master; } }

        public static void Play(string key, float pitch = 1f, float volume = 1f)
        {
            if (I != null) I.PlayInternal(key, pitch, volume);
        }

        public static void PlayVaried(string key, float minPitch, float maxPitch, float volume = 1f)
        {
            if (I != null) I.PlayInternal(key, Random.Range(minPitch, maxPitch), volume);
        }

        private void Awake()
        {
            I = this;
            Dictionary<string, AudioClip> generated = SynthClips.BuildAll();
            foreach (KeyValuePair<string, AudioClip> kv in generated) clips[kv.Key] = kv.Value;
            foreach (AudioClip c in clipOverrides)
            {
                if (c != null) clips[c.name] = c;
            }

            if (mixer != null)
            {
                AudioMixerGroup[] g = mixer.FindMatchingGroups("SFX");
                if (g.Length > 0) sfxGroup = g[0];
                g = mixer.FindMatchingGroups("Music");
                if (g.Length > 0) musicGroup = g[0];
            }

            for (int i = 0; i < voiceCount; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.outputAudioMixerGroup = sfxGroup;
                voices.Add(s);
            }

            vacuumSource = MakeLoop("vacuum_loop");
            hissSource = MakeLoop("hiss");

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = musicOverride != null ? musicOverride : SynthClips.BuildMusic(false);
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            drumSource = gameObject.AddComponent<AudioSource>();
            drumSource.clip = musicDrumsOverride != null ? musicDrumsOverride : SynthClips.BuildMusic(true);
            drumSource.loop = true;
            drumSource.playOnAwake = false;
            drumSource.volume = 0f;
            musicSource.outputAudioMixerGroup = musicGroup;
            drumSource.outputAudioMixerGroup = musicGroup;
            vacuumSource.outputAudioMixerGroup = sfxGroup;
            hissSource.outputAudioMixerGroup = sfxGroup;
            ApplyVolumes();
        }

        /// <summary>Pushes Master / Music / SFX settings into the AudioMixer (dB) or onto the sources.</summary>
        public void ApplyVolumes()
        {
            mixerActive = false;
            if (mixer == null || mixerUnusable) return;
            bool ok = mixer.SetFloat("MasterVolume", LinearToDb(GameSettings.Master));
            ok &= mixer.SetFloat("MusicVolume", LinearToDb(GameSettings.Music));
            ok &= mixer.SetFloat("SfxVolume", LinearToDb(GameSettings.Sfx));
            // A mixer without the exposed parameters warns on every SetFloat; try once, then use source volumes.
            if (!ok) mixerUnusable = true;
            mixerActive = ok && sfxGroup != null && musicGroup != null;
        }

        public bool MixerActive { get { return mixerActive; } }
        private bool mixerUnusable;

        /// <summary>Softens music while the game is paused.</summary>
        public void SetPaused(bool paused)
        {
            pauseDuck = paused && Balance.B != null ? Balance.B.menu.pauseMusicDuck : 1f;
            if (paused) SetVacuum(false, 0f);
        }

        private void Start()
        {
            // AudioMixer parameters set during Awake can be overwritten by the snapshot; set them again here.
            ApplyVolumes();
            musicSource.Play();
            drumSource.Play();
            drumSource.timeSamples = 0;
        }

        private void OnEnable()
        {
            EventBus.EventStarted += OnEventStarted;
            EventBus.EventEnded += OnEventEnded;
        }

        private void OnDisable()
        {
            EventBus.EventStarted -= OnEventStarted;
            EventBus.EventEnded -= OnEventEnded;
        }

        private void OnEventStarted(EventDef d) { drumTarget = 1f; }

        private void OnEventEnded(EventDef d, bool ok) { drumTarget = 0f; }

        private AudioSource MakeLoop(string key)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clips.TryGetValue(key, out AudioClip c) ? c : null;
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            return s;
        }

        private void PlayInternal(string key, float pitch, float volume)
        {
            if (string.IsNullOrEmpty(key) || !clips.TryGetValue(key, out AudioClip clip) || clip == null) return;
            if (key == "coin")
            {
                // Limit coin ticks to N per second so a coin shower never turns into noise.
                int limit = Balance.B != null ? Balance.B.juice.coinSoundsPerSecond : 20;
                if (Time.unscaledTime - coinWindowStart > 1f)
                {
                    coinWindowStart = Time.unscaledTime;
                    coinsThisWindow = 0;
                }
                if (coinsThisWindow >= limit) return;
                coinsThisWindow++;
            }

            AudioSource v = null;
            for (int i = 0; i < voices.Count; i++)
            {
                AudioSource candidate = voices[(nextVoice + i) % voices.Count];
                if (!candidate.isPlaying)
                {
                    v = candidate;
                    nextVoice = (nextVoice + i + 1) % voices.Count;
                    break;
                }
            }
            if (v == null)
            {
                v = voices[nextVoice];
                nextVoice = (nextVoice + 1) % voices.Count;
            }
            v.clip = clip;
            v.pitch = pitch;
            v.volume = Mathf.Clamp01(volume) * sfxBaseVolume * SfxFactor;
            v.Play();
        }

        /// <summary>Vacuum loop volume and pitch follow how many blobs are held (0..1).</summary>
        public void SetVacuum(bool active, float fill01)
        {
            vacuumTarget = active ? 0.35f + 0.65f * Mathf.Clamp01(fill01) : 0f;
            if (active && !vacuumSource.isPlaying) vacuumSource.Play();
            vacuumSource.pitch = 0.85f + 0.5f * Mathf.Clamp01(fill01);
        }

        public void SetHiss(bool on)
        {
            if (on && !hissSource.isPlaying) hissSource.Play();
            hissSource.volume = on ? 0.25f * SfxFactor : 0f;
            if (!on) hissSource.Stop();
        }

        /// <summary>Fades music to a fraction of normal (used by the quiet Clear Channel ending).</summary>
        public void SetMusicDuck(float amount)
        {
            musicDuck = Mathf.Clamp01(amount);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float sfx = SfxFactor;
            float current = vacuumSource.volume;
            float target = vacuumTarget * 0.4f * sfx;
            vacuumSource.volume = Mathf.MoveTowards(current, target, dt * 2.5f);
            if (vacuumTarget <= 0f && vacuumSource.volume <= 0.001f && vacuumSource.isPlaying) vacuumSource.Stop();

            float music = musicBaseVolume * MusicFactor * musicDuck * pauseDuck;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, music, dt * 0.8f);
            drumSource.volume = Mathf.MoveTowards(drumSource.volume, music * drumTarget * 0.9f, dt * 0.9f);
            if (drumSource.isPlaying && musicSource.isPlaying && Mathf.Abs(drumSource.timeSamples - musicSource.timeSamples) > 2000)
            {
                drumSource.timeSamples = Mathf.Min(musicSource.timeSamples, drumSource.clip.samples - 1);
            }
        }
    }
}
