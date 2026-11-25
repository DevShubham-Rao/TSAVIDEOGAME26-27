using System.Collections.Generic;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Keeps the number of Sweeper Drones in sync with upgrades and timed "+3 drones" buffs.</summary>
    public class DroneManager : MonoBehaviour
    {
        public static DroneManager I { get; private set; }

        private readonly List<Drone> drones = new List<Drone>();
        private float pausedUntil;
        private float checkTimer;

        public bool Paused { get { return Time.time < pausedUntil; } }
        public int Count { get { return drones.Count; } }

        private void Awake()
        {
            I = this;
        }

        private void OnEnable()
        {
            EventBus.UpgradeBought += OnUpgrade;
            EventBus.BuffsChanged += Sync;
            EventBus.GameReady += Sync;
        }

        private void OnDisable()
        {
            EventBus.UpgradeBought -= OnUpgrade;
            EventBus.BuffsChanged -= Sync;
            EventBus.GameReady -= Sync;
        }

        private void OnUpgrade(string id, int level)
        {
            if (id == "drone_count") Sync();
        }

        public void Pause(float seconds)
        {
            pausedUntil = Mathf.Max(pausedUntil, Time.time + seconds);
        }

        private void Update()
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = 1f;
                Sync();
            }
        }

        public void Sync()
        {
            if (GameManager.I == null || !GameManager.I.IsReady) return;
            GameAssets a = GameManager.I.Assets;
            if (a == null || a.dronePrefab == null) return;
            int want = GameStats.DroneCount;
            int permanent = GameStats.PermanentDroneCount;
            while (drones.Count < want)
            {
                Vector3 from = GameManager.I.Cam.transform.position + GameManager.I.Cam.transform.forward * 4f + Vector3.up * 2f;
                if (GameManager.I.Station != null) from = GameManager.I.Station.transform.position + Vector3.up * 3f;
                GameObject go = Instantiate(a.dronePrefab, from, Quaternion.identity, transform);
                Drone d = go.GetComponent<Drone>();
                if (d == null) d = go.AddComponent<Drone>();
                d.Init(drones.Count >= permanent, from);
                drones.Add(d);
                if (GameManager.I.State != GameState.Loading)
                {
                    AudioManager.Play("whoosh", 1.3f, 0.5f);
                    VFXPlayer.Sparkle(from, 10, Palette.Cyan);
                }
            }
            while (drones.Count > want)
            {
                Drone d = drones[drones.Count - 1];
                drones.RemoveAt(drones.Count - 1);
                VFXPlayer.Sparkle(d.transform.position, 8, Palette.Cyan);
                Destroy(d.gameObject);
            }
        }

        public void RefreshSkins()
        {
            foreach (Drone d in drones) if (d != null) d.ApplySkin();
        }
    }
}
