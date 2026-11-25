using System;
using System.Collections;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Base for a 5 to 10 second puzzle event. Events always reward and never punish:
    /// failing just means a smaller reward. Subclasses are picked by EventDef.puzzle.
    /// </summary>
    public abstract class PuzzleEvent
    {
        protected readonly EventDef Def;
        protected readonly EventOverlay UI;

        public bool Succeeded { get; protected set; }

        protected PuzzleEvent(EventDef def, EventOverlay ui)
        {
            Def = def;
            UI = ui;
        }

        public static PuzzleEvent Create(EventDef def, EventOverlay ui)
        {
            switch (def.puzzle)
            {
                case "mash": return new MashPuzzle(def, ui);
                case "sequence": return new SequencePuzzle(def, ui);
                case "patch": return new PatchPuzzle(def, ui);
                case "rush": return new RushPuzzle(def, ui);
                case "swarm": return new SwarmPuzzle(def, ui);
                default:
                    Debug.LogWarning("[Events] Unknown puzzle type '" + def.puzzle + "' for " + def.id + "; using mash.");
                    return new MashPuzzle(def, ui);
            }
        }

        /// <summary>Called while the warning banner is up (alarm, sparks, flashes).</summary>
        public virtual void OnWarning() { }

        public abstract IEnumerator Play(float timeLimit);

        public virtual void Cleanup() { }

        public virtual void OnSorted(Blob b) { }

        /// <summary>Ticks the timer bar until done() returns true or the time runs out.</summary>
        protected IEnumerator Countdown(float limit, Func<bool> done, Action tick = null)
        {
            float t = limit;
            while (t > 0f && !done())
            {
                t -= Time.deltaTime;
                if (UI != null) UI.SetTimer(Mathf.Clamp01(t / limit), t);
                if (tick != null) tick();
                yield return null;
            }
            if (UI != null) UI.SetTimer(Mathf.Clamp01(t / limit), Mathf.Max(0f, t));
        }
    }
}
