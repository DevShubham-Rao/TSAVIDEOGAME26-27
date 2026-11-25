using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// The one central skill tree (tree.json). It replaces the old upgrade cards, level-up stats and the
    /// separate skill tree. Nodes are bought with Data on the end-of-day screen only. Each rank adds its
    /// effects to named stats; gameplay reads stats through <see cref="GameStats"/>, so a new node that
    /// targets an existing stat needs no code. Deep Signal nodes (rebirth) are bought with Echoes and are
    /// never reset by Rebroadcast.
    /// </summary>
    public class SkillTree : MonoBehaviour
    {
        public static SkillTree I { get; private set; }

        public const string CenterId = "core";

        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        private readonly Dictionary<string, int> deepLevels = new Dictionary<string, int>();
        private readonly List<string> tonight = new List<string>();
        private readonly Dictionary<string, double> statCache = new Dictionary<string, double>();
        private bool cacheDirty = true;

        private void Awake()
        {
            I = this;
        }

        public IEnumerable<UpgradeDef> Nodes(bool deep)
        {
            foreach (UpgradeDef u in Balance.I.Upgrades) if (u.deep == deep) yield return u;
        }

        // ---------------- ownership ----------------

        public int Level(string id)
        {
            if (id == CenterId) return 1;
            if (levels.TryGetValue(id, out int l)) return l;
            return deepLevels.TryGetValue(id, out int d) ? d : 0;
        }

        public bool Owns(string id)
        {
            return Level(id) > 0;
        }

        /// <summary>How many nodes of a chain are owned (pop_power_1..pop_power_5 count as 0 to 5).</summary>
        public int TierCount(string baseId)
        {
            int n = 0;
            for (int i = 1; i <= 12; i++)
            {
                if (Owns(baseId + "_" + i)) n++;
                else break;
            }
            return n;
        }

        /// <summary>Debug menu: show every node (fog of war off) without owning it.</summary>
        public bool RevealAll { get; set; }

        /// <summary>
        /// Fog of war: a node is visible once you own it or own a node next to it (one of its requirements).
        /// Nodes that join the center need nothing, so each branch's first node is visible from the start.
        /// </summary>
        public bool IsVisible(UpgradeDef u)
        {
            if (RevealAll || u.id == CenterId) return true;
            if (Owns(u.id)) return true;
            if (u.requires == null || u.requires.Count == 0) return true;
            foreach (string r in u.requires) if (Owns(r)) return true;
            return false;
        }

        /// <summary>Debug menu: owns every node, except the last Antenna levels (they end the run).</summary>
        public void DebugOwnAll()
        {
            foreach (UpgradeDef u in Balance.I.Upgrades)
            {
                if (u.id == CenterId || u.deep) continue;
                if (u.id == "antenna_9" || u.id == "antenna_10") continue;
                levels[u.id] = 1;
            }
            cacheDirty = true;
            EventBus.RaiseUpgradeBought("debug", 0);
            SaveManager.I.RequestSave();
        }

        public bool IsMaxed(UpgradeDef u)
        {
            return Level(u.id) >= u.maxLevel;
        }

        /// <summary>Cost of the next rank: base x growth^rank (Data, or Echoes for Deep Signal nodes).</summary>
        public double Cost(UpgradeDef u, int level)
        {
            double c = u.baseCost * Math.Pow(u.growth, level);
            return u.deep ? Math.Ceiling(c) : Math.Round(c);
        }

        public double NextCost(UpgradeDef u)
        {
            return Cost(u, Level(u.id));
        }

        /// <summary>Connected to something you own (the tree grows out from the center).</summary>
        public bool IsReachable(UpgradeDef u)
        {
            if (u.id == CenterId) return true;
            if (u.requires == null || u.requires.Count == 0) return true;
            foreach (string r in u.requires) if (Owns(r)) return true;
            return false;
        }

        /// <summary>Extra gates on top of reachability: Antenna level, or a Deep Signal memory's ending.</summary>
        public bool MeetsUnlock(UpgradeDef u)
        {
            if (u.unlock == null) return true;
            if (u.unlock.antenna > GameStats.Antenna) return false;
            if (!string.IsNullOrEmpty(u.unlock.ending) && (GameManager.I == null || !GameManager.I.HasSeenEnding(u.unlock.ending))) return false;
            return true;
        }

        public bool CanAfford(UpgradeDef u)
        {
            double cost = NextCost(u);
            return u.deep ? GameManager.I.Echoes >= cost : GameManager.I.CanAfford(cost);
        }

        /// <summary>Main-tree nodes can only be bought between days (the end-of-day screen).</summary>
        public bool ShopOpen(UpgradeDef u)
        {
            if (u.deep) return true;
            return DayManager.I == null || DayManager.I.ShopOpen;
        }

        public bool CanBuy(UpgradeDef u)
        {
            return u != null && u.id != CenterId && !IsMaxed(u) && IsReachable(u) && MeetsUnlock(u) && ShopOpen(u) && CanAfford(u);
        }

        public bool TryBuy(UpgradeDef u)
        {
            if (!CanBuy(u)) return false;
            double cost = NextCost(u);
            if (u.deep)
            {
                if (!GameManager.I.TrySpendEchoes((int)cost)) return false;
                deepLevels[u.id] = Level(u.id) + 1;
            }
            else
            {
                if (!GameManager.I.TrySpend(cost)) return false;
                levels[u.id] = Level(u.id) + 1;
                tonight.Add(u.id);
            }
            cacheDirty = true;
            AudioManager.Play("buy");
            EventBus.RaiseUpgradeBought(u.id, Level(u.id));
            SaveManager.I.RequestSave();
            return true;
        }

        // ---------------- undo tonight ----------------

        public bool HasTonightPurchases { get { return tonight.Count > 0; } }

        public double TonightSpent
        {
            get
            {
                double sum = 0d;
                Dictionary<string, int> sim = new Dictionary<string, int>(levels);
                for (int i = tonight.Count - 1; i >= 0; i--)
                {
                    UpgradeDef u = Balance.I.Upgrade(tonight[i]);
                    if (u == null) continue;
                    int l = sim.TryGetValue(u.id, out int v) ? v : 0;
                    sum += Cost(u, Mathf.Max(0, l - 1));
                    sim[u.id] = l - 1;
                }
                return sum;
            }
        }

        /// <summary>"Undo tonight": refunds everything bought on this end-of-day screen.</summary>
        public void UndoTonight()
        {
            if (tonight.Count == 0) return;
            double refund = 0d;
            for (int i = tonight.Count - 1; i >= 0; i--)
            {
                string id = tonight[i];
                UpgradeDef u = Balance.I.Upgrade(id);
                int l = Level(id);
                if (u == null || l <= 0) continue;
                refund += Cost(u, l - 1);
                if (l - 1 <= 0) levels.Remove(id);
                else levels[id] = l - 1;
            }
            tonight.Clear();
            cacheDirty = true;
            GameManager.I.Refund(refund);
            AudioManager.Play("whoosh", 0.8f, 0.6f);
            EventBus.RaiseUpgradeBought("undo", 0);
            SaveManager.I.RequestSave();
        }

        /// <summary>Purchases lock in once the next day starts.</summary>
        public void LockTonight()
        {
            tonight.Clear();
        }

        // ---------------- stats ----------------

        /// <summary>Final value of a stat: its base in balance.json plus every owned node's effects.</summary>
        public double Stat(string stat)
        {
            if (cacheDirty)
            {
                statCache.Clear();
                cacheDirty = false;
            }
            if (statCache.TryGetValue(stat, out double cached)) return cached;
            double value = Balance.B.StatBase(stat);
            double add = 0d, pct = 0d, mult = 1d;
            foreach (UpgradeDef u in Balance.I.Upgrades)
            {
                int l = Level(u.id);
                if (l <= 0 || u.id == CenterId) continue;
                foreach (UpgradeEffect e in u.AllEffects())
                {
                    if (e.stat != stat) continue;
                    switch (e.mode)
                    {
                        case "pct": pct += e.perLevel * l; break;
                        case "mult": mult *= Math.Pow(1d + e.perLevel, l); break;
                        default: add += e.perLevel * l; break;
                    }
                }
            }
            value = (value + add) * (1d + pct) * mult;
            statCache[stat] = value;
            return value;
        }

        public bool Has(string stat)
        {
            return Stat(stat) > 0d;
        }

        /// <summary>Stat value if this node had extra ranks (for "now -> next" previews).</summary>
        public double StatPreview(UpgradeDef u, string stat, int extraLevels)
        {
            Dictionary<string, int> map = u.deep ? deepLevels : levels;
            int l = Level(u.id);
            map[u.id] = l + extraLevels;
            cacheDirty = true;
            double v = Stat(stat);
            if (l == 0) map.Remove(u.id);
            else map[u.id] = l;
            cacheDirty = true;
            return v;
        }

        public void MarkDirty()
        {
            cacheDirty = true;
        }

        // ---------------- save ----------------

        public void ReadSave(SaveData s)
        {
            levels.Clear();
            deepLevels.Clear();
            tonight.Clear();
            if (s.upgrades != null) foreach (KeyValuePair<string, int> kv in s.upgrades) if (Balance.I.Upgrade(kv.Key) != null) levels[kv.Key] = kv.Value;
            if (s.deep != null) foreach (KeyValuePair<string, int> kv in s.deep) if (Balance.I.Upgrade(kv.Key) != null) deepLevels[kv.Key] = kv.Value;
            if (s.tonight != null) tonight.AddRange(s.tonight);
            cacheDirty = true;
        }

        public void WriteSave(SaveData s)
        {
            s.upgrades = new Dictionary<string, int>(levels);
            s.deep = new Dictionary<string, int>(deepLevels);
            s.tonight = new List<string>(tonight);
        }
    }
}
