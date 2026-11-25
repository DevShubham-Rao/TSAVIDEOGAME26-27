using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Fires a puzzle event during shifts (random interval from balance.json). The shift clock pauses while
    /// one runs. Rewards scale with the Event Rewards node; Clear Channel Memory makes them unable to fail.
    /// </summary>
    public class EventDirector : MonoBehaviour
    {
        public static EventDirector I { get; private set; }

        [SerializeField] private EventOverlay overlay;

        public bool Running { get; private set; }
        public EventDef Current { get; private set; }

        private float timer;
        private int hullLeakSuccesses;
        private PuzzleEvent active;
        private EventDef lastEvent;

        private void Awake()
        {
            I = this;
        }

        private void OnEnable()
        {
            EventBus.GameReady += OnReady;
            EventBus.DayStarted += OnDayStarted;
        }

        private void OnDisable()
        {
            EventBus.GameReady -= OnReady;
            EventBus.DayStarted -= OnDayStarted;
        }

        private void OnDayStarted(int day)
        {
            timer = Balance.B.events.firstDelay;
        }

        /// <summary>The shift ended: events never start during last call or at night.</summary>
        public void CancelWarning()
        {
            timer = Mathf.Max(timer, 5f);
        }

        /// <summary>Debug: abandons a running event (the overlay is cleaned up).</summary>
        public void DebugCancel()
        {
            if (!Running) return;
            StopAllCoroutines();
            if (active != null) active.Cleanup();
            if (overlay != null) overlay.Hide();
            active = null;
            Current = null;
            Running = false;
            timer = 30f;
        }

        /// <summary>Restarts the countdown to the first event (after the tutorial hands over control).</summary>
        public void ResetTimer()
        {
            timer = Balance.B.events.firstDelay;
        }

        private void OnReady()
        {
            timer = Balance.B.events.firstDelay;
            if (overlay == null) overlay = EventOverlay.I;
        }

        private void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.IsReady || Running) return;
            if (gm.State != GameState.Playing) return;
            if (DayManager.I != null && (!DayManager.I.InShift || DayManager.I.TimeLeft < 10f)) return;
            if (GameStats.NoEventsToday) return;
            if (GameStats.Antenna < MinUnlock()) return;
            if (UIManager.I != null && UIManager.I.BlocksEvents) return;
            if (TutorialDirector.I != null && TutorialDirector.I.EventsBlocked) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            EventDef def = Pick();
            if (def != null) StartEvent(def);
            else timer = 10f;
        }

        private int MinUnlock()
        {
            int min = int.MaxValue;
            foreach (EventDef e in Balance.I.Events) min = Mathf.Min(min, e.unlockAntenna);
            return min == int.MaxValue ? 999 : min;
        }

        private EventDef Pick()
        {
            List<EventDef> pool = new List<EventDef>();
            float total = 0f;
            foreach (EventDef e in Balance.I.Events)
            {
                if (GameStats.Antenna < e.unlockAntenna) continue;
                if (e == lastEvent && Balance.I.Events.Count > 1) continue;
                pool.Add(e);
                total += Mathf.Max(0.01f, e.weight);
            }
            if (pool.Count == 0) return null;
            float r = Random.value * total;
            foreach (EventDef e in pool)
            {
                r -= Mathf.Max(0.01f, e.weight);
                if (r <= 0f) return e;
            }
            return pool[pool.Count - 1];
        }

        public EventDef Find(string id)
        {
            foreach (EventDef e in Balance.I.Events) if (e.id == id) return e;
            return null;
        }

        /// <summary>Starts an event now (used by the timer and by mail replies such as "echo it back").</summary>
        public bool StartEvent(EventDef def)
        {
            if (def == null || Running) return false;
            if (overlay == null) overlay = EventOverlay.I;
            if (overlay == null) return false;
            StartCoroutine(Run(def));
            return true;
        }

        public void NotifySorted(Blob b)
        {
            if (active != null) active.OnSorted(b);
        }

        private IEnumerator Run(EventDef def)
        {
            Running = true;
            Current = def;
            lastEvent = def;
            BalanceData b = Balance.B;
            active = PuzzleEvent.Create(def, overlay);

            overlay.Show(def);
            AudioManager.Play(string.IsNullOrEmpty(def.warningSound) ? "alarm" : def.warningSound);
            overlay.ShowWarning(def.warning, Palette.Hex(def.tint));
            active.OnWarning();
            EventBus.RaiseEventStarted(def);
            yield return new WaitForSeconds(b.events.warningSeconds);

            // Clear Channel Memory (the upgrade Hale's ending unlocks): the event still pops up, but Hale
            // handles it. No puzzle to play; the reward still lands.
            bool haleHandles = GameStats.EventsNeverFail;
            if (haleHandles)
            {
                Color hale = Palette.Hex("#7FA8FF");
                overlay.ShowWarning("CLEAR CHANNEL", hale);
                AudioManager.Play("shield", 1f, 0.7f);
                yield return new WaitForSeconds(0.9f);
                overlay.Stamp("HALE HANDLES IT", hale);
                AudioManager.Play("solved", 1.1f, 0.7f);
                GameManager.I.Tracking.eventsSolved++;
                ApplyReward(def);
                yield return new WaitForSeconds(b.events.stampSeconds + 0.4f);
                active.Cleanup();
                overlay.Hide();
                EventBus.RaiseEventEnded(def, true);
                active = null;
                Current = null;
                Running = false;
                timer = Random.Range(b.events.intervalMin, b.events.intervalMax);
                SaveManager.I.RequestSave();
                yield break;
            }

            overlay.BeginPuzzle(def);
            float limit = (float)(def.baseTime * GameStats.EventTimerMult);
            // First time this event shows up: Hale explains it, the timer is doubled, and it cannot fail.
            bool guided = TutorialDirector.I != null && TutorialDirector.I.BeginEventTip(def);
            if (guided) limit *= Balance.B.tutorial.firstEventTimeMult;
            yield return active.Play(limit);
            bool success = active.Succeeded || guided;

            if (success)
            {
                GameManager.I.Tracking.eventsSolved++;
                AudioManager.Play("solved");
                overlay.Stamp("SOLVED!", Palette.Green);
                ApplyReward(def);
            }
            else
            {
                AudioManager.Play("boop");
                overlay.Stamp("CLOSE ONE!", Palette.Gold);
                ApplyFail(def);
            }

            yield return new WaitForSeconds(b.events.stampSeconds);
            active.Cleanup();
            overlay.Hide();
            EventBus.RaiseEventEnded(def, success);
            active = null;
            Current = null;
            Running = false;
            timer = Random.Range(b.events.intervalMin, b.events.intervalMax);
            SaveManager.I.RequestSave();
        }

        private void ApplyReward(EventDef def)
        {
            EventOutcome r = def.reward;
            double luck = GameStats.EventRewardMult;
            Vector3 burst = BlobSpawner.I.ViewCenter + Vector3.up * 1.5f;
            switch (r.kind)
            {
                case "buff":
                    BuffSystem.I.Add(r.buff, r.amount * luck, r.seconds, BuffSystem.Describe(r.buff, r.amount * luck));
                    VFXPlayer.Confetti(burst, 60);
                    break;
                case "data_seconds":
                    {
                        double perSec = System.Math.Max(GameManager.I.DataPerSec, GameStats.AverageSpawnValue * GameStats.GlobalMult * GameStats.CompressorMult);
                        double amount = perSec * r.amount * luck;
                        GameManager.I.Earn(amount, burst, Balance.B.compressor.crunchCoins);
                        AudioManager.Play("coin_shower");
                        Juice.FloatText(burst + Vector3.up, "+" + NumberFormat.Format(amount), Palette.Gold, 2f);
                        break;
                    }
            }

            if (def.Param("stat_point_every", 0) > 0) hullLeakSuccesses++;
        }

        private void ApplyFail(EventDef def)
        {
            EventOutcome f = def.fail;
            switch (f.kind)
            {
                case "freeze_blobs": BlobSpawner.I.Freeze(f.seconds); break;
                case "drone_pause": if (DroneManager.I != null) DroneManager.I.Pause(f.seconds); break;
                case "combo_reset": ComboTracker.I.ResetCombo(); break;
            }
        }

        public void ReadSave(SaveData s)
        {
            hullLeakSuccesses = s.flags != null && s.flags.TryGetValue("hull_leak_successes", out double v) ? (int)v : 0;
        }

        public void WriteSave(SaveData s)
        {
            if (s.flags == null) s.flags = new Dictionary<string, double>();
            s.flags["hull_leak_successes"] = hullLeakSuccesses;
        }
    }
}
