using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// References to the generated prefabs and materials. Built by Tools > Build Placeholder Models and
    /// assigned to the GameManager by the scene builder. Swap any prefab's Visual child to restyle it.
    /// </summary>
    [CreateAssetMenu(menuName = "Signal Lost/Game Assets", fileName = "GameAssets")]
    public class GameAssets : ScriptableObject
    {
        [Header("Prefabs")]
        public GameObject blobPrefab;
        public GameObject coinPrefab;
        public GameObject dronePrefab;
        public GameObject stationSegmentPrefab;

        [Header("VFX prefabs")]
        public GameObject popVfx;
        public GameObject sparkleVfx;
        public GameObject confettiVfx;
        public GameObject vacuumSwirlVfx;
        public GameObject shockwaveVfx;

        [Header("Materials")]
        public Material lineMaterial;
        public Material blobMaterial;
        public Material skyboxMaterial;
        public Material glyphMaterial;

        [Header("UI text materials (TextMeshPro)")]
        public Material tmpGlowMaterial;
        public Material tmpOutlineMaterial;
    }
}
