using System.Collections.Generic;

namespace SignalLost
{
    /// <summary>
    /// One node of the skill tree (StreamingAssets/Data/tree.json). Nodes have ranks; each rank adds its
    /// effects to named stats, so gameplay only ever reads stats. A node can be bought once any node in
    /// <see cref="requires"/> is owned (the tree grows outward from the center). Deep Signal nodes
    /// (deep = true) are the permanent rebirth tree, bought with Echoes instead of Data.
    /// </summary>
    public class UpgradeDef
    {
        public string id;
        public string name;
        public string desc;
        public string icon;
        public double baseCost = 10;
        public double growth = 1.15;
        public int maxLevel = 1;

        /// <summary>pop, sweep, station, signal (main tree) or deep (rebirth tree).</summary>
        public string branch = "pop";
        /// <summary>Grid position inside the branch: col -1..1, row 0 = closest to the center.</summary>
        public float col;
        public int row;
        /// <summary>Any one of these owned unlocks this node. Empty = connected to the center.</summary>
        public List<string> requires = new List<string>();
        /// <summary>Big unlock: changes how you play (drawn as a star).</summary>
        public bool star;
        public bool deep;

        // Single-effect shorthand: stat + per_level + mode.
        public string stat;
        public double perLevel;
        public string mode = "add";

        // Multi-effect form. If present it replaces the shorthand.
        public List<UpgradeEffect> effects;

        public UnlockDef unlock = new UnlockDef();

        /// <summary>How the node shows "now -> next": int, meters, mult, bonus_pct, percent, seconds, level, unlock.</summary>
        public string display = "int";
        public string displayStat;

        private List<UpgradeEffect> cached;

        public List<UpgradeEffect> AllEffects()
        {
            if (cached != null) return cached;
            if (effects != null && effects.Count > 0)
            {
                cached = effects;
            }
            else
            {
                cached = new List<UpgradeEffect>();
                if (!string.IsNullOrEmpty(stat)) cached.Add(new UpgradeEffect { stat = stat, perLevel = perLevel, mode = mode });
            }
            return cached;
        }

        public string PrimaryStat
        {
            get
            {
                if (!string.IsNullOrEmpty(displayStat)) return displayStat;
                List<UpgradeEffect> all = AllEffects();
                return all.Count > 0 ? all[0].stat : id;
            }
        }
    }

    public class UpgradeEffect
    {
        public string stat;
        public double perLevel;
        /// <summary>add: base + per*L. pct: base * (1 + per*L). mult: base * (1 + per)^L.</summary>
        public string mode = "add";
    }

    public class UnlockDef
    {
        public int antenna;
        /// <summary>Deep Signal memory nodes: only buyable after seeing this ending.</summary>
        public string ending;
    }
}
