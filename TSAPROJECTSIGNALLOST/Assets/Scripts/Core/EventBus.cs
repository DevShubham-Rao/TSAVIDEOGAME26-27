using System;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Static C# events. Gameplay raises; UI listens.</summary>
    public static class EventBus
    {
        public static event Action<Blob, double> BlobPopped;
        public static event Action<double> DataGained;
        public static event Action<int> LevelUp;
        public static event Action<MailEntry> MailReceived;
        public static event Action<string, int> UpgradeBought;
        public static event Action<EventDef> EventStarted;
        public static event Action<EventDef, bool> EventEnded;

        public static event Action GameReady;
        public static event Action<int, float> ComboChanged;
        public static event Action<int> ComboMilestone;
        public static event Action<double> CoinLanded;
        public static event Action<bool> Sorted;
        public static event Action<double> Crunched;
        public static event Action SkillsChanged;
        public static event Action BuffsChanged;
        public static event Action StatsChanged;
        public static event Action XPChanged;
        public static event Action TrustChanged;
        public static event Action SettingsChanged;
        public static event Action<string> UpgradeRevealed;
        public static event Action GoldenPopped;
        public static event Action InboxChanged;
        public static event Action DataSpent;
        public static event Action GameplayStarted;
        public static event Action ReturnedToMenu;
        public static event Action<bool> PauseChanged;
        public static event Action<Blob> GoldenSpawned;
        public static event Action AwayShown;
        public static event Action AwayCollected;
        public static event Action<string> PanelOpened;
        public static event Action<int> DayStarted;
        public static event Action<int> DayEnded;
        public static event Action<string> NightStep;

        public static void RaiseBlobPopped(Blob b, double payout) { BlobPopped?.Invoke(b, payout); }
        public static void RaiseDataGained(double amount) { DataGained?.Invoke(amount); }
        public static void RaiseLevelUp(int level) { LevelUp?.Invoke(level); }
        public static void RaiseMailReceived(MailEntry e) { MailReceived?.Invoke(e); }
        public static void RaiseUpgradeBought(string id, int level) { UpgradeBought?.Invoke(id, level); }
        public static void RaiseEventStarted(EventDef d) { EventStarted?.Invoke(d); }
        public static void RaiseEventEnded(EventDef d, bool success) { EventEnded?.Invoke(d, success); }
        public static void RaiseGameReady() { GameReady?.Invoke(); }
        public static void RaiseComboChanged(int combo, float mult) { ComboChanged?.Invoke(combo, mult); }
        public static void RaiseComboMilestone(int combo) { ComboMilestone?.Invoke(combo); }
        public static void RaiseCoinLanded(double amount) { CoinLanded?.Invoke(amount); }
        public static void RaiseSorted(bool correct) { Sorted?.Invoke(correct); }
        public static void RaiseCrunched(double payout) { Crunched?.Invoke(payout); }
        public static void RaiseSkillsChanged() { SkillsChanged?.Invoke(); }
        public static void RaiseBuffsChanged() { BuffsChanged?.Invoke(); }
        public static void RaiseStatsChanged() { StatsChanged?.Invoke(); }
        public static void RaiseXPChanged() { XPChanged?.Invoke(); }
        public static void RaiseTrustChanged() { TrustChanged?.Invoke(); }
        public static void RaiseSettingsChanged() { SettingsChanged?.Invoke(); }
        public static void RaiseUpgradeRevealed(string id) { UpgradeRevealed?.Invoke(id); }
        public static void RaiseGoldenPopped() { GoldenPopped?.Invoke(); }
        public static void RaiseInboxChanged() { InboxChanged?.Invoke(); }
        public static void RaiseDataSpent() { DataSpent?.Invoke(); }
        public static void RaiseGameplayStarted() { GameplayStarted?.Invoke(); }
        public static void RaiseReturnedToMenu() { ReturnedToMenu?.Invoke(); }
        public static void RaisePauseChanged(bool paused) { PauseChanged?.Invoke(paused); }
        public static void RaiseGoldenSpawned(Blob b) { GoldenSpawned?.Invoke(b); }
        public static void RaiseAwayShown() { AwayShown?.Invoke(); }
        public static void RaiseAwayCollected() { AwayCollected?.Invoke(); }
        public static void RaisePanelOpened(string id) { PanelOpened?.Invoke(id); }
        public static void RaiseDayStarted(int day) { DayStarted?.Invoke(day); }
        public static void RaiseDayEnded(int day) { DayEnded?.Invoke(day); }
        public static void RaiseNightStep(string step) { NightStep?.Invoke(step); }

        /// <summary>Clears every listener so Enter Play Mode without domain reload stays clean.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            BlobPopped = null; DataGained = null; LevelUp = null; MailReceived = null;
            UpgradeBought = null; EventStarted = null; EventEnded = null; GameReady = null;
            ComboChanged = null; ComboMilestone = null; CoinLanded = null; Sorted = null;
            Crunched = null; SkillsChanged = null; BuffsChanged = null; StatsChanged = null;
            XPChanged = null; TrustChanged = null; SettingsChanged = null; UpgradeRevealed = null;
            GoldenPopped = null; InboxChanged = null; DataSpent = null;
            GameplayStarted = null; ReturnedToMenu = null; PauseChanged = null; GoldenSpawned = null;
            AwayShown = null; AwayCollected = null; PanelOpened = null;
            DayStarted = null; DayEnded = null; NightStep = null;
        }
    }
}
