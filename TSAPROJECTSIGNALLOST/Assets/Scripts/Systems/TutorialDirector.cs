using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Data-driven tutorial (tutorial.json). Day 1's shift teaches the one line: Pop, Sweep, Sort, Crunch
    /// (the shift clock waits while Hale explains). The first end-of-day screen teaches the report, mail
    /// and the skill tree. Each step waits for the player to DO the thing, then celebrates. One-time
    /// contextual tips cover later firsts (blue Static, wrong chute, full tank, last call, first event...).
    /// Progress is saved; every step keeps its Static and bits alive and times out, so nothing soft-locks.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector I { get; private set; }

        /// <summary>Verification bot / debug only: skip the intro cutscene and title cards.</summary>
        public static bool DebugSkipCutscenes;

        private TutorialSave data = new TutorialSave();
        private bool guidedActive;
        private string guidedPhase;
        private int stepIndex = -1;
        private int progress;
        private float stepClock;
        private float idleClock;
        private float keepAliveTimer;
        private bool stepDone;
        private float stepDoneTimer;
        private Vector3 lastTargetWorld;
        private readonly HashSet<string> disabled = new HashSet<string>();
        private readonly List<KeyValuePair<Blob, int>> tutorialBlobs = new List<KeyValuePair<Blob, int>>();
        private TutorialTipDef activeTip;
        private float tipClock;
        private readonly Queue<TutorialTipDef> tipQueue = new Queue<TutorialTipDef>();
        private readonly List<string> deferredTips = new List<string>();
        private bool eventTipActive;
        private Blob goldenTarget;
        private int nudgeIndex;

        public bool GuidedActive { get { return guidedActive; } }
        public string CurrentStepId { get { return guidedActive && Step != null ? Step.id : null; } }
        public string ActiveTipId { get { return activeTip != null ? activeTip.id : null; } }
        public bool EventsBlocked { get { return guidedActive && disabled.Contains("events"); } }
        public bool MailBlocked { get { return guidedActive && disabled.Contains("mail"); } }
        public bool SpawnsBlocked { get { return guidedActive && disabled.Contains("spawns"); } }
        /// <summary>The Day 1 shift clock waits while Hale walks you through the line.</summary>
        public bool ClockFrozen { get { return guidedActive && guidedPhase == "shift"; } }
        public bool SkipCutscenes { get { return DebugSkipCutscenes; } }
        public bool SkipTitleCard { get { return DebugSkipCutscenes; } }

        private static TutorialFile File { get { return Balance.I != null ? Balance.I.Tutorial : null; } }
        private TutorialStepDef Step { get { return File != null && stepIndex >= 0 && stepIndex < File.steps.Count ? File.steps[stepIndex] : null; } }

        private void Awake()
        {
            I = this;
        }

        private void OnEnable()
        {
            EventBus.DayStarted += OnDayStarted;
            EventBus.NightStep += OnNightStep;
            EventBus.BlobPopped += OnBlobPopped;
            EventBus.UpgradeBought += OnUpgradeBought;
            EventBus.Crunched += OnCrunched;
            EventBus.Sorted += OnSorted;
            EventBus.MailReceived += OnMailReceived;
            EventBus.PanelOpened += OnPanelOpened;
            EventBus.GoldenSpawned += OnGoldenSpawned;
            EventBus.GoldenPopped += OnGoldenPopped;
            EventBus.EventEnded += OnEventEnded;
            EventBus.DayEnded += OnDayEnded;
        }

        private void OnDisable()
        {
            EventBus.DayStarted -= OnDayStarted;
            EventBus.NightStep -= OnNightStep;
            EventBus.BlobPopped -= OnBlobPopped;
            EventBus.UpgradeBought -= OnUpgradeBought;
            EventBus.Crunched -= OnCrunched;
            EventBus.Sorted -= OnSorted;
            EventBus.MailReceived -= OnMailReceived;
            EventBus.PanelOpened -= OnPanelOpened;
            EventBus.GoldenSpawned -= OnGoldenSpawned;
            EventBus.GoldenPopped -= OnGoldenPopped;
            EventBus.EventEnded -= OnEventEnded;
            EventBus.DayEnded -= OnDayEnded;
        }

        // ---------------- guided steps ----------------

        private bool PhaseDone(string phase)
        {
            if (File == null) return true;
            foreach (TutorialStepDef s in File.steps) if (s.phase == phase && !data.completed.Contains(s.id)) return false;
            return true;
        }

        private void OnDayStarted(int day)
        {
            if (guidedActive && Step != null && Step.waitFor == "day_started")
            {
                // "Start Day 2" ends the night lesson right away.
                if (!data.completed.Contains(Step.id)) data.completed.Add(Step.id);
                stepDone = true;
                EndPhase();
                return;
            }
            if (data.guidedDone || guidedActive) return;
            if (!PhaseDone("shift")) StartGuided("shift");
        }

        private void OnNightStep(string step)
        {
            if (guidedActive && Step != null && Step.waitFor == "night_step" && Step.waitParam == step) AddProgress(1);
            if (step == "report" && !data.guidedDone && !guidedActive && PhaseDone("shift") && !PhaseDone("night")) StartGuided("night");
        }

        private void StartGuided(string phase)
        {
            if (File == null || File.steps.Count == 0)
            {
                FinishGuided(false);
                return;
            }
            guidedActive = true;
            guidedPhase = phase;
            if (phase == "shift" && BlobSpawner.I != null) BlobSpawner.I.ClearField();
            stepIndex = -1;
            Next();
        }

        private void Next()
        {
            stepIndex++;
            while (Step != null && (data.completed.Contains(Step.id) || Step.phase != guidedPhase)) stepIndex++;
            if (Step == null)
            {
                EndPhase();
                return;
            }
            BeginStep();
        }

        private void BeginStep()
        {
            TutorialStepDef s = Step;
            progress = 0;
            stepClock = 0f;
            idleClock = 0f;
            keepAliveTimer = 0f;
            stepDone = false;
            disabled.Clear();
            if (s.setup != null && s.setup.disable != null) foreach (string d in s.setup.disable) disabled.Add(d);
            if (s.setup != null)
            {
                if (s.setup.closePanels && MailUI.I != null && guidedPhase == "shift") MailUI.I.Close();
                if (!string.IsNullOrEmpty(s.setup.ensureDataFor)) EnsureData(s.setup.ensureDataFor);
                if (Compressor.I != null && guidedPhase == "shift") Compressor.I.CapacityOverride = s.setup.compressorCapacity;
                tutorialBlobs.Clear();
                if (s.setup.spawn != null) SpawnSet(s.setup.spawn, s.setup.spawn.count);
            }
            if (TutorialUI.I != null)
            {
                TutorialUI.I.Say(s.speaker, TextFor(s));
                TutorialUI.I.ShowSkip(true);
                bool counted = s.waitFor == "blob_popped" || s.waitFor == "sorted";
                TutorialUI.I.SetProgress(0, counted && s.count > 1 ? s.count : 0);
            }
        }

        private static string TextFor(TutorialStepDef s)
        {
            return GameSettings.VacuumToggle && !string.IsNullOrEmpty(s.textToggle) ? s.textToggle : s.text;
        }

        /// <summary>Makes sure the first tree purchase is affordable even after a rough first day.</summary>
        private void EnsureData(string nodeId)
        {
            UpgradeDef u = Balance.I.Upgrade(nodeId);
            if (u == null || SkillTree.I.IsMaxed(u)) return;
            double missing = SkillTree.I.NextCost(u) - GameManager.I.Data;
            if (missing > 0d) GameManager.I.Refund(missing + 1d);
        }

        private Vector3 PlanePoint(float vx, float vy)
        {
            Camera cam = GameManager.I.Cam;
            Ray r = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
            return GameManager.I.PlayPlane.Raycast(r, out float d) ? r.GetPoint(d) : BlobSpawner.I.ViewCenter;
        }

        private void SpawnSet(TutorialSpawn sp, int count)
        {
            if (count <= 0) return;
            BlobColor color = Palette.ParseColor(sp.color);
            Vector3 center = sp.where == "cluster" ? PlanePoint(0.42f, 0.4f) : PlanePoint(0.5f, 0.42f);
            if (sp.bits)
            {
                BlobTypeDef t = BlobSpawner.I.TypeForColor(color);
                double each = (t != null ? t.value / Mathf.Max(1, t.bits) : 1d) * Balance.B.dataValueScale;
                BlobSpawner.I.SpawnBits(center, color, count, each * count, false);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 p;
                if (sp.where == "spread") p = BlobSpawner.I.RandomInteriorPoint();
                else if (sp.where == "cluster") p = center + new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.5f, 0.5f));
                else p = center + (count > 1 ? new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-0.5f, 0.5f)) : Vector3.zero);
                Blob b = BlobSpawner.I.SpawnTutorial(color, p, sp.scale, sp.speed);
                if (b != null) tutorialBlobs.Add(new KeyValuePair<Blob, int>(b, b.Serial));
            }
        }

        private int AliveTutorialBlobs(out Vector3 centroid)
        {
            int n = 0;
            centroid = Vector3.zero;
            for (int i = tutorialBlobs.Count - 1; i >= 0; i--)
            {
                Blob b = tutorialBlobs[i].Key;
                bool alive = b != null && b.gameObject.activeInHierarchy && b.Serial == tutorialBlobs[i].Value && b.IsPoppable;
                if (!alive)
                {
                    if (b == null || b.Serial != tutorialBlobs[i].Value || !b.gameObject.activeInHierarchy) tutorialBlobs.RemoveAt(i);
                    continue;
                }
                n++;
                centroid += b.Position;
            }
            if (n > 0) centroid /= n;
            return n;
        }

        /// <summary>Bits still in play: loose on the floor, held, or flying to a chute or the Compressor.</summary>
        private static int BitsInPlay(out Vector3 centroid)
        {
            int n = 0, floor = 0;
            centroid = Vector3.zero;
            foreach (Blob b in BlobSpawner.I.Active)
            {
                if (!b.IsBit || b.State == BlobState.Compressed) continue;
                n++;
                if (b.IsSweepable)
                {
                    floor++;
                    centroid += b.Position;
                }
            }
            if (floor > 0) centroid /= floor;
            return n;
        }

        private void KeepAlive()
        {
            TutorialStepDef s = Step;
            if (s == null || s.setup == null || !s.setup.keepAlive || guidedPhase != "shift") return;
            int alive = AliveTutorialBlobs(out _);
            int bits = BitsInPlay(out _);
            int held = Vacuum.I != null ? Vacuum.I.Held.Count : 0;
            switch (s.waitFor)
            {
                case "blob_popped":
                    if (s.setup.spawn != null && alive < s.count - progress) SpawnSet(s.setup.spawn, s.count - progress - alive);
                    break;
                case "vacuum_held":
                    if (bits < s.count) SpawnSet(new TutorialSpawn { color = "red", count = s.count - bits, where = "cluster", bits = true }, s.count - bits);
                    break;
                case "crunch":
                    if (Compressor.I != null && !Compressor.I.Crunching && bits + held + Compressor.I.Count < Compressor.I.Capacity)
                    {
                        int need = Compressor.I.Capacity - Compressor.I.Count - bits;
                        SpawnSet(new TutorialSpawn { color = "red", count = need, where = "cluster", bits = true }, need);
                    }
                    break;
            }
        }

        private void AddProgress(int amount)
        {
            if (!guidedActive || stepDone || Step == null) return;
            progress += amount;
            idleClock = 0f;
            if (TutorialUI.I != null && (Step.waitFor == "blob_popped" || Step.waitFor == "sorted") && Step.count > 1) TutorialUI.I.SetProgress(Mathf.Min(progress, Step.count), Step.count);
            if (progress >= Step.count) CompleteStep();
        }

        private void CompleteStep()
        {
            TutorialStepDef s = Step;
            if (s == null || stepDone) return;
            stepDone = true;
            TutorialConfig cfg = Balance.B.tutorial;
            if (s.reward != null && s.reward.data > 0d)
            {
                int coins = s.reward.coins > 0 ? s.reward.coins : cfg.celebrateCoinsSmall;
                GameManager.I.Earn(s.reward.data, lastTargetWorld == Vector3.zero ? BlobSpawner.I.ViewCenter : lastTargetWorld, coins);
            }
            if (TutorialUI.I != null)
            {
                if (guidedPhase == "shift") TutorialUI.I.Celebrate(s.celebrate, s.stamp);
                TutorialUI.I.SetTarget(false, Vector2.zero, 0f, false);
                if (!string.IsNullOrEmpty(s.praise)) TutorialUI.I.Say(s.speaker, s.praise);
            }
            if (s.setup != null && s.setup.compressorCapacity > 0 && Compressor.I != null) Compressor.I.CapacityOverride = 0;
            if (!data.completed.Contains(s.id)) data.completed.Add(s.id);
            if (SaveManager.I != null) SaveManager.I.RequestSave();
            // Time to read the praise line (it often carries the lore): about 14 characters a second.
            stepDoneTimer = string.IsNullOrEmpty(s.praise) ? 0.4f : Mathf.Clamp(s.praise.Length / 14f + 1.2f, cfg.praiseSeconds, 8f);
        }

        /// <summary>The shift part (or the night part) of the tutorial is done.</summary>
        private void EndPhase()
        {
            string phase = guidedPhase;
            guidedActive = false;
            guidedPhase = null;
            disabled.Clear();
            if (Compressor.I != null) Compressor.I.CapacityOverride = 0;
            foreach (KeyValuePair<Blob, int> kv in tutorialBlobs) if (kv.Key != null && kv.Key.Serial == kv.Value) kv.Key.SpeedMult = 1f;
            tutorialBlobs.Clear();
            if (TutorialUI.I != null)
            {
                TutorialUI.I.ShowSkip(false);
                TutorialUI.I.SetTarget(false, Vector2.zero, 0f, false);
            }
            if (phase == "shift")
            {
                if (BlobSpawner.I != null) BlobSpawner.I.SpawnOpeningBurst(Balance.B.tutorial.handoffBurst);
                if (EventDirector.I != null) EventDirector.I.ResetTimer();
            }
            if (PhaseDone("shift") && PhaseDone("night")) data.guidedDone = true;
            Juice.Run(HideCommsLater(2.5f));
            foreach (string id in deferredTips) TryQueueTip(id);
            deferredTips.Clear();
            if (SaveManager.I != null) SaveManager.I.RequestSave();
        }

        private void FinishGuided(bool skipped)
        {
            data.guidedDone = true;
            data.skipped = skipped;
            if (File != null) foreach (TutorialStepDef s in File.steps) if (!data.completed.Contains(s.id)) data.completed.Add(s.id);
            if (guidedActive) EndPhase();
            if (skipped && TutorialUI.I != null) TutorialUI.I.HideComms();
        }

        private System.Collections.IEnumerator HideCommsLater(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!guidedActive && activeTip == null && !eventTipActive && TutorialUI.I != null) TutorialUI.I.HideComms();
        }

        /// <summary>Skip button (after confirm). Restores normal play immediately.</summary>
        public void Skip()
        {
            FinishGuided(true);
        }

        /// <summary>Settings > Replay Tutorial: resets only tutorial flags.</summary>
        public void Replay()
        {
            data.completed.Clear();
            data.tips.Clear();
            data.guidedDone = false;
            data.skipped = false;
            tipQueue.Clear();
            deferredTips.Clear();
            EndTip();
            if (GameManager.I != null && GameManager.I.State == GameState.Playing && !guidedActive && DayManager.I != null && DayManager.I.InShift) StartGuided("shift");
            if (SaveManager.I != null) SaveManager.I.RequestSave();
        }

        // ---------------- tips ----------------

        private TutorialTipDef FindTip(string id)
        {
            if (File == null) return null;
            foreach (TutorialTipDef t in File.tips) if (t.id == id) return t;
            return null;
        }

        private void TryQueueTip(string id)
        {
            if (!GameSettings.TutorialHints || data.tips.Contains(id)) return;
            if (guidedActive)
            {
                if (!deferredTips.Contains(id)) deferredTips.Add(id);
                return;
            }
            TutorialTipDef t = FindTip(id);
            if (t == null || (activeTip != null && activeTip.id == id)) return;
            foreach (TutorialTipDef q in tipQueue) if (q.id == id) return;
            tipQueue.Enqueue(t);
        }

        private void TryTrigger(string trigger, string param)
        {
            if (File == null) return;
            foreach (TutorialTipDef t in File.tips)
            {
                if (t.trigger != trigger) continue;
                if (!string.IsNullOrEmpty(t.param) && t.param != param) continue;
                TryQueueTip(t.id);
            }
        }

        private void ShowTip(TutorialTipDef t)
        {
            activeTip = t;
            tipClock = 0f;
            if (!data.tips.Contains(t.id)) data.tips.Add(t.id);
            if (SaveManager.I != null) SaveManager.I.RequestSave();
            if (TutorialUI.I != null) TutorialUI.I.Say("hale", t.text);
            if (t.slowmo > 0f) CoroutineHost.I.SlowMo(t.slowmo, t.slowmoScale);
            if (t.glowChutes) Chute.TutorialGlow = true;
        }

        private void EndTip()
        {
            if (activeTip == null) return;
            activeTip = null;
            Chute.TutorialGlow = false;
            goldenTarget = null;
            if (TutorialUI.I != null)
            {
                TutorialUI.I.SetTarget(false, Vector2.zero, 0f, false);
                if (!guidedActive && !eventTipActive) TutorialUI.I.HideComms();
            }
        }

        private void EndTipIf(string until, string param)
        {
            if (activeTip == null || activeTip.until != until) return;
            if (!string.IsNullOrEmpty(activeTip.untilParam) && activeTip.untilParam != param) return;
            EndTip();
        }

        /// <summary>First time an event appears: Hale explains it; the event runs at 2x time and cannot fail.</summary>
        public bool BeginEventTip(EventDef def)
        {
            if (def == null || !GameSettings.TutorialHints || guidedActive) return false;
            string id = "event_" + def.id;
            if (data.tips.Contains(id)) return false;
            data.tips.Add(id);
            EndTip();
            eventTipActive = true;
            TutorialTipDef t = FindTip(id);
            if (TutorialUI.I != null) TutorialUI.I.Say("hale", t != null ? t.text : def.instructions);
            if (SaveManager.I != null) SaveManager.I.RequestSave();
            return true;
        }

        // ---------------- event hooks ----------------

        private void OnBlobPopped(Blob b, double payout)
        {
            if (b == null || b.IsBit) return;
            if (guidedActive && Step != null && Step.waitFor == "blob_popped") AddProgress(1);
        }

        private void OnUpgradeBought(string id, int level)
        {
            if (guidedActive && Step != null && Step.waitFor == "upgrade_bought" && (string.IsNullOrEmpty(Step.upgrade) || Step.upgrade == id) && id != "undo") AddProgress(1);
            EndTipIf("upgrade_bought", id);
            if (id == "antenna") TryTrigger("antenna_reached", level.ToString());
            TryTrigger("node_bought", id);
        }

        private void OnCrunched(double payout)
        {
            if (guidedActive && Step != null && Step.waitFor == "crunch") AddProgress(1);
        }

        private void OnSorted(bool correct)
        {
            if (!correct)
            {
                TryTrigger("wrong_sort", null);
                return;
            }
            if (guidedActive && Step != null && Step.waitFor == "sorted") AddProgress(1);
            EndTipIf("sorted", null);
        }

        private void OnMailReceived(MailEntry e)
        {
            if (e != null && !e.IsFollowup && e.Def != null && e.Def.replies != null)
            {
                foreach (MailReply r in e.Def.replies) if (r.NeedsOpenChannel && GameStats.OpenChannel) TryTrigger("open_channel_mail", null);
            }
        }

        private void OnPanelOpened(string id)
        {
            EndTipIf("panel_open", id);
        }

        private void OnGoldenSpawned(Blob b)
        {
            if (!GameSettings.TutorialHints || data.tips.Contains("first_golden") || guidedActive) return;
            goldenTarget = b;
            TryTrigger("golden_spawn", null);
        }

        private void OnGoldenPopped()
        {
            EndTipIf("golden_popped", null);
        }

        private void OnDayEnded(int day)
        {
            EndTip();
            if (DayManager.I != null && DayManager.I.Report.bitsLost > 0) TryTrigger("bits_lost", null);
        }

        private void OnEventEnded(EventDef d, bool ok)
        {
            if (!eventTipActive) return;
            eventTipActive = false;
            if (!guidedActive && activeTip == null && TutorialUI.I != null) TutorialUI.I.HideComms();
        }

        // ---------------- targets ----------------

        private static bool RectTarget(RectTransform rt, out Vector2 screen, out float radius)
        {
            screen = Vector2.zero;
            radius = 0f;
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            Vector3 center = rt.TransformPoint(rt.rect.center);
            screen = RectTransformUtility.WorldToScreenPoint(null, center);
            Vector2 size = Vector2.Scale(rt.rect.size, rt.lossyScale);
            radius = Mathf.Max(size.x, size.y) * 0.5f + 24f;
            return true;
        }

        private bool ResolveTarget(string target, float radiusHint, out Vector2 screen, out float radius)
        {
            screen = Vector2.zero;
            radius = radiusHint > 0f ? radiusHint : (Balance.B != null ? Balance.B.tutorial.spotlightRadius : 110f);
            if (string.IsNullOrEmpty(target) || target == "none") return false;
            Camera cam = GameManager.I.Cam;
            Vector3 world;
            EndOfDayScreen eod = EndOfDayScreen.I;
            if (target == "blob" || target == "cluster")
            {
                Vector3 c;
                int n = AliveTutorialBlobs(out c);
                if (n == 0) return false;
                if (target == "blob")
                {
                    foreach (KeyValuePair<Blob, int> kv in tutorialBlobs) if (kv.Key.IsPoppable) { c = kv.Key.Position; break; }
                }
                world = c;
            }
            else if (target == "bits")
            {
                Vector3 c;
                if (BitsInPlay(out c) == 0 || c == Vector3.zero) return false;
                world = c;
            }
            else if (target == "compressor")
            {
                if (Compressor.I == null) return false;
                world = Compressor.I.transform.position + Vector3.up * 1.3f;
            }
            else if (target.StartsWith("chute:"))
            {
                Chute ch = Chute.ForColor(Palette.ParseColor(target.Substring(6)));
                if (ch == null) return false;
                world = ch.Mouth;
            }
            else if (target == "chutes")
            {
                Chute mid = Chute.ForColor(BlobColor.Blue);
                if (mid == null) return false;
                world = mid.Mouth;
                Chute red = Chute.ForColor(BlobColor.Red);
                if (red != null && red != mid)
                {
                    Vector3 a = cam.WorldToScreenPoint(red.Mouth), b = cam.WorldToScreenPoint(mid.Mouth);
                    radius = Vector2.Distance(a, b) * 1.5f;
                }
            }
            else if (target == "golden")
            {
                if (goldenTarget == null || !goldenTarget.IsInteractive) return false;
                world = goldenTarget.Position;
            }
            else if (target.StartsWith("node:"))
            {
                if (eod == null || !eod.IsOpen || eod.Tree == null) return false;
                return RectTarget(eod.Tree.NodeRect(target.Substring(5)), out screen, out radius);
            }
            else if (target == "continue_button")
            {
                return eod != null && eod.IsOpen && RectTarget(eod.ContinueButtonRect, out screen, out radius);
            }
            else if (target == "night_mail")
            {
                if (eod == null || !eod.IsOpen || (MailUI.I != null && MailUI.I.IsOpen)) return false;
                bool waiting = MailDirector.I != null && MailDirector.I.AnyNeedsReply;
                return RectTarget(waiting ? eod.OpenMailRect : eod.ContinueButtonRect, out screen, out radius);
            }
            else if (target == "start_button")
            {
                return eod != null && eod.IsOpen && RectTarget(eod.StartButtonRect, out screen, out radius);
            }
            else if (target == "clock")
            {
                return HUD.I != null && RectTarget(HUD.I.ClockRect, out screen, out radius);
            }
            else
            {
                return false;
            }
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return false;
            lastTargetWorld = world;
            screen = sp;
            float scale = Screen.height / 1080f;
            radius *= Mathf.Max(0.5f, scale);
            return true;
        }

        // ---------------- per frame ----------------

        private void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.IsReady || Balance.B == null) return;
            if (gm.State != GameState.Playing || gm.IsPaused) return;
            if (CutscenePlayer.I != null && CutscenePlayer.I.IsPlaying) return;
            if (DayTitleCard.I != null && DayTitleCard.I.IsShowing) return;
            float dt = Time.unscaledDeltaTime;
            TutorialConfig cfg = Balance.B.tutorial;
            TutorialUI ui = TutorialUI.I;

            if (guidedActive)
            {
                TutorialStepDef s = Step;
                if (s == null)
                {
                    EndPhase();
                    return;
                }
                if (stepDone)
                {
                    stepDoneTimer -= dt;
                    if (stepDoneTimer <= 0f) Next();
                    return;
                }
                stepClock += dt;
                idleClock += dt;
                if (s.waitFor == "vacuum_held" && Vacuum.I != null && Vacuum.I.Held.Count >= s.count) AddProgress(s.count);
                if (s.waitFor == "seconds" && stepClock >= s.count) CompleteStep();
                if (s.waitFor == "night_step" && EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen && EndOfDayScreen.I.Current.ToString().ToLowerInvariant() == s.waitParam) AddProgress(1);
                keepAliveTimer -= dt;
                if (keepAliveTimer <= 0f)
                {
                    keepAliveTimer = cfg.keepAliveInterval;
                    if (!stepDone) KeepAlive();
                    if (s.setup != null && !string.IsNullOrEmpty(s.setup.ensureDataFor)) EnsureData(s.setup.ensureDataFor);
                }
                if (ui != null)
                {
                    bool has = ResolveTarget(s.target, s.radius, out Vector2 screen, out float radius);
                    ui.SetTarget(has, screen, radius, s.spotlight);
                    if (idleClock > cfg.idleNudgeSeconds)
                    {
                        idleClock = 0f;
                        ui.Nudge();
                        string nudge = !string.IsNullOrEmpty(s.nudge) ? s.nudge
                            : (File.nudges.Count > 0 ? File.nudges[nudgeIndex++ % File.nudges.Count] : "Today, Technician.");
                        ui.SayTemporary(nudge, 2.2f);
                    }
                }
                float timeout = s.timeout > 0f ? s.timeout : cfg.stepTimeoutSeconds;
                if (stepClock > timeout) CompleteStep();
                return;
            }

            // Contextual tips (one at a time, never blocking).
            if (GameSettings.TutorialHints && activeTip == null && !eventTipActive)
            {
                if (Vacuum.I != null && Vacuum.I.Active && Vacuum.I.Held.Count >= GameStats.VacuumCapacity) TryTrigger("tank_full", null);
                if (DayManager.I != null && DayManager.I.Phase == DayPhase.LastCall) TryTrigger("last_call", null);
                if (tipQueue.Count > 0) ShowNextAllowedTip();
            }
            if (activeTip != null)
            {
                // The clock waits while a tip is held back by another popup, so it is not missed.
                if (TipAllowedNow(activeTip)) tipClock += dt;
                float duration = activeTip.duration > 0f ? activeTip.duration : cfg.tipSeconds;
                duration = Mathf.Max(duration, activeTip.text.Length / 13f + 2.5f);
                if (ui != null)
                {
                    bool has = ResolveTarget(activeTip.target, 0f, out Vector2 screen, out float radius) && TipAllowedNow(activeTip);
                    ui.SetTarget(has, screen, radius, activeTip.spotlight);
                }
                if (tipClock > duration) EndTip();
            }
        }

        /// <summary>A tip waits while an unrelated popup or menu is up, so Hale never talks over something else.</summary>
        private static bool TipAllowedNow(TutorialTipDef t)
        {
            if (GameManager.I.IsPaused) return false;
            if ((PauseMenu.I != null && PauseMenu.I.IsOpen) || (SettingsScreen.I != null && SettingsScreen.I.IsOpen)) return false;
            if (ConfirmDialog.I != null && ConfirmDialog.I.IsOpen) return false;
            bool night = EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen;
            bool nightTip = t.id.StartsWith("night_") || t.trigger == "bits_lost" || t.trigger == "open_channel_mail";
            if (night != nightTip) return false;
            if (MailUI.I != null && MailUI.I.IsOpen) return t.trigger == "open_channel_mail";
            if (EventDirector.I != null && EventDirector.I.Running) return t.id == "first_golden";
            return true;
        }

        private void ShowNextAllowedTip()
        {
            TutorialTipDef next = null;
            foreach (TutorialTipDef q in tipQueue)
            {
                if (!TipAllowedNow(q)) continue;
                next = q;
                break;
            }
            if (next == null) return;
            int n = tipQueue.Count;
            for (int i = 0; i < n; i++)
            {
                TutorialTipDef q = tipQueue.Dequeue();
                if (q != next) tipQueue.Enqueue(q);
            }
            ShowTip(next);
        }

        // ---------------- save ----------------

        public void ReadSave(SaveData s)
        {
            data = s.tutorial ?? new TutorialSave();
            if (data.completed == null) data.completed = new List<string>();
            if (data.tips == null) data.tips = new List<string>();
        }

        public void WriteSave(SaveData s)
        {
            s.tutorial = data;
        }
    }
}
