using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Live view of the current settings (SettingsManager owns loading, saving and applying them).
    /// Gameplay reads these every frame, so every change takes effect instantly.
    /// </summary>
    public static class GameSettings
    {
        public static SettingsData S { get; private set; } = new SettingsData();

        public static void Set(SettingsData data)
        {
            S = data ?? new SettingsData();
            NumberFormat.Scientific = S.notation == "scientific";
        }

        public static float Master { get { return S.master; } }
        public static float Music { get { return S.music; } }
        public static float Sfx { get { return S.sfx; } }

        /// <summary>Screen shake intensity 0..1.</summary>
        public static float ShakeIntensity { get { return Mathf.Clamp01(S.shake); } }
        public static bool Scientific { get { return S.notation == "scientific"; } }
        public static bool VacuumToggle { get { return S.vacuumMode == "toggle"; } }
        public static bool AutoCollectOffline { get { return S.autoCollectOffline; } }
        public static bool TutorialHints { get { return S.tutorialHints; } }
        public static bool Colorblind { get { return S.colorblind; } }
        public static bool ReduceFlashing { get { return S.reduceFlashing; } }
        public static bool HighContrast { get { return S.highContrast; } }

        public static float ParticleDensityMult
        {
            get
            {
                BalanceData b = Balance.B;
                if (b == null || b.graphics.particleDensity.Count == 0) return 1f;
                int i = Mathf.Clamp(S.particleDensity, 0, b.graphics.particleDensity.Count - 1);
                return b.graphics.particleDensity[i];
            }
        }

        /// <summary>Mail typewriter speed multiplier; 0 means instant.</summary>
        public static float MailSpeedMult
        {
            get
            {
                BalanceData b = Balance.B;
                if (b == null || b.graphics.mailSpeedMult.Count == 0) return 1f;
                int i = Mathf.Clamp(S.mailSpeed, 0, b.graphics.mailSpeedMult.Count - 1);
                return b.graphics.mailSpeedMult[i];
            }
        }

        /// <summary>Scales a particle count by the Particle Density setting (never below 1).</summary>
        public static int Particles(int count)
        {
            if (count <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(count * ParticleDensityMult));
        }

        public static void Changed()
        {
            EventBus.RaiseSettingsChanged();
        }
    }
}
