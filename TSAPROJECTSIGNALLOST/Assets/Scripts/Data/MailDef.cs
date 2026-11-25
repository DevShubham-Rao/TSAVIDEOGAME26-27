using System.Collections.Generic;

namespace SignalLost
{
    /// <summary>Root of StreamingAssets/Data/mail.json.</summary>
    public class MailFile
    {
        public List<SenderDef> senders = new List<SenderDef>();
        public List<MailDef> messages = new List<MailDef>();
        public List<EndingDef> endings = new List<EndingDef>();
        /// <summary>One line per day for the day title card.</summary>
        public List<DayBriefing> briefings = new List<DayBriefing>();
    }

    public class SenderDef
    {
        public string id;
        public string name;
        public string title;
        public string portrait;
        public string color = "#3BF0FF";
    }

    public class MailDef
    {
        public string id;
        public MailTrigger trigger = new MailTrigger();
        public string from;
        public string subject;
        public string body;
        /// <summary>True when the body is a ??? symbol string (rendered as glowing shapes).</summary>
        public bool symbols;
        public List<MailReply> replies = new List<MailReply>();
    }

    public class MailTrigger
    {
        /// <summary>day (story, delivered at the end of that day), antenna (finale) or flavor (side mail).</summary>
        public string type = "flavor";
        public int level;
        public int minAntenna;
    }

    public class MailReply
    {
        public string text;
        public bool symbols;
        public Dictionary<string, int> requires;
        public List<MailEffect> effects = new List<MailEffect>();
        public Dictionary<string, int> trust;
        public string followup;
        /// <summary>What this reply gives and costs, shown under the reply button.</summary>
        public string hint;

        /// <summary>Extra replies that need the Open Channel node (or the First Contact memory).</summary>
        public bool NeedsOpenChannel
        {
            get { return requires != null && requires.ContainsKey("open_channel"); }
        }
    }

    public class MailEffect
    {
        /// <summary>Timed buff key (all_data, click_data, sort_mult, ...). Empty when instant.</summary>
        public string buff;
        public double amount;
        public float seconds;
        /// <summary>Instant effect: tomorrow (value = modifier key), data_days, drone_skin, golden_hour, ending.</summary>
        public string instant;
        public string value;
    }

    public class EndingDef
    {
        public string id;
        public string name;
        public string trust;
        public string tint = "#3BF0FF";
        public List<string> cards = new List<string>();
    }
}
