using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    public class ActiveBuff
    {
        public string id;
        public double amount;
        public float remaining;
        public float total;
        public string label;
    }

    /// <summary>
    /// Timed buffs from mail replies and events. Keys:
    /// all_data, click_data, crunch_payout, drone_speed (x(1+a)); sort_mult, xp_mult (x a);
    /// combo_window, luck, golden_chance, extra_drones (+a); golden_hour (special).
    /// </summary>
    public class BuffSystem : MonoBehaviour
    {
        public static BuffSystem I { get; private set; }

        private static readonly HashSet<string> MultiplyByAmount = new HashSet<string> { "sort_mult", "xp_mult" };
        private static readonly HashSet<string> OnePlusAmount = new HashSet<string> { "all_data", "click_data", "crunch_payout", "drone_speed" };

        public readonly List<ActiveBuff> Active = new List<ActiveBuff>();

        private void Awake()
        {
            I = this;
        }

        /// <summary>Timed buffs never carry over to the next day.</summary>
        public void ClearAll()
        {
            if (Active.Count == 0) return;
            Active.Clear();
            EventBus.RaiseBuffsChanged();
        }

        public void Add(string id, double amount, float seconds, string label = null)
        {
            if (string.IsNullOrEmpty(id) || seconds <= 0f) return;
            Active.Add(new ActiveBuff { id = id, amount = amount, remaining = seconds, total = seconds, label = label ?? Describe(id, amount) });
            EventBus.RaiseBuffsChanged();
        }

        public bool IsActive(string id)
        {
            foreach (ActiveBuff b in Active) if (b.id == id) return true;
            return false;
        }

        public double Sum(string id)
        {
            double s = 0d;
            foreach (ActiveBuff b in Active) if (b.id == id) s += b.amount;
            return s;
        }

        /// <summary>Multiplier for a key: each buff multiplies (1 + amount), or amount for *_mult keys.</summary>
        public double Product(string id)
        {
            double m = 1d;
            foreach (ActiveBuff b in Active)
            {
                if (b.id != id) continue;
                if (MultiplyByAmount.Contains(id)) m *= b.amount;
                else m *= 1d + b.amount;
            }
            return m;
        }

        private void Update()
        {
            if (Active.Count == 0) return;
            // Buff timers only run while the shift clock runs (not behind menus or on the end-of-day screen).
            GameManager gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            if (DayManager.I != null && !DayManager.I.InShiftOrLastCall) return;
            bool changed = false;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                Active[i].remaining -= Time.deltaTime;
                if (Active[i].remaining <= 0f)
                {
                    Active.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed) EventBus.RaiseBuffsChanged();
        }

        public static string Describe(string id, double amount)
        {
            switch (id)
            {
                case "all_data": return "+" + NumberFormat.Percent(amount) + " all Data";
                case "click_data": return "+" + NumberFormat.Percent(amount) + " click Data";
                case "crunch_payout": return "+" + NumberFormat.Percent(amount) + " crunch";
                case "drone_speed": return "+" + NumberFormat.Percent(amount) + " drone speed";
                case "sort_mult": return NumberFormat.Mult(amount) + " sort Data";
                case "xp_mult": return NumberFormat.Mult(amount) + " XP";
                case "combo_window": return "+" + amount.ToString("0.0#") + "s combo";
                case "luck": return "+" + amount.ToString("0") + " Luck";
                case "golden_chance": return "+" + NumberFormat.Percent(amount, 1) + " Golden";
                case "extra_drones": return "+" + amount.ToString("0") + " drones";
                case "golden_hour": return "Golden Hour";
                default: return id;
            }
        }

        public static bool IsMultiplier(string id)
        {
            return OnePlusAmount.Contains(id) || MultiplyByAmount.Contains(id);
        }

        public void ReadSave(SaveData s)
        {
            Active.Clear();
            foreach (BuffSave b in s.buffs)
            {
                if (b.remaining > 0f) Active.Add(new ActiveBuff { id = b.id, amount = b.amount, remaining = b.remaining, total = Mathf.Max(b.total, b.remaining), label = b.label ?? Describe(b.id, b.amount) });
            }
            EventBus.RaiseBuffsChanged();
        }

        public void WriteSave(SaveData s)
        {
            s.buffs = new List<BuffSave>();
            foreach (ActiveBuff b in Active) s.buffs.Add(new BuffSave { id = b.id, amount = b.amount, remaining = b.remaining, total = b.total, label = b.label });
        }
    }
}
