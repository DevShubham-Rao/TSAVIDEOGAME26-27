using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Signal Rush: 20 blobs of 3 colors flood in; sort as many as possible. Each correct sort pays 5x.</summary>
    public class RushPuzzle : PuzzleEvent
    {
        private int sorted;
        private readonly List<Blob> spawned = new List<Blob>();

        public RushPuzzle(EventDef def, EventOverlay ui) : base(def, ui) { }

        public override IEnumerator Play(float timeLimit)
        {
            int total = (int)Def.Param("blob_count", 20);
            int goal = (int)Def.Param("success_sorts", 8);
            float mult = (float)Def.Param("sort_mult", 5);
            sorted = 0;
            BuffSystem.I.Add("sort_mult", mult, timeLimit + 0.5f, "Signal Rush x" + mult.ToString("0"));
            UI.SetInstructions(Def.instructions);
            UI.SetCounter("SORTED 0");
            Juice.Run(SpawnWave(total));
            yield return Countdown(timeLimit, () => sorted >= total);
            Succeeded = sorted >= goal;
        }

        private IEnumerator SpawnWave(int total)
        {
            // A shower of loose Data bits in all three colors: sweep and sort as many as you can.
            BlobColor[] colors = { BlobColor.Red, BlobColor.Blue, BlobColor.Green };
            for (int i = 0; i < total; i++)
            {
                BlobColor c = colors[i % 3];
                BlobTypeDef t = BlobSpawner.I.TypeForColor(c);
                double value = (t != null ? t.value / Mathf.Max(1, t.bits) : 1d) * Balance.B.dataValueScale;
                BlobSpawner.I.SpawnBits(BlobSpawner.I.RandomInteriorPoint(), c, 1, value, false);
                yield return new WaitForSeconds(0.05f);
            }
        }

        public override void OnSorted(Blob b)
        {
            sorted++;
            UI.SetCounter("SORTED " + sorted);
        }

        public override void Cleanup()
        {
            foreach (Blob b in spawned) if (b != null) b.EventBlob = false;
            spawned.Clear();
        }
    }
}
