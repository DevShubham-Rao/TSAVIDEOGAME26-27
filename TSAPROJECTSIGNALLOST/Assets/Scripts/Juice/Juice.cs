using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Game-feel helpers called everywhere: PunchScale, Shake, Hitstop, FloatText, CoinBurst plus
    /// coroutine tweens (DOTween is optional; these use unscaled time so UI keeps moving during hitstop).
    /// </summary>
    public static class Juice
    {
        private static readonly Dictionary<Transform, Coroutine> punches = new Dictionary<Transform, Coroutine>();
        private static readonly Dictionary<Transform, Vector3> punchBase = new Dictionary<Transform, Vector3>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            punches.Clear();
            punchBase.Clear();
        }

        public static Coroutine Run(IEnumerator routine)
        {
            return CoroutineHost.I.StartCoroutine(routine);
        }

        public static void Stop(Coroutine c)
        {
            if (c != null && CoroutineHost.I != null) CoroutineHost.I.StopCoroutine(c);
        }

        // ---------------- easing ----------------

        public static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float EaseInOut(float t) { return t * t * (3f - 2f * t); }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        // ---------------- tweens ----------------

        public static IEnumerator Tween(float duration, Action<float> step, bool unscaled = true)
        {
            float t = 0f;
            while (t < duration)
            {
                step(Mathf.Clamp01(t / duration));
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            step(1f);
        }

        /// <summary>Scale punch: up to punch x base, then springs back. Safe to call repeatedly.</summary>
        public static void PunchScale(Transform t, float punch, float duration)
        {
            if (t == null) return;
            if (punches.TryGetValue(t, out Coroutine running) && running != null)
            {
                Stop(running);
                if (punchBase.TryGetValue(t, out Vector3 b)) t.localScale = b;
            }
            punchBase[t] = t.localScale;
            punches[t] = Run(PunchRoutine(t, punch, duration));
        }

        private static IEnumerator PunchRoutine(Transform t, float punch, float duration)
        {
            Vector3 baseScale = punchBase[t];
            float up = duration * 0.4f;
            float time = 0f;
            while (time < duration && t != null)
            {
                float s;
                if (time < up) s = Mathf.Lerp(1f, punch, EaseOutCubic(time / up));
                else s = Mathf.LerpUnclamped(punch, 1f, EaseOutBack((time - up) / (duration - up)));
                t.localScale = baseScale * s;
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            if (t != null)
            {
                t.localScale = baseScale;
                punches.Remove(t);
                punchBase.Remove(t);
            }
        }

        /// <summary>Text-stamp pop: scale 0 -> 1.4 -> 1.0.</summary>
        public static Coroutine StampIn(Transform t, float duration = 0.35f)
        {
            if (t == null) return null;
            return Run(Tween(duration, k =>
            {
                if (t == null) return;
                float s = k < 0.6f ? Mathf.Lerp(0f, 1.4f, EaseOutCubic(k / 0.6f)) : Mathf.Lerp(1.4f, 1f, EaseInOut((k - 0.6f) / 0.4f));
                t.localScale = Vector3.one * s;
            }));
        }

        /// <summary>A few quick vertical hops (mail icon: 3 bounces over 0.6 s).</summary>
        public static Coroutine Bounce(RectTransform rt, int bounces, float duration, float height = 22f)
        {
            if (rt == null) return null;
            Vector2 start = rt.anchoredPosition;
            return Run(BounceRoutine(rt, start, bounces, duration, height));
        }

        private static IEnumerator BounceRoutine(RectTransform rt, Vector2 start, int bounces, float duration, float height)
        {
            float t = 0f;
            while (t < duration && rt != null)
            {
                float k = t / duration;
                float y = Mathf.Abs(Mathf.Sin(k * Mathf.PI * bounces)) * height * (1f - k);
                rt.anchoredPosition = start + new Vector2(0f, y);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            if (rt != null) rt.anchoredPosition = start;
        }

        // ---------------- feel ----------------

        public static void Shake(float amplitude, float duration)
        {
            if (CameraShake.I != null) CameraShake.I.Shake(amplitude, duration);
        }

        /// <summary>Sets Time.timeScale to 0.05 for the duration (unscaled). Rate-limited so it never sticks.</summary>
        public static void Hitstop(float duration)
        {
            BalanceData b = Balance.B;
            float scale = b != null ? b.juice.hitstopTimeScale : 0.05f;
            float cooldown = b != null ? b.click.hitstopCooldown : 0.1f;
            CoroutineHost.I.Hitstop(duration, scale, cooldown);
        }

        public static void FloatText(Vector3 worldPos, string text, Color color, float sizeMult = 1f)
        {
            if (FloatingTextLayer.I != null) FloatingTextLayer.I.ShowWorld(worldPos, text, color, sizeMult);
        }

        public static void FloatTextScreen(Vector2 screenPos, string text, Color color, float sizeMult = 1f)
        {
            if (FloatingTextLayer.I != null) FloatingTextLayer.I.ShowScreen(screenPos, text, color, sizeMult);
        }

        /// <summary>Spawns coins that fly to the Data counter. Returns how many were spawned.</summary>
        public static int CoinBurst(Vector3 worldPos, double amount, int count)
        {
            return CoinFlock.I != null ? CoinFlock.I.Burst(worldPos, amount, count) : 0;
        }

        /// <summary>Coin count for a payout: 1 to 8, growing with the size of the number.</summary>
        public static int CoinsFor(double amount, double reference)
        {
            BalanceData b = Balance.B;
            double ratio = reference > 0 ? amount / reference : amount;
            int n = 1 + (int)Math.Floor(Math.Log10(Math.Max(1d, ratio)) * 2.2d);
            return Mathf.Clamp(n, b.juice.coinsMin, b.juice.coinsMax);
        }
    }
}
