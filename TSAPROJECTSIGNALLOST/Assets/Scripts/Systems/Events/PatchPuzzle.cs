using System.Collections;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Hull Leak: 3 to 5 leak sparks appear on the station; click each to patch it before oxygen hits 0.</summary>
    public class PatchPuzzle : PuzzleEvent
    {
        private int patched;
        private int count;

        public PatchPuzzle(EventDef def, EventOverlay ui) : base(def, ui) { }

        public override void OnWarning()
        {
            if (AudioManager.I != null) AudioManager.I.SetHiss(true);
            UI.SetOxygen(1f, true);
        }

        public override IEnumerator Play(float timeLimit)
        {
            int min = (int)Def.Param("leaks_min", 3);
            int max = (int)Def.Param("leaks_max", 5);
            count = Random.Range(min, max + 1);
            patched = 0;
            Station st = GameManager.I.Station;
            int offset = Random.Range(0, 12);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = st != null ? st.LeakPoint(offset + i * 2) : Vector3.up * 2f;
                Vector3 world = p;
                UI.CreateWorldButton(world, () =>
                {
                    patched++;
                    VFXPlayer.Sparkle(world, 14, Palette.Cyan);
                    AudioManager.Play("shield", 1.3f);
                    UI.SetCounter(patched + " / " + count + " PATCHED");
                });
            }
            UI.SetInstructions(Def.instructions);
            UI.SetCounter("0 / " + count + " PATCHED");
            yield return Countdown(timeLimit, () => patched >= count, () => UI.SetOxygen(UI.TimerFraction, true));
            Succeeded = patched >= count;
        }

        public override void Cleanup()
        {
            if (AudioManager.I != null) AudioManager.I.SetHiss(false);
            UI.SetOxygen(1f, false);
        }
    }
}
