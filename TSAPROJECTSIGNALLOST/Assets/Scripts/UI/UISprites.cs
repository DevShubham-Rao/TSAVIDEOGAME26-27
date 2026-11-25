using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Procedural UI sprites: rounded panels, hexagons, rings, glows, ~25 icons and the 3 sender portraits.
    /// Drawn once with anti-aliased signed-distance shapes and cached. Replace with real art any time.
    /// </summary>
    public static class UISprites
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache.Clear();
            glyphTextures.Clear();
        }

        public static Sprite RoundedFill { get { return Get("rounded_fill", () => Rounded(64, 14f, -1f), 18); } }
        public static Sprite RoundedBorder { get { return Get("rounded_border", () => Rounded(64, 14f, 3f), 18); } }
        public static Sprite RoundedSmallFill { get { return Get("rounded_small_fill", () => Rounded(32, 7f, -1f), 9); } }
        public static Sprite Square { get { return Get("square", () => Solid(8), 0); } }
        public static Sprite Circle { get { return Get("circle", () => Disk(128, 0.5f, -1f), 0); } }
        public static Sprite Ring { get { return Get("ring", () => Disk(128, 0.5f, 0.07f), 0); } }
        public static Sprite ThinRing { get { return Get("thin_ring", () => Disk(128, 0.5f, 0.035f), 0); } }
        public static Sprite Glow { get { return Get("glow", () => Radial(128, false), 0); } }
        public static Sprite Vignette { get { return Get("vignette", () => Radial(256, true), 0); } }
        public static Sprite HexFill { get { return Get("hex_fill", () => Hex(128, -1f), 0); } }
        public static Sprite HexOutline { get { return Get("hex_outline", () => Hex(128, 6f), 0); } }
        public static Sprite Triangle { get { return Get("triangle", () => Poly(64, Tri()), 0); } }
        public static Sprite Star { get { return Get("star", () => Poly(64, StarPts(5, 0.48f, 0.2f)), 0); } }
        public static Sprite Scanlines { get { return Get("scanlines", ScanTex, 0); } }

        /// <summary>
        /// Tutorial spotlight: opaque everywhere except a soft round hole in the middle. The hole radius is
        /// 1/64 of the texture width, so an Image sized 64 x R has a hole of radius R.
        /// </summary>
        public static Sprite SpotlightHole { get { return Get("spotlight", SpotTex, 0); } }
        public const float SpotlightHoleFraction = 64f;

        private static readonly Dictionary<string, Texture2D> glyphTextures = new Dictionary<string, Texture2D>();

        /// <summary>Colorblind glyph: white shape with a thick dark outline (readable on any blob color).</summary>
        public static Texture2D GlyphTexture(string shape)
        {
            if (glyphTextures.TryGetValue(shape, out Texture2D t) && t != null) return t;
            Painter p = new Painter(128);
            Color ink = new Color(0.02f, 0.02f, 0.05f, 1f);
            for (int pass = 0; pass < 2; pass++)
            {
                Color c = pass == 0 ? ink : Color.white;
                float grow = pass == 0 ? 0.09f : 0f;
                switch (shape)
                {
                    case "triangle":
                        p.Polygon(new[] { new Vector2(0.5f, 0.88f + grow), new Vector2(0.9f + grow, 0.16f - grow * 0.6f), new Vector2(0.1f - grow, 0.16f - grow * 0.6f) }, c);
                        break;
                    case "circle":
                        p.Circle(0.5f, 0.5f, 0.32f + grow, c);
                        break;
                    case "square":
                        p.RoundRect(0.2f - grow, 0.2f - grow, 0.8f + grow, 0.8f + grow, 0.04f, c);
                        break;
                    case "star":
                        p.Polygon(StarPts(5, 0.44f + grow, 0.19f + grow * 0.6f), c);
                        break;
                    default:
                        p.Polygon(new[] { new Vector2(0.5f, 0.9f + grow), new Vector2(0.84f + grow, 0.5f), new Vector2(0.5f, 0.1f - grow), new Vector2(0.16f - grow, 0.5f) }, c);
                        break;
                }
            }
            t = p.ToTexture();
            t.name = "Glyph_" + shape;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.Apply(true, true);
            glyphTextures[shape] = t;
            return t;
        }

        private static Texture2D SpotTex()
        {
            const int n = 1024;
            Texture2D t = NewTex(n, n);
            Color32[] px = new Color32[n * n];
            float h = n * 0.5f;
            float hole = n / SpotlightHoleFraction;
            float soft = hole * 0.45f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - h, dy = y + 0.5f - h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - (hole - soft)) / soft);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            return t;
        }

        private delegate Texture2D Maker();

        private static Sprite Get(string key, Maker make, int border)
        {
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            Texture2D tex = make();
            tex.name = "UI_" + key;
            tex.wrapMode = key == "scanlines" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = key;
            cache[key] = s;
            return s;
        }

        // ---------------- base shapes ----------------

        private static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        private static Texture2D Solid(int n)
        {
            Texture2D t = NewTex(n, n);
            Color32[] px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px);
            return t;
        }

        private static float RoundedSdf(float px, float py, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(px) - (hw - r);
            float qy = Mathf.Abs(py) - (hh - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Texture2D Rounded(int n, float radius, float thickness)
        {
            Texture2D t = NewTex(n, n);
            Color32[] px = new Color32[n * n];
            float h = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = RoundedSdf(x + 0.5f - h, y + 0.5f - h, h - 1f, h - 1f, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (thickness > 0f) a *= Mathf.Clamp01(d + thickness + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            return t;
        }

        private static Texture2D Disk(int n, float radius01, float ring01)
        {
            Texture2D t = NewTex(n, n);
            Color32[] px = new Color32[n * n];
            float h = n * 0.5f;
            float r = radius01 * n - 1.5f;
            float th = ring01 * n;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - h, dy = y + 0.5f - h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (ring01 > 0f) a *= Mathf.Clamp01(d + th + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            return t;
        }

        private static Texture2D Radial(int n, bool inverted)
        {
            Texture2D t = NewTex(n, n);
            Color32[] px = new Color32[n * n];
            float h = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (inverted) a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.35f) / 0.75f));
                    else a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            return t;
        }

        private static Texture2D Hex(int n, float thickness)
        {
            Texture2D t = NewTex(n, n);
            Color32[] px = new Color32[n * n];
            float h = n * 0.5f;
            float r = h - 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // Pointy-top hexagon SDF.
                    float pxv = Mathf.Abs(y + 0.5f - h), pyv = Mathf.Abs(x + 0.5f - h);
                    const float kx = -0.866025404f, ky = 0.5f, kz = 0.577350269f;
                    float dot = Mathf.Min(kx * pxv + ky * pyv, 0f) * 2f;
                    pxv -= dot * kx;
                    pyv -= dot * ky;
                    float rr = r * 0.866f;
                    pxv -= Mathf.Clamp(pxv, -kz * rr, kz * rr);
                    pyv -= rr;
                    float d = Mathf.Sqrt(pxv * pxv + pyv * pyv) * Mathf.Sign(pyv);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (thickness > 0f) a *= Mathf.Clamp01(d + thickness + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            return t;
        }

        private static Vector2[] Tri()
        {
            return new[] { new Vector2(0.5f, 0.92f), new Vector2(0.94f, 0.12f), new Vector2(0.06f, 0.12f) };
        }

        private static Vector2[] StarPts(int points, float outer, float inner)
        {
            Vector2[] pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
                pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r - 0.02f);
            }
            return pts;
        }

        private static Texture2D Poly(int n, Vector2[] pts)
        {
            Painter p = new Painter(n);
            p.Polygon(pts, Color.white);
            return p.ToTexture();
        }

        private static Texture2D ScanTex()
        {
            Texture2D t = NewTex(4, 4);
            Color32[] px = new Color32[16];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, (byte)(y < 2 ? 40 : 0));
            }
            t.SetPixels32(px);
            return t;
        }

        // ---------------- icons ----------------

        /// <summary>White icon masks (tint with Image.color). Unknown ids fall back to a star.</summary>
        public static Sprite Icon(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "star";
            return Get("icon_" + id, () => PaintIcon(id), 0);
        }

        private static Texture2D PaintIcon(string id)
        {
            Painter p = new Painter(96);
            Color w = Color.white;
            switch (id)
            {
                case "pop":
                case "click_data":
                    p.Polygon(StarPts(8, 0.46f, 0.24f), w);
                    p.Circle(0.5f, 0.48f, 0.12f, Color.clear, true);
                    break;
                case "radius":
                case "suction":
                    p.Ring(0.5f, 0.5f, 0.42f, 0.06f, w);
                    p.Ring(0.5f, 0.5f, 0.27f, 0.06f, w);
                    p.Circle(0.5f, 0.5f, 0.1f, w);
                    break;
                case "capacity":
                    p.Circle(0.33f, 0.36f, 0.17f, w);
                    p.Circle(0.67f, 0.36f, 0.17f, w);
                    p.Circle(0.5f, 0.66f, 0.17f, w);
                    break;
                case "compressor":
                    p.RoundRect(0.22f, 0.1f, 0.78f, 0.9f, 0.08f, w);
                    p.RoundRect(0.3f, 0.3f, 0.7f, 0.36f, 0.02f, Color.clear, true);
                    p.RoundRect(0.3f, 0.5f, 0.7f, 0.56f, 0.02f, Color.clear, true);
                    p.RoundRect(0.3f, 0.7f, 0.7f, 0.76f, 0.02f, Color.clear, true);
                    break;
                case "crunch":
                case "crunch_payout":
                    p.Polygon(new[] { new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.9f), new Vector2(0.5f, 0.55f) }, w);
                    p.Polygon(new[] { new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.45f), new Vector2(0.9f, 0.1f) }, w);
                    break;
                case "magnet":
                    p.Ring(0.5f, 0.45f, 0.32f, 0.13f, w);
                    p.RoundRect(0.12f, 0.45f, 0.9f, 0.92f, 0f, Color.clear, true);
                    p.RoundRect(0.18f, 0.45f, 0.36f, 0.8f, 0.02f, w);
                    p.RoundRect(0.64f, 0.45f, 0.82f, 0.8f, 0.02f, w);
                    break;
                case "drone":
                case "extra_drones":
                    p.Circle(0.5f, 0.5f, 0.2f, w);
                    p.Ring(0.5f, 0.5f, 0.4f, 0.06f, w);
                    p.RoundRect(0.04f, 0.44f, 0.2f, 0.56f, 0.03f, w);
                    p.RoundRect(0.8f, 0.44f, 0.96f, 0.56f, 0.03f, w);
                    break;
                case "overclock":
                case "drone_speed":
                case "speed":
                    p.Polygon(new[] { new Vector2(0.58f, 0.95f), new Vector2(0.22f, 0.45f), new Vector2(0.48f, 0.45f), new Vector2(0.4f, 0.05f), new Vector2(0.78f, 0.58f), new Vector2(0.52f, 0.58f) }, w);
                    break;
                case "sort":
                case "sort_mult":
                    p.Polygon(new[] { new Vector2(0.1f, 0.85f), new Vector2(0.9f, 0.85f), new Vector2(0.58f, 0.42f), new Vector2(0.42f, 0.42f) }, w);
                    p.RoundRect(0.42f, 0.1f, 0.58f, 0.45f, 0.02f, w);
                    break;
                case "gold":
                case "golden_chance":
                    p.Polygon(new[] { new Vector2(0.5f, 0.95f), new Vector2(0.88f, 0.55f), new Vector2(0.5f, 0.05f), new Vector2(0.12f, 0.55f) }, w);
                    break;
                case "rhythm":
                case "combo_window":
                    p.RoundRect(0.14f, 0.1f, 0.3f, 0.55f, 0.04f, w);
                    p.RoundRect(0.42f, 0.1f, 0.58f, 0.9f, 0.04f, w);
                    p.RoundRect(0.7f, 0.1f, 0.86f, 0.7f, 0.04f, w);
                    break;
                case "antenna":
                    p.Polygon(new[] { new Vector2(0.5f, 0.7f), new Vector2(0.78f, 0.05f), new Vector2(0.22f, 0.05f) }, w);
                    p.Polygon(new[] { new Vector2(0.5f, 0.55f), new Vector2(0.62f, 0.2f), new Vector2(0.38f, 0.2f) }, Color.clear, true);
                    p.Circle(0.5f, 0.8f, 0.1f, w);
                    p.Ring(0.5f, 0.8f, 0.22f, 0.04f, w);
                    break;
                case "luck":
                    p.Circle(0.5f, 0.7f, 0.17f, w);
                    p.Circle(0.3f, 0.5f, 0.17f, w);
                    p.Circle(0.7f, 0.5f, 0.17f, w);
                    p.Circle(0.5f, 0.3f, 0.17f, w);
                    p.RoundRect(0.47f, 0.04f, 0.53f, 0.3f, 0.02f, w);
                    break;
                case "charm":
                    p.Circle(0.33f, 0.62f, 0.2f, w);
                    p.Circle(0.67f, 0.62f, 0.2f, w);
                    p.Polygon(new[] { new Vector2(0.14f, 0.56f), new Vector2(0.86f, 0.56f), new Vector2(0.5f, 0.1f) }, w);
                    break;
                case "all_data":
                case "coin":
                    p.Polygon(HexPts(0.44f), w);
                    p.Polygon(HexPts(0.28f), Color.clear, true);
                    p.Polygon(HexPts(0.18f), w);
                    break;
                case "xp_mult":
                    p.Polygon(new[] { new Vector2(0.5f, 0.92f), new Vector2(0.88f, 0.5f), new Vector2(0.62f, 0.5f), new Vector2(0.62f, 0.08f), new Vector2(0.38f, 0.08f), new Vector2(0.38f, 0.5f), new Vector2(0.12f, 0.5f) }, w);
                    break;
                case "golden_hour":
                    p.Circle(0.5f, 0.5f, 0.22f, w);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        p.Line(0.5f + Mathf.Cos(a) * 0.3f, 0.5f + Mathf.Sin(a) * 0.3f, 0.5f + Mathf.Cos(a) * 0.44f, 0.5f + Mathf.Sin(a) * 0.44f, 0.05f, w);
                    }
                    break;
                case "mail":
                    p.RoundRect(0.08f, 0.2f, 0.92f, 0.8f, 0.06f, w);
                    p.Line(0.12f, 0.76f, 0.5f, 0.45f, 0.055f, Color.clear, true);
                    p.Line(0.88f, 0.76f, 0.5f, 0.45f, 0.055f, Color.clear, true);
                    break;
                case "gear":
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        p.Line(0.5f, 0.5f, 0.5f + Mathf.Cos(a) * 0.44f, 0.5f + Mathf.Sin(a) * 0.44f, 0.13f, w);
                    }
                    p.Circle(0.5f, 0.5f, 0.32f, w);
                    p.Circle(0.5f, 0.5f, 0.14f, Color.clear, true);
                    break;
                case "skills":
                    p.Polygon(HexPts(0.45f), w);
                    p.Polygon(HexPts(0.32f), Color.clear, true);
                    p.Polygon(StarPts(4, 0.2f, 0.08f), w);
                    break;
                case "pause":
                    p.RoundRect(0.24f, 0.18f, 0.42f, 0.82f, 0.04f, w);
                    p.RoundRect(0.58f, 0.18f, 0.76f, 0.82f, 0.04f, w);
                    break;
                case "close":
                    p.Line(0.2f, 0.2f, 0.8f, 0.8f, 0.1f, w);
                    p.Line(0.2f, 0.8f, 0.8f, 0.2f, 0.1f, w);
                    break;
                case "shield":
                    p.Polygon(new[] { new Vector2(0.5f, 0.95f), new Vector2(0.9f, 0.8f), new Vector2(0.84f, 0.38f), new Vector2(0.5f, 0.05f), new Vector2(0.16f, 0.38f), new Vector2(0.1f, 0.8f) }, w);
                    break;
                case "spark":
                    p.Polygon(StarPts(4, 0.48f, 0.12f), w);
                    p.Circle(0.5f, 0.48f, 0.12f, w);
                    break;
                case "clicker":
                    p.Polygon(new[] { new Vector2(0.25f, 0.95f), new Vector2(0.25f, 0.2f), new Vector2(0.42f, 0.35f), new Vector2(0.55f, 0.05f), new Vector2(0.67f, 0.1f), new Vector2(0.54f, 0.4f), new Vector2(0.78f, 0.42f) }, w);
                    break;
                case "idle":
                    p.Ring(0.5f, 0.5f, 0.42f, 0.08f, w);
                    p.Line(0.5f, 0.5f, 0.5f, 0.8f, 0.07f, w);
                    p.Line(0.5f, 0.5f, 0.72f, 0.4f, 0.07f, w);
                    break;
                case "lucky":
                    p.Polygon(StarPts(5, 0.46f, 0.2f), w);
                    break;
                default:
                    p.Polygon(StarPts(5, 0.46f, 0.2f), w);
                    break;
            }
            return p.ToTexture();
        }

        private static Vector2[] HexPts(float r)
        {
            Vector2[] pts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI / 6f + i * Mathf.PI / 3f;
                pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r);
            }
            return pts;
        }

        // ---------------- portraits ----------------

        public static Sprite Portrait(string id)
        {
            return Get("portrait_" + (id ?? "unknown"), () => PaintPortrait(id), 0);
        }

        private static Texture2D PaintPortrait(string id)
        {
            Painter p = new Painter(160);
            Color bg = Palette.Hex("#0B1026");
            p.RoundRect(0f, 0f, 1f, 1f, 0.12f, bg);
            switch (id)
            {
                case "hale":
                    {
                        Color skin = Palette.Hex("#C9A27E");
                        Color uniform = Palette.Hex("#26355E");
                        p.RoundRect(0.15f, 0f, 0.85f, 0.32f, 0.12f, uniform);
                        p.RoundRect(0.42f, 0.2f, 0.58f, 0.34f, 0.02f, skin);
                        p.RoundRect(0.26f, 0.3f, 0.74f, 0.8f, 0.14f, skin);
                        p.RoundRect(0.22f, 0.7f, 0.78f, 0.86f, 0.05f, Palette.Hex("#1A2340"));
                        p.RoundRect(0.18f, 0.68f, 0.82f, 0.73f, 0.02f, Palette.Hex("#1A2340"));
                        p.RoundRect(0.44f, 0.75f, 0.56f, 0.83f, 0.02f, Palette.Gold);
                        p.RoundRect(0.33f, 0.56f, 0.45f, 0.6f, 0.01f, Palette.Hex("#3A2A20"));
                        p.RoundRect(0.55f, 0.56f, 0.67f, 0.6f, 0.01f, Palette.Hex("#3A2A20"));
                        p.Circle(0.39f, 0.52f, 0.03f, Palette.Hex("#1B1B1B"));
                        p.Circle(0.61f, 0.52f, 0.03f, Palette.Hex("#1B1B1B"));
                        p.RoundRect(0.36f, 0.38f, 0.64f, 0.43f, 0.02f, Palette.Hex("#5A4030"));
                        p.RoundRect(0.42f, 0.34f, 0.58f, 0.36f, 0.01f, Palette.Hex("#7A3A30"));
                        break;
                    }
                case "iva":
                    {
                        Color skin = Palette.Hex("#8E6248");
                        Color hair = Palette.Hex("#2A1A3A");
                        p.RoundRect(0.15f, 0f, 0.85f, 0.3f, 0.12f, Palette.Hex("#E8EEF8"));
                        p.Circle(0.5f, 0.56f, 0.3f, hair);
                        p.RoundRect(0.42f, 0.2f, 0.58f, 0.34f, 0.02f, skin);
                        p.Circle(0.5f, 0.52f, 0.22f, skin);
                        p.Circle(0.5f, 0.8f, 0.14f, hair);
                        p.Ring(0.41f, 0.53f, 0.07f, 0.018f, Palette.Cyan);
                        p.Ring(0.59f, 0.53f, 0.07f, 0.018f, Palette.Cyan);
                        p.Line(0.47f, 0.53f, 0.53f, 0.53f, 0.015f, Palette.Cyan);
                        p.Circle(0.41f, 0.53f, 0.025f, Palette.Hex("#1B1B1B"));
                        p.Circle(0.59f, 0.53f, 0.025f, Palette.Hex("#1B1B1B"));
                        p.RoundRect(0.43f, 0.39f, 0.57f, 0.42f, 0.015f, Palette.Hex("#5A2A2A"));
                        break;
                    }
                case "static":
                    {
                        p.Circle(0.5f, 0.48f, 0.34f, Palette.Magenta * 0.5f);
                        p.Circle(0.46f, 0.52f, 0.3f, Palette.Red);
                        p.Circle(0.56f, 0.44f, 0.22f, Palette.Blue * 0.95f);
                        p.Circle(0.4f, 0.4f, 0.16f, Palette.Green * 0.9f);
                        p.Circle(0.42f, 0.56f, 0.06f, Color.white);
                        p.Circle(0.6f, 0.56f, 0.06f, Color.white);
                        p.Circle(0.43f, 0.55f, 0.03f, Color.black);
                        p.Circle(0.61f, 0.55f, 0.03f, Color.black);
                        for (int i = 0; i < 9; i++)
                        {
                            float y = 0.1f + i * 0.095f;
                            p.RoundRect(0.05f + (i % 3) * 0.1f, y, 0.95f - (i % 2) * 0.2f, y + 0.012f, 0f, new Color(1f, 1f, 1f, 0.18f));
                        }
                        break;
                    }
                case "supply_bot":
                    {
                        Color metal = Palette.Hex("#9AA8C8");
                        p.RoundRect(0.25f, 0.1f, 0.75f, 0.45f, 0.06f, metal * 0.8f);
                        p.RoundRect(0.22f, 0.4f, 0.78f, 0.82f, 0.1f, metal);
                        p.RoundRect(0.3f, 0.5f, 0.7f, 0.72f, 0.06f, Palette.Hex("#0B1026"));
                        p.Circle(0.4f, 0.61f, 0.05f, Palette.Green);
                        p.Circle(0.6f, 0.61f, 0.05f, Palette.Green);
                        p.Line(0.5f, 0.82f, 0.5f, 0.93f, 0.02f, metal);
                        p.Circle(0.5f, 0.94f, 0.03f, Palette.Red);
                        break;
                    }
                default:
                    p.Circle(0.5f, 0.5f, 0.3f, Palette.Cyan);
                    break;
            }
            return p.ToTexture();
        }

        /// <summary>Tiny anti-aliased rasterizer used for icons and portraits (coordinates in 0..1).</summary>
        private class Painter
        {
            private readonly int n;
            private readonly Color[] px;

            public Painter(int size)
            {
                n = size;
                px = new Color[n * n];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
            }

            private void Blend(int i, Color c, float cover, bool erase)
            {
                if (cover <= 0f) return;
                if (erase)
                {
                    px[i].a *= 1f - cover;
                    return;
                }
                float a = c.a * cover;
                Color dst = px[i];
                float outA = a + dst.a * (1f - a);
                if (outA <= 0.0001f) return;
                Color rgb = (new Color(c.r, c.g, c.b) * a + new Color(dst.r, dst.g, dst.b) * dst.a * (1f - a)) / outA;
                px[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
            }

            private void Shape(System.Func<float, float, float> sdf, Color c, bool erase)
            {
                float scale = n;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                        float d = sdf(u, v) * scale;
                        Blend(y * n + x, c, Mathf.Clamp01(0.5f - d), erase);
                    }
                }
            }

            public void Circle(float cx, float cy, float r, Color c, bool erase = false)
            {
                Shape((u, v) => Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r, c, erase);
            }

            public void Ring(float cx, float cy, float r, float th, Color c, bool erase = false)
            {
                Shape((u, v) => Mathf.Abs(Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r) - th * 0.5f, c, erase);
            }

            public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c, bool erase = false)
            {
                float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hw = (x1 - x0) * 0.5f, hh = (y1 - y0) * 0.5f;
                float rr = Mathf.Min(r, Mathf.Min(hw, hh));
                Shape((u, v) => RoundedSdf(u - cx, v - cy, hw, hh, rr), c, erase);
            }

            public void Line(float x0, float y0, float x1, float y1, float th, Color c, bool erase = false)
            {
                Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
                Shape((u, v) =>
                {
                    Vector2 p = new Vector2(u, v);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    return (p - (a + ab * t)).magnitude - th * 0.5f;
                }, c, erase);
            }

            public void Polygon(Vector2[] pts, Color c, bool erase = false)
            {
                Shape((u, v) => PolySdf(pts, new Vector2(u, v)), c, erase);
            }

            private static float PolySdf(Vector2[] poly, Vector2 p)
            {
                float d = (p - poly[0]).sqrMagnitude;
                float s = 1f;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i, i++)
                {
                    Vector2 e = poly[j] - poly[i];
                    Vector2 w = p - poly[i];
                    Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(1e-9f, Vector2.Dot(e, e)));
                    d = Mathf.Min(d, b.sqrMagnitude);
                    bool c1 = p.y >= poly[i].y, c2 = p.y < poly[j].y, c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1f;
                }
                return s * Mathf.Sqrt(d);
            }

            public Texture2D ToTexture()
            {
                Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                t.SetPixels(px);
                return t;
            }
        }
    }
}
