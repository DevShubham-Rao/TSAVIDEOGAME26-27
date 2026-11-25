using UnityEngine;

namespace SignalLost
{
    /// <summary>Sorting colors. Prism counts as any color; Gold matches no chute.</summary>
    public enum BlobColor { Red = 0, Blue = 1, Green = 2, Gold = 3, Prism = 4 }

    public enum BlobState { Inactive, Drifting, Held, Flying, Compressed, Arranged, Escaping, Absorbing, Popping }

    /// <summary>What caused a blob to pop. No combat: blobs are popped, collected or compressed.</summary>
    public enum PopCause { Click, Crit, Chain, Shockwave, Drone, Event }

    public enum GameState { Loading, MainMenu, Playing, Finale, Ending }

    public static class Palette
    {
        public static readonly Color Navy = Hex("#05081A");
        public static readonly Color PanelNavy = WithAlpha(Hex("#0B1026"), 0.85f);
        public static readonly Color Cyan = Hex("#3BF0FF");
        public static readonly Color Moon = Hex("#3A3F55");
        public static readonly Color Station = Hex("#D8E1F0");
        public static readonly Color Red = Hex("#FF3B6B");
        public static readonly Color Blue = Hex("#3BC8FF");
        public static readonly Color Green = Hex("#7CFF3B");
        public static readonly Color Gold = Hex("#FFD23B");
        public static readonly Color Magenta = Hex("#FF4BE1");
        public static readonly Color Orange = Hex("#FF8A3B");
        public static readonly Color Pink = Hex("#FF8AD8");
        public static readonly Color Purple = Hex("#8E5BFF");
        public static readonly Color Dim = Hex("#5A6488");
        public static readonly Color TextSoft = Hex("#AFC3E6");

        public static Color Hex(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            return Color.white;
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static Color ForBlob(BlobColor c)
        {
            switch (c)
            {
                case BlobColor.Red: return Red;
                case BlobColor.Blue: return Blue;
                case BlobColor.Green: return Green;
                case BlobColor.Gold: return Gold;
                default: return Color.white;
            }
        }

        public static BlobColor ParseColor(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "blue": return BlobColor.Blue;
                case "green": return BlobColor.Green;
                case "gold": return BlobColor.Gold;
                case "prism": return BlobColor.Prism;
                default: return BlobColor.Red;
            }
        }

        /// <summary>Cycling rainbow used by Prism blobs and confetti.</summary>
        public static Color Rainbow(float t)
        {
            return Color.HSVToRGB(Mathf.Repeat(t, 1f), 0.75f, 1f);
        }
    }
}
