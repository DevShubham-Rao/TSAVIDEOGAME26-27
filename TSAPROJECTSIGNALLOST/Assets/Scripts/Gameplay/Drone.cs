using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Sweeper Drone: flies to the nearest blob and pops it every 2 s (faster with Overclock).
    /// With Smart Chutes it beams blobs into the matching chute instead; with the Idle skill
    /// "Drones feed the Compressor" every other blob goes into the Compressor.
    /// </summary>
    public class Drone : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Light droneLight;
        [SerializeField] private LineRenderer beam;

        private static readonly int IdBase = Shader.PropertyToID("_BaseColor");
        private static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");

        private Blob target;
        private float cycle;
        private Vector3 velocity;
        private float bobPhase;
        private float orbitAngle;
        private int trips;
        private float beamTimer;
        private Vector3 beamEnd;
        private MaterialPropertyBlock mpb;
        private bool temporary;
        private float arriveTime;

        public bool Temporary { get { return temporary; } }

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            bobPhase = Random.value * 10f;
            orbitAngle = Random.value * Mathf.PI * 2f;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        }

        public void Init(bool isTemporary, Vector3 from)
        {
            temporary = isTemporary;
            transform.position = from;
            cycle = Random.Range(0f, 0.5f);
            target = null;
            arriveTime = Time.time;
            if (trail != null) trail.Clear();
            ApplySkin();
            Juice.PunchScale(transform, 1.4f, 0.3f);
        }

        public void ApplySkin()
        {
            bool pink = GameManager.I != null && GameManager.I.DroneSkin == "pink";
            Color tint = pink ? Palette.Pink : Palette.Cyan;
            if (ringRenderer != null)
            {
                ringRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(IdBase, tint);
                mpb.SetColor(IdEmission, tint * 2.5f);
                ringRenderer.SetPropertyBlock(mpb);
            }
            if (bodyRenderer != null && pink)
            {
                bodyRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(IdBase, Color.Lerp(Palette.Station, Palette.Pink, 0.6f));
                bodyRenderer.SetPropertyBlock(mpb);
            }
            if (droneLight != null) droneLight.color = tint;
            if (trail != null)
            {
                trail.startColor = tint * 1.5f;
                trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            }
        }

        public void ReleaseClaim(Blob b)
        {
            if (target == b)
            {
                target = null;
                b.ClaimedBy = null;
            }
        }

        private void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.IsReady || Balance.B == null) return;
            DroneConfig cfg = Balance.B.drones;
            float dt = Time.deltaTime;
            float speedMult = GameStats.DroneSpeedMult;
            bool paused = DroneManager.I != null && DroneManager.I.Paused;
            bool idle = gm.State != GameState.Playing || (DayManager.I != null && !DayManager.I.InShiftOrLastCall);

            if (trail != null) trail.emitting = SkillTree.I.TierCount("drone_speed") >= cfg.trailFromLevel;

            if (target != null && (!target.IsSweepable || target.ClaimedBy != this))
            {
                if (target.ClaimedBy == this) target.ClaimedBy = null;
                target = null;
            }
            if (target == null && !paused && !idle) FindTarget();

            Vector3 hover = Vector3.up * cfg.hoverHeight;
            Vector3 goal;
            if (target != null && !paused && !idle)
            {
                goal = target.Position + hover + Vector3.back * 0.25f;
            }
            else
            {
                orbitAngle += dt * 0.4f;
                Vector3 home = BlobSpawner.I != null ? BlobSpawner.I.AbsorbPoint : Vector3.zero;
                goal = home + new Vector3(Mathf.Cos(orbitAngle) * 3.5f, cfg.hoverHeight + 1.2f, Mathf.Sin(orbitAngle) * 1.6f - 1.5f);
            }

            Vector3 to = goal - transform.position;
            Vector3 desired = to.normalized * Mathf.Min(to.magnitude * 4f, cfg.moveSpeed * speedMult * 1.5f);
            velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-5f * dt));
            transform.position += velocity * dt;

            // Bob up and down with a sine wave; bank into the direction of travel.
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f + bobPhase) * 0.08f, 0f);
                Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
                Quaternion bank = Quaternion.Euler(Mathf.Clamp(flat.z * 6f, -25f, 25f), Time.time * 90f, Mathf.Clamp(-flat.x * 6f, -25f, 25f));
                visual.localRotation = Quaternion.Slerp(visual.localRotation, bank, 1f - Mathf.Exp(-8f * dt));
            }

            if (!paused && !idle)
            {
                cycle += dt * speedMult;
                float interval = cfg.popInterval;
                if (target != null && cycle >= interval)
                {
                    Vector3 flat = target.Position - transform.position;
                    flat.y = 0f;
                    if (flat.magnitude <= cfg.actRange)
                    {
                        cycle = 0f;
                        Act(target);
                        target = null;
                    }
                }
                cycle = Mathf.Min(cycle, interval * 1.5f);
            }

            UpdateBeam(dt);
        }

        private void FindTarget()
        {
            // Sweeper drones collect loose Data bits (never Static) and carry them to the matching chute.
            Blob b = BlobSpawner.I.FindNearest(transform.position, 60f, x => x.IsSweepable && x.ClaimedBy == null && !x.EventBlob);
            if (b == null) return;
            b.ClaimedBy = this;
            target = b;
        }

        private readonly System.Collections.Generic.List<Blob> grab = new System.Collections.Generic.List<Blob>();

        private void Act(Blob b)
        {
            b.ClaimedBy = null;
            trips++;
            beamEnd = b.Position;
            beamTimer = 0.18f;
            // Drone Tank: grab extra bits of the same color next to the target in one trip.
            grab.Clear();
            grab.Add(b);
            int tank = GameStats.DroneTank;
            if (tank > 1)
            {
                BlobSpawner.I.CollectInRadius(b.Position, 1.4f, tempNear, x => x.IsSweepable && x != b && x.ClaimedBy == null && x.Color == b.Color);
                for (int i = 0; i < tempNear.Count && grab.Count < tank; i++) grab.Add(tempNear[i]);
            }
            for (int i = 0; i < grab.Count; i++)
            {
                Blob bit = grab[i];
                Chute chute = Chute.ForBit(bit);
                if (chute == null) continue;
                Chute c = chute;
                bit.FlyTo(c.Mouth + Random.insideUnitSphere * 0.1f, 0.5f + i * 0.05f, 1.4f, x => BlobActions.Sort(x, c, false));
            }
        }

        private readonly System.Collections.Generic.List<Blob> tempNear = new System.Collections.Generic.List<Blob>();

        private void UpdateBeam(float dt)
        {
            if (beam == null) return;
            beamTimer -= dt;
            bool on = beamTimer > 0f;
            beam.enabled = on;
            if (!on) return;
            beam.SetPosition(0, transform.position);
            beam.SetPosition(1, beamEnd);
            Color c = GameManager.I.DroneSkin == "pink" ? Palette.Pink : Palette.Cyan;
            c *= 2f;
            beam.startColor = c;
            beam.endColor = c;
        }
    }
}
