using System;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// A glowing, jiggly blob of Static. One prefab for every type: color, scale and wobble are set with a
    /// MaterialPropertyBlock. Blobs only move on the play plane (x and z). They are popped, collected or
    /// compressed; they never harm anything.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Blob : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer bodyRenderer;

        private static readonly int IdColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdEmission = Shader.PropertyToID("_EmissionStrength");
        private static readonly int IdFlash = Shader.PropertyToID("_Flash");
        private static readonly int IdRainbow = Shader.PropertyToID("_Rainbow");
        private static readonly int IdPhase = Shader.PropertyToID("_Phase");
        private static readonly int IdWobble = Shader.PropertyToID("_Wobble");
        private static readonly int IdMode = Shader.PropertyToID("_Mode");

        public BlobTypeDef Type { get; private set; }
        public BlobColor Color { get; private set; }
        public bool IsGolden { get; private set; }
        public bool IsMega { get; private set; }
        public bool IsPrism { get { return Color == BlobColor.Prism; } }
        public int Stability { get; private set; }
        public int MaxStability { get; private set; }
        public double BaseValue { get; private set; }
        public BlobState State { get; private set; }
        public Drone ClaimedBy { get; set; }
        public bool EventBlob { get; set; }
        public bool NoEscape { get; set; }
        public float Hover { get; set; }
        /// <summary>Movement speed multiplier (the tutorial makes its blobs slow and easy to hit).</summary>
        public float SpeedMult { get; set; } = 1f;
        /// <summary>Increments every time this pooled blob is reused, so stale references can be detected.</summary>
        public int Serial { get; private set; }
        public float Age { get { return age; } }

        public float Radius { get { return 0.35f * transform.localScale.x; } }
        public Vector3 Position { get { return transform.position; } }
        public bool IsInteractive { get { return State == BlobState.Drifting || State == BlobState.Escaping; } }
        /// <summary>A Data bit dropped by popped Static: swept up by the vacuum and sorted into a chute.</summary>
        public bool IsBit { get; private set; }
        /// <summary>Static you can click to pop.</summary>
        public bool IsPoppable { get { return IsInteractive && !IsBit; } }
        /// <summary>A loose bit the vacuum (or a drone) can pick up.</summary>
        public bool IsSweepable { get { return IsInteractive && IsBit; } }

        private SphereCollider col;
        private MaterialPropertyBlock mpb;
        private float baseScale = 1f;
        private float speed;
        private float age;
        private float phase;
        private Vector3 velocity;
        private float hopClock;
        private Vector3 wanderDir;
        private float wanderTimer;
        private float hoverAmount;
        private float flash;
        private float squashT = 1f;
        private float stretch = 1f;
        private Vector3 stretchDir = Vector3.forward;
        private float stretchHold;
        private float baseEmission = 2.6f;
        private ShapeGlyph glyph;

        // Flight (fling to chute / Compressor, bounce out).
        private Vector3 flyFrom;
        private Vector3 flyTo;
        private float flyT;
        private float flyDuration;
        private float flyArc;
        private Action<Blob> onArrive;
        private Transform flyTargetTransform;

        // Held in the vacuum cluster.
        private Vector3 heldTarget;
        private Vector3 heldVelocity;

        // Popping / absorbing animation.
        private float animT;
        private Action<Blob> onPopped;
        private Vector3 absorbTarget;

        // Finale arrangement.
        private Vector3 arrangeTarget;
        private Vector3 bitHome;

        private void Awake()
        {
            col = GetComponent<SphereCollider>();
            mpb = new MaterialPropertyBlock();
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
            glyph = ShapeGlyph.Create(transform, ShapeGlyph.Mode.BlobSurface);
        }

        /// <summary>Configures a pooled blob. scaleMult already includes Mega size or split-child size.</summary>
        public void Init(BlobTypeDef type, BlobColor color, bool golden, bool mega, float scaleMult, int stability, double value, Vector3 position)
        {
            BalanceData b = Balance.B;
            Type = type;
            Color = color;
            IsGolden = golden;
            IsMega = mega;
            MaxStability = Mathf.Max(1, stability);
            Stability = MaxStability;
            BaseValue = value;
            speed = golden ? b.golden.speed : (type != null ? type.speed : 0.5f);
            if (mega) speed *= 0.7f;
            speed *= UnityEngine.Random.Range(0.85f, 1.15f);
            baseScale = (type != null ? type.scale : 1f) * scaleMult;
            age = 0f;
            phase = UnityEngine.Random.value * 10f;
            hopClock = UnityEngine.Random.value;
            velocity = Vector3.zero;
            wanderDir = UnityEngine.Random.insideUnitSphere;
            wanderDir.y = 0f;
            wanderTimer = 0f;
            hoverAmount = 0f;
            Hover = 0f;
            flash = 0f;
            squashT = 1f;
            stretch = 1f;
            stretchHold = 0f;
            ClaimedBy = null;
            IsBit = false;
            EventBlob = false;
            NoEscape = false;
            SpeedMult = 1f;
            Serial++;
            onArrive = null;
            onPopped = null;
            flyTargetTransform = null;
            baseEmission = b.juice.emissionStrength;
            position.y = GameManager.I.PlaneY;
            transform.SetParent(BlobSpawner.I != null ? BlobSpawner.I.transform : null, true);
            transform.position = position;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * baseScale * 0.2f;
            col.enabled = true;
            State = BlobState.Drifting;
            gameObject.layer = BlobSpawner.BlobLayer;
            if (glyph != null) glyph.SetShape(ShapeGlyph.ShapeFor(golden ? BlobColor.Gold : color));
            ApplyMaterial(0f);
        }

        /// <summary>Configures a pooled blob as a small Data bit of a color.</summary>
        public void InitBit(BlobTypeDef type, BlobColor color, double value, Vector3 position)
        {
            Init(type, color, false, false, Balance.B.bits.scale, 1, value, position);
            IsBit = true;
            speed = Balance.B.bits.drift;
            bitHome = position;
        }

        // ---------------- interaction ----------------

        /// <summary>Removes stability. Returns true when the blob reaches 0 and should pop.</summary>
        public bool Squish(int power)
        {
            Stability -= Mathf.Max(1, power);
            squashT = 0f;
            flash = 0.6f;
            return Stability <= 0;
        }

        public void BeginPop(Action<Blob> finished)
        {
            State = BlobState.Popping;
            col.enabled = false;
            animT = 0f;
            onPopped = finished;
            flash = 1f;
        }

        public void BeginAbsorb(Vector3 target)
        {
            State = BlobState.Absorbing;
            col.enabled = false;
            animT = 0f;
            absorbTarget = target;
        }

        public void SetHeld(bool held)
        {
            if (held)
            {
                State = BlobState.Held;
                heldVelocity = Vector3.zero;
                heldTarget = transform.position;
                if (ClaimedBy != null) ClaimedBy.ReleaseClaim(this);
            }
            else if (State == BlobState.Held)
            {
                State = BlobState.Drifting;
            }
        }

        public void SetHeldTarget(Vector3 target)
        {
            heldTarget = target;
        }

        /// <summary>Vacuum suction: pull toward the cursor and stretch along the pull direction.</summary>
        public void Pull(Vector3 cursor, float pullSpeed, float dt)
        {
            Vector3 to = cursor - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.0001f) return;
            Vector3 dir = to / dist;
            float step = Mathf.Min(dist, pullSpeed * dt);
            transform.position += dir * step;
            stretchDir = dir;
            float maxStretch = Balance.B.vacuum.maxStretch;
            stretch = Mathf.Lerp(stretch, Mathf.Lerp(1.15f, maxStretch, Mathf.Clamp01(pullSpeed * dt / Mathf.Max(0.01f, dist) * 6f)), 0.35f);
            stretchHold = 0.1f;
        }

        /// <summary>Flies in an arc to a world point, then calls arrive.</summary>
        public void FlyTo(Vector3 target, float duration, float arc, Action<Blob> arrive, Transform follow = null)
        {
            State = BlobState.Flying;
            col.enabled = false;
            flyFrom = transform.position;
            flyTo = target;
            flyTargetTransform = follow;
            flyT = 0f;
            flyDuration = Mathf.Max(0.05f, duration);
            flyArc = arc;
            onArrive = arrive;
            if (ClaimedBy != null) ClaimedBy.ReleaseClaim(this);
        }

        /// <summary>Wrong chute: bounce back out onto the field.</summary>
        public void BounceOut(Vector3 from)
        {
            Vector3 dir = transform.position - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-35f, 35f), 0f) * dir.normalized;
            Vector3 landing = from + dir * Balance.B.sort.bounceDistance + Vector3.forward * 1.2f;
            landing.y = GameManager.I.PlaneY;
            transform.position = new Vector3(transform.position.x, GameManager.I.PlaneY, transform.position.z);
            squashT = 0f;
            FlyTo(landing, 0.45f, 1.4f, b => b.ReturnToDrift());
        }

        public void ReturnToDrift()
        {
            State = BlobState.Drifting;
            col.enabled = true;
            transform.SetParent(BlobSpawner.I.transform, true);
            Vector3 p = transform.position;
            p.y = GameManager.I.PlaneY;
            transform.position = p;
            bitHome = p;
            squashT = 0f;
        }

        public void EnterCompressor(Transform parent, Vector3 localPos)
        {
            State = BlobState.Compressed;
            col.enabled = false;
            transform.SetParent(parent, false);
            transform.localPosition = localPos;
            squashT = 0f;
            flash = 0.5f;
        }

        public void Arrange(Vector3 target)
        {
            State = BlobState.Arranged;
            col.enabled = false;
            arrangeTarget = target;
            if (ClaimedBy != null) ClaimedBy.ReleaseClaim(this);
        }

        /// <summary>Moves an arranged blob's anchor (endings carry the swarm along with the station).</summary>
        public void ShiftArrange(Vector3 delta)
        {
            arrangeTarget += delta;
            if (State == BlobState.Arranged) transform.position += delta;
        }

        public void SetEscaping()
        {
            if (State == BlobState.Drifting) State = BlobState.Escaping;
        }

        /// <summary>Small extra movement on top of drifting (menu Static leaning toward the cursor).</summary>
        public void Nudge(Vector3 delta)
        {
            if (State != BlobState.Drifting) return;
            delta.y = 0f;
            transform.position += delta;
        }

        public void Flash(float amount)
        {
            flash = Mathf.Max(flash, amount);
        }

        // ---------------- update ----------------

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            switch (State)
            {
                case BlobState.Drifting: UpdateDrift(dt); break;
                case BlobState.Escaping: UpdateEscape(dt); break;
                case BlobState.Held: UpdateHeld(dt); break;
                case BlobState.Flying: UpdateFlight(dt); break;
                case BlobState.Arranged:
                    transform.position = Vector3.Lerp(transform.position, arrangeTarget, 1f - Mathf.Exp(-3f * dt));
                    break;
                case BlobState.Popping: UpdatePop(); return;
                case BlobState.Absorbing: UpdateAbsorb(dt); return;
            }
            UpdateVisual(dt);
        }

        private void UpdateBit(float dt)
        {
            // Bits sit where they fell and bob. Bit Magnet rolls them toward their own chute.
            Vector3 pos = transform.position;
            float magnet = GameStats.BitMagnet;
            if (magnet > 0f && (DayManager.I == null || DayManager.I.InShiftOrLastCall))
            {
                Chute c = Chute.ForBit(this);
                if (c != null)
                {
                    Vector3 to = c.transform.position - pos;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist <= c.CatchRadius * 0.9f)
                    {
                        FlyTo(c.Mouth, 0.3f, 0.6f, x => BlobActions.Sort(x, c, false));
                        return;
                    }
                    pos += to / Mathf.Max(0.01f, dist) * Balance.B.days.bitMagnetSpeed * magnet * dt;
                    bitHome = pos;
                }
            }
            else
            {
                Vector3 wander = new Vector3(Mathf.Sin(age * 0.7f + phase), 0f, Mathf.Cos(age * 0.6f + phase * 1.3f)) * Balance.B.bits.drift;
                pos = Vector3.Lerp(pos, bitHome + wander, 1f - Mathf.Exp(-2f * dt));
            }
            pos.y = GameManager.I.PlaneY;
            transform.position = pos;
        }

        private void UpdateDrift(float dt)
        {
            if (IsBit)
            {
                UpdateBit(dt);
                return;
            }
            if (BlobSpawner.I != null && BlobSpawner.I.IsFrozen) return;
            Vector3 target = BlobSpawner.I.AbsorbPoint;
            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.001f ? to / dist : Vector3.zero;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
            Vector3 desired;

            if (IsGolden)
            {
                wanderTimer -= dt;
                if (wanderTimer <= 0f)
                {
                    wanderTimer = UnityEngine.Random.Range(0.4f, 0.9f);
                    Vector2 r = UnityEngine.Random.insideUnitCircle.normalized;
                    wanderDir = new Vector3(r.x, 0f, r.y);
                }
                Vector3 inward = BlobSpawner.I.SteerInside(pos);
                desired = (wanderDir + inward * 2f).normalized * speed;
                bool canEscape = !NoEscape && !BlobSpawner.I.GoldenNeverEscapes;
                if (canEscape && age >= Balance.B.golden.escapeSeconds) State = BlobState.Escaping;
                float sparkleEvery = Balance.B.golden.sparkleInterval * (GameSettings.ReduceFlashing ? Balance.B.accessibility.sparkleIntervalReducedMult : 1f);
                if (UnityEngine.Random.value < dt / Mathf.Max(0.01f, sparkleEvery)) VFXPlayer.Sparkle(pos, 1);
            }
            else
            {
                string behavior = Type != null ? Type.behavior : "drift";
                switch (behavior)
                {
                    case "sine":
                        desired = dir * speed + perp * Mathf.Cos(age * 2.4f + phase) * speed * 1.6f;
                        break;
                    case "hop":
                        hopClock += dt;
                        float cycle = 1.0f;
                        float local = hopClock % cycle;
                        bool dashing = local > 0.7f;
                        desired = dir * speed * (dashing ? 4.2f : 0.25f) + perp * Mathf.Sin(phase) * speed * (dashing ? 1.5f : 0f);
                        if (dashing && local - dt <= 0.7f) squashT = 0.3f;
                        break;
                    default:
                        desired = dir * speed + perp * Mathf.Sin(age * 0.9f + phase) * speed * 0.35f;
                        break;
                }
                if (dist < Balance.B.absorb.radius + Radius)
                {
                    BlobSpawner.I.Absorb(this);
                    return;
                }
            }

            velocity = Vector3.Lerp(velocity, desired * SpeedMult, 1f - Mathf.Exp(-6f * dt));
            pos += velocity * dt;
            pos.y = GameManager.I.PlaneY;
            transform.position = pos;
        }

        private void UpdateEscape(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 away = BlobSpawner.I.EscapeDirection(pos);
            velocity = Vector3.Lerp(velocity, away * speed * 1.4f, 1f - Mathf.Exp(-4f * dt));
            pos += velocity * dt;
            pos.y = GameManager.I.PlaneY;
            transform.position = pos;
            if (UnityEngine.Random.value < dt / 0.12f) VFXPlayer.Sparkle(pos, 1);
            if (!BlobSpawner.I.IsInsideEscapeBounds(pos)) BlobSpawner.I.Despawn(this);
        }

        private void UpdateHeld(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 accel = (heldTarget - pos) * 140f;
            heldVelocity += accel * dt;
            heldVelocity *= Mathf.Exp(-11f * dt);
            pos += heldVelocity * dt;
            pos.y = GameManager.I.PlaneY;
            transform.position = pos;
            float v = heldVelocity.magnitude;
            if (v > 0.3f)
            {
                stretchDir = heldVelocity / v;
                stretch = Mathf.Lerp(stretch, 1f + Mathf.Min(0.4f, v * 0.05f), 0.3f);
                stretchHold = 0.05f;
            }
        }

        private void UpdateFlight(float dt)
        {
            if (flyTargetTransform != null) flyTo = flyTargetTransform.position;
            flyT += dt / flyDuration;
            float t = Mathf.Clamp01(flyT);
            float e = 1f - (1f - t) * (1f - t);
            Vector3 p = Vector3.Lerp(flyFrom, flyTo, e);
            p.y += flyArc * 4f * t * (1f - t);
            Vector3 delta = p - transform.position;
            transform.position = p;
            if (delta.sqrMagnitude > 0.0001f)
            {
                stretchDir = delta.normalized;
                stretch = 1.25f;
                stretchHold = 0.05f;
            }
            if (t >= 1f)
            {
                Action<Blob> cb = onArrive;
                onArrive = null;
                if (cb != null) cb(this);
                else ReturnToDrift();
            }
        }

        private void UpdatePop()
        {
            // Scale punch to 1.3x over 0.05 s (unscaled so hitstop never stretches it), then burst.
            float punchTime = Balance.B.juice.popPunchTime;
            animT += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(animT / punchTime);
            float s = Mathf.Lerp(1f, Balance.B.juice.popPunch, t);
            transform.localScale = Vector3.one * baseScale * s;
            flash = 1f;
            ApplyMaterial(1f);
            if (t >= 1f)
            {
                Action<Blob> cb = onPopped;
                onPopped = null;
                if (cb != null) cb(this);
            }
        }

        private void UpdateAbsorb(float dt)
        {
            animT += dt / 0.3f;
            transform.position = Vector3.Lerp(transform.position, absorbTarget, 1f - Mathf.Exp(-10f * dt));
            transform.localScale = Vector3.one * baseScale * Mathf.Max(0f, 1f - animT);
            if (animT >= 1f) BlobSpawner.I.Despawn(this);
        }

        private void UpdateVisual(float dt)
        {
            BalanceData b = Balance.B;
            hoverAmount = Mathf.MoveTowards(hoverAmount, Hover, dt * 10f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 5f);

            // Spawn grow-in.
            float grow = Mathf.Clamp01(age / 0.25f);
            grow = 1f + 2.70158f * Mathf.Pow(grow - 1f, 3f) + 1.70158f * Mathf.Pow(grow - 1f, 2f);

            // Squash and wobble: (1.2, 0.8, 1.2) springing back over 0.15 s.
            Vector3 squash = Vector3.one;
            if (squashT < 1f)
            {
                squashT = Mathf.Min(1f, squashT + dt / Mathf.Max(0.01f, b.juice.squashTime));
                float spring = Mathf.Exp(-5f * squashT) * Mathf.Cos(squashT * 18f) * (1f - squashT);
                squash = new Vector3(1f + (b.juice.squashX - 1f) * spring, 1f + (b.juice.squashY - 1f) * spring, 1f + (b.juice.squashX - 1f) * spring);
            }

            stretchHold -= dt;
            if (stretchHold <= 0f) stretch = Mathf.MoveTowards(stretch, 1f, dt * 4f);

            float hoverScale = 1f + (b.juice.hoverScale - 1f) * hoverAmount;
            float s = baseScale * hoverScale * grow;
            if (State == BlobState.Compressed) s *= 0.55f;
            float inv = 1f / Mathf.Sqrt(Mathf.Max(0.01f, stretch));
            transform.localScale = new Vector3(s * squash.x * inv, s * squash.y * inv, s * squash.z * stretch);

            Quaternion targetRot;
            if (stretch > 1.02f)
            {
                Vector3 d = stretchDir;
                d.y = 0f;
                targetRot = d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d) : transform.rotation;
            }
            else
            {
                Camera cam = GameManager.I.Cam;
                Vector3 toCam = cam != null ? cam.transform.position - transform.position : Vector3.back;
                toCam.y = 0f;
                targetRot = toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam) : Quaternion.identity;
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-12f * dt));

            ApplyMaterial(hoverAmount);
        }

        private void ApplyMaterial(float hover)
        {
            if (bodyRenderer == null) return;
            UnityEngine.Color c = Color == BlobColor.Prism ? UnityEngine.Color.white : Palette.ForBlob(Color);
            bodyRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(IdColor, c);
            float emission = baseEmission * (1f + Balance.B.juice.hoverEmission * hover);
            if (IsGolden) emission *= GameSettings.ReduceFlashing ? Balance.B.accessibility.goldenEmissionReduced : 1.35f;
            if (IsBit) emission *= 1.7f;
            mpb.SetFloat(IdMode, IsBit ? 1f : 0f);
            mpb.SetFloat(IdEmission, emission);
            mpb.SetFloat(IdFlash, flash);
            mpb.SetFloat(IdRainbow, IsPrism ? 1f : 0f);
            mpb.SetFloat(IdPhase, phase);
            mpb.SetFloat(IdWobble, IsMega ? 0.07f : 0.05f);
            bodyRenderer.SetPropertyBlock(mpb);
        }

        public void ResetForPool()
        {
            State = BlobState.Inactive;
            ClaimedBy = null;
            onArrive = null;
            onPopped = null;
            flyTargetTransform = null;
            Hover = 0f;
        }
    }
}
