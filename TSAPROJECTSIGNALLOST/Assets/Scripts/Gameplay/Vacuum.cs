using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Hold to vacuum: blobs inside the suction radius stretch toward the cursor and stick to it in a wobbly
    /// cluster (max = Vacuum Capacity). Release over a chute to sort, over the Compressor to feed it.
    /// Also draws the cursor ring, which thickens with Pop Power.
    /// </summary>
    public class Vacuum : MonoBehaviour
    {
        public static Vacuum I { get; private set; }

        [SerializeField] private LineRenderer ring;
        [SerializeField] private int ringSegments = 56;

        public bool Active { get; private set; }
        public readonly List<Blob> Held = new List<Blob>();
        public Vector3 Cursor { get { return cursor; } }

        private readonly List<Blob> temp = new List<Blob>();
        private Vector3 cursor;
        private bool cursorVisible;
        private float ringRadius = 0.3f;
        private float spin;
        private ParticleSystem swirl;

        private void Awake()
        {
            I = this;
        }

        private void Start()
        {
            GameAssets a = GameManager.I != null ? GameManager.I.Assets : null;
            if (a != null && a.vacuumSwirlVfx != null)
            {
                GameObject go = Instantiate(a.vacuumSwirlVfx, transform);
                swirl = go.GetComponent<ParticleSystem>();
                if (swirl != null) swirl.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (ring != null)
            {
                ring.loop = true;
                ring.useWorldSpace = true;
                ring.positionCount = ringSegments;
                if (ring.sharedMaterial == null && a != null) ring.sharedMaterial = a.lineMaterial;
            }
        }

        public void SetCursor(Vector3 planePoint, bool visible)
        {
            cursor = planePoint;
            cursorVisible = visible;
        }

        public void Begin(Vector3 p)
        {
            cursor = p;
            Active = true;
            if (AudioManager.I != null) AudioManager.I.SetVacuum(true, 0f);
            if (swirl != null) swirl.Play();
        }

        public void Tick(Vector3 p)
        {
            cursor = p;
            if (!Active) return;
            BalanceData b = Balance.B;
            float radius = GameStats.VacuumRadius;
            int capacity = GameStats.VacuumCapacity;
            float dt = Time.deltaTime;

            if (Held.Count < capacity)
            {
                BlobSpawner.I.CollectInRadius(p, radius, temp, x => x.IsSweepable);
                for (int i = 0; i < temp.Count; i++)
                {
                    Blob blob = temp[i];
                    Vector3 d = blob.Position - p;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist <= b.vacuum.stickDistance + blob.Radius && Held.Count < capacity)
                    {
                        Attach(blob);
                        continue;
                    }
                    float closeness = 1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, radius));
                    blob.Pull(p, b.vacuum.pullSpeed * GameStats.SweepSpeed * (0.45f + closeness), dt);
                }
            }

            // Wobbly cluster: golden-angle spiral around the cursor, slowly turning.
            spin += dt * 1.6f;
            for (int i = 0; i < Held.Count; i++)
            {
                Blob blob = Held[i];
                float rr = i == 0 ? 0f : 0.16f * Mathf.Sqrt(i) * Mathf.Max(0.8f, blob.Radius / 0.14f);
                float a = i * 2.39996f + spin;
                Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr;
                offset += new Vector3(Mathf.Sin(Time.time * 7f + i), 0f, Mathf.Cos(Time.time * 6f + i * 1.3f)) * 0.04f;
                blob.SetHeldTarget(p + offset);
            }

            if (AudioManager.I != null) AudioManager.I.SetVacuum(true, Held.Count / (float)Mathf.Max(1, capacity));
        }

        private void Attach(Blob blob)
        {
            if (Held.Contains(blob)) return;
            blob.SetHeld(true);
            Held.Add(blob);
            AudioManager.PlayVaried("squish", 1.2f, 1.45f, 0.35f);
        }

        public void End(Vector3 p)
        {
            cursor = p;
            Active = false;
            if (AudioManager.I != null) AudioManager.I.SetVacuum(false, 0f);
            if (swirl != null) swirl.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (Held.Count == 0) return;

            BalanceData b = Balance.B;
            Chute chute = Chute.FindAt(p);
            List<Blob> flung = new List<Blob>(Held);
            Held.Clear();

            if (chute != null)
            {
                // Sort: every bit in the tank goes into this chute. Wrong colors bounce back out.
                AudioManager.Play("whoosh", 1.1f, 0.6f);
                for (int i = 0; i < flung.Count; i++)
                {
                    Blob blob = flung[i];
                    float dur = b.vacuum.flingDuration + i * 0.035f;
                    Vector3 target = chute.Mouth + Random.insideUnitSphere * 0.12f;
                    Chute c = chute;
                    blob.FlyTo(target, dur, 1.1f, x => BlobActions.Sort(x, c, true));
                }
            }
            else
            {
                // Released anywhere else (the Compressor only takes bits from the chutes): bits spill out.
                if (Compressor.I != null && Compressor.I.Contains(p) && HUD.I != null) HUD.I.Toast("SORT INTO A CHUTE FIRST", Palette.Orange);
                foreach (Blob blob in flung)
                {
                    blob.SetHeld(false);
                    Vector3 scatter = Random.insideUnitSphere;
                    scatter.y = 0f;
                    blob.FlyTo(BlobSpawner.I.ClampToField(blob.Position + scatter.normalized * 0.6f), 0.25f, 0.3f, x => x.ReturnToDrift());
                }
            }
        }

        public void Forget(Blob b)
        {
            Held.Remove(b);
        }

        public void DropAll()
        {
            foreach (Blob blob in Held) blob.SetHeld(false);
            Held.Clear();
            Active = false;
            if (AudioManager.I != null) AudioManager.I.SetVacuum(false, 0f);
            if (swirl != null) swirl.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void LateUpdate()
        {
            if (Balance.B == null) return;
            VacuumConfig v = Balance.B.vacuum;
            float targetRadius = Active ? GameStats.VacuumRadius : v.idleRingRadius;
            ringRadius = Mathf.Lerp(ringRadius, targetRadius, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (swirl != null)
            {
                swirl.transform.position = cursor + Vector3.up * 0.05f;
                ParticleSystem.ShapeModule shape = swirl.shape;
                shape.radius = Mathf.Max(0.2f, ringRadius);
            }
            if (ring == null) return;
            bool show = cursorVisible && GameManager.I != null && GameManager.I.State != GameState.Ending;
            ring.enabled = show;
            if (!show) return;
            int level = SkillTree.I != null ? SkillTree.I.TierCount("pop_power") : 0;
            float width = Mathf.Min(v.ringWidthMax, v.ringWidthBase + v.ringWidthPerLevel * level);
            ring.widthMultiplier = Active ? width * 1.4f : width;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            Color c = Active ? Color.Lerp(Palette.Cyan, Palette.Magenta, Held.Count / (float)Mathf.Max(1, GameStats.VacuumCapacity)) : Palette.Cyan;
            c *= Active ? 2.2f : 1.3f * pulse;
            c.a = 1f;
            ring.startColor = c;
            ring.endColor = c;
            float y = cursor.y + 0.03f;
            float rot = Time.unscaledTime * (Active ? 3f : 0.6f);
            for (int i = 0; i < ringSegments; i++)
            {
                float a = rot + i * Mathf.PI * 2f / ringSegments;
                float wob = Active ? 1f + 0.04f * Mathf.Sin(a * 6f + Time.unscaledTime * 10f) : 1f;
                ring.SetPosition(i, new Vector3(cursor.x + Mathf.Cos(a) * ringRadius * wob, y, cursor.z + Mathf.Sin(a) * ringRadius * wob));
            }
        }
    }
}
