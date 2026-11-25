using System.Collections.Generic;

namespace SignalLost
{
    /// <summary>One puzzle event in StreamingAssets/Data/events.json.</summary>
    public class EventDef
    {
        public string id;
        public string name;
        public string type;
        /// <summary>Which puzzle runner handles it: mash, sequence, patch, rush, swarm.</summary>
        public string puzzle;
        public int unlockAntenna;
        public float baseTime = 5;
        public string warning;
        public string instructions;
        public string warningSound = "alarm";
        public string tint = "#FF8A3B";
        public float weight = 1;
        public Dictionary<string, double> @params = new Dictionary<string, double>();
        public EventOutcome reward = new EventOutcome();
        public EventOutcome fail = new EventOutcome();

        public double Param(string key, double fallback)
        {
            return @params != null && @params.TryGetValue(key, out double v) ? v : fallback;
        }
    }

    public class EventOutcome
    {
        /// <summary>buff, data_seconds, none / freeze_blobs, drone_pause, combo_reset.</summary>
        public string kind = "none";
        public string buff;
        public double amount;
        public float seconds;
    }
}
