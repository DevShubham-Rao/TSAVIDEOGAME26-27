using UnityEngine;

namespace SignalLost
{
    /// <summary>Spawns pooled coins for payouts and tracks where the Data counter sits in the world.</summary>
    public class CoinFlock : MonoBehaviour
    {
        public static CoinFlock I { get; private set; }

        [SerializeField] private Pool pool;
        [SerializeField] private float targetDepth = 5f;

        public Vector3 TargetWorld { get; private set; }

        private void Awake()
        {
            I = this;
        }

        private void LateUpdate()
        {
            Camera cam = GameManager.I != null ? GameManager.I.Cam : Camera.main;
            if (cam == null) return;
            Vector2 screen = HUD.I != null ? HUD.I.CoinTargetScreen : new Vector2(Screen.width * 0.5f, Screen.height * 0.92f);
            TargetWorld = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, targetDepth));
        }

        public int Burst(Vector3 worldPos, double amount, int count)
        {
            if (pool == null || count <= 0) return 0;
            int maxActive = Balance.B.juice.maxActiveCoins;
            int room = maxActive - pool.CountActive;
            if (room <= 0) return 0;
            count = Mathf.Min(count, room);
            double share = amount / count;
            float flight = Balance.B.juice.coinFlightTime;
            for (int i = 0; i < count; i++)
            {
                Coin c = pool.Get<Coin>();
                if (c == null) continue;
                float delay = count > 10 ? Random.Range(0.05f, 0.35f) : Random.Range(0.02f, 0.12f);
                c.Launch(worldPos + Random.insideUnitSphere * 0.2f, share, flight * Random.Range(0.85f, 1.25f), delay);
            }
            return count;
        }

        public void Landed(Coin c, double share)
        {
            GameManager.I.CoinArrived(share);
            AudioManager.PlayVaried("coin", 0.95f, 1.15f, 0.45f);
            pool.Release(c.gameObject);
        }
    }
}
