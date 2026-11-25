using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Relay Station Omega. The antenna root stacks one StationSegment per Antenna level and the dish rides
    /// on top, so the station visibly grows as you upgrade.
    /// </summary>
    public class Station : MonoBehaviour
    {
        [SerializeField] private Transform antennaRoot;
        [SerializeField] private Transform dish;
        [SerializeField] private Transform absorbPoint;
        [SerializeField] private GameObject segmentPrefab;
        [SerializeField] private float segmentHeight = 0.22f;
        [SerializeField] private Transform[] leakPoints = new Transform[0];
        [SerializeField] private Renderer[] lights = new Renderer[0];

        private static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");

        private readonly List<Transform> segments = new List<Transform>();
        private MaterialPropertyBlock mpb;
        private float sparkTimer;
        private bool sparking;

        public Transform AbsorbPoint { get { return absorbPoint; } }
        public Transform Dish { get { return dish; } }
        public Vector3 AntennaTop { get { return dish != null ? dish.position : transform.position + Vector3.up * 3f; } }

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            EventBus.UpgradeBought += OnUpgrade;
        }

        private void OnDisable()
        {
            EventBus.UpgradeBought -= OnUpgrade;
        }

        private void OnUpgrade(string id, int level)
        {
            if (id == "antenna") SetLevel(level, false);
        }

        public void SetLevel(int level, bool instant)
        {
            if (segmentPrefab == null && GameManager.I != null && GameManager.I.Assets != null) segmentPrefab = GameManager.I.Assets.stationSegmentPrefab;
            if (antennaRoot == null || segmentPrefab == null) return;
            while (segments.Count < level)
            {
                GameObject go = Instantiate(segmentPrefab, antennaRoot);
                go.transform.localPosition = new Vector3(0f, segments.Count * segmentHeight, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, segments.Count * 25f, 0f);
                segments.Add(go.transform);
                if (!instant) StartCoroutine(GrowIn(go.transform));
            }
            while (segments.Count > level)
            {
                Transform t = segments[segments.Count - 1];
                segments.RemoveAt(segments.Count - 1);
                Destroy(t.gameObject);
            }
            if (dish != null)
            {
                Vector3 top = new Vector3(0f, segments.Count * segmentHeight, 0f);
                if (instant) dish.localPosition = top;
                else StartCoroutine(MoveDish(top));
            }
            if (!instant)
            {
                Vector3 p = antennaRoot.position + Vector3.up * (segments.Count * segmentHeight);
                VFXPlayer.Sparkle(p, 30, Palette.Cyan);
                VFXPlayer.Shockwave(new Vector3(transform.position.x, GameManager.I.PlaneY, transform.position.z), 4f, Palette.Cyan);
                Juice.Shake(0.12f, 0.35f);
                AudioManager.Play("level_up", 0.8f, 0.8f);
            }
        }

        private IEnumerator GrowIn(Transform t)
        {
            float d = 0.5f;
            float time = 0f;
            while (time < d && t != null)
            {
                float k = Juice.EaseOutBack(time / d);
                t.localScale = new Vector3(1f, Mathf.Max(0.01f, k), 1f);
                time += Time.deltaTime;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        private IEnumerator MoveDish(Vector3 target)
        {
            Vector3 start = dish.localPosition;
            float d = 0.5f;
            float time = 0f;
            while (time < d)
            {
                dish.localPosition = Vector3.LerpUnclamped(start, target, Juice.EaseOutBack(time / d));
                time += Time.deltaTime;
                yield return null;
            }
            dish.localPosition = target;
            Juice.PunchScale(dish, 1.3f, 0.3f);
        }

        /// <summary>World points where Hull Leak sparks appear.</summary>
        public Vector3 LeakPoint(int i)
        {
            if (leakPoints != null && leakPoints.Length > 0) return leakPoints[i % leakPoints.Length].position;
            float a = i * 1.3f;
            return transform.position + new Vector3(Mathf.Cos(a) * 1.4f, 0.8f + (i % 3) * 0.5f, Mathf.Sin(a) * 0.8f - 0.6f);
        }

        public int LeakPointCount { get { return leakPoints != null && leakPoints.Length > 0 ? leakPoints.Length : 6; } }

        /// <summary>Power Surge warning: wires on the station spark.</summary>
        public void SetSparking(bool on)
        {
            sparking = on;
        }

        private void Update()
        {
            if (sparking)
            {
                sparkTimer -= Time.deltaTime;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = Random.Range(0.05f, 0.2f);
                    VFXPlayer.Sparkle(LeakPoint(Random.Range(0, 12)), 3, Palette.Cyan);
                }
            }
            if (lights == null || lights.Length == 0) return;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f + i * 1.7f);
                lights[i].GetPropertyBlock(mpb);
                mpb.SetColor(IdEmission, Palette.Cyan * (1.5f + blink * 2.5f));
                lights[i].SetPropertyBlock(mpb);
            }
        }

        /// <summary>Become the Signal: the blobs lift the station off the moon.</summary>
        public IEnumerator LiftOff(float seconds)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < seconds)
            {
                float k = Juice.EaseInOut(t / seconds);
                transform.position = start + Vector3.up * (k * 14f) + new Vector3(Mathf.Sin(t * 20f), 0f, 0f) * 0.03f * (1f - k);
                transform.rotation = Quaternion.Euler(0f, k * 120f, 0f);
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
