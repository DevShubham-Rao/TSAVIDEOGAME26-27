namespace SignalLost
{
    /// <summary>
    /// Player settings, saved to persistentDataPath/settings.json (separate from the game save so
    /// resetting progress keeps settings). Defaults come from balance.json "settings_defaults".
    /// </summary>
    public class SettingsData
    {
        public int version = 1;

        // Audio (0..1 linear, converted to dB for the AudioMixer).
        public float master = 1f;
        public float music = 0.7f;
        public float sfx = 0.9f;

        // Graphics.
        public bool fullscreen = true;
        public int resolutionWidth;
        public int resolutionHeight;
        public bool vsync = true;
        /// <summary>Frames per second cap; -1 = unlimited.</summary>
        public int fpsCap = 60;
        /// <summary>0 Low, 1 Medium, 2 High.</summary>
        public int quality = 2;
        public bool bloom = true;
        /// <summary>0 Low, 1 Medium, 2 High.</summary>
        public int particleDensity = 2;

        // Gameplay.
        public float shake = 1f;
        public string notation = "suffix";
        public string vacuumMode = "hold";
        /// <summary>0 Slow, 1 Normal, 2 Instant.</summary>
        public int mailSpeed = 1;
        public bool autoCollectOffline;
        public bool tutorialHints = true;

        // Accessibility.
        public bool colorblind;
        public bool reduceFlashing;
        public float uiScale = 1f;
        public bool highContrast;

        public SettingsData Clone()
        {
            return (SettingsData)MemberwiseClone();
        }
    }
}
