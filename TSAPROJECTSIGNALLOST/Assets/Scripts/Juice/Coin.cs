using UnityEngine;

namespace SignalLost
{
    /// <summary>A real coin mesh that flies in an arc to the Data counter. The counter ticks up when it lands.</summary>
    public class Coin : MonoBehaviour
    {
        private Vector3 start;
        private Vector3 control;
        private float t;
        private float duration;
        private float delay;
        private double share;
        private float spin;
        private Vector3 spinAxis;
        private float startScale = 1f;

        public bool Busy { get; private set; }

        public void Launch(Vector3 from, double value, float flightTime, float startDelay)
        {
            start = from;
            share = value;
            duration = flightTime;
            delay = startDelay;
            t = 0f;
            Vector2 r = Random.insideUnitCircle * 1.6f;
            control = from + new Vector3(r.x, Random.Range(1.8f, 3.2f), r.y * 0.6f);
            spin = Random.Range(540f, 1080f);
            spinAxis = Random.onUnitSphere;
            startScale = Random.Range(0.9f, 1.2f);
            transform.position = from;
            transform.localScale = Vector3.one * startScale;
            Busy = true;
        }

        private void Update()
        {
            if (!Busy) return;
            if (delay > 0f)
            {
                delay -= Time.deltaTime;
                // Pop outward first so a burst reads as an explosion.
                transform.position = Vector3.Lerp(transform.position, control, Time.deltaTime * 2f);
                return;
            }
            t += Time.deltaTime / duration;
            Vector3 target = CoinFlock.I.TargetWorld;
            float k = Mathf.Clamp01(t);
            float e = k * k * (3f - 2f * k);
            Vector3 a = Vector3.Lerp(start, control, e);
            Vector3 b = Vector3.Lerp(control, target, e);
            transform.position = Vector3.Lerp(a, b, e);
            transform.Rotate(spinAxis, spin * Time.deltaTime, Space.World);
            transform.localScale = Vector3.one * startScale * Mathf.Lerp(1f, 0.45f, e);
            if (k >= 1f)
            {
                Busy = false;
                CoinFlock.I.Landed(this, share);
            }
        }
    }
}
