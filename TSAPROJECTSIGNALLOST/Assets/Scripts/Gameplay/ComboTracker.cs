using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Every correct sort or pop within the combo window adds +1. Multiplier = 1 + combo x 0.05, capped at
    /// 5x (10x with Overload). Decays to 0 after the window passes without an action.
    /// SPEC-ASSUMPTION: only the player's own pops and sorts build combo; drones do not.
    /// </summary>
    public class ComboTracker : MonoBehaviour
    {
        public static ComboTracker I { get; private set; }

        public int Combo { get; private set; }
        /// <summary>Best combo since the current day started (for the shift report).</summary>
        public int BestToday { get; private set; }
        private float lastAction = -100f;

        private void Awake()
        {
            I = this;
        }

        public float Window { get { return GameStats.ComboWindow; } }

        /// <summary>
        /// Coyote time: the bar runs empty after Window seconds, but the combo itself lives a little longer,
        /// so a near-miss on a chute or a slow sweep does not cost the streak.
        /// </summary>
        public float Grace { get { return Balance.B.combo.grace; } }

        /// <summary>True when the bar is empty but the combo is still alive.</summary>
        public bool InGrace { get { return Combo > 0 && Time.time - lastAction > Window; } }

        public float Mult
        {
            get { return Mathf.Min(1f + Combo * Balance.B.combo.step, GameStats.ComboCap); }
        }

        /// <summary>0..1 of the window remaining (for the combo bar).</summary>
        public float TimeLeft01
        {
            get
            {
                if (Combo <= 0) return 0f;
                return Mathf.Clamp01(1f - (Time.time - lastAction) / Mathf.Max(0.05f, Window));
            }
        }

        public void Register()
        {
            if (Combo > 0 && Time.time - lastAction > Window + Grace) Combo = 0;
            Combo++;
            lastAction = Time.time;
            if (GameManager.I != null && Combo > GameManager.I.Tracking.bestCombo) GameManager.I.Tracking.bestCombo = Combo;
            if (Combo > BestToday) BestToday = Combo;
            if (Balance.B.combo.milestones.Contains(Combo)) EventBus.RaiseComboMilestone(Combo);
            EventBus.RaiseComboChanged(Combo, Mult);
        }

        public void ResetCombo()
        {
            if (Combo == 0) return;
            Combo = 0;
            EventBus.RaiseComboChanged(0, 1f);
        }

        private void OnEnable()
        {
            EventBus.DayStarted += OnDayStarted;
        }

        private void OnDisable()
        {
            EventBus.DayStarted -= OnDayStarted;
        }

        private void OnDayStarted(int day)
        {
            BestToday = 0;
            ResetCombo();
        }

        /// <summary>True while a puzzle event (QTE) runs: the combo timer is frozen.</summary>
        public bool Frozen { get { return EventDirector.I != null && EventDirector.I.Running; } }

        private void Update()
        {
            // QTE combo freeze: while an event runs the window does not tick down, so a forced QTE never
            // costs you the streak. The timer resumes where it left off when the event ends.
            if (Frozen && Combo > 0) lastAction += Time.deltaTime;
            if (Combo > 0 && Time.time - lastAction > Window + Grace)
            {
                Combo = 0;
                EventBus.RaiseComboChanged(0, 1f);
            }
        }
    }
}
