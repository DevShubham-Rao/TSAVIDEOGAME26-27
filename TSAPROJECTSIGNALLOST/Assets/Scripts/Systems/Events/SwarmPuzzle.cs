using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Golden Swarm: 15 Golden blobs appear at once; pop them all for Golden payouts.</summary>
    public class SwarmPuzzle : PuzzleEvent
    {
        private readonly List<Blob> swarm = new List<Blob>();
        private int popped;

        public SwarmPuzzle(EventDef def, EventOverlay ui) : base(def, ui) { }

        public override void OnWarning()
        {
            UI.Flash(Palette.Gold, 0.5f);
            AudioManager.Play("golden");
        }

        public override IEnumerator Play(float timeLimit)
        {
            int total = (int)Def.Param("blob_count", 15);
            popped = 0;
            EventBus.BlobPopped += OnPopped;
            for (int i = 0; i < total; i++)
            {
                Blob b = BlobSpawner.I.SpawnGolden(BlobSpawner.I.RandomEdgePoint(), true);
                if (b == null) continue;
                b.NoEscape = true;
                swarm.Add(b);
                b.FlyTo(BlobSpawner.I.RandomInteriorPoint(), Random.Range(0.4f, 0.8f), 1f, x => x.ReturnToDrift());
            }
            UI.SetInstructions(Def.instructions);
            UI.SetCounter("0 / " + swarm.Count);
            int target = swarm.Count;
            yield return Countdown(timeLimit, () => popped >= target);
            Succeeded = popped >= target;
        }

        private void OnPopped(Blob b, double payout)
        {
            if (!swarm.Contains(b)) return;
            swarm.Remove(b);
            popped++;
            UI.SetCounter(popped + " / " + (popped + swarm.Count));
        }

        public override void Cleanup()
        {
            EventBus.BlobPopped -= OnPopped;
            foreach (Blob b in swarm)
            {
                if (b == null || !b.IsInteractive) continue;
                b.NoEscape = false;
                b.EventBlob = false;
                b.SetEscaping();
            }
            swarm.Clear();
        }
    }
}
