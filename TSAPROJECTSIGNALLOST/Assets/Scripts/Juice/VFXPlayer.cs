using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Plays the PS_Pop / PS_Sparkle / PS_Confetti / PS_Shockwave particle prefabs.
    /// SPEC-ASSUMPTION: instead of pooling one Particle System per pop, each effect is a single shared
    /// world-space system that Emit()s bursts at a position. Same look, far cheaper at 100+ pops a second.
    /// </summary>
    public class VFXPlayer : MonoBehaviour
    {
        public static VFXPlayer I { get; private set; }

        private ParticleSystem pop;
        private ParticleSystem sparkle;
        private ParticleSystem confetti;
        private ParticleSystem shockwave;

        private void Awake()
        {
            I = this;
        }

        private void Start()
        {
            GameAssets a = GameManager.I != null ? GameManager.I.Assets : null;
            if (a == null) return;
            pop = Make(a.popVfx);
            sparkle = Make(a.sparkleVfx);
            confetti = Make(a.confettiVfx);
            shockwave = Make(a.shockwaveVfx);
        }

        private ParticleSystem Make(GameObject prefab)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, transform);
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                ps.Play();
            }
            return ps;
        }

        private static void Emit(ParticleSystem ps, Vector3 pos, Color color, int count, float size = -1f)
        {
            if (ps == null || count <= 0) return;
            count = GameSettings.Particles(count);
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams
            {
                position = pos,
                applyShapeToPosition = true,
                startColor = color,
            };
            if (size > 0f) ep.startSize = size;
            ps.Emit(ep, count);
        }

        public static void Pop(Vector3 pos, Color color, int count)
        {
            if (I != null) Emit(I.pop, pos, color, count);
        }

        public static void Sparkle(Vector3 pos, int count)
        {
            if (I != null) Emit(I.sparkle, pos, Palette.Gold, count);
        }

        public static void Sparkle(Vector3 pos, int count, Color color)
        {
            if (I != null) Emit(I.sparkle, pos, color, count);
        }

        public static void Confetti(Vector3 pos, int count)
        {
            if (I == null || I.confetti == null) return;
            Color[] colors = { Palette.Red, Palette.Blue, Palette.Green, Palette.Gold, Palette.Cyan, Palette.Magenta };
            int per = Mathf.Max(1, count / colors.Length);
            foreach (Color c in colors) Emit(I.confetti, pos, c, per);
        }

        /// <summary>Confetti in front of the camera (level up, endings).</summary>
        public static void ConfettiScreen(int count)
        {
            Camera cam = GameManager.I != null ? GameManager.I.Cam : Camera.main;
            if (cam == null) return;
            Vector3 p = cam.transform.position + cam.transform.forward * 7f - cam.transform.up * 2.5f;
            Confetti(p, count);
        }

        /// <summary>A quick dotted beam of sparkles (Auto-Pop Tower).</summary>
        public static void Beam(Vector3 from, Vector3 to, Color color)
        {
            if (I == null) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(from, to) * 3f), 4, 24);
            for (int i = 0; i <= n; i++) Emit(I.sparkle, Vector3.Lerp(from, to, i / (float)n), color, 1);
        }

        public static void Shockwave(Vector3 pos, float radius, Color color)
        {
            if (I != null) Emit(I.shockwave, pos + Vector3.up * 0.05f, color, 1, radius * 2f);
        }
    }
}
