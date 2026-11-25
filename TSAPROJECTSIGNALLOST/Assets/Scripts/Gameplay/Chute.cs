using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Color sorting chute (a funnel with a glowing rim). Fling matching blobs in for combo + bonus Data.
    /// Catch radius grows with Suction, so chutes "magnet" blobs that land close.
    /// </summary>
    public class Chute : MonoBehaviour
    {
        public static readonly List<Chute> All = new List<Chute>();

        [SerializeField] private BlobColor color = BlobColor.Red;
        [SerializeField] private Renderer rimRenderer;
        [SerializeField] private SphereCollider catchTrigger;
        [SerializeField] private Transform mouth;
        [SerializeField] private Light glowLight;

        private static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdBase = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock mpb;
        private float flash;
        private float wrongFlash;
        private float fedGlow;
        private float baseRadius = 0.8f;
        private ShapeGlyph glyph;

        /// <summary>Tutorial tip "Match colors in the chutes": makes every chute pulse brightly.</summary>
        public static bool TutorialGlow { get; set; }

        public BlobColor Color { get { return color; } }
        public Vector3 Mouth { get { return mouth != null ? mouth.position : transform.position + Vector3.up * 0.6f; } }

        public float CatchRadius { get { return GameStats.ChuteCatchRadius; } }

        /// <summary>Universal Chute node: the middle chute takes every color and the other two fold away.</summary>
        public bool IsUniversal { get { return GameStats.UniversalChute && color == BlobColor.Blue; } }
        public bool IsHidden { get { return GameStats.UniversalChute && color != BlobColor.Blue; } }

        private Vector3 baseScale = Vector3.one;
        private float universalBlend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            TutorialGlow = false;
        }

        public void Configure(BlobColor c)
        {
            color = c;
        }

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            baseScale = transform.localScale;
            if (catchTrigger != null) baseRadius = catchTrigger.radius;
            glyph = ShapeGlyph.Create(mouth != null ? mouth : transform, ShapeGlyph.Mode.Floating);
        }

        private void Start()
        {
            if (glyph != null) glyph.SetShape(ShapeGlyph.ShapeFor(color));
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>True when a point on the play plane is within this chute's catch radius.</summary>
        public bool Contains(Vector3 planePoint)
        {
            if (IsHidden) return false;
            Vector3 d = planePoint - transform.position;
            d.y = 0f;
            return d.magnitude <= CatchRadius;
        }

        /// <summary>
        /// Chute under the mouse, judged on SCREEN position: the chute's glowing rim vs where the cursor is.
        /// The cursor lives on a plane above the ground, so a world-space test missed chutes that looked
        /// hit. Picks the nearest rim inside the screen radius.
        /// </summary>
        public static Chute FindAtScreen(Vector2 pointer)
        {
            Camera cam = GameManager.I != null ? GameManager.I.Cam : null;
            if (cam == null) return null;
            Chute best = null;
            float bestD = float.MaxValue;
            foreach (Chute c in All)
            {
                if (c.IsHidden) continue;
                Vector3 sp = cam.WorldToScreenPoint(c.Mouth);
                if (sp.z < 0f) continue;
                float radius = Screen.height * 0.075f * (c.CatchRadius / 0.8f) * (c.IsUniversal ? 1.4f : 1f);
                float d = Vector2.Distance(pointer, new Vector2(sp.x, sp.y));
                if (d <= radius && d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        public static Chute FindAt(Vector3 planePoint)
        {
            Chute best = null;
            float bestD = float.MaxValue;
            foreach (Chute c in All)
            {
                if (!c.Contains(planePoint)) continue;
                Vector3 d = planePoint - c.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < bestD)
                {
                    bestD = d.sqrMagnitude;
                    best = c;
                }
            }
            return best;
        }

        public static Chute ForColor(BlobColor c)
        {
            foreach (Chute ch in All) if (ch.IsUniversal) return ch;
            foreach (Chute ch in All) if (ch.color == c) return ch;
            return All.Count > 0 ? All[0] : null;
        }

        /// <summary>Where a bit belongs: its color's chute (Gold and Prism bits use the nearest chute).</summary>
        public static Chute ForBit(Blob b)
        {
            if (b == null) return null;
            foreach (Chute ch in All) if (ch.IsUniversal) return ch;
            if (b.Color == BlobColor.Gold || b.IsPrism)
            {
                Chute best = null;
                float bestD = float.MaxValue;
                foreach (Chute ch in All)
                {
                    if (ch.IsHidden) continue;
                    float d = (ch.transform.position - b.Position).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = ch;
                    }
                }
                return best;
            }
            foreach (Chute ch in All) if (ch.color == b.Color) return ch;
            return null;
        }

        public void Fed(bool correct)
        {
            if (correct)
            {
                flash = 1f;
                fedGlow = 1f;
                Juice.PunchScale(transform, 1.12f, 0.18f);
            }
            else
            {
                wrongFlash = 1f;
                Juice.PunchScale(transform, 0.9f, 0.2f);
            }
        }

        private void Update()
        {
            if (Balance.B == null) return;
            if (catchTrigger != null) catchTrigger.radius = baseRadius * CatchRadius / Mathf.Max(0.01f, Balance.B.sort.catchRadius);
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 3f);
            wrongFlash = Mathf.MoveTowards(wrongFlash, 0f, Time.deltaTime * 3f);
            fedGlow = Mathf.MoveTowards(fedGlow, 0f, Time.deltaTime * 0.8f);

            // Universal Chute: the side chutes sink away, the middle one grows and glows every color.
            universalBlend = Mathf.MoveTowards(universalBlend, GameStats.UniversalChute ? 1f : 0f, Time.deltaTime * 1.5f);
            float targetScale = IsHidden ? Mathf.Lerp(1f, 0.001f, universalBlend) : (color == BlobColor.Blue ? 1f + 0.35f * universalBlend : 1f);
            Vector3 want = baseScale * targetScale;
            transform.localScale = Vector3.Lerp(transform.localScale, want, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (glyph != null) glyph.gameObject.SetActive(!GameStats.UniversalChute);

            if (rimRenderer == null) return;
            UnityEngine.Color c = IsUniversal ? Palette.Rainbow(Time.time * 0.25f) : Palette.ForBlob(color);
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 3f + (int)color);
            float intensity = 2.2f * pulse + flash * 5f + fedGlow * 1.2f;
            if (TutorialGlow) intensity += 2.5f + 2f * Mathf.Sin(Time.unscaledTime * 6f);
            UnityEngine.Color emission = UnityEngine.Color.Lerp(c, UnityEngine.Color.white, flash * 0.5f) * intensity;
            if (wrongFlash > 0f) emission = UnityEngine.Color.Lerp(emission, new UnityEngine.Color(1f, 0.3f, 0.3f) * 2f, wrongFlash * 0.4f);
            rimRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(IdEmission, emission);
            mpb.SetColor(IdBase, c);
            rimRenderer.SetPropertyBlock(mpb);
            if (glowLight != null)
            {
                glowLight.color = c;
                glowLight.intensity = 1.2f + flash * 4f + fedGlow;
            }
        }
    }
}
