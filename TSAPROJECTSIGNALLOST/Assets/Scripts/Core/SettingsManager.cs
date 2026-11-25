using System;
using System.IO;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Loads, applies and saves settings.json (separate from save.json, so resetting progress keeps
    /// settings). Every change applies instantly; the file is written shortly after the last change.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager I { get; private set; }

        public bool Loaded { get; private set; }

        private bool dirty;
        private float saveTimer;
        private FullScreenMode lastMode = (FullScreenMode)(-1);
        private int lastWidth;
        private int lastHeight;

        private static string FilePath
        {
            get
            {
                string dir = string.IsNullOrEmpty(SaveManager.DirectoryOverride) ? Application.persistentDataPath : SaveManager.DirectoryOverride;
                return Path.Combine(dir, "settings.json");
            }
        }

        private void Awake()
        {
            I = this;
        }

        /// <summary>Called by GameManager once balance.json is loaded.</summary>
        public void Init(SettingsSave legacy)
        {
            SettingsData data = null;
            try
            {
                if (File.Exists(FilePath)) data = JsonUtil.Parse<SettingsData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] Could not read settings.json, using defaults: " + e.Message);
            }
            bool firstRun = data == null;
            if (data == null)
            {
                data = Defaults();
                if (legacy != null)
                {
                    // Migrate the v1 settings that used to live inside save.json.
                    data.master = legacy.master;
                    data.music = legacy.music;
                    data.sfx = legacy.sfx;
                    data.shake = legacy.shake ? 1f : 0f;
                    data.notation = legacy.notation ?? "suffix";
                }
                dirty = true;
            }
            Sanitize(data);
            GameSettings.Set(data);
            Loaded = true;
            if (firstRun)
            {
                // First launch: keep the window exactly as the player started it (launcher flags, OS size).
                lastMode = Screen.fullScreenMode == FullScreenMode.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
                lastWidth = data.resolutionWidth;
                lastHeight = data.resolutionHeight;
            }
            ApplyAll();
        }

        /// <summary>
        /// Alt+Enter, window resizing or launch flags can change the window behind the menu's back.
        /// Called when Settings opens so the Display row always shows the real state.
        /// </summary>
        public void SyncDisplayFromWindow()
        {
            if (!Loaded || Application.isEditor) return;
            SettingsData s = GameSettings.S;
            bool fs = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (fs == s.fullscreen && Screen.width == s.resolutionWidth && Screen.height == s.resolutionHeight) return;
            s.fullscreen = fs;
            s.resolutionWidth = Screen.width;
            s.resolutionHeight = Screen.height;
            lastMode = fs ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            lastWidth = s.resolutionWidth;
            lastHeight = s.resolutionHeight;
            dirty = true;
            saveTimer = 0.4f;
            GameSettings.Changed();
        }

        /// <summary>Defaults from balance.json; display mode starts from whatever the player launched with.</summary>
        public SettingsData Defaults()
        {
            SettingsData d = Balance.B != null ? Balance.B.settingsDefaults.Clone() : new SettingsData();
            d.fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            d.resolutionWidth = Screen.width;
            d.resolutionHeight = Screen.height;
            return d;
        }

        private static void Sanitize(SettingsData d)
        {
            d.master = Mathf.Clamp01(d.master);
            d.music = Mathf.Clamp01(d.music);
            d.sfx = Mathf.Clamp01(d.sfx);
            d.shake = Mathf.Clamp01(d.shake);
            d.quality = Mathf.Clamp(d.quality, 0, 2);
            d.particleDensity = Mathf.Clamp(d.particleDensity, 0, 2);
            d.mailSpeed = Mathf.Clamp(d.mailSpeed, 0, 2);
            float min = Balance.B != null ? Balance.B.graphics.uiScaleMin : 0.8f;
            float max = Balance.B != null ? Balance.B.graphics.uiScaleMax : 1.3f;
            d.uiScale = Mathf.Clamp(d.uiScale <= 0f ? 1f : d.uiScale, min, max);
            if (d.notation != "scientific") d.notation = "suffix";
            if (d.vacuumMode != "toggle") d.vacuumMode = "hold";
            if (d.resolutionWidth <= 0 || d.resolutionHeight <= 0)
            {
                d.resolutionWidth = Screen.width;
                d.resolutionHeight = Screen.height;
            }
        }

        /// <summary>Changes settings, applies them immediately, and schedules a save.</summary>
        public void Change(Action<SettingsData> mutate)
        {
            SettingsData s = GameSettings.S;
            mutate(s);
            Sanitize(s);
            GameSettings.Set(s);
            ApplyAll();
            dirty = true;
            saveTimer = 0.4f;
        }

        public void RestoreDefaults(string tab)
        {
            SettingsData d = Defaults();
            Change(s =>
            {
                switch (tab)
                {
                    case "audio":
                        s.master = d.master;
                        s.music = d.music;
                        s.sfx = d.sfx;
                        break;
                    case "graphics":
                        s.fullscreen = d.fullscreen;
                        s.resolutionWidth = Screen.currentResolution.width;
                        s.resolutionHeight = Screen.currentResolution.height;
                        s.vsync = d.vsync;
                        s.fpsCap = d.fpsCap;
                        s.quality = d.quality;
                        s.bloom = d.bloom;
                        s.particleDensity = d.particleDensity;
                        break;
                    case "gameplay":
                        s.shake = d.shake;
                        s.notation = d.notation;
                        s.vacuumMode = d.vacuumMode;
                        s.mailSpeed = d.mailSpeed;
                        s.autoCollectOffline = d.autoCollectOffline;
                        s.tutorialHints = d.tutorialHints;
                        break;
                    case "accessibility":
                        s.colorblind = d.colorblind;
                        s.reduceFlashing = d.reduceFlashing;
                        s.uiScale = d.uiScale;
                        s.highContrast = d.highContrast;
                        break;
                }
            });
        }

        // ---------------- apply ----------------

        public void ApplyAll()
        {
            ApplyGraphics();
            if (AudioManager.I != null) AudioManager.I.ApplyVolumes();
            if (UIManager.I != null) UIManager.I.ApplyUiScale(GameSettings.S.uiScale);
            if (PostFXController.I != null) PostFXController.I.Apply();
            UIContrast.ApplyAll();
            GameSettings.Changed();
        }

        private void ApplyGraphics()
        {
            SettingsData s = GameSettings.S;
            // Quality first: switching levels resets VSync.
            if (Balance.B != null && s.quality < Balance.B.graphics.qualityLevels.Count)
            {
                int q = Array.IndexOf(QualitySettings.names, Balance.B.graphics.qualityLevels[s.quality]);
                if (q >= 0 && QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
            }
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            Application.targetFrameRate = s.vsync ? -1 : s.fpsCap;

            FullScreenMode mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (!Application.isEditor && (mode != lastMode || s.resolutionWidth != lastWidth || s.resolutionHeight != lastHeight))
            {
                bool first = (int)lastMode < 0;
                bool same = Screen.fullScreenMode == mode && Screen.width == s.resolutionWidth && Screen.height == s.resolutionHeight;
                if (!(first && same)) Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, mode);
            }
            lastMode = mode;
            lastWidth = s.resolutionWidth;
            lastHeight = s.resolutionHeight;
        }

        // ---------------- save ----------------

        public void SaveNow()
        {
            if (!Loaded) return;
            dirty = false;
            try
            {
                string path = FilePath;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtil.Write(GameSettings.S));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] Save failed: " + e.Message);
            }
        }

        private void Update()
        {
            if (!dirty) return;
            saveTimer -= Time.unscaledDeltaTime;
            if (saveTimer <= 0f) SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && dirty) SaveNow();
        }

        private void OnApplicationQuit()
        {
            if (dirty) SaveNow();
        }
    }
}
