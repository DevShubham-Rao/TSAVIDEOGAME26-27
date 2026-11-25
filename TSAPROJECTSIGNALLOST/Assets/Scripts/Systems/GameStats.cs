using System;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Final gameplay values, all read from data: stat bases in balance.json, plus skill tree nodes,
    /// plus timed buffs, plus tomorrow's conditions set by mail (DayManager modifiers).
    /// </summary>
    public static class GameStats
    {
        private static BalanceData B { get { return Balance.B; } }

        private static double T(string stat)
        {
            return SkillTree.I != null ? SkillTree.I.Stat(stat) : B.StatBase(stat);
        }

        private static double BuffAdd(string key)
        {
            return BuffSystem.I != null ? BuffSystem.I.Sum(key) : 0d;
        }

        private static double BuffMult(string key)
        {
            return BuffSystem.I != null ? BuffSystem.I.Product(key) : 1d;
        }

        private static double Mod(string key, double fallback)
        {
            return DayManager.I != null ? DayManager.I.Modifier(key, fallback) : fallback;
        }

        private static int Day { get { return DayManager.I != null ? DayManager.I.Day : 1; } }

        // ---------- story / signal ----------

        public static int Antenna { get { return (int)Math.Round(T("antenna")); } }
        public static double SignalStrength { get { return Math.Min(1d, T("signal_strength")); } }

        // ---------- shift ----------

        public static float ShiftSeconds { get { return Mathf.Max(20f, (float)(T("shift_seconds") + Mod("shift_seconds", 0d))); } }

        public static float SpawnPerSecond
        {
            get
            {
                float daily = 1f + B.days.spawnGrowthPerDay * (Day - 1);
                return (float)(B.spawn.basePerSecond * T("spawn_rate") * daily * Mod("spawn_mult", 1d));
            }
        }

        public static int MaxBlobs
        {
            get
            {
                double cap = T("max_blobs") + B.days.capGrowthPerDay * (Day - 1);
                cap *= Mod("spawn_mult", 1d);
                return Math.Min(B.spawn.hardCap, Math.Max(1, (int)Math.Round(cap)));
            }
        }

        // ---------- pop ----------

        public static int ClickPower { get { return Math.Max(1, (int)Math.Round(T("click_power"))); } }
        public static float PopArea { get { return (float)T("pop_area"); } }
        public static double CritChance { get { return T("crit_chance"); } }
        public static int CritBitsMult { get { return Math.Max(1, (int)Math.Round(T("crit_bits_mult"))); } }
        public static bool ChainPop { get { return T("chain_pop") > 0d; } }
        public static int ShockwaveEvery { get { return T("shockwave") > 0d ? Math.Max(1, (int)Math.Round(T("shockwave_every"))) : 0; } }
        public static float ShockwaveRadius { get { return (float)T("shockwave_radius"); } }
        public static int AutoPop { get { return (int)Math.Round(T("auto_pop")); } }
        public static int ExtraBitsPerPop { get { return (int)Math.Round(T("bits_per_pop")); } }

        public static float ComboWindow { get { return (float)(T("combo_window") + BuffAdd("combo_window")); } }
        public static float ComboCap { get { return B.combo.cap; } }

        // ---------- sweep ----------

        public static float VacuumRadius { get { return (float)T("vacuum_radius"); } }
        public static int VacuumCapacity { get { return Math.Max(1, (int)Math.Round(T("vacuum_capacity"))); } }
        public static float SweepSpeed { get { return (float)T("sweep_speed"); } }
        public static float BitMagnet { get { return (float)T("bit_magnet"); } }
        public static bool MagnetPulse { get { return T("magnet_pulse") > 0d; } }
        public static bool UniversalChute { get { return T("universal_chute") > 0d; } }
        public static float ChuteCatchRadius { get { return B.sort.catchRadius * (UniversalChute ? 1.8f : 1f); } }

        // ---------- station ----------

        public static int CompressorCapacity { get { return Math.Max(1, (int)Math.Round(T("compressor_size"))); } }
        public static double CompressorMult { get { return T("compressor_mult"); } }
        public static float CrunchSpeed { get { return (float)(1d + T("crunch_speed")) * (OverclockActive ? 2f : 1f); } }
        public static bool AutoFeed { get { return T("auto_feed") > 0d; } }
        public static double JackpotChance { get { return T("jackpot_chance"); } }
        public static double JackpotMult { get { return T("jackpot_mult"); } }
        public static bool OverclockActive { get { return T("overclock") > 0d && DayManager.I != null && DayManager.I.InShift && DayManager.I.TimeLeft <= B.days.overclockSeconds; } }

        public static int DroneCount
        {
            get { return Math.Min(B.drones.max + 3, (int)Math.Round(T("drone_count") + BuffAdd("extra_drones"))); }
        }

        /// <summary>Drones from the tree (without temporary mail/event drones).</summary>
        public static int PermanentDroneCount { get { return (int)Math.Round(T("drone_count")); } }

        public static int DroneTank { get { return Math.Max(1, (int)Math.Round(T("drone_tank"))); } }

        public static float DroneSpeedMult
        {
            get { return (float)(T("drone_speed") * BuffMult("drone_speed")) * (OverclockActive ? 2f : 1f); }
        }

        // ---------- signal ----------

        /// <summary>Gold Static is found from Antenna 3 on.</summary>
        public static bool GoldUnlockedByAntenna { get { return Antenna >= B.days.goldenFromAntenna; } }

        /// <summary>
        /// A mail deal that promises Gold Static ("tomorrow Gold is likelier") makes it appear even before
        /// Antenna 3, so the game never promises something the player cannot find.
        /// </summary>
        public static bool GoldPromisedByMail { get { return Mod("golden_mult", 1d) > 1.001d; } }

        public static bool GoldenUnlocked { get { return GoldUnlockedByAntenna || GoldPromisedByMail || BuffAdd("golden_chance") > 0d; } }

        public static float GoldenChance
        {
            get
            {
                if (!GoldenUnlocked) return 0f;
                double c = T("golden_chance") + BuffAdd("golden_chance");
                c *= 1d + T("golden_chance_pct");
                c *= Mod("golden_mult", 1d);
                if (DayManager.I != null && DayManager.I.Lucky) c *= B.days.luckyShiftGoldenMult;
                // Before Antenna 3 only the mail deal opens Gold: make sure one really shows up that day.
                if (!GoldUnlockedByAntenna && GoldPromisedByMail) c = Math.Max(c, B.days.questGoldenChance);
                return (float)Math.Min(0.5d, c);
            }
        }

        /// <summary>Resolves {goldx2} / {goldx3} / {nogold} in mail reply hints to words that are true right now.</summary>
        public static string ResolveHint(string hint)
        {
            if (string.IsNullOrEmpty(hint) || hint.IndexOf('{') < 0) return hint;
            bool known = GoldUnlockedByAntenna;
            hint = hint.Replace("{goldx2}", known ? "Gold Static twice as likely" : "A rare Gold Static will show up (your first)");
            hint = hint.Replace("{goldx3}", known ? "Gold Static three times as likely" : "A rare Gold Static will show up (your first)");
            hint = hint.Replace("{nogold}", known ? "no Gold Static at all" : "a calmer, quieter shift");
            return hint;
        }

        public static bool OpenChannel { get { return T("open_channel") > 0d; } }
        public static bool NightStorage { get { return T("night_storage") > 0d; } }
        public static bool EventsNeverFail { get { return T("events_never_fail") > 0d; } }
        public static bool NoEventsToday { get { return Mod("no_events", 0d) > 0d; } }

        // ---------- multipliers ----------

        /// <summary>Product of every source that says "all Data": Antenna, Data Value, buffs, tomorrow's mail deal.</summary>
        public static double GlobalMult
        {
            get
            {
                double m = T("antenna_data_mult");
                m *= 1d + T("data_mult");
                m *= BuffMult("all_data");
                m *= Mod("data_mult", 1d);
                return m;
            }
        }

        public static double CrunchBuffMult { get { return BuffMult("crunch_payout") * BuffMult("sort_mult") * BuffMult("click_data"); } }

        public static double EventTimerMult { get { return 1d + T("event_timer_pct"); } }
        public static double EventRewardMult { get { return 1d + T("event_reward_pct"); } }

        // ---------- blob values ----------

        public static bool IsUnlocked(BlobTypeDef t)
        {
            if (t.id == "prism" && T("prism_unlock") <= 0d) return false;
            return Antenna >= t.unlockAntenna;
        }

        /// <summary>Golden Static is worth 100x the best unlocked regular type (Prism excluded).</summary>
        public static double BestBlobValue
        {
            get
            {
                double best = 1d;
                foreach (BlobTypeDef t in B.blobs)
                {
                    if (t.id == "prism" || !IsUnlocked(t)) continue;
                    best = Math.Max(best, t.value);
                }
                return best * B.dataValueScale;
            }
        }

        public static double AverageSpawnValue
        {
            get
            {
                double wsum = 0d, vsum = 0d;
                foreach (BlobTypeDef t in B.blobs)
                {
                    if (!IsUnlocked(t)) continue;
                    wsum += t.weight;
                    vsum += t.weight * t.value;
                }
                double avg = wsum > 0 ? vsum / wsum : 1d;
                return avg * B.dataValueScale;
            }
        }
    }
}
