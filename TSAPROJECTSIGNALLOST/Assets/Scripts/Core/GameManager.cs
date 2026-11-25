using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost
{
    /// <summary>
    /// Owns the Data wallet, game state and startup order. Lives on the "GameManager" object together with
    /// Balance, SaveManager, BuffSystem, SkillTree and DayManager.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [SerializeField] private GameAssets assets;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Station station;
        [SerializeField] private Transform playPlane;

        public GameAssets Assets { get { return assets; } }
        public Camera Cam { get { return worldCamera != null ? worldCamera : Camera.main; } }
        public Station Station { get { return station; } }
        public GameState State { get; private set; }
        public bool IsReady { get; private set; }

        public double Data { get; private set; }
        public double LifetimeData { get; private set; }
        /// <summary>Data carried by coins still in flight. The counter only shows it once they land.</summary>
        public double PendingVisual { get; private set; }
        public double DisplayData { get { return Math.Max(0d, Data - PendingVisual); } }

        public float PlaneY { get; private set; }
        public Plane PlayPlane { get; private set; }

        public StatsTracking Tracking { get; private set; }
        public int Echoes { get; private set; }
        public int Rebroadcasts { get; private set; }
        public string DroneSkin { get; private set; }
        public string EndingId { get; private set; }

        /// <summary>Echoes paid by the last ending (shown on the ending screen).</summary>
        public int LastEchoAward { get; private set; }
        /// <summary>True when the ending just reached had not been seen on any earlier run.</summary>
        public bool LastEndingWasNew { get; private set; }

        private List<string> endingsSeen = new List<string>();

        private Dictionary<string, double> flags = new Dictionary<string, double>();
        private float autoPenaltyUntil;

        // Data/sec: income bucketed into 0.25 s slots over the last 4 seconds.
        private const float BucketSize = 0.25f;
        private const int BucketCount = 16;
        private readonly double[] buckets = new double[BucketCount];
        private int bucketIndex;
        private float bucketTimer;

        private void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            Tracking = new StatsTracking();
            DroneSkin = "default";
            PlaneY = 1.5f;
            PlayPlane = new Plane(Vector3.up, new Vector3(0f, PlaneY, 0f));
            if (assets == null) assets = Resources.Load<GameAssets>("GameAssets");
        }

        private IEnumerator Start()
        {
            State = GameState.Loading;
            Application.targetFrameRate = 60;
            yield return Balance.I.LoadAll();
            BalanceData b = Balance.B;
            Application.targetFrameRate = b.targetFps;
            PlaneY = b.playPlaneY;
            PlayPlane = new Plane(Vector3.up, new Vector3(0f, PlaneY, 0f));
            if (playPlane != null) playPlane.position = new Vector3(playPlane.position.x, PlaneY, playPlane.position.z);

            SaveData save = SaveManager.I.LoadOrCreate();
            if (SettingsManager.I != null) SettingsManager.I.Init(save.settings);
            ReadSave(save);
            SkillTree.I.ReadSave(save);
            if (DayManager.I != null) DayManager.I.ReadSave(save);
            BuffSystem.I.ReadSave(save);
            MailDirector.I.ReadSave(save);
            EventDirector.I.ReadSave(save);
            if (TutorialDirector.I != null) TutorialDirector.I.ReadSave(save);

            if (station != null) station.SetLevel(GameStats.Antenna, true);

            IsReady = true;
            SaveManager.I.Ready = true;
            State = GameState.MainMenu;
            EventBus.RaiseGameReady();

            if (SkipMenuOnLoad)
            {
                // New Game from the menu (or Rebroadcast): go straight into play.
                SkipMenuOnLoad = false;
                StartGameplay();
            }
        }

        // ---------------- Menu / pause flow ----------------

        /// <summary>Set before reloading the scene to skip the main menu (New Game, Rebroadcast).</summary>
        public static bool SkipMenuOnLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SkipMenuOnLoad = false;
        }

        public bool IsPaused { get { return CoroutineHost.Paused; } }

        /// <summary>True when the player can see and use a save (Continue button).</summary>
        public bool HasSave { get { return SaveManager.I != null && (!SaveManager.I.WasNewGame || startedThisSession); } }

        // A fresh game becomes a save as soon as it is played (it autosaves), so Continue / New Game confirm apply.
        private bool startedThisSession;

        /// <summary>Main menu -> gameplay: camera eases in, HUD slides in. No loading screen.</summary>
        public void StartGameplay()
        {
            if (State != GameState.MainMenu) return;
            State = GameState.Playing;
            startedThisSession = true;
            if (CameraDirector.I != null) CameraDirector.I.GoToGameplay();
            EventBus.RaiseGameplayStarted();
        }

        /// <summary>Pause menu -> main menu (autosaves first). The world keeps living behind the menu.</summary>
        public void ReturnToMenu()
        {
            SaveManager.I.SaveNow();
            SetPaused(false);
            if (Vacuum.I != null) Vacuum.I.DropAll();
            if (State == GameState.Finale && BlobSpawner.I != null) BlobSpawner.I.ReleaseFinale();
            State = GameState.MainMenu;
            if (CameraDirector.I != null) CameraDirector.I.GoToMenu();
            EventBus.RaiseReturnedToMenu();
        }

        public void SetPaused(bool paused)
        {
            if (CoroutineHost.Paused == paused) return;
            if (paused && State != GameState.Playing && State != GameState.Finale) return;
            CoroutineHost.SetPaused(paused);
            if (AudioManager.I != null) AudioManager.I.SetPaused(paused);
            if (paused && Vacuum.I != null && Vacuum.I.Active) Vacuum.I.DropAll();
            if (paused) SaveManager.I.RequestSave();
            EventBus.RaisePauseChanged(paused);
        }

        /// <summary>New Game over an existing save: erase progress (settings are kept) and start fresh.</summary>
        public void NewGame()
        {
            if (!HasSave)
            {
                StartGameplay();
                return;
            }
            SaveManager.I.DeleteSave();
            SkipMenuOnLoad = true;
            ReloadScene();
        }

        public void QuitGame()
        {
            if (State == GameState.Playing || State == GameState.Finale) SaveManager.I.SaveNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            if (!IsReady) return;
            if (State == GameState.Playing && !IsPaused) Tracking.playSeconds += Time.unscaledDeltaTime;
            bucketTimer += Time.unscaledDeltaTime;
            while (bucketTimer >= BucketSize)
            {
                bucketTimer -= BucketSize;
                bucketIndex = (bucketIndex + 1) % BucketCount;
                buckets[bucketIndex] = 0d;
            }
        }

        // ---------------- Data wallet ----------------

        /// <summary>Adds Data. Coins fly from worldPos to the counter, which only ticks up as they land.</summary>
        public void Earn(double amount, Vector3 worldPos, int coinCount)
        {
            if (amount <= 0d || double.IsNaN(amount)) return;
            Data += amount;
            LifetimeData += amount;
            buckets[bucketIndex] += amount;
            int spawned = coinCount > 0 ? Juice.CoinBurst(worldPos, amount, coinCount) : 0;
            if (spawned > 0) PendingVisual += amount;
            else EventBus.RaiseCoinLanded(amount);
            EventBus.RaiseDataGained(amount);
        }

        /// <summary>Called by each coin as it reaches the counter.</summary>
        public void CoinArrived(double share)
        {
            PendingVisual = Math.Max(0d, PendingVisual - share);
            EventBus.RaiseCoinLanded(share);
        }

        public bool CanAfford(double cost)
        {
            return Data + 1e-9 >= cost;
        }

        /// <summary>Undo tonight: gives back Data spent on the end-of-day screen.</summary>
        public void Refund(double amount)
        {
            if (amount <= 0d) return;
            Data += amount;
            EventBus.RaiseCoinLanded(amount);
            EventBus.RaiseDataSpent();
        }

        public bool TrySpendEchoes(int cost)
        {
            if (cost < 0 || Echoes < cost) return false;
            Echoes -= cost;
            EventBus.RaiseDataSpent();
            return true;
        }

        /// <summary>Debug: grants Echoes (or removes them with a negative amount).</summary>
        public void AddEchoes(int amount)
        {
            Echoes = Mathf.Max(0, Echoes + amount);
            EventBus.RaiseDataSpent();
            SaveManager.I.RequestSave();
        }

        public bool HasSeenEnding(string id)
        {
            return endingsSeen.Contains(id);
        }

        public IList<string> EndingsSeen { get { return endingsSeen; } }

        public bool TrySpend(double cost)
        {
            if (cost < 0d || !CanAfford(cost)) return false;
            Data = Math.Max(0d, Data - cost);
            PendingVisual = Math.Min(PendingVisual, Data);
            EventBus.RaiseDataSpent();
            return true;
        }

        /// <summary>Rolling average of all income over the last few seconds.</summary>
        public double DataPerSec
        {
            get
            {
                double sum = 0d;
                for (int i = 0; i < BucketCount; i++) if (i != bucketIndex) sum += buckets[i];
                return sum / (BucketSize * (BucketCount - 1));
            }
        }

        /// <summary>Auto-system income drops 5% for 3 s after a blob is absorbed by the antenna.</summary>
        public double AutoIncomeMult
        {
            get { return Time.time < autoPenaltyUntil ? Balance.B.absorb.autoIncomeMult : 1d; }
        }

        public void NotifyAbsorbed()
        {
            autoPenaltyUntil = Time.time + Balance.B.absorb.seconds;
        }

        // ---------------- Flags ----------------

        public double Flag(string key)
        {
            return flags.TryGetValue(key, out double v) ? v : 0d;
        }

        public void SetFlag(string key, double value)
        {
            flags[key] = value;
        }

        public void AddFlag(string key, double delta)
        {
            flags[key] = Flag(key) + delta;
        }

        public void SetDroneSkin(string skin)
        {
            DroneSkin = string.IsNullOrEmpty(skin) ? "default" : skin;
            if (DroneManager.I != null) DroneManager.I.RefreshSkins();
        }

        // ---------------- Game flow ----------------

        /// <summary>Antenna Lv 10: every piece of Static freezes and arranges into one big shape, then ??? writes.</summary>
        public void BeginFinale()
        {
            if (State == GameState.Finale || State == GameState.Ending) return;
            State = GameState.Finale;
            if (BlobSpawner.I != null) BlobSpawner.I.ArrangeFinale();
            if (MailDirector.I != null) MailDirector.I.EnsureFinaleMail();
            StartCoroutine(OpenFinaleMail());
        }

        private IEnumerator OpenFinaleMail()
        {
            yield return new WaitForSeconds(3f);
            if (State == GameState.Finale && MailUI.I != null && !MailUI.I.IsOpen) MailUI.I.Open();
        }

        public string ChooseEnding()
        {
            int hale = MailDirector.I.Trust("hale");
            int iva = MailDirector.I.Trust("iva");
            int stat = MailDirector.I.Trust("static");
            // Highest trust wins; ties go Iva, then Hale.
            if (iva >= hale && iva >= stat) return "first_contact";
            if (hale >= stat) return "clear_channel";
            return "become_signal";
        }

        public void TriggerEnding()
        {
            if (State == GameState.Ending) return;
            State = GameState.Ending;
            EndingId = ChooseEnding();
            // Echoes for the Deep Signal tree: every ending pays, a new ending pays extra, a fast run pays extra.
            PrestigeConfig p = Balance.B.prestige;
            int day = DayManager.I != null ? DayManager.I.Day : Balance.B.days.targetDays;
            int award = p.echoesPerEnding + Mathf.Max(0, Balance.B.days.targetDays - day) * p.echoPerDayUnderTarget;
            LastEndingWasNew = !endingsSeen.Contains(EndingId);
            if (LastEndingWasNew)
            {
                award += p.firstEndingBonus;
                endingsSeen.Add(EndingId);
            }
            LastEchoAward = award;
            Echoes += award;
            SaveManager.I.RequestSave();
            EndingDef def = null;
            foreach (EndingDef e in Balance.I.Mail.endings) if (e.id == EndingId) def = e;
            if (EndingScreen.I != null) EndingScreen.I.Play(def);
        }

        /// <summary>After the end screen: keep playing in endless mode (reloads so the world resets).</summary>
        public void ContinueAfterEnding()
        {
            State = GameState.Playing;
            SaveManager.I.SaveNow();
            SkipMenuOnLoad = true;
            ReloadScene();
        }

        /// <summary>
        /// Rebirth: a new run at Day 1. The main tree, Data, Antenna, trust and story reset. The Deep Signal
        /// tree, Echoes, endings seen and settings are kept. The run opens on the Deep Signal screen.
        /// </summary>
        public void Rebroadcast()
        {
            SaveData s = SaveManager.I.Collect();
            s.rebroadcasts = Rebroadcasts + 1;
            s.data = 0d;
            s.upgrades = new Dictionary<string, int>();
            s.tonight = new List<string>();
            s.trust = new Dictionary<string, int>();
            s.mail = new MailSave();
            s.buffs = new List<BuffSave>();
            s.flags = new Dictionary<string, double>();
            s.endingId = null;
            s.day = new DaySave { day = 1, phase = "deep", introSeen = true };
            s.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveManager.I.WriteAndLock(s);
            SkipMenuOnLoad = true;
            ReloadScene();
        }

        public void ResetSave()
        {
            SaveManager.I.DeleteSave();
            ReloadScene();
        }

        private static void ReloadScene()
        {
            CoroutineHost.SetPaused(false);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------- Save ----------------

        private void ReadSave(SaveData s)
        {
            Data = s.data;
            LifetimeData = Math.Max(s.lifetimeData, s.data);
            Tracking = s.statsTracking ?? new StatsTracking();
            Echoes = s.echoes;
            Rebroadcasts = s.rebroadcasts;
            DroneSkin = string.IsNullOrEmpty(s.droneSkin) ? "default" : s.droneSkin;
            EndingId = s.endingId;
            flags = s.flags ?? new Dictionary<string, double>();
            endingsSeen = s.endingsSeen != null ? new List<string>(s.endingsSeen) : new List<string>();
        }

        public void WriteSave(SaveData s)
        {
            s.data = Data;
            s.lifetimeData = LifetimeData;
            s.statsTracking = Tracking;
            s.echoes = Echoes;
            s.rebroadcasts = Rebroadcasts;
            s.droneSkin = DroneSkin;
            s.endingId = EndingId;
            s.flags = new Dictionary<string, double>(flags);
            s.endingsSeen = new List<string>(endingsSeen);
        }
    }
}
