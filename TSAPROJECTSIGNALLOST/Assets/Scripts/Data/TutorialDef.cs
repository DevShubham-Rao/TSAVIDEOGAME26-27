using System.Collections.Generic;

namespace SignalLost
{
    /// <summary>Root of StreamingAssets/Data/tutorial.json.</summary>
    public class TutorialFile
    {
        public List<TutorialStepDef> steps = new List<TutorialStepDef>();
        public List<TutorialTipDef> tips = new List<TutorialTipDef>();
        /// <summary>Idle nudges Hale says after the player does nothing for a while.</summary>
        public List<string> nudges = new List<string>();
    }

    /// <summary>One guided step. Waits for the player to DO the action, then celebrates.</summary>
    public class TutorialStepDef
    {
        public string id;
        public string speaker = "hale";
        public string text;
        /// <summary>Alternate line when Vacuum mode is Toggle.</summary>
        public string textToggle;
        /// <summary>blob, cluster, upgrade:&lt;id&gt;, compressor, chutes, skills_button, mail_button, none.</summary>
        public string target = "none";
        public bool spotlight;
        public float radius;
        /// <summary>blob_popped, upgrade_bought, vacuum_held, crunch, sorted, seconds.</summary>
        public string waitFor;
        public int count = 1;
        /// <summary>Upgrade id for upgrade_bought.</summary>
        public string upgrade;
        /// <summary>shift (Day 1 on the station) or night (the first end-of-day screen).</summary>
        public string phase = "shift";
        /// <summary>Extra parameter for waits such as night_step (report, mail, tree).</summary>
        public string waitParam;
        public TutorialSetup setup = new TutorialSetup();
        public TutorialReward reward = new TutorialReward();
        /// <summary>small, big, huge.</summary>
        public string celebrate = "small";
        public string stamp;
        public string praise;
        public string nudge;
        public float timeout;
    }

    public class TutorialSetup
    {
        public TutorialSpawn spawn;
        /// <summary>events, mail, spawns, level_popup.</summary>
        public List<string> disable = new List<string>();
        /// <summary>Tops up Data so the next level of this upgrade is affordable.</summary>
        public string ensureDataFor;
        public int compressorCapacity;
        public bool closePanels;
        /// <summary>Respawn tutorial blobs if the player runs out (prevents soft-locks).</summary>
        public bool keepAlive = true;
    }

    public class TutorialSpawn
    {
        public string color = "red";
        public int count = 1;
        public float scale = 1f;
        /// <summary>center, spread, cluster.</summary>
        public string where = "center";
        public float speed = 1f;
        /// <summary>Spawn loose Data bits instead of Static.</summary>
        public bool bits;
    }

    public class TutorialReward
    {
        public double data;
        public int coins;
    }

    /// <summary>A one-time contextual tip, triggered the first time something appears.</summary>
    public class TutorialTipDef
    {
        public string id;
        /// <summary>affordable, antenna_reached, level_up, skill_point, event, mail_received, mail_opened, golden_spawn, away_popup.</summary>
        public string trigger;
        public string param;
        public string text;
        public string target = "none";
        public bool spotlight;
        /// <summary>upgrade_bought, sorted, stat_spent, panel_open, golden_popped, away_collected, event_end, seconds.</summary>
        public string until = "seconds";
        public string untilParam;
        public float duration;
        public float slowmo;
        public float slowmoScale = 0.3f;
        public bool glowChutes;
    }
}
