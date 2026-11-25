using System;
using UnityEngine;
using UnityEngine.Pool;

namespace SignalLost
{
    /// <summary>
    /// Pool component wrapping UnityEngine.Pool.ObjectPool. Blobs, coins and floating text come from here;
    /// nothing is Instantiated or Destroyed per pop.
    /// </summary>
    public class Pool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private int prewarm = 0;
        [SerializeField] private int maxSize = 2000;

        private ObjectPool<GameObject> pool;
        private Func<GameObject> factory;
        private Transform container;

        public int CountActive { get { return pool != null ? pool.CountActive : 0; } }

        public GameObject Prefab
        {
            get { return prefab; }
            set { prefab = value; }
        }

        private void Awake()
        {
            EnsurePool();
        }

        private void Start()
        {
            if (prewarm > 0 && (prefab != null || factory != null))
            {
                GameObject[] temp = new GameObject[prewarm];
                for (int i = 0; i < prewarm; i++) temp[i] = pool.Get();
                for (int i = 0; i < prewarm; i++) pool.Release(temp[i]);
            }
        }

        /// <summary>Optional code factory used when no prefab is assigned (UI elements built in code).</summary>
        public void SetFactory(Func<GameObject> f, Transform parent = null)
        {
            factory = f;
            if (parent != null) container = parent;
            EnsurePool();
        }

        private void EnsurePool()
        {
            if (container == null) container = transform;
            if (pool != null) return;
            pool = new ObjectPool<GameObject>(Create, OnGet, OnRelease, OnDestroyItem, false, 32, maxSize);
        }

        private GameObject Create()
        {
            GameObject go;
            if (factory != null) go = factory();
            else if (prefab != null) go = Instantiate(prefab, container);
            else go = new GameObject("PooledItem");
            go.SetActive(false);
            return go;
        }

        private void OnGet(GameObject go)
        {
            go.SetActive(true);
        }

        private void OnRelease(GameObject go)
        {
            go.SetActive(false);
            if (go.transform.parent != container) go.transform.SetParent(container, false);
        }

        private static void OnDestroyItem(GameObject go)
        {
            if (go != null) Destroy(go);
        }

        public GameObject Get()
        {
            EnsurePool();
            return pool.Get();
        }

        public T Get<T>() where T : Component
        {
            GameObject go = Get();
            return go != null ? go.GetComponent<T>() : null;
        }

        public void Release(GameObject go)
        {
            if (go == null || pool == null) return;
            if (!go.activeSelf) return;
            pool.Release(go);
        }
    }
}
