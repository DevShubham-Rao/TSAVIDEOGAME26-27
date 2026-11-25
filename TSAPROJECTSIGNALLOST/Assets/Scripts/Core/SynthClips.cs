using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Placeholder audio generated in code with AudioClip.Create (Section 12).
    /// Every clip is a short sine / noise recipe so every hook is wired from day one.
    /// </summary>
    public static class SynthClips
    {
        private const int Rate = 44100;
        private const float TwoPi = Mathf.PI * 2f;

        public static Dictionary<string, AudioClip> BuildAll()
        {
            Dictionary<string, AudioClip> d = new Dictionary<string, AudioClip>();
            d["squish"] = Make("squish", 0.16f, Squish);
            d["pop"] = Make("pop", 0.12f, Pop);
            d["vacuum_loop"] = MakeLoop("vacuum_loop", 1.0f, VacuumLoop);
            d["sort_ding"] = Make("sort_ding", 0.8f, t => Bell(t, 1568f, 0.35f) * 0.8f);
            d["sort_bonk"] = Make("sort_bonk", 0.32f, Bonk);
            d["crunch"] = Make("crunch", 0.5f, Crunch);
            d["coin"] = Make("coin", 0.07f, Coin);
            d["coin_shower"] = Make("coin_shower", 1.0f, null, CoinShower);
            d["golden"] = Make("golden", 1.1f, Choir);
            d["buy"] = Make("buy", 0.55f, KaChing);
            d["level_up"] = Make("level_up", 0.9f, Fanfare);
            d["skill"] = Make("skill", 0.6f, PowerUp);
            d["mail"] = Make("mail", 0.2f, Bloop);
            d["type"] = Make("type", 0.03f, TypeTick);
            d["alarm"] = Make("alarm", 0.85f, Alarm);
            d["solved"] = Make("solved", 1.0f, Solved);
            d["boop"] = Make("boop", 0.28f, Boop);
            d["arpeggio"] = Make("arpeggio", 0.6f, Arpeggio);
            d["hiss"] = MakeLoop("hiss", 1.0f, Hiss);
            d["shield"] = Make("shield", 0.1f, Shield);
            d["whoosh"] = Make("whoosh", 0.35f, Whoosh);
            d["absorb"] = Make("absorb", 0.25f, Absorb);
            d["blip"] = Make("blip", 0.07f, Blip);
            return d;
        }

        // ---------- helpers ----------

        private static AudioClip Make(string name, float seconds, Func<float, float> fn, Action<float[]> fill = null)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            float[] data = new float[n];
            if (fill != null) fill(data);
            else for (int i = 0; i < n; i++) data[i] = fn(i / (float)Rate);
            FadeEdges(data, 0.002f, 0.01f);
            Normalize(data, 0.9f);
            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeLoop(string name, float seconds, Func<float, float> fn)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            int xf = Rate / 20;
            float[] raw = new float[n + xf];
            for (int i = 0; i < raw.Length; i++) raw[i] = fn(i / (float)Rate);
            float[] data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            // Crossfade the tail into the head so the loop point is seamless.
            for (int i = 0; i < xf; i++)
            {
                float a = i / (float)xf;
                data[i] = raw[n + i] * (1f - a) + raw[i] * a;
            }
            Normalize(data, 0.8f);
            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void FadeEdges(float[] d, float fadeIn, float fadeOut)
        {
            int a = Mathf.Min(d.Length, (int)(fadeIn * Rate));
            int b = Mathf.Min(d.Length, (int)(fadeOut * Rate));
            for (int i = 0; i < a; i++) d[i] *= i / (float)a;
            for (int i = 0; i < b; i++) d[d.Length - 1 - i] *= i / (float)b;
        }

        private static void Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        private static uint noiseState = 22222u;

        private static float Noise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState / (float)uint.MaxValue) * 2f - 1f;
        }

        private static float Env(float t, float attack, float decay)
        {
            float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
            return a * Mathf.Exp(-Mathf.Max(0f, t - attack) / decay);
        }

        private static float Sine(float t, float f) { return Mathf.Sin(TwoPi * f * t); }

        private static float Square(float t, float f)
        {
            return Sine(t, f) + Sine(t, f * 3f) / 3f + Sine(t, f * 5f) / 5f;
        }

        private static float Bell(float t, float f, float decay)
        {
            return Sine(t, f) * Mathf.Exp(-t / decay)
                 + 0.5f * Sine(t, f * 2.76f) * Mathf.Exp(-t / (decay * 0.5f))
                 + 0.25f * Sine(t, f * 5.4f) * Mathf.Exp(-t / (decay * 0.3f));
        }

        private static float Note(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        // ---------- recipes ----------

        private static float lp1;

        private static float Squish(float t)
        {
            if (t == 0f) lp1 = 0f;
            lp1 += (Noise() - lp1) * 0.08f;
            float f = 210f - 520f * t;
            float wob = 1f + 0.4f * Mathf.Sin(TwoPi * 28f * t);
            return (Sine(t, Mathf.Max(60f, f)) * 0.7f * wob + lp1 * 1.4f) * Env(t, 0.006f, 0.045f);
        }

        private static float Pop(float t)
        {
            float f = 380f + 2600f * t;
            float body = Mathf.Sin(TwoPi * (380f * t + 1300f * t * t)) * Env(t, 0.002f, 0.035f);
            float click = Noise() * Env(t, 0f, 0.004f) * 0.6f;
            return body + click + 0.0f * f;
        }

        private static float VacuumLoop(float t)
        {
            lp1 += (Noise() - lp1) * 0.12f;
            float swell = 0.75f + 0.25f * Mathf.Sin(TwoPi * 2f * t);
            return lp1 * swell + 0.15f * Sine(t, 180f + 30f * Mathf.Sin(TwoPi * 1f * t));
        }

        private static float Bonk(float t)
        {
            float f = 150f + 90f * Mathf.Sin(TwoPi * 11f * t) * Mathf.Exp(-t * 7f);
            return Mathf.Sin(TwoPi * f * t) * Env(t, 0.004f, 0.11f);
        }

        private static float Crunch(float t)
        {
            float n = Noise();
            n = Mathf.Round(n * 4f) / 4f;
            float grind = 0.6f + 0.4f * Mathf.Sign(Mathf.Sin(TwoPi * 32f * t));
            float low = Sine(t, 58f - 20f * t) * 0.9f;
            return (n * grind * 0.8f + low) * Env(t, 0.004f, 0.16f);
        }

        private static float Coin(float t)
        {
            return (Sine(t, 2637f) * 0.6f + Sine(t, 3951f) * 0.4f) * Env(t, 0.001f, 0.02f);
        }

        private static void CoinShower(float[] d)
        {
            System.Random r = new System.Random(7);
            for (int k = 0; k < 34; k++)
            {
                float start = (float)Math.Pow(r.NextDouble(), 1.6) * 0.8f;
                float pitch = 0.8f + (float)r.NextDouble() * 0.6f;
                float amp = 0.4f + (float)r.NextDouble() * 0.6f;
                int s0 = (int)(start * Rate);
                int len = (int)(0.06f * Rate);
                for (int i = 0; i < len && s0 + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[s0 + i] += amp * (Sine(t, 2637f * pitch) * 0.6f + Sine(t, 3951f * pitch) * 0.4f) * Env(t, 0.001f, 0.018f);
                }
            }
        }

        private static float Choir(float t)
        {
            float[] chord = { Note(69), Note(73), Note(76), Note(81) };
            float vib = 1f + 0.006f * Mathf.Sin(TwoPi * 5.2f * t);
            float s = 0f;
            for (int i = 0; i < chord.Length; i++)
            {
                for (int v = -1; v <= 1; v++)
                {
                    float f = chord[i] * vib * (1f + v * 0.004f);
                    s += Sine(t, f) * 0.5f + Sine(t, f * 2f) * 0.22f + Sine(t, f * 3f) * 0.3f + Sine(t, f * 4f) * 0.1f;
                }
            }
            float env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1.1f - t) / 0.45f);
            return s * env;
        }

        private static float KaChing(float t)
        {
            float tick = Noise() * (Env(t, 0f, 0.006f) + Env(Mathf.Max(0f, t - 0.06f), 0f, 0.006f) * (t > 0.06f ? 1f : 0f)) * 0.7f;
            float tb = Mathf.Max(0f, t - 0.09f);
            float bell = t > 0.09f ? (Bell(tb, Note(84), 0.25f) + Bell(tb, Note(88), 0.22f) * 0.7f + Bell(tb, Note(91), 0.2f) * 0.6f) : 0f;
            return tick + bell * 0.55f;
        }

        private static float Fanfare(float t)
        {
            int[] notes = { 72, 76, 79, 84 };
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float st = i * 0.1f;
                if (t < st) continue;
                float lt = t - st;
                float decay = i == notes.Length - 1 ? 0.35f : 0.12f;
                s += Square(lt, Note(notes[i])) * Env(lt, 0.005f, decay);
            }
            return s;
        }

        private static float PowerUp(float t)
        {
            float f = 220f * Mathf.Pow(6f, t / 0.6f);
            lp1 += (Noise() - lp1) * (0.05f + t * 0.4f);
            float tone = Mathf.Sin(TwoPi * f * t * 0.5f);
            return (tone * 0.6f + lp1 * 0.8f) * Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.6f - t) / 0.2f);
        }

        private static float Bloop(float t)
        {
            float phase = TwoPi * (560f * t - 700f * t * t);
            return Mathf.Sin(phase) * Env(t, 0.005f, 0.06f);
        }

        private static float TypeTick(float t)
        {
            return (Noise() * 0.6f + Sine(t, 1900f) * 0.4f) * Env(t, 0f, 0.006f);
        }

        private static float Alarm(float t)
        {
            float f = ((int)(t / 0.2f)) % 2 == 0 ? 660f : 880f;
            float local = t % 0.2f;
            return (Sine(t, f) + 0.3f * Sine(t, f * 2f)) * Mathf.Clamp01(local / 0.01f) * Mathf.Clamp01((0.2f - local) / 0.03f) * 0.7f;
        }

        private static float Solved(float t)
        {
            int[] notes = { 79, 83, 86, 91 };
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float st = i * 0.08f;
                if (t < st) continue;
                s += Bell(t - st, Note(notes[i]), 0.35f);
            }
            return s;
        }

        private static float Boop(float t)
        {
            return Sine(t, 300f - 200f * t) * Env(t, 0.01f, 0.09f);
        }

        private static float Arpeggio(float t)
        {
            int[] notes = { 84, 88, 91, 96 };
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float st = i * 0.065f;
                if (t < st) continue;
                s += Square(t - st, Note(notes[i])) * Env(t - st, 0.003f, 0.09f);
            }
            return s;
        }

        private static float Hiss(float t)
        {
            float n = Noise();
            lp1 += (n - lp1) * 0.5f;
            return (n - lp1) * (0.8f + 0.2f * Mathf.Sin(TwoPi * 3f * t));
        }

        private static float Shield(float t)
        {
            return (Noise() * 0.5f + Sine(t, 320f - 900f * t)) * Env(t, 0.002f, 0.025f);
        }

        private static float Whoosh(float t)
        {
            lp1 += (Noise() - lp1) * (0.03f + t * 0.6f);
            return lp1 * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f));
        }

        private static float Blip(float t)
        {
            return Sine(t, 1180f + 600f * t) * Env(t, 0.003f, 0.02f) * 0.5f;
        }

        private static float Absorb(float t)
        {
            return Sine(t, 500f - 1200f * t) * Env(t, 0.01f, 0.08f) * 0.6f;
        }

        // ---------- music ----------

        /// <summary>Chill lo-fi loop: 100 BPM, 8 bars, Am7 - Fmaj7 - Cmaj7 - G6.</summary>
        public static AudioClip BuildMusic(bool drumLayer)
        {
            const int sr = 22050;
            const float bpm = 100f;
            float beat = 60f / bpm;
            int beats = 32;
            int n = Mathf.CeilToInt(beats * beat * sr);
            float[] d = new float[n];
            int[][] chords =
            {
                new[] { 57, 60, 64, 67 },
                new[] { 53, 57, 60, 64 },
                new[] { 48, 52, 55, 59 },
                new[] { 55, 59, 62, 64 },
            };
            int[] scale = { 69, 72, 74, 76, 79, 81, 84 };
            System.Random r = new System.Random(99);

            if (!drumLayer)
            {
                // Pads.
                for (int c = 0; c < 4; c++)
                {
                    int[] ch = chords[c];
                    int s0 = (int)(c * 8 * beat * sr);
                    int len = (int)(8 * beat * sr);
                    for (int i = 0; i < len && s0 + i < n; i++)
                    {
                        float t = i / (float)sr;
                        float env = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((len / (float)sr - t) / 0.6f);
                        float s = 0f;
                        for (int k = 0; k < ch.Length; k++)
                        {
                            float f = Note(ch[k]);
                            s += Mathf.Sin(TwoPi * f * t) * 0.5f + Mathf.Sin(TwoPi * f * 1.003f * t) * 0.35f + Mathf.Sin(TwoPi * f * 2f * t) * 0.08f;
                        }
                        d[s0 + i] += s * env * 0.06f;
                    }
                    // Bass on beats 1 and 3 of each bar.
                    for (int b = 0; b < 8; b += 2)
                    {
                        int bs = (int)((c * 8 + b) * beat * sr);
                        float f = Note(ch[0] - 12);
                        int bl = (int)(beat * 1.6f * sr);
                        for (int i = 0; i < bl && bs + i < n; i++)
                        {
                            float t = i / (float)sr;
                            d[bs + i] += (Mathf.Sin(TwoPi * f * t) + 0.25f * Mathf.Sin(TwoPi * f * 2f * t)) * Env(t, 0.01f, 0.5f) * 0.22f;
                        }
                    }
                }
                // Soft bell melody on 8th notes.
                for (int e = 0; e < beats * 2; e++)
                {
                    if (r.NextDouble() < 0.45) continue;
                    int note = scale[r.Next(scale.Length)];
                    int s0 = (int)(e * beat * 0.5f * sr);
                    int len = (int)(0.9f * sr);
                    float f = Note(note);
                    for (int i = 0; i < len && s0 + i < n; i++)
                    {
                        float t = i / (float)sr;
                        d[s0 + i] += Bell(t, f, 0.3f) * 0.05f;
                    }
                }
                // Lazy kick + hat.
                for (int b = 0; b < beats; b++)
                {
                    int s0 = (int)(b * beat * sr);
                    if (b % 2 == 0) AddKick(d, s0, sr, 0.35f);
                    AddHat(d, (int)((b + 0.5f) * beat * sr), sr, 0.05f);
                }
            }
            else
            {
                // Faster drum layer crossfaded in during events.
                for (int s = 0; s < beats * 4; s++)
                {
                    int s0 = (int)(s * beat * 0.25f * sr);
                    if (s % 4 == 0) AddKick(d, s0, sr, 0.5f);
                    if (s % 8 == 4) AddSnare(d, s0, sr, 0.28f);
                    AddHat(d, s0, sr, s % 2 == 0 ? 0.08f : 0.045f);
                }
            }

            for (int i = 0; i < n; i++) d[i] = (float)Math.Tanh(d[i] * 1.4f) * 0.8f;
            AudioClip clip = AudioClip.Create(drumLayer ? "music_drums" : "music_loop", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static void AddKick(float[] d, int s0, int sr, float amp)
        {
            int len = (int)(0.3f * sr);
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = i / (float)sr;
                float f = 45f + 90f * Mathf.Exp(-t * 30f);
                d[s0 + i] += Mathf.Sin(TwoPi * f * t) * Env(t, 0.002f, 0.12f) * amp;
            }
        }

        private static void AddHat(float[] d, int s0, int sr, float amp)
        {
            int len = (int)(0.05f * sr);
            float prev = 0f;
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float n = Noise();
                float hp = n - prev;
                prev = n;
                d[s0 + i] += hp * Env(i / (float)sr, 0.001f, 0.015f) * amp;
            }
        }

        private static void AddSnare(float[] d, int s0, int sr, float amp)
        {
            int len = (int)(0.2f * sr);
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = i / (float)sr;
                d[s0 + i] += (Noise() * 0.8f + Mathf.Sin(TwoPi * 190f * t) * 0.4f) * Env(t, 0.002f, 0.06f) * amp;
            }
        }
    }
}
