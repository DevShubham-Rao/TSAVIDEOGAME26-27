using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Renders ???'s messages ("speaks in colors and shapes") as glowing shape glyphs:
    /// triangles red, circles blue, stars gold. Avoids relying on font coverage for those characters.
    /// </summary>
    public static class SymbolText
    {
        public const char Triangle = '▲';
        public const char Circle = '●';
        public const char Star = '★';

        public static bool IsSymbolic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            bool any = false;
            foreach (char c in s)
            {
                if (c == Triangle || c == Circle || c == Star) { any = true; continue; }
                if (c == ' ' || c == '?' || c == '"') continue;
                return false;
            }
            return any;
        }

        /// <summary>ASCII stand-in for places that use plain text (inbox rows, subjects).</summary>
        public static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace(Triangle, '^').Replace(Circle, 'o').Replace(Star, '*');
        }

        /// <summary>Builds a centered row of glyphs inside parent. Returns the row.</summary>
        public static RectTransform Build(Transform parent, string s, float size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            RectTransform row = UIFactory.Rect("Symbols", parent);
            UIFactory.Stretch(row);
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = align;
            h.spacing = size * 0.18f;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            int index = 0;
            foreach (char c in s)
            {
                if (c == '"') continue;
                if (c == ' ')
                {
                    RectTransform gap = UIFactory.Rect("Gap", row);
                    gap.sizeDelta = new Vector2(size * 0.45f, size);
                    continue;
                }
                if (c == '?')
                {
                    var q = UIFactory.GlowText(row, "Q", "?", size * 0.95f, Palette.Magenta);
                    q.rectTransform.sizeDelta = new Vector2(size * 0.6f, size);
                    continue;
                }
                Sprite sprite;
                Color color;
                switch (c)
                {
                    case Triangle: sprite = UISprites.Triangle; color = Palette.Red; break;
                    case Circle: sprite = UISprites.Circle; color = Palette.Blue; break;
                    default: sprite = UISprites.Star; color = Palette.Gold; break;
                }
                RectTransform cell = UIFactory.Rect("Glyph", row);
                cell.sizeDelta = new Vector2(size, size);
                Image glow = UIFactory.Image(cell, "Glow", UISprites.Glow, new Color(color.r, color.g, color.b, 0.45f), false);
                UIFactory.Place(glow.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size * 1.8f, size * 1.8f));
                Image g = UIFactory.Image(cell, "Shape", sprite, color, false);
                float inner = c == Circle ? 0.72f : 0.92f;
                UIFactory.Place(g.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size * inner, size * inner));
                cell.gameObject.AddComponent<GlyphFlicker>().Init(index++);
            }
            return row;
        }
    }
}
