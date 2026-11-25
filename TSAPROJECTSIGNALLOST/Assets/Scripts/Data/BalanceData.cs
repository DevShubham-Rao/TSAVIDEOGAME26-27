using System.Collections.Generic;

namespace SignalLost
{
    // Plain C# classes mirrored from StreamingAssets/Data/balance.json.
    // Field names are camelCase; JsonUtil maps them to snake_case keys.

    public class BalanceData
    {
        public int version = 1;
        public int targetFps = 60;
        public float playPlaneY = 1.5f;
        public double dataValueScale = 1;
        public float autosaveSeconds = 30;
        public int endingAntennaLevel = 10;
        public Dictionary<string, double> statBase = new Dictionary<string, double>();
        public SpawnConfig spawn = new SpawnConfig();
        public List<BlobTypeDef> blobs = new List<BlobTypeDef>();
        public GoldenConfig golden = new GoldenConfig();
        public MegaConfig mega = new MegaConfig();
        public ComboConfig combo = new ComboConfig();
        public CompressorConfig compressor = new CompressorConfig();
        public SortConfig sort = new SortConfig();
        public VacuumConfig vacuum = new VacuumConfig();
        public DroneConfig drones = new DroneConfig();
        public ClickConfig click = new ClickConfig();
        public AbsorbConfig absorb = new AbsorbConfig();
        public DaysConfig days = new DaysConfig();
        public BitsConfig bits = new BitsConfig();
        public TreeConfig tree = new TreeConfig();
        public EventTimingConfig events = new EventTimingConfig();
        public MailTimingConfig mail = new MailTimingConfig();
        public PrestigeConfig prestige = new PrestigeConfig();
        public JuiceConfig juice = new JuiceConfig();
        public MenuConfig menu = new MenuConfig();
        public TutorialConfig tutorial = new TutorialConfig();
        public GraphicsConfig graphics = new GraphicsConfig();
        public AccessibilityConfig accessibility = new AccessibilityConfig();
        public SettingsData settingsDefaults = new SettingsData();

        public double StatBase(string key, double fallback = 0)
        {
            return statBase != null && statBase.TryGetValue(key, out double v) ? v : fallback;
        }
    }

    public class SpawnConfig
    {
        public float basePerSecond = 1.2f;
        public int initialBurst = 6;
        public int hardCap = 150;
        public float edgeInset = 0.4f;
        public float wanderStrength = 0.25f;
    }

    public class BlobTypeDef
    {
        public string id;
        public string color;
        public int stability = 1;
        public double value = 1;
        public int unlockAntenna;
        public float weight = 1;
        public float speed = 0.5f;
        public float scale = 1;
        public string behavior = "drift";
        /// <summary>Data bits dropped when popped (they share the blob's value).</summary>
        public int bits = 1;
        /// <summary>Small Static this type splits into when popped (0 = none).</summary>
        public int split;
        public string splitInto = "static";
    }

    public class GoldenConfig
    {
        public int stability = 1;
        public double valueMultOfBest = 100;
        public float speed = 1.8f;
        public float escapeSeconds = 6;
        public float sparkleInterval = 0.08f;
        public float goldenHourSpawnPerSecond = 3f;
    }

    public class MegaConfig
    {
        public float chance = 0.07f;
        public float scale = 2f;
        public int splitCount = 4;
        public int stability = 15;
        public double value = 100;
        public int unlockAntenna = 5;
        public float splitChildScale = 0.8f;
    }

    public class ComboConfig
    {
        public float window = 1.5f;
        /// <summary>Extra seconds the combo survives after the bar is empty (coyote time).</summary>
        public float grace = 0.8f;
        public float step = 0.05f;
        public float cap = 5f;
        public List<int> milestones = new List<int> { 10, 25, 50, 80 };
    }

    public class CompressorConfig
    {
        public float scalePerLevel = 0.03f;
        public float glowPerLevel = 0.12f;
        public float feedRadius = 1.3f;
        public int crunchCoins = 44;
        public float crunchWindup = 0.45f;
        public int maxVisibleInside = 36;
    }

    public class SortConfig
    {
        public double correctMult = 3;
        public float catchRadius = 0.8f;
        public float bounceDistance = 2.2f;
    }

    public class VacuumConfig
    {
        public float pullSpeed = 9f;
        public float stickDistance = 0.55f;
        public float maxStretch = 1.4f;
        public float flingDuration = 0.42f;
        public float idleRingRadius = 0.32f;
        public float ringWidthBase = 0.025f;
        public float ringWidthPerLevel = 0.004f;
        public float ringWidthMax = 0.14f;
    }

    public class DroneConfig
    {
        public float popInterval = 2f;
        public float moveSpeed = 4f;
        public float hoverHeight = 0.9f;
        public int max = 25;
        public float actRange = 1.1f;
        public int trailFromLevel = 1;
    }

    public class ClickConfig
    {
        public float pickRadius = 0.12f;
        public float hitstopCooldown = 0.1f;
    }

    public class AbsorbConfig
    {
        public double autoIncomeMult = 0.95;
        public float seconds = 3;
        public float radius = 0.9f;
    }

