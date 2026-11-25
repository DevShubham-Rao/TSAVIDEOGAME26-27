using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Spawns blobs from the screen edges, keeps the registry of active blobs, and handles absorbing,
    /// freezing, Golden Hour and the finale arrangement.
    /// </summary>
    public class BlobSpawner : MonoBehaviour
    {
        public static BlobSpawner I { get; private set; }
        public static int BlobLayer { get; private set; }

        [SerializeField] private Pool pool;
        [SerializeField] private Transform absorbPoint;

        public readonly List<Blob> Active = new List<Blob>();

        private float spawnAccumulator;
        private float frozenUntil;
        private float goldenHourClock;
        private float goldenHourUntil;
        private float viewportTop = 0.62f;
        private Vector2Int lastScreen;
        private bool spawningSuspended;
        private bool initialBurstDone;
        private int questGoldToday;

        public bool IsFrozen { get { return Time.time < frozenUntil; } }
        public bool GoldenHourActive { get { return Time.time < goldenHourUntil || (BuffSystem.I != null && BuffSystem.I.IsActive("golden_hour")); } }

        public bool GoldenNeverEscapes
        {
            get { return GoldenHourActive; }
        }

        public Vector3 AbsorbPoint
        {
            get
            {
                Vector3 p = absorbPoint != null ? absorbPoint.position : new Vector3(0f, 0f, 4.5f);
                p.y = GameManager.I != null ? GameManager.I.PlaneY : 1.5f;
                return p;
            }
        }

        public bool SpawningSuspended
        {
            get { return spawningSuspended; }
            set { spawningSuspended = value; }
        }

        private void Awake()
        {
            I = this;
            int layer = LayerMask.NameToLayer("Blob");
            BlobLayer = layer >= 0 ? layer : 0;
        }

        private void OnEnable()
        {
            EventBus.GameReady += OnGameReady;
            EventBus.DayStarted += OnDayStarted;
            EventBus.BlobPopped += OnBlobPopped;
        }

        private void OnDisable()
        {
            EventBus.GameReady -= OnGameReady;
            EventBus.DayStarted -= OnDayStarted;
            EventBus.BlobPopped -= OnBlobPopped;
        }

        private void OnGameReady()
        {
            RecomputeView();
            if (GameManager.I.State == GameState.MainMenu)
            {
                // A crowd of Static already floating behind the main menu.
                int n = MenuCap;
                for (int i = 0; i < n; i++)
                {
                    Blob b = SpawnAmbient();
                    if (b != null) b.transform.position = RandomInteriorPoint();
                }
            }
        }

        private void OnDayStarted(int day)
        {
            if (TutorialDirector.I != null && TutorialDirector.I.GuidedActive) return;
            initialBurstDone = false;
            questGoldToday = 0;
            SpawnOpeningBurst(Balance.B.spawn.initialBurst);
        }

        private static int MenuCap
        {
            get
            {
                MenuConfig m = Balance.B.menu;
                return GameSettings.S != null && GameSettings.S.quality == 0 ? m.maxBlobsLow : m.maxBlobs;
            }
        }

        /// <summary>Menu Static flinch when a neighbor is popped, then drift back.</summary>
        private void OnBlobPopped(Blob popped, double payout)
        {
            if (GameManager.I == null || GameManager.I.State != GameState.MainMenu || popped == null) return;
            MenuConfig m = Balance.B.menu;
            Vector3 p = popped.Position;
            foreach (Blob b in Active.ToArray())
            {
                if (b == popped || !b.IsInteractive || b.IsGolden) continue;
                Vector3 d = b.Position - p;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > m.flinchRadius || dist < 0.01f) continue;
                Vector3 dest = b.Position + d / dist * m.flinchDistance * (1.3f - dist / m.flinchRadius);
                b.Flash(0.4f);
                b.FlyTo(dest, 0.25f, 0.5f, x => x.ReturnToDrift());
            }
        }

        /// <summary>Menu Static near the cursor perk up and lean toward it.</summary>
        private void UpdateMenuCrowd()
        {
            if (InputReader.I == null || GameManager.I.Cam == null) return;
            Ray r = GameManager.I.Cam.ScreenPointToRay(InputReader.I.PointerPosition);
            if (!GameManager.I.PlayPlane.Raycast(r, out float d)) return;
            Vector3 cursor = r.GetPoint(d);
            float radius = Balance.B.menu.lookRadius;
            for (int i = 0; i < Active.Count; i++)
            {
                Blob b = Active[i];
                if (!b.IsInteractive) continue;
                Vector3 to = cursor - b.Position;
                to.y = 0f;
                float near = 1f - Mathf.Clamp01(to.magnitude / radius);
                b.Hover = near * 0.7f;
                if (near > 0f && to.sqrMagnitude > 0.0001f) b.Nudge(to.normalized * near * 0.25f * Time.deltaTime);
            }
        }

        /// <summary>Puts blobs on screen right away so the player can pop within seconds.</summary>
        public void SpawnOpeningBurst(int count)
        {
            if (initialBurstDone && FieldCount() >= count) return;
            initialBurstDone = true;
            int n = Mathf.Min(count, GameStats.MaxBlobs) - FieldCount();
            for (int i = 0; i < n; i++)
            {
                Blob b = SpawnRandom(false);
                if (b != null) b.transform.position = RandomInteriorPoint();
            }
        }

        /// <summary>Removes every blob on the field (not inside the Compressor). Used by the tutorial.</summary>
        public void ClearField()
        {
            foreach (Blob b in Active.ToArray())
            {
                if (b.State == BlobState.Compressed || b.State == BlobState.Popping) continue;
                VFXPlayer.Pop(b.Position, BlobActions.ColorOf(b), 6);
                Despawn(b);
            }
        }

        /// <summary>Spawns a tutorial blob of a color at a point (bigger, slower, never golden).</summary>
        public Blob SpawnTutorial(BlobColor color, Vector3 pos, float scale, float speedMult)
        {
            Blob b = SpawnColor(color, pos, scale, false);
            if (b != null) b.SpeedMult = speedMult;
            return b;
        }

        private void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.IsReady) return;
            Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != lastScreen) RecomputeView();

            if (gm.State == GameState.Finale || gm.State == GameState.Ending || spawningSuspended) return;

            float dt = Time.deltaTime;
            if (gm.State == GameState.MainMenu)
            {
                // A crowd of Static behind the main menu, in every color.
                MenuConfig m = Balance.B.menu;
                spawnAccumulator = Mathf.Min(spawnAccumulator + dt * m.spawnPerSecond, 4f);
                while (spawnAccumulator >= 1f)
                {
                    spawnAccumulator -= 1f;
                    if (FieldCount() < MenuCap) SpawnAmbient();
                }
                UpdateMenuCrowd();
                return;
            }
            if (gm.State != GameState.Playing) return;
            if (DayManager.I != null && !DayManager.I.SpawningAllowed) return;
            if (TutorialDirector.I != null && TutorialDirector.I.SpawnsBlocked) return;

            bool goldenHour = GoldenHourActive;
            float rate = goldenHour ? Balance.B.golden.goldenHourSpawnPerSecond : GameStats.SpawnPerSecond;
            if (IsFrozen) rate = 0f;
            spawnAccumulator = Mathf.Min(spawnAccumulator + dt * rate, 4f);
            int cap = GameStats.MaxBlobs;
            while (spawnAccumulator >= 1f)
            {
                spawnAccumulator -= 1f;
                if (FieldCount() < cap || goldenHour) SpawnRandom(goldenHour);
            }
        }

        public void StartGoldenHour(float seconds)
        {
            goldenHourUntil = Mathf.Max(goldenHourUntil, Time.time + seconds);
            AudioManager.Play("golden");
            if (HUD.I != null) HUD.I.Toast("GOLDEN HOUR!", Palette.Gold);
        }

        /// <summary>Static that count toward the on-screen cap (not bits, not inside the Compressor).</summary>
        public int FieldCount()
        {
            int n = 0;
            for (int i = 0; i < Active.Count; i++)
            {
                Blob b = Active[i];
                if (b.State != BlobState.Compressed && !b.EventBlob && !b.IsBit) n++;
            }
            return n;
        }

        /// <summary>Loose bits on the floor (not held, flying or inside the Compressor).</summary>
        public int BitCount()
        {
            int n = 0;
            for (int i = 0; i < Active.Count; i++) if (Active[i].IsSweepable) n++;
            return n;
        }

        /// <summary>Popped Static scatters its value as small bits of its color around where it popped.</summary>
        public void SpawnBits(Vector3 pos, BlobColor color, int count, double totalValue, bool golden)
        {
            count = Mathf.Max(1, count);
            BlobColor c = golden ? BlobColor.Gold : color;
            BlobTypeDef type = TypeForColor(color == BlobColor.Gold || color == BlobColor.Prism ? BlobColor.Red : color);
            double each = totalValue / count;
            float spread = Balance.B.bits.spread;
            for (int i = 0; i < count; i++)
            {
                if (pool == null) return;
                Blob bit = pool.Get<Blob>();
                if (bit == null) return;
                float a = i * 2.39996f + UnityEngine.Random.value * 0.6f;
                float rr = spread * (0.35f + 0.65f * Mathf.Sqrt((i + 0.5f) / count)) * UnityEngine.Random.Range(0.8f, 1.2f);
                Vector3 dest = ClampToField(pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr);
                bit.InitBit(type, c, each, pos);
                Active.Add(bit);
                bit.FlyTo(dest, UnityEngine.Random.Range(0.22f, 0.34f), 0.55f, x => x.ReturnToDrift());
            }
        }

        /// <summary>Keeps a point inside the visible play area.</summary>
        public Vector3 ClampToField(Vector3 p)
        {
            Camera cam = GameManager.I != null ? GameManager.I.Cam : null;
            if (cam == null) return p;
            Vector3 vp = cam.WorldToViewportPoint(p);
            if (vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < viewportTop) return p;
            vp.x = Mathf.Clamp(vp.x, 0.06f, 0.94f);
            vp.y = Mathf.Clamp(vp.y, 0.08f, viewportTop - 0.02f);
            PlanePoint(new Vector3(vp.x, vp.y, 0f), out Vector3 q);
            return q;
        }

        // ---------------- spawning ----------------

        /// <summary>Menu crowd: every regular color, plus a rare Gold one that runs from the cursor.</summary>
        private Blob SpawnAmbient()
        {
            List<BlobTypeDef> types = Balance.B.blobs;
            if (UnityEngine.Random.value < 0.03f) return SpawnGolden(RandomEdgePoint(), false);
            BlobTypeDef type = types[UnityEngine.Random.Range(0, Mathf.Min(3, types.Count))];
            float scale = UnityEngine.Random.Range(0.75f, 1.15f);
            return Spawn(type, Palette.ParseColor(type.color), false, false, scale, type.stability, type.value * Balance.B.dataValueScale, RandomEdgePoint());
        }

        public Blob SpawnRandom(bool forceGolden)
        {
            BalanceData b = Balance.B;
            if (Active.Count >= b.spawn.hardCap + 60) return null;
            bool golden = forceGolden || UnityEngine.Random.value < GameStats.GoldenChance;
            if (golden && !forceGolden && !GameStats.GoldUnlockedByAntenna)
            {
                // Before Antenna 3 a mail deal opens Gold: exactly one shows up that day (a first sighting, not a jackpot).
                if (questGoldToday >= 1) golden = false;
                else questGoldToday++;
            }
            if (golden) return SpawnGolden(RandomEdgePoint(), false);

            BlobTypeDef type = PickType();
            if (type == null) return null;
            BlobColor color = Palette.ParseColor(type.color);
            bool mega = false;
            float scale = 1f;
            int stability = type.stability;
            double value = type.value;
            if (type.id != "prism" && GameStats.Antenna >= b.mega.unlockAntenna && UnityEngine.Random.value < b.mega.chance)
            {
                mega = true;
                scale = b.mega.scale;
                stability = b.mega.stability;
                value = b.mega.value;
            }
            return Spawn(type, color, false, mega, scale, stability, value * b.dataValueScale, RandomEdgePoint());
        }

        public Blob SpawnGolden(Vector3 pos, bool eventBlob)
        {
            BalanceData b = Balance.B;
            BlobTypeDef baseType = PickType() ?? b.blobs[0];
            Blob blob = Spawn(baseType, BlobColor.Gold, true, false, 1f, b.golden.stability, GameStats.BestBlobValue * b.golden.valueMultOfBest, pos);
            if (blob != null)
            {
                blob.EventBlob = eventBlob;
                AudioManager.Play("golden", 1f, 0.7f);
                if (GameManager.I.State == GameState.Playing) EventBus.RaiseGoldenSpawned(blob);
            }
            return blob;
        }

        /// <summary>Spawns a regular blob of a given color (used by splits and Signal Rush).</summary>
        public Blob SpawnColor(BlobColor color, Vector3 pos, float scaleMult, bool eventBlob)
        {
            BlobTypeDef type = TypeForColor(color);
            if (type == null) return null;
            Blob b = Spawn(type, color, false, false, scaleMult, type.stability, type.value * Balance.B.dataValueScale, pos);
            if (b != null) b.EventBlob = eventBlob;
            return b;
        }

        public Blob Spawn(BlobTypeDef type, BlobColor color, bool golden, bool mega, float scale, int stability, double value, Vector3 pos)
        {
            if (pool == null) return null;
            Blob blob = pool.Get<Blob>();
            if (blob == null) return null;
            blob.Init(type, color, golden, mega, scale, stability, value, pos);
            Active.Add(blob);
            return blob;
        }

        public BlobTypeDef TypeForColor(BlobColor color)
        {
            string c = color.ToString().ToLowerInvariant();
            foreach (BlobTypeDef t in Balance.B.blobs) if (t.color == c) return t;
            return Balance.B.blobs.Count > 0 ? Balance.B.blobs[0] : null;
        }

        private BlobTypeDef PickType()
        {
            float total = 0f;
            foreach (BlobTypeDef t in Balance.B.blobs) if (GameStats.IsUnlocked(t)) total += t.weight;
            if (total <= 0f) return Balance.B.blobs.Count > 0 ? Balance.B.blobs[0] : null;
            float r = UnityEngine.Random.value * total;
            foreach (BlobTypeDef t in Balance.B.blobs)
            {
                if (!GameStats.IsUnlocked(t)) continue;
                r -= t.weight;
                if (r <= 0f) return t;
            }
            return Balance.B.blobs[0];
        }

        public void Despawn(Blob b)
        {
            if (b == null) return;
            Active.Remove(b);
            if (Vacuum.I != null) Vacuum.I.Forget(b);
            if (b.ClaimedBy != null) b.ClaimedBy.ReleaseClaim(b);
            b.ResetForPool();
            if (pool != null) pool.Release(b.gameObject);
            else b.gameObject.SetActive(false);
        }

        /// <summary>A blob reached the antenna: it is absorbed and simply disappears (a soft nudge, no fail).</summary>
        public void Absorb(Blob b)
        {
            b.BeginAbsorb(AbsorbPoint + Vector3.up * 0.8f);
            GameManager.I.NotifyAbsorbed();
            AudioManager.Play("absorb", UnityEngine.Random.Range(0.9f, 1.1f), 0.35f);
        }

        public void Freeze(float seconds)
        {
            frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
        }

        // ---------------- view / edges ----------------

        public void RecomputeView()
        {
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            Camera cam = GameManager.I != null ? GameManager.I.Cam : Camera.main;
            if (cam == null) return;
            Vector3 far = AbsorbPoint + Vector3.forward * 1.2f;
            viewportTop = Mathf.Clamp(cam.WorldToViewportPoint(far).y, 0.3f, 0.95f);
        }

        private bool PlanePoint(Vector3 viewport, out Vector3 p)
        {
            Camera cam = GameManager.I.Cam;
            Ray r = cam.ViewportPointToRay(viewport);
            if (GameManager.I.PlayPlane.Raycast(r, out float d))
            {
                p = r.GetPoint(d);
                return true;
            }
            p = AbsorbPoint;
            return false;
        }

        /// <summary>A point just off the left, right or bottom screen edge on the play plane.</summary>
        public Vector3 RandomEdgePoint()
        {
            float m = 0.04f;
            float side = UnityEngine.Random.value;
            Vector3 vp;
            if (side < 0.38f) vp = new Vector3(-m, UnityEngine.Random.Range(0.05f, viewportTop), 0f);
            else if (side < 0.76f) vp = new Vector3(1f + m, UnityEngine.Random.Range(0.05f, viewportTop), 0f);
            else vp = new Vector3(UnityEngine.Random.Range(0.08f, 0.92f), -m, 0f);
            PlanePoint(vp, out Vector3 p);
            return p;
        }

        public Vector3 RandomInteriorPoint()
        {
            Vector3 vp = new Vector3(UnityEngine.Random.Range(0.18f, 0.82f), UnityEngine.Random.Range(0.25f, viewportTop - 0.05f), 0f);
            PlanePoint(vp, out Vector3 p);
            return p;
        }

        public Vector3 ViewCenter
        {
            get
            {
                PlanePoint(new Vector3(0.5f, viewportTop * 0.55f, 0f), out Vector3 p);
                return p;
            }
        }

        /// <summary>Steering vector that keeps wandering (Golden) blobs on screen.</summary>
        public Vector3 SteerInside(Vector3 p)
        {
            Vector3 vp = GameManager.I.Cam.WorldToViewportPoint(p);
            bool inside = vp.x > 0.07f && vp.x < 0.93f && vp.y > 0.1f && vp.y < viewportTop;
            if (inside) return Vector3.zero;
            Vector3 to = ViewCenter - p;
            to.y = 0f;
            return to.normalized;
        }

        public Vector3 EscapeDirection(Vector3 p)
        {
            Vector3 away = p - ViewCenter;
            away.y = 0f;
            away.z *= 0.3f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            return away.normalized;
        }

        public bool IsInsideEscapeBounds(Vector3 p)
        {
            Vector3 vp = GameManager.I.Cam.WorldToViewportPoint(p);
            return vp.x > -0.08f && vp.x < 1.08f && vp.y > -0.12f && vp.y < 1f;
        }

        // ---------------- queries ----------------

        public Blob FindNearest(Vector3 p, float maxDist, Func<Blob, bool> filter)
        {
            Blob best = null;
            float bestD = maxDist * maxDist;
            for (int i = 0; i < Active.Count; i++)
            {
                Blob b = Active[i];
                if (filter != null && !filter(b)) continue;
                Vector3 d = b.Position - p;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestD)
                {
                    bestD = sq;
                    best = b;
                }
            }
            return best;
        }

        public void CollectInRadius(Vector3 p, float radius, List<Blob> results, Func<Blob, bool> filter)
        {
            results.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < Active.Count; i++)
            {
                Blob b = Active[i];
                if (filter != null && !filter(b)) continue;
                Vector3 d = b.Position - p;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) results.Add(b);
            }
        }

        // ---------------- finale ----------------

        /// <summary>Every blob on screen freezes and arranges into one big shape (a five-point star).</summary>
        public void ArrangeFinale()
        {
            if (Vacuum.I != null) Vacuum.I.DropAll();
            foreach (Blob b in Active.ToArray()) if (b.IsBit && b.State != BlobState.Compressed) Despawn(b);
            PlanePoint(new Vector3(0.5f, viewportTop * 0.8f, 0f), out Vector3 center);
            int target = 70;
            int need = target - FieldCount();
            for (int i = 0; i < need; i++) SpawnRandom(false);
            List<Blob> field = new List<Blob>();
            foreach (Blob b in Active)
            {
                if (b.State == BlobState.Drifting || b.State == BlobState.Escaping || b.State == BlobState.Held) field.Add(b);
            }
            for (int i = 0; i < field.Count; i++)
            {
                float t = i / (float)field.Count;
                field[i].Arrange(center + StarPoint(t) * 3.1f);
            }
        }

        private static Vector3 StarPoint(float t)
        {
            // Walk the outline of a 5-point star.
            float seg = t * 10f;
            int i = Mathf.FloorToInt(seg) % 10;
            float f = seg - Mathf.Floor(seg);
            Vector3 a = StarVertex(i);
            Vector3 b = StarVertex((i + 1) % 10);
            return Vector3.Lerp(a, b, f);
        }

        private static Vector3 StarVertex(int i)
        {
            float r = i % 2 == 0 ? 1f : 0.45f;
            float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            return new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r * 0.8f);
        }

        public void ReleaseFinale()
        {
            foreach (Blob b in Active.ToArray())
            {
                if (b.State == BlobState.Arranged) b.ReturnToDrift();
            }
        }

        /// <summary>Clear Channel ending: the Static is scrubbed away quietly, no coins.</summary>
        public void ScrubAll()
        {
            foreach (Blob b in Active.ToArray())
            {
                if (b.State == BlobState.Compressed) continue;
                VFXPlayer.Pop(b.Position, UnityEngine.Color.white, 6);
                Despawn(b);
            }
        }

        /// <summary>Become the Signal ending: blobs swarm the station.</summary>
        public void SwarmTo(Vector3 point)
        {
            foreach (Blob b in Active.ToArray())
            {
                if (b.State == BlobState.Compressed) continue;
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * 1.2f;
                b.FlyTo(point + jitter, UnityEngine.Random.Range(0.6f, 1.6f), UnityEngine.Random.Range(1f, 3f), x => x.Arrange(x.Position));
            }
        }
    }
}
