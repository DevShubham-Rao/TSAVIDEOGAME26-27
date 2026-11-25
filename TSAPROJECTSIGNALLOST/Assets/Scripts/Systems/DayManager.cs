using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    public enum DayPhase { Idle, Cutscene, TitleCard, Shift, LastCall, Night }

    /// <summary>
    /// The day-by-day structure. Each day: title card, then a timed shift (Pop, Sweep, Sort, Crunch), then
    /// a short last call to finish crunching, then the end-of-day screen (report, mail, skill tree).
    /// "Start Day N+1" on that screen begins the next day. The game saves at the start of every day and on
    /// the end-of-day screen; quitting mid-shift resumes at the start of that same day.
    /// </summary>
    public class DayManager : MonoBehaviour
    {
        public static DayManager I { get; private set; }

        public int Day { get; private set; } = 1;
        public DayPhase Phase { get; private set; } = DayPhase.Idle;
        public float TimeLeft { get; private set; }
        public float ShiftLength { get; private set; } = 60f;
        public bool Lucky { get; private set; }
        public DayReport Report { get; private set; } = new DayReport();
        public double DayStartData { get; private set; }

        public bool InShift { get { return Phase == DayPhase.Shift; } }
        public bool InShiftOrLastCall { get { return Phase == DayPhase.Shift || Phase == DayPhase.LastCall; } }
        /// <summary>Static spawns only while the shift clock is running.</summary>
        public bool SpawningAllowed { get { return Phase == DayPhase.Shift; } }
        /// <summary>Main-tree nodes can be bought on the end-of-day screen only.</summary>
        public bool ShopOpen { get { return Phase == DayPhase.Night; } }

        private Dictionary<string, double> modifiers = new Dictionary<string, double>();
        private Dictionary<string, double> pendingModifiers = new Dictionary<string, double>();
        private List<int> storedBits = new List<int>();
        private bool introSeen;
        private bool midpointSeen;
        private bool resumeAtNight;
        private bool deepFirst;
        private float magnetTimer;
        private float autoPopTimer;
        private bool lastCallToastShown;
        private bool tenSecondWarning;

        private void Awake()
        {
            I = this;
        }

        private void OnEnable()
        {
            EventBus.GameplayStarted += OnGameplayStarted;
            EventBus.ReturnedToMenu += OnReturnedToMenu;
            EventBus.BlobPopped += OnPopped;
            EventBus.Sorted += OnSorted;
            EventBus.Crunched += OnCrunched;
            EventBus.DataGained += OnDataGained;
        }

        private void OnDisable()
        {
            EventBus.GameplayStarted -= OnGameplayStarted;
            EventBus.ReturnedToMenu -= OnReturnedToMenu;
            EventBus.BlobPopped -= OnPopped;
            EventBus.Sorted -= OnSorted;
            EventBus.Crunched -= OnCrunched;
            EventBus.DataGained -= OnDataGained;
        }

        // ---------------- modifiers (tomorrow's conditions from mail) ----------------

        public double Modifier(string key, double fallback)
        {
            return modifiers.TryGetValue(key, out double v) ? v : fallback;
        }

        /// <summary>Mail replies change tomorrow: multiplicative keys stack by multiplying, others add.</summary>
        public void AddTomorrow(string key, double amount)
        {
            bool mult = key == "data_mult" || key == "golden_mult" || key == "spawn_mult";
            if (mult)
            {
                double cur = pendingModifiers.TryGetValue(key, out double v) ? v : 1d;
                pendingModifiers[key] = cur * (1d + amount);
            }
            else
            {
                double cur = pendingModifiers.TryGetValue(key, out double v) ? v : 0d;
                pendingModifiers[key] = cur + amount;
            }
            SaveManager.I.RequestSave();
        }

        public Dictionary<string, double> TomorrowModifiers { get { return pendingModifiers; } }

        // ---------------- flow ----------------

        private void OnGameplayStarted()
        {
            if (GameManager.I.State != GameState.Playing) return;
            if (deepFirst)
            {
                // After a Rebroadcast: spend Echoes in the Deep Signal tree, then Day 1.
                deepFirst = false;
                Phase = DayPhase.Night;
                if (EndOfDayScreen.I != null) EndOfDayScreen.I.OpenDeep();
                return;
            }
            if (resumeAtNight)
            {
                resumeAtNight = false;
                Phase = DayPhase.Night;
                if (EndOfDayScreen.I != null) EndOfDayScreen.I.Open(Report, true);
                return;
            }
            BeginDay();
        }

        private void OnReturnedToMenu()
        {
            // Back at the main menu: Continue resumes from the last save point.
            StopAllCoroutines();
            if (Phase == DayPhase.Night) resumeAtNight = true;
            Phase = DayPhase.Idle;
        }

        /// <summary>Title card (and on Day 1 the intro cutscene), then the shift.</summary>
        public void BeginDay()
        {
            StopAllCoroutines();
            StartCoroutine(BeginDayRoutine());
        }

        private IEnumerator BeginDayRoutine()
        {
            GameManager gm = GameManager.I;
            if (gm.State != GameState.Playing) yield break;
            DayStartData = gm.Data;
            SkillTree.I.LockTonight();
            if (Vacuum.I != null) Vacuum.I.DropAll();
            BlobSpawner.I.ClearField();
            Compressor.I.ResetContents();

            if (Day == 1 && !introSeen && CutscenePlayer.I != null && !(TutorialDirector.I != null && TutorialDirector.I.SkipCutscenes))
            {
                introSeen = true;
                Phase = DayPhase.Cutscene;
                bool done = false;
                CutscenePlayer.I.Play("intro", () => done = true);
                while (!done) yield return null;
            }
            introSeen = true;

            // Deep Signal "Head Start": Data at the start of a run.
            if (Day == 1 && gm.Flag("head_start_given") <= 0d)
            {
                gm.SetFlag("head_start_given", 1d);
                double start = SkillTree.I.Stat("start_data");
                if (start > 0d) gm.Refund(start);
            }

            if (GameStats.Antenna >= Balance.B.endingAntennaLevel && string.IsNullOrEmpty(gm.EndingId))
            {
                // Antenna 10 was bought last night: the finale replaces the next shift.
                Phase = DayPhase.Idle;
                gm.BeginFinale();
                yield break;
            }

            if (!midpointSeen && GameStats.Antenna >= Balance.B.days.midpointAntenna && CutscenePlayer.I != null)
            {
                midpointSeen = true;
                Phase = DayPhase.Cutscene;
                bool done = false;
                CutscenePlayer.I.Play("midpoint", () => done = true);
                while (!done) yield return null;
            }

            Lucky = SkillTree.I.Has("lucky_shift") && UnityEngine.Random.value < Balance.B.days.luckyShiftChance;
            ShiftLength = GameStats.ShiftSeconds;
            TimeLeft = ShiftLength;
            DayStartData = gm.Data;
            Report = new DayReport();
            lastCallToastShown = false;
            tenSecondWarning = false;
            SaveManager.I.RequestSave();

            Phase = DayPhase.TitleCard;
            if (DayTitleCard.I != null && !(TutorialDirector.I != null && TutorialDirector.I.SkipTitleCard))
            {
                bool done = false;
                DayTitleCard.I.Show(Day, Subtitle(), BriefingLine(), () => done = true);
                while (!done) yield return null;
            }

            RestoreStoredBits();
            Phase = DayPhase.Shift;
            if (Lucky && HUD.I != null) HUD.I.Toast("LUCKY SHIFT! DOUBLE GOLD STATIC", Palette.Gold);
            EventBus.RaiseDayStarted(Day);
        }

        private string Subtitle()
        {
            int pct = Mathf.RoundToInt((float)GameStats.SignalStrength * 100f);
            return "Signal at " + pct + "%";
        }

        private string BriefingLine()
        {
            DayBriefing b = MailDirector.I != null ? MailDirector.I.BriefingFor(Day) : null;
            return b != null ? b.text : "";
        }

        private void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.IsReady || gm.State != GameState.Playing) return;
            float dt = Time.deltaTime;
            if (Phase == DayPhase.Shift)
            {
                bool frozen = (EventDirector.I != null && EventDirector.I.Running)
                              || (TutorialDirector.I != null && TutorialDirector.I.ClockFrozen);
                if (!frozen) TimeLeft -= dt;
                if (!tenSecondWarning && TimeLeft <= 10f && TimeLeft > 0f)
                {
                    tenSecondWarning = true;
                    AudioManager.Play("alarm", 1.4f, 0.35f);
                }
                TickAutomation(dt);
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    StartCoroutine(LastCallRoutine());
                }
            }
            else if (Phase == DayPhase.LastCall)
            {
                TickAutomation(dt);
            }
        }

        private IEnumerator LastCallRoutine()
        {
            Phase = DayPhase.LastCall;
            if (EventDirector.I != null) EventDirector.I.CancelWarning();
            if (!lastCallToastShown && HUD.I != null) HUD.I.Toast("LAST CALL! CRUNCH WHAT YOU HAVE", Palette.Orange);
            lastCallToastShown = true;
            AudioManager.Play("alarm", 1f, 0.6f);
            float t = Balance.B.days.lastCallSeconds;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                yield return null;
            }
            // Clock out: drop the tank, crunch whatever is inside, count what was left behind.
            if (Vacuum.I != null && Vacuum.I.Active) Vacuum.I.End(Vacuum.I.Cursor);
            yield return new WaitForSeconds(0.6f);
            Compressor.I.FlushCrunch();
            float wait = 0f;
            while ((Compressor.I.Crunching || Compressor.I.Count > 0 || AnyBitsInFlight()) && wait < 4f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            EnterNight();
        }

        /// <summary>The shift is over: tally what is left, clear the field, and open the end-of-day screen.</summary>
        private void EnterNight()
        {
            StoreOrLoseBits();
            BlobSpawner.I.ClearField();
            // Today's timed buffs end with the shift; buffs from tonight's mail run tomorrow.
            if (BuffSystem.I != null) BuffSystem.I.ClearAll();
            Phase = DayPhase.Night;
            if (Report.bestCombo < ComboTracker.I.BestToday) Report.bestCombo = ComboTracker.I.BestToday;
            EventBus.RaiseDayEnded(Day);
            SaveManager.I.SaveNow();
            if (EndOfDayScreen.I != null) EndOfDayScreen.I.Open(Report, false);
        }

        // ---------------- debug menu hooks ----------------

        /// <summary>Debug: ends whatever the current day is doing and jumps to the end-of-day screen.</summary>
        public bool DebugEndDay()
        {
            if (GameManager.I == null || GameManager.I.State != GameState.Playing) return false;
            if (Phase == DayPhase.Night) return true;
            StopAllCoroutines();
            if (CutscenePlayer.I != null && CutscenePlayer.I.IsPlaying) CutscenePlayer.I.Abort();
            if (DayTitleCard.I != null) DayTitleCard.I.Dismiss();
            if (Vacuum.I != null) Vacuum.I.DropAll();
            if (EventDirector.I != null) EventDirector.I.DebugCancel();
            if (Compressor.I != null) Compressor.I.ResetContents();
            EnterNight();
            return true;
        }

        /// <summary>Debug: jump straight to the skill tree screen from anywhere in play.</summary>
        public bool DebugOpenTree()
        {
            if (!DebugEndDay()) return false;
            if (EndOfDayScreen.I != null) EndOfDayScreen.I.GoTo(EndOfDayScreen.Step.Tree);
            return true;
        }

        private static bool AnyBitsInFlight()
        {
            foreach (Blob b in BlobSpawner.I.Active) if (b.IsBit && b.State == BlobState.Flying) return true;
            return false;
        }

        /// <summary>Night Storage keeps loose bits for tomorrow; otherwise they are lost.</summary>
        private void StoreOrLoseBits()
        {
            storedBits.Clear();
            for (int i = 0; i < 5; i++) storedBits.Add(0);
            int lost = 0;
            bool keep = GameStats.NightStorage;
            foreach (Blob b in BlobSpawner.I.Active)
            {
                if (!b.IsBit || b.State == BlobState.Compressed) continue;
                if (keep) storedBits[Mathf.Clamp((int)b.Color, 0, 4)]++;
                else lost++;
            }
            Report.bitsLost = lost;
        }

        private void RestoreStoredBits()
        {
            if (storedBits == null || storedBits.Count == 0) return;
            Vector3 c = BlobSpawner.I.ViewCenter;
            for (int i = 0; i < storedBits.Count; i++)
            {
                if (storedBits[i] <= 0) continue;
                BlobColor color = (BlobColor)i;
                BlobTypeDef t = BlobSpawner.I.TypeForColor(color == BlobColor.Gold ? BlobColor.Red : color);
                double each = (t != null ? t.value / Mathf.Max(1, t.bits) : 1d) * Balance.B.dataValueScale;
                BlobSpawner.I.SpawnBits(c + UnityEngine.Random.insideUnitSphere * 1.5f, color, storedBits[i], each * storedBits[i], color == BlobColor.Gold);
            }
            storedBits.Clear();
        }

        /// <summary>End-of-day screen: "Start Day N+1".</summary>
        public void StartNextDay()
        {
            if (Phase != DayPhase.Night) return;
            Day++;
            modifiers = pendingModifiers;
            pendingModifiers = new Dictionary<string, double>();
            SkillTree.I.LockTonight();
            Phase = DayPhase.Idle;
            SaveManager.I.RequestSave();
            BeginDay();
        }

        /// <summary>After a Rebroadcast the Deep Signal screen ends with "Start Day 1".</summary>
        public void StartFirstDay()
        {
            Phase = DayPhase.Idle;
            BeginDay();
        }

        // ---------------- automation (Auto-Pop Tower, Magnet Pulse) ----------------

        private void TickAutomation(float dt)
        {
            int towers = GameStats.AutoPop;
            if (towers > 0 && Phase == DayPhase.Shift)
            {
                autoPopTimer += dt;
                float interval = Balance.B.days.autoPopInterval / towers;
                if (autoPopTimer >= interval)
                {
                    autoPopTimer = 0f;
                    Blob target = BlobSpawner.I.FindNearest(BlobSpawner.I.AbsorbPoint, 60f, x => x.IsPoppable && !x.EventBlob);
                    if (target != null)
                    {
                        Vector3 from = BlobSpawner.I.AbsorbPoint + Vector3.up * 2.5f;
                        VFXPlayer.Beam(from, target.Position, Palette.Purple);
                        BlobActions.Pop(target, PopCause.Drone, 1d, 0);
                    }
                }
            }
            if (GameStats.MagnetPulse)
            {
                magnetTimer += dt;
                if (magnetTimer >= Balance.B.days.magnetPulseInterval)
                {
                    magnetTimer = 0f;
                    bool any = false;
                    foreach (Blob b in BlobSpawner.I.Active.ToArray())
                    {
                        if (!b.IsSweepable) continue;
                        Chute c = Chute.ForBit(b);
                        if (c == null) continue;
                        Vector3 to = c.transform.position - b.Position;
                        to.y = 0f;
                        float step = Mathf.Min(to.magnitude - 0.2f, Balance.B.days.magnetPulseDistance);
                        if (step <= 0f) continue;
                        Vector3 dest = b.Position + to.normalized * step;
                        if (to.magnitude <= Balance.B.days.magnetPulseDistance + 0.3f) b.FlyTo(c.Mouth, 0.35f, 0.8f, x => BlobActions.Sort(x, c, false));
                        else b.FlyTo(dest, 0.3f, 0.5f, x => x.ReturnToDrift());
                        any = true;
                    }
                    if (any)
                    {
                        AudioManager.Play("whoosh", 1.3f, 0.5f);
                        VFXPlayer.Shockwave(BlobSpawner.I.ViewCenter, 4f, Palette.Pink);
                    }
                }
            }
        }

        // ---------------- report ----------------

        private void OnPopped(Blob b, double payout)
        {
            if (InShiftOrLastCall && !b.IsBit) Report.staticPopped++;
        }

        private void OnSorted(bool correct)
        {
            if (!InShiftOrLastCall) return;
            if (correct) Report.bitsSorted++;
            else Report.wrongSorts++;
        }

        private void OnCrunched(double payout)
        {
            if (InShiftOrLastCall) Report.crunches++;
        }

        private void OnDataGained(double amount)
        {
            if (InShiftOrLastCall) Report.dataEarned += amount;
        }

        // ---------------- save ----------------

        public void ReadSave(SaveData s)
        {
            DaySave d = s.day ?? new DaySave();
            Day = Mathf.Max(1, d.day);
            // Saved modifiers belong to the next day to be played: tomorrow's (night save) or today's (shift save).
            Dictionary<string, double> saved = d.modifiers != null ? new Dictionary<string, double>(d.modifiers) : new Dictionary<string, double>();
            bool night = d.phase == "night";
            modifiers = night ? new Dictionary<string, double>() : saved;
            pendingModifiers = night ? saved : new Dictionary<string, double>();
            storedBits = d.storedBits != null ? new List<int>(d.storedBits) : new List<int>();
            introSeen = d.introSeen;
            midpointSeen = d.midpointSeen;
            Report = d.lastReport ?? new DayReport();
            resumeAtNight = d.phase == "night";
            deepFirst = d.phase == "deep";
            DayStartData = d.dayStartData;
            Phase = DayPhase.Idle;
        }

        public void WriteSave(SaveData s)
        {
            DaySave d = new DaySave();
            d.day = Day;
            bool night = Phase == DayPhase.Night || (Phase == DayPhase.Idle && resumeAtNight);
            d.phase = deepFirst ? "deep" : (night ? "night" : "shift");
            d.dayStartData = DayStartData;
            // Tomorrow's deals were made at night; during a shift the active ones are today's and are lost on quit.
            d.modifiers = new Dictionary<string, double>(night ? pendingModifiers : modifiers);
            d.storedBits = new List<int>(storedBits);
            d.introSeen = introSeen;
            d.midpointSeen = midpointSeen;
            d.lastReport = Report;
            s.day = d;
            // Mid-shift saves rewind to the start of the day.
            if (!night && Phase != DayPhase.Idle) s.data = DayStartData;
        }
    }

    /// <summary>One line shown on a day's title card (from mail.json "briefings").</summary>
    public class DayBriefing
    {
        public int day;
        public string from;
        public string text;
    }
}
