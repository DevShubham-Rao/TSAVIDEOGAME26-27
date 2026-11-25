using System.Collections.Generic;

namespace SignalLost
{
    /// <summary>Everything written to persistentDataPath/save.json (Section 13).</summary>
    public class SaveData
    {
        public int version = SaveManager.CurrentVersion;
        public long timestamp;
        public double data;
        public double lifetimeData;
        /// <summary>Main skill tree ranks (reset by Rebroadcast).</summary>
        public Dictionary<string, int> upgrades = new Dictionary<string, int>();
        /// <summary>Deep Signal ranks (kept forever).</summary>
        public Dictionary<string, int> deep = new Dictionary<string, int>();
        /// <summary>Nodes bought on the current end-of-day screen (for Undo tonight).</summary>
        public List<string> tonight = new List<string>();
        public DaySave day = new DaySave();
        public List<string> endingsSeen = new List<string>();
        public Dictionary<string, int> trust = new Dictionary<string, int>();
        public MailSave mail = new MailSave();
        public List<BuffSave> buffs = new List<BuffSave>();
        public int echoes;
        /// <summary>Legacy (v1-v2). Settings now live in settings.json; kept only to migrate old saves.</summary>
        public SettingsSave settings;
        public StatsTracking statsTracking = new StatsTracking();
        public Dictionary<string, double> flags = new Dictionary<string, double>();
        public string droneSkin = "default";
        public string endingId;
        public int rebroadcasts;
        public TutorialSave tutorial = new TutorialSave();
    }

    /// <summary>Where the run is in its day cycle. Saved at the start of every day and on the end-of-day screen.</summary>
    public class DaySave
    {
        public int day = 1;
        /// <summary>shift (resume at the start of that day) or night (resume on the end-of-day screen).</summary>
        public string phase = "shift";
        public double dayStartData;
        /// <summary>Tomorrow's conditions set by mail replies: data_mult, shift_seconds, golden_mult, spawn_mult, no_events.</summary>
        public Dictionary<string, double> modifiers = new Dictionary<string, double>();
        /// <summary>Night Storage: bits kept overnight, by color index.</summary>
        public List<int> storedBits = new List<int>();
        public bool lucky;
        public bool introSeen;
        public bool midpointSeen;
        public DayReport lastReport = new DayReport();
    }

    public class DayReport
    {
        public double dataEarned;
        public int staticPopped;
        public int bitsSorted;
        public int wrongSorts;
        public int crunches;
        public int bestCombo;
        public int bitsLost;
    }

    /// <summary>Tutorial progress: completed guided steps and one-time tips already shown.</summary>
    public class TutorialSave
    {
        public List<string> completed = new List<string>();
        public List<string> tips = new List<string>();
        public bool guidedDone;
        public bool skipped;
    }

    public class MailSave
    {
        public List<string> read = new List<string>();
        public List<string> unread = new List<string>();
        public List<InboxSave> inbox = new List<InboxSave>();
        public List<string> flavorSeen = new List<string>();
        public List<FollowupSave> pending = new List<FollowupSave>();
    }

    public class InboxSave
    {
        public string mailId;
        public string from;
        public string followupText;
        public bool read;
        public int replied = -1;
    }

    public class FollowupSave
    {
        public string from;
        public string text;
        public float remaining;
    }

    public class BuffSave
    {
        public string id;
        public double amount;
        public float remaining;
        public float total;
        public string label;
    }

    public class SettingsSave
    {
        public float master = 1f;
        public float music = 0.7f;
        public float sfx = 0.9f;
        public bool shake = true;
        public string notation = "suffix";
    }

    public class StatsTracking
    {
        public long blobsPopped;
        public int bestCombo;
        public int eventsSolved;
        public long correctSorts;
        public long crunches;
        public long goldenPopped;
        public double playSeconds;
    }
}