    /// <summary>The shift structure: timed days with an end-of-day screen.</summary>
    public class DaysConfig
    {
        public float lastCallSeconds = 5f;
        public float titleCardSeconds = 3f;
        public float spawnGrowthPerDay = 0.1f;
        public float capGrowthPerDay = 0.1f;
        public int goldenFromAntenna = 3;
        /// <summary>Per-spawn Gold chance on a day a mail deal opened Gold before Antenna 3.</summary>
        public float questGoldenChance = 0.12f;
        public float luckyShiftChance = 0.25f;
        public float luckyShiftGoldenMult = 2f;
        public float magnetPulseInterval = 10f;
        public float magnetPulseDistance = 2f;
        public float autoPopInterval = 3f;
        public float overclockSeconds = 15f;
        public float autoFeedAt = 0.8f;
        public float bitMagnetSpeed = 0.35f;
        public int targetDays = 15;
        public int midpointAntenna = 5;
    }

    /// <summary>Data bits: what popped Static drops. Swept up and sorted into chutes.</summary>
    public class BitsConfig
    {
        public float scale = 0.38f;
        public float spread = 0.7f;
        public int goldenBits = 20;
        public float bobAmplitude = 0.05f;
        public float drift = 0.05f;
    }

    public class TreeConfig
    {
        public float columnSpacing = 380f;
        public float rowSpacing = 112f;
        public float nodeSize = 70f;
        public float laneSpacing = 95f;
    }

    public class EventTimingConfig
    {
        public float intervalMin = 60;
        public float intervalMax = 180;
        public float firstDelay = 45;
        public float warningSeconds = 1.4f;
        public float stampSeconds = 1.3f;
    }

    public class MailTimingConfig
    {
        public float flavorMin = 180;
        public float flavorMax = 300;
        public float firstFlavorDelay = 100;
        public float followupDelay = 10;
        public float typewriterCharsPerSecond = 90;
    }

    public class PrestigeConfig
    {
        public int echoesPerEnding = 3;
        public int firstEndingBonus = 5;
        public int echoPerDayUnderTarget = 1;
    }

    public class JuiceConfig
    {
        public float hoverScale = 1.08f;
        public float hoverEmission = 0.3f;
        public float squashX = 1.2f;
        public float squashY = 0.8f;
        public float squashTime = 0.15f;
        public float popPunch = 1.3f;
        public float popPunchTime = 0.05f;
        public int popParticlesMin = 12;
        public int popParticlesMax = 30;
        public float popHitstop = 0.04f;
        public float hitstopTimeScale = 0.05f;
        public float popPitchStep = 0.01f;
        public float popPitchMax = 1.8f;
        public int coinsMin = 1;
        public int coinsMax = 8;
        public int maxActiveCoins = 320;
        public float coinFlightTime = 0.6f;
        public float crunchShakeTime = 0.3f;
        public float crunchShakeAmp = 0.15f;
        public float critShakeAmp = 0.06f;
        public float critShakeAmpStrong = 0.16f;
        public float floatTextRise = 60f;
        public float floatTextTime = 0.6f;
        public float counterTween = 0.3f;
        public float buyPunch = 1.15f;
        public float buyPunchTime = 0.1f;
        public float levelUpFlashAlpha = 0.6f;
        public float levelUpFlashTime = 0.4f;
        public int mailBounces = 3;
        public float mailBounceTime = 0.6f;
        public int coinSoundsPerSecond = 20;
        public float emissionStrength = 2.6f;
    }

    public class MenuConfig
    {
        public float spawnPerSecond = 0.7f;
        public int maxBlobs = 6;
        public int maxBlobsLow = 18;
        public float lookRadius = 2.2f;
        public float flinchRadius = 2.5f;
        public float flinchDistance = 0.7f;
        public float cameraPitch = 9f;
        public float cameraYaw = -12f;
        public float cameraDistance = 27f;
        public float cameraLookHeight = 3.2f;
        public float driftYaw = 3.5f;
        public float driftPitch = 1.2f;
        public float driftSpeed = 0.12f;
        public float transitionSeconds = 0.8f;
        public float hudSlideSeconds = 0.55f;
        public float buttonFlySeconds = 0.35f;
        public float buttonStagger = 0.05f;
        public float logoPulseSpeed = 2f;
        public float logoWobbleDegrees = 2.5f;
        public float hoverScale = 1.05f;
        public float clickPunch = 1.15f;
        public float pauseMusicDuck = 0.45f;
    }

    public class TutorialConfig
    {
        public float idleNudgeSeconds = 8f;
        public float nudgeScale = 1.6f;
        public float spotlightAlpha = 0.62f;
        public float spotlightRadius = 110f;
        public float firstEventTimeMult = 2f;
        public float stepTimeoutSeconds = 120f;
        public float praiseSeconds = 1.1f;
        public float textCharsPerSecond = 60f;
        public float tipSeconds = 7f;
        public int handoffBurst = 6;
        public float keepAliveInterval = 0.5f;
        public int celebrateCoinsSmall = 10;
        public int celebrateCoinsBig = 24;
        public int celebrateCoinsHuge = 60;
    }

    public class GraphicsConfig
    {
        public List<float> particleDensity = new List<float> { 0.35f, 0.65f, 1f };
        public List<int> fpsOptions = new List<int> { 30, 60, 120, -1 };
        public List<string> qualityLevels = new List<string> { "Low", "Medium", "High" };
        public float uiScaleMin = 0.8f;
        public float uiScaleMax = 1.3f;
        public List<float> mailSpeedMult = new List<float> { 0.45f, 1f, 0f };
    }

    public class AccessibilityConfig
    {
        public float blobGlyphScale = 1.15f;
        public float chuteGlyphSize = 0.75f;
        public float chuteGlyphHeight = 0.75f;
        public float goldenEmissionReduced = 1.0f;
        public float sparkleIntervalReducedMult = 3f;
    }
}
