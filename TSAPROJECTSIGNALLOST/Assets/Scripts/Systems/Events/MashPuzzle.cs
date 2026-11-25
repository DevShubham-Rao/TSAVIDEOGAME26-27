using System.Collections;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Solar Flare: mash the big SHIELD button 20 times (+5 per Antenna level) before time runs out.
    /// SPEC-ASSUMPTION: the +5 per level counts levels above the event's unlock level, and Space also
    /// counts as a press, so it stays a "no brain required" mash at high Antenna levels.
    /// </summary>
    public class MashPuzzle : PuzzleEvent
    {
        private int presses;
        private int required;

        public MashPuzzle(EventDef def, EventOverlay ui) : base(def, ui) { }

        public override void OnWarning()
        {
            UI.EdgePulse(Palette.Hex(Def.tint), true);
        }

        public override IEnumerator Play(float timeLimit)
        {
            int extraLevels = Mathf.Max(0, GameStats.Antenna - Def.unlockAntenna);
            required = (int)(Def.Param("presses_base", 20) + Def.Param("presses_per_antenna", 5) * extraLevels);
            presses = 0;
            UI.SetInstructions(Def.instructions);
            UI.CreateShieldButton(Press);
            UI.SetShieldProgress(0f, required);
            yield return Countdown(timeLimit, () => presses >= required, () =>
            {
                if (InputReader.I != null && InputReader.I.SpacePressed) Press();
            });
            Succeeded = presses >= required;
        }

        private void Press()
        {
            if (presses >= required) return;
            presses++;
            AudioManager.Play("shield", 0.9f + presses * 0.012f, 0.8f);
            UI.SetShieldProgress(presses / (float)required, required - presses);
        }

        public override void Cleanup()
        {
            UI.EdgePulse(Color.clear, false);
        }
    }
}
