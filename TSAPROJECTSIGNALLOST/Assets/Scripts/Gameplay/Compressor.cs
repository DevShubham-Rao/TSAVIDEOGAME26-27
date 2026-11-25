using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Feed blobs in; when it fills it shakes, glows and CRUNCHES, paying out every blob at once:
    /// payout = sum of blob values x Compressor Multiplier x current combo multiplier (x GlobalMult).
    /// </summary>
    public class Compressor : MonoBehaviour
    {
        public static Compressor I { get; private set; }

        [SerializeField] private Transform visual;
        [SerializeField] private Transform interior;
        [SerializeField] private Transform hopperMouth;
        [SerializeField] private Renderer[] bands = new Renderer[0];
        [SerializeField] private Transform fillGlow;
        [SerializeField] private Renderer fillRenderer;
        [SerializeField] private Light glowLight;
        [SerializeField] private float interiorRadius = 0.42f;
        [SerializeField] private float interiorHeight = 1.0f;

        private static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdRim = Shader.PropertyToID("_RimColor");
        private static readonly int IdBase = Shader.PropertyToID("_BaseColor");

        private readonly List<Blob> visibleInside = new List<Blob>();
        private readonly List<Blob> queued = new List<Blob>();
        private int count;
        private double storedValue;
        private bool crunching;
        private float shake;
        private float whiteGlow;
        private MaterialPropertyBlock mpb;
        private Vector3 visualBasePos;
        private float displayFill;

        /// <summary>Temporary capacity used by the tutorial's first crunch (0 = use the upgrade stat).</summary>
        public int CapacityOverride { get; set; }
        public int Capacity { get { return CapacityOverride > 0 ? CapacityOverride : GameStats.CompressorCapacity; } }

        /// <summary>Crunches now if the current contents already meet the threshold.</summary>
        public void Recheck()
        {
            if (count > 0) CheckCrunch();
        }
        public int Count { get { return count; } }
        public float Fill01 { get { return Mathf.Clamp01(count / (float)Mathf.Max(1, Capacity)); } }
        public bool Crunching { get { return crunching; } }
        public Vector3 Mouth { get { return hopperMouth != null ? hopperMouth.position : transform.position + Vector3.up * 1.6f; } }

        private void Awake()
        {
            I = this;
            mpb = new MaterialPropertyBlock();
            if (visual != null) visualBasePos = visual.localPosition;
        }

        public bool Contains(Vector3 planePoint)
        {
            Vector3 d = planePoint - transform.position;
            d.y = 0f;
            return d.magnitude <= Balance.B.compressor.feedRadius * ScaleFactor;
        }

        private float ScaleFactor
        {
            get { return 1f + Balance.B.compressor.scalePerLevel * (SkillTree.I != null ? SkillTree.I.TierCount("compressor_size") : 0); }
        }

        /// <summary>Clears anything left inside without paying (a new day starts empty).</summary>
        public void ResetContents()
        {
            StopAllCoroutines();
            crunching = false;
            shake = 0f;
            foreach (Blob b in visibleInside) if (b != null) BlobSpawner.I.Despawn(b);
            foreach (Blob b in queued) if (b != null) BlobSpawner.I.Despawn(b);
            visibleInside.Clear();
            queued.Clear();
            count = 0;
            storedValue = 0d;
        }

        /// <summary>Last call: crunch whatever is inside, even if it is not full.</summary>
        public void FlushCrunch()
        {
            if (!crunching && count > 0) StartCoroutine(Crunch());
        }

        public void Feed(Blob b)
        {
            if (b == null) return;
            if (crunching)
            {
                queued.Add(b);
                b.EnterCompressor(hopperMouth != null ? hopperMouth : transform, Vector3.zero);
                return;
            }
            AddInside(b);
            AudioManager.PlayVaried("squish", 0.6f, 0.75f, 0.7f);
            Juice.PunchScale(visual != null ? visual : transform, 1.05f, 0.15f);
            CheckCrunch();
        }

        private void AddInside(Blob b)
        {
            count++;
            storedValue += b.BaseValue;
            if (visibleInside.Count < Balance.B.compressor.maxVisibleInside && interior != null)
            {
                Vector2 r = Random.insideUnitCircle * interiorRadius;
                float y = Mathf.Lerp(-interiorHeight * 0.45f, interiorHeight * 0.45f, Mathf.Clamp01(visibleInside.Count / (float)Balance.B.compressor.maxVisibleInside));
                b.EnterCompressor(interior, new Vector3(r.x, y, r.y));
                visibleInside.Add(b);
            }
            else
            {
                BlobSpawner.I.Despawn(b);
            }
        }

        private void CheckCrunch()
        {
            if (crunching) return;
            int threshold = Capacity;
            if (GameStats.AutoFeed && CapacityOverride <= 0) threshold = Mathf.Max(1, Mathf.CeilToInt(Capacity * Balance.B.days.autoFeedAt));
            if (count >= threshold) StartCoroutine(Crunch());
        }

        private IEnumerator Crunch()
        {
            crunching = true;
            BalanceData B = Balance.B;
            float windup = B.compressor.crunchWindup / Mathf.Max(0.25f, GameStats.CrunchSpeed);
            float t = 0f;
            while (t < windup)
            {
                t += Time.deltaTime;
                shake = Mathf.Lerp(0.02f, 0.09f, t / windup);
                whiteGlow = t / windup;
                yield return null;
            }
            shake = 0f;

            double jackpotChance = GameStats.JackpotChance;
            bool jackpot = jackpotChance > 0d && Random.value < jackpotChance;
            double jackpotMult = jackpot ? GameStats.JackpotMult : 1d;
            // Combos already paid out as extra bits when the Static was popped, so they are not counted again here.
            double payout = storedValue * GameStats.CompressorMult * GameStats.GlobalMult
                          * GameStats.CrunchBuffMult * jackpotMult;

            Vector3 mouth = Mouth;
            AudioManager.Play("crunch");
            AudioManager.Play("coin_shower", 1f, 0.8f);
            Juice.Shake(B.juice.crunchShakeAmp * (jackpot ? 1.8f : 1f), B.juice.crunchShakeTime);
            Juice.PunchScale(visual != null ? visual : transform, 1.22f, 0.3f);
            VFXPlayer.Shockwave(transform.position + Vector3.up * 0.1f, 2.4f, Color.white);
            VFXPlayer.Pop(mouth, Color.white, 40);
            foreach (Blob b in visibleInside) VFXPlayer.Pop(b.Position, BlobActions.ColorOf(b), 6);
            if (jackpot)
            {
                VFXPlayer.Sparkle(mouth, 40);
                Juice.FloatText(mouth + Vector3.up * 1.2f, "JACKPOT!", Palette.Gold, 2f);
                AudioManager.Play("golden");
            }
            GameManager.I.Earn(payout, mouth, B.compressor.crunchCoins + (jackpot ? 30 : 0));
            Juice.FloatText(mouth + Vector3.up * 0.6f, "+" + NumberFormat.Format(payout), jackpot ? Palette.Gold : Palette.Cyan, 1.8f);
            GameManager.I.Tracking.crunches++;
            EventBus.RaiseCrunched(payout);

            foreach (Blob b in visibleInside) BlobSpawner.I.Despawn(b);
            visibleInside.Clear();
            count = 0;
            storedValue = 0d;
            whiteGlow = 1.6f;
            crunching = false;

            if (queued.Count > 0)
            {
                List<Blob> q = new List<Blob>(queued);
                queued.Clear();
                foreach (Blob b in q) AddInside(b);
                CheckCrunch();
            }
        }

        private void Update()
        {
            if (Balance.B == null || SkillTree.I == null) return;
            if (visual != null)
            {
                visual.localPosition = visualBasePos + (shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero);
            }
            // Compressor Size upgrade: the whole machine scales up 3% per level.
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * ScaleFactor, Time.deltaTime * 6f);
            whiteGlow = Mathf.MoveTowards(whiteGlow, 0f, Time.deltaTime * 2.5f);
            displayFill = Mathf.Lerp(displayFill, Fill01, Time.deltaTime * 8f);

            int multLevel = SkillTree.I.TierCount("crunch_bonus");
            float glow = 1.8f + Balance.B.compressor.glowPerLevel * multLevel + displayFill * 1.5f;
            Color band = Color.Lerp(Palette.Cyan, Color.white, Mathf.Clamp01(whiteGlow)) * (glow + whiteGlow * 4f);
            foreach (Renderer r in bands)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(IdEmission, band);
                r.SetPropertyBlock(mpb);
            }
            if (fillGlow != null)
            {
                Vector3 s = fillGlow.localScale;
                s.y = Mathf.Max(0.001f, displayFill);
                fillGlow.localScale = s;
                if (fillRenderer != null)
                {
                    Color fc = Color.Lerp(Palette.Cyan, Palette.Magenta, displayFill);
                    fillRenderer.GetPropertyBlock(mpb);
                    mpb.SetColor(IdRim, fc * (1.4f + displayFill * 2f + whiteGlow * 3f));
                    mpb.SetColor(IdBase, new Color(fc.r, fc.g, fc.b, 0.14f + displayFill * 0.12f));
                    fillRenderer.SetPropertyBlock(mpb);
                }
            }
            if (glowLight != null) glowLight.intensity = 1f + displayFill * 2f + whiteGlow * 6f;
        }
    }
}
