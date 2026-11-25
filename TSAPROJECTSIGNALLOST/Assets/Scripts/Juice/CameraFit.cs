using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Keeps the same horizontal view on any aspect ratio (16:9, 16:10, 4:3) by adjusting vertical FOV,
    /// so the play field and bottom-row chutes always fit on screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        [SerializeField] private float referenceVerticalFov = 50f;
        [SerializeField] private float referenceAspect = 16f / 9f;

        private Camera cam;
        private float lastAspect;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            if (!Mathf.Approximately(cam.aspect, lastAspect)) Apply();
        }

        private void Apply()
        {
            lastAspect = cam.aspect;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(referenceVerticalFov * 0.5f * Mathf.Deg2Rad) * referenceAspect);
            float vFov = 2f * Mathf.Atan(Mathf.Tan(hFov * 0.5f) / Mathf.Max(0.5f, cam.aspect)) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Max(referenceVerticalFov, vFov);
        }
    }
}
