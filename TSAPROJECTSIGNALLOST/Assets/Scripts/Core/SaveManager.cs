using System;
using System.IO;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Autosaves to persistentDataPath/save.json every 30 s, on purchases, mail replies, pause and quit.
    /// Writes to a temp file first, then swaps it in, so a crash never corrupts the save.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public const int CurrentVersion = 5;

        public static SaveManager I { get; private set; }

        public SaveData Loaded { get; private set; }
        public bool WasNewGame { get; private set; }
        public bool Ready { get; set; }

        private float timer;
        private bool saveQueued;
        private bool suppressSaving;

        /// <summary>Optional folder override (automated tests), otherwise Application.persistentDataPath.</summary>
        public static string DirectoryOverride;

        private static string SavePath
        {
            get { return Path.Combine(string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride, "save.json"); }
        }
        private static string TempPath { get { return SavePath + ".tmp"; } }
        private static string BackupPath { get { return SavePath + ".bak"; } }

        private void Awake()
        {
            I = this;
        }

        public SaveData LoadOrCreate()
        {
            WasNewGame = true;
            Loaded = new SaveData();
            try
            {
                string path = File.Exists(SavePath) ? SavePath : (File.Exists(BackupPath) ? BackupPath : null);
                if (path != null)
                {
                    SaveData s = JsonUtil.Parse<SaveData>(File.ReadAllText(path));
                    SaveData migrated = s != null ? Migrate(s) : null;
                    if (migrated != null)
                    {
                        Loaded = migrated;
                        WasNewGame = false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not read save, starting fresh: " + e.Message);
                Loaded = new SaveData();
                WasNewGame = true;
            }
            return Loaded;
        }

        /// <summary>
        /// Save layout v5 (v4 was the day-by-day rework; v5 renamed every tree node to a single-rank id). Older saves (v1 to v3) used upgrade cards, levels and a
        /// separate skill tree that no longer exist, so they start over at Day 1 (settings live in
        /// settings.json and are kept). Returns null when the save cannot be carried over.
        /// </summary>
        private static SaveData Migrate(SaveData s)
        {
            if (s.version < CurrentVersion)
            {
                Debug.Log("[Save] Save from before the day-by-day update (v" + s.version + "): starting a fresh run.");
                return null;
            }
            if (s.tutorial == null) s.tutorial = new TutorialSave();
            if (s.upgrades == null) s.upgrades = new System.Collections.Generic.Dictionary<string, int>();
            if (s.deep == null) s.deep = new System.Collections.Generic.Dictionary<string, int>();
            if (s.tonight == null) s.tonight = new System.Collections.Generic.List<string>();
            if (s.day == null) s.day = new DaySave();
            if (s.day.modifiers == null) s.day.modifiers = new System.Collections.Generic.Dictionary<string, double>();
            if (s.day.storedBits == null) s.day.storedBits = new System.Collections.Generic.List<int>();
            if (s.day.lastReport == null) s.day.lastReport = new DayReport();
            if (s.endingsSeen == null) s.endingsSeen = new System.Collections.Generic.List<string>();
            if (s.trust == null) s.trust = new System.Collections.Generic.Dictionary<string, int>();
            if (s.buffs == null) s.buffs = new System.Collections.Generic.List<BuffSave>();
            if (s.flags == null) s.flags = new System.Collections.Generic.Dictionary<string, double>();
            if (s.statsTracking == null) s.statsTracking = new StatsTracking();
            if (s.mail == null) s.mail = new MailSave();
            if (s.mail.inbox == null) s.mail.inbox = new System.Collections.Generic.List<InboxSave>();
            if (s.mail.read == null) s.mail.read = new System.Collections.Generic.List<string>();
            if (s.mail.unread == null) s.mail.unread = new System.Collections.Generic.List<string>();
            if (s.mail.flavorSeen == null) s.mail.flavorSeen = new System.Collections.Generic.List<string>();
            if (s.mail.pending == null) s.mail.pending = new System.Collections.Generic.List<FollowupSave>();
            return s;
        }

        public SaveData Collect()
        {
            SaveData s = new SaveData();
            s.version = CurrentVersion;
            s.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (GameManager.I != null) GameManager.I.WriteSave(s);
            if (SkillTree.I != null) SkillTree.I.WriteSave(s);
            if (DayManager.I != null) DayManager.I.WriteSave(s);
            if (BuffSystem.I != null) BuffSystem.I.WriteSave(s);
            if (MailDirector.I != null) MailDirector.I.WriteSave(s);
            if (EventDirector.I != null) EventDirector.I.WriteSave(s);
            if (TutorialDirector.I != null) TutorialDirector.I.WriteSave(s);
            return s;
        }

        /// <summary>Queues a save for the end of this frame (cheap to call from many places).</summary>
        public void RequestSave()
        {
            saveQueued = true;
        }

        public void SaveNow()
        {
            if (!Ready || suppressSaving) return;
            saveQueued = false;
            timer = 0f;
            try
            {
                string json = JsonUtil.Write(Collect());
                File.WriteAllText(TempPath, json);
                if (File.Exists(SavePath))
                {
                    try
                    {
                        File.Replace(TempPath, SavePath, BackupPath);
                    }
                    catch (Exception)
                    {
                        // Some platforms do not support File.Replace; fall back to copy + move.
                        File.Copy(SavePath, BackupPath, true);
                        File.Delete(SavePath);
                        File.Move(TempPath, SavePath);
                    }
                }
                else
                {
                    File.Move(TempPath, SavePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Save failed: " + e.Message);
            }
        }

        /// <summary>Deletes every save file. The caller reloads the scene afterwards.</summary>
        public void DeleteSave()
        {
            suppressSaving = true;
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not delete save: " + e.Message);
            }
        }

        /// <summary>Writes an explicit SaveData (used by Rebroadcast), then blocks further saves until reload.</summary>
        public void WriteAndLock(SaveData s)
        {
            try
            {
                s.version = CurrentVersion;
                s.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                File.WriteAllText(TempPath, JsonUtil.Write(s));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(TempPath, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Rebroadcast save failed: " + e.Message);
            }
            suppressSaving = true;
        }

        private void Update()
        {
            if (!Ready) return;
            timer += Time.unscaledDeltaTime;
            float interval = Balance.B != null ? Balance.B.autosaveSeconds : 30f;
            if (timer >= interval) saveQueued = true;
        }

        private void LateUpdate()
        {
            if (saveQueued) SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }
    }
}
