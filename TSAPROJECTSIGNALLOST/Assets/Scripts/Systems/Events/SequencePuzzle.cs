using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Power Surge: 3 to 6 wire nodes light up in a sequence; tap them back in the same order.</summary>
    public class SequencePuzzle : PuzzleEvent
    {
        private readonly List<int> sequence = new List<int>();
        private int index;
        private bool accepting;

        public SequencePuzzle(EventDef def, EventOverlay ui) : base(def, ui) { }

        public override void OnWarning()
        {
            if (GameManager.I.Station != null) GameManager.I.Station.SetSparking(true);
        }

        public override IEnumerator Play(float timeLimit)
        {
            int nodes = (int)Def.Param("node_count", 6);
            int min = (int)Def.Param("sequence_min", 3);
            int max = (int)Def.Param("sequence_max", 6);
            // Longer patterns as the Antenna grows, always between min and max.
            int length = Mathf.Clamp(min + (GameStats.Antenna - Def.unlockAntenna) / 2 + Random.Range(0, 2), min, max);
            sequence.Clear();
            int last = -1;
            for (int i = 0; i < length; i++)
            {
                int n;
                do { n = Random.Range(0, nodes); } while (n == last && nodes > 1);
                sequence.Add(n);
                last = n;
            }

            UI.CreateNodes(nodes, OnTap);
            UI.SetInstructions("WATCH THE WIRES...");
            float step = (float)Def.Param("show_step", 0.45);
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < sequence.Count; i++)
            {
                UI.LightNode(sequence[i], Palette.Cyan, step * 0.8f);
                AudioManager.Play("sort_ding", 0.8f + sequence[i] * 0.12f, 0.6f);
                yield return new WaitForSeconds(step);
            }

            UI.SetInstructions(Def.instructions);
            index = 0;
            accepting = true;
            yield return Countdown(timeLimit, () => index >= sequence.Count);
            accepting = false;
            Succeeded = index >= sequence.Count;
        }

        private void OnTap(int node)
        {
            if (!accepting) return;
            if (node == sequence[index])
            {
                UI.LightNode(node, Palette.Green, 0.25f);
                AudioManager.Play("sort_ding", 0.8f + node * 0.12f, 0.8f);
                index++;
                if (index >= sequence.Count) accepting = false;
            }
            else
            {
                // Never punishing: a wrong tap just restarts the input from the first node.
                UI.LightNode(node, Palette.Red, 0.3f);
                AudioManager.Play("boop");
                index = 0;
            }
            UI.SetCounter(index + " / " + sequence.Count);
        }

        public override void Cleanup()
        {
            if (GameManager.I.Station != null) GameManager.I.Station.SetSparking(false);
        }
    }
}
