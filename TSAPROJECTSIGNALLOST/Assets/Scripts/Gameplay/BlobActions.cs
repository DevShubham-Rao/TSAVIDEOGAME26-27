using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// The one production line: Pop Static (clicks) so it drops Data bits, Sweep the bits up with the
    /// vacuum, Sort them into the matching color chute, and the chutes feed the Compressor, which is the
    /// only place that pays Data (on the Crunch).
    /// </summary>
    public static class BlobActions
    {
        private static int clickCount;
        private static readonly List<Blob> temp = new List<Blob>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            clickCount = 0;
        }

        public static int ClickCount { get { return clickCount; } }

        public static Color ColorOf(Blob b)
        {
            return b.IsPrism ? Palette.Rainbow(Time.time * 0.5f) : Palette.ForBlob(b.Color);
        }

        /// <summary>Behind the main menu blobs can be popped for fun, but nothing drops.</summary>
        public static bool Scoring
        {
            get { return GameManager.I != null && (GameManager.I.State == GameState.Playing || GameManager.I.State == GameState.Finale); }
        }

        // ---------------- Pop ----------------

        public static void Click(Blob b)
        {
            if (b == null || !b.IsPoppable) return;
            clickCount++;
            Vector3 pos = b.Position;
            bool crit = Scoring && GameStats.CritChance > 0d && Random.value < GameStats.CritChance;

            if (crit)
            {
                Juice.Shake(Balance.B.juice.critShakeAmp, 0.2f);
                Juice.FloatText(pos + Vector3.up * 0.6f, "CRIT!", Palette.Magenta, 1.3f);
                Pop(b, PopCause.Crit, GameStats.CritBitsMult, 0);
            }
            else if (b.Squish(GameStats.ClickPower))
            {
                Pop(b, PopCause.Click, 1d, 0);
            }
            else
            {
                AudioManager.PlayVaried("squish", 0.9f, 1.1f, 0.8f);
                VFXPlayer.Pop(pos, ColorOf(b), 4);
            }

            // Pop Area: the click also squishes Static right next to it.
            float area = Scoring ? GameStats.PopArea : 0f;
            if (area > 0f)
            {
                BlobSpawner.I.CollectInRadius(pos, area + b.Radius, temp, x => x.IsPoppable && x != b);
                foreach (Blob n in temp.ToArray())
                {
                    if (n.Squish(GameStats.ClickPower)) Pop(n, PopCause.Click, 1d, 0);
                }
            }

            int every = Scoring ? GameStats.ShockwaveEvery : 0;
            if (every > 0 && clickCount % every == 0) Shockwave(pos);
        }

        /// <summary>Shockwave Pop: every 25th click pops all Static within 2 m.</summary>
        public static void Shockwave(Vector3 pos)
        {
            float radius = GameStats.ShockwaveRadius;
            VFXPlayer.Shockwave(pos, radius, Palette.Cyan);
            AudioManager.Play("whoosh", 1.2f);
            Juice.Shake(0.1f, 0.25f);
            BlobSpawner.I.CollectInRadius(pos, radius, temp, x => x.IsPoppable);
            foreach (Blob b in temp.ToArray()) Pop(b, PopCause.Shockwave, 1d, 0);
        }

        public static bool IsPlayerCause(PopCause c)
        {
            return c != PopCause.Drone;
        }

        /// <summary>Pops Static. bitsMult multiplies the bits it drops (Crit Pop).</summary>
        public static void Pop(Blob b, PopCause cause, double bitsMult, int chainDepth)
        {
            if (b == null || b.State == BlobState.Popping || b.IsBit) return;
            bool byPlayer = IsPlayerCause(cause);
            if (byPlayer && Scoring) ComboTracker.I.Register();
            int depth = chainDepth;
            b.BeginPop(x => FinishPop(x, cause, bitsMult, depth));
        }

        private static void FinishPop(Blob b, PopCause cause, double bitsMult, int chainDepth)
        {
            BalanceData B = Balance.B;
            Vector3 pos = b.Position;
            Color col = ColorOf(b);
            bool byPlayer = IsPlayerCause(cause);
            bool golden = b.IsGolden;
            bool crit = bitsMult > 1d;
            BlobColor color = b.Color;
            float radius = b.Radius;
            BlobTypeDef type = b.Type;

            int particles = Random.Range(B.juice.popParticlesMin, B.juice.popParticlesMax + 1);
            if (b.IsMega) particles = (int)(particles * 1.6f);
            if (!byPlayer) particles = Mathf.Max(6, particles / 2);
            VFXPlayer.Pop(pos, col, particles);
            if (golden) VFXPlayer.Sparkle(pos, 16);

            float pitch = 1f;
            if (byPlayer) pitch = Mathf.Min(1f + ComboTracker.I.Combo * B.juice.popPitchStep, B.juice.popPitchMax);
            AudioManager.Play("pop", pitch * Random.Range(0.97f, 1.03f), byPlayer ? 0.9f : 0.4f);
            if (byPlayer && cause != PopCause.Chain) Juice.Hitstop(B.juice.popHitstop);

            if (!Scoring)
            {
                // Menu pop: all the juice, nothing drops.
                EventBus.RaiseBlobPopped(b, 0d);
                BlobSpawner.I.Despawn(b);
                return;
            }

            // Drop Data bits. Combo streaks add bits; the value is split evenly between them.
            int baseBits = golden ? B.bits.goldenBits : (b.IsMega ? B.mega.splitCount + 2 : (type != null ? Mathf.Max(1, type.bits) : 1));
            int comboBonus = byPlayer ? Mathf.FloorToInt(Mathf.Max(0f, ComboTracker.I.Mult - 1f) * baseBits) : 0;
            int bits = Mathf.Max(1, (int)System.Math.Round((baseBits + comboBonus + GameStats.ExtraBitsPerPop) * bitsMult));
            double value = b.BaseValue * bits / Mathf.Max(1, baseBits);
            BlobSpawner.I.SpawnBits(pos, color, bits, value, golden);
            if (bits > 1 || golden) Juice.FloatText(pos + Vector3.up * 0.4f, "+" + bits + " BITS", golden ? Palette.Gold : (crit ? Palette.Magenta : Color.white), golden ? 1.6f : 0.9f);

            GameManager.I.Tracking.blobsPopped++;
            if (golden)
            {
                GameManager.I.Tracking.goldenPopped++;
                AudioManager.Play("golden");
                EventBus.RaiseGoldenPopped();
            }
            EventBus.RaiseBlobPopped(b, value);
            BlobSpawner.I.Despawn(b);

            if (type != null && type.split > 0 && !golden) Split(pos, Palette.ParseColor(type.splitInto == "static" ? "red" : type.splitInto), type.split, 0.7f);
            else if (b.IsMega) Split(pos, color, B.mega.splitCount, B.mega.splitChildScale);

            if (byPlayer && GameStats.ChainPop && chainDepth < 8)
            {
                Blob next = BlobSpawner.I.FindNearest(pos, radius + 0.75f, x => x.IsPoppable && x.Color == color && !x.IsGolden);
                if (next != null) Juice.Run(ChainAfter(next, chainDepth + 1));
            }
        }

        private static IEnumerator ChainAfter(Blob next, int depth)
        {
            yield return new WaitForSeconds(0.06f);
            if (next != null && next.IsPoppable)
            {
                VFXPlayer.Sparkle(next.Position, 4, ColorOf(next));
                Pop(next, PopCause.Chain, 1d, depth);
            }
        }

        /// <summary>Green Static and Mega Static split into smaller Static when popped.</summary>
        private static void Split(Vector3 pos, BlobColor color, int count, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count + Random.Range(-0.3f, 0.3f);
                Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.6f;
                Blob child = BlobSpawner.I.SpawnColor(color, pos, scale, false);
                if (child != null) child.FlyTo(BlobSpawner.I.ClampToField(pos + offset * 1.6f), 0.35f, 0.8f, x => x.ReturnToDrift());
            }
            VFXPlayer.Shockwave(pos, 1.2f, Palette.ForBlob(color));
        }

        // ---------------- Sort ----------------

        /// <summary>Gold and Prism bits fit any chute; the Universal Chute takes every color.</summary>
        public static bool Matches(Blob b, Chute c)
        {
            return c.IsUniversal || b.IsPrism || b.Color == BlobColor.Gold || b.Color == c.Color;
        }

        /// <summary>A bit arrives at a chute: a match slides down into the Compressor, a mismatch bounces out.</summary>
        public static void Sort(Blob b, Chute chute, bool byPlayer)
        {
            if (b == null || chute == null) return;
            Vector3 mouth = chute.Mouth;
            if (Matches(b, chute))
            {
                if (byPlayer && Scoring) ComboTracker.I.Register();
                chute.Fed(true);
                float pitch = byPlayer ? Mathf.Min(1f + ComboTracker.I.Combo * 0.012f, 1.8f) : 1f;
                AudioManager.Play("sort_ding", pitch, byPlayer ? 0.55f : 0.25f);
                VFXPlayer.Pop(mouth, chute.IsUniversal ? ColorOf(b) : Palette.ForBlob(chute.Color), byPlayer ? 6 : 3);
                GameManager.I.Tracking.correctSorts++;
                if (EventDirector.I != null) EventDirector.I.NotifySorted(b);
                EventBus.RaiseSorted(true);
                float travel = 0.45f / Mathf.Max(0.25f, GameStats.CrunchSpeed);
                b.FlyTo(Compressor.I.Mouth, travel, 1.6f, x => Compressor.I.Feed(x));
            }
            else
            {
                // A wrong chute never costs the combo.
                chute.Fed(false);
                AudioManager.Play("sort_bonk");
                EventBus.RaiseSorted(false);
                b.BounceOut(mouth);
            }
        }
    }
}
