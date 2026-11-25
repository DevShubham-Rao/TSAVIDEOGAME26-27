using TMPro;
using UnityEngine;

namespace SignalLost
{
    /// <summary>Canvas layer that owns the FloatingText pool and converts world points to UI positions.</summary>
    [RequireComponent(typeof(Pool))]
    public class FloatingTextLayer : MonoBehaviour
    {
        public static FloatingTextLayer I { get; private set; }

        [SerializeField] private int maxActive = 140;
        [SerializeField] private float fontSize = 40f;

        private Pool pool;
        private RectTransform rect;

        private void Awake()
        {
            I = this;
            rect = (RectTransform)transform;
            pool = GetComponent<Pool>();
            pool.SetFactory(Create, transform);
        }

        private GameObject Create()
        {
            TextMeshProUGUI t = UIFactory.OutlineText(transform, "FloatingText", "+1", fontSize, Color.white);
            t.enableWordWrapping = false;
            t.rectTransform.sizeDelta = new Vector2(400f, 80f);
            FloatingText ft = t.gameObject.AddComponent<FloatingText>();
            ft.Bind(t);
            return t.gameObject;
        }

        public void ShowWorld(Vector3 world, string text, Color color, float size)
        {
            Camera cam = GameManager.I != null ? GameManager.I.Cam : Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            ShowScreen(sp, text, color, size);
        }

        public void ShowScreen(Vector2 screen, string text, Color color, float size)
        {
            if (pool.CountActive >= maxActive) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local)) return;
            FloatingText ft = pool.Get<FloatingText>();
            if (ft == null) return;
            ft.transform.SetAsLastSibling();
            ft.Play(local, text, color, size);
        }

        public void Release(FloatingText ft)
        {
            pool.Release(ft.gameObject);
        }
    }
}
