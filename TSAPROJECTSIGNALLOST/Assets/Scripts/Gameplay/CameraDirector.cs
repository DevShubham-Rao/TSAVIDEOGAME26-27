using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Moves the camera rig: a slow drifting orbit behind the main menu, and an eased 0.8 s move into the
    /// gameplay pose (the pose the scene was built with). Camera shake lives on the child camera.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        public static CameraDirector I { get; private set; }

        [SerializeField] private Transform lookTarget;

        private enum Mode { Menu, Gameplay, Transition, Cutscene }

        private Vector3 gameplayPos;
        private Quaternion gameplayRot;
        private Mode mode = Mode.Gameplay;
        private bool transitionToMenu;
        private Vector3 fromPos;
        private Quaternion fromRot;
        private float t;
        private float duration;
        private float driftTime;

        public bool InTransition { get { return mode == Mode.Transition; } }

        private Vector3 shotPos;
        private Quaternion shotRot;

        /// <summary>Cutscene shot: orbit the station at this pitch / yaw / distance, eased in over blend seconds.</summary>
        public void SetShot(float pitch, float yaw, float distance, float lookHeight, float blend)
        {
            Vector3 baseP = lookTarget != null ? lookTarget.position : gameplayPos + gameplayRot * Vector3.forward * 20f;
            shotRot = Quaternion.Euler(pitch, yaw, 0f);
            shotPos = baseP + Vector3.up * lookHeight - shotRot * Vector3.forward * distance;
            fromPos = transform.position;
            fromRot = transform.rotation;
            t = 0f;
            duration = Mathf.Max(0.01f, blend);
            mode = Mode.Cutscene;
        }

        /// <summary>Back to the gameplay pose after a cutscene.</summary>
        public void EndCutscene()
        {
            Begin(false);
        }

        private void Awake()
        {
            I = this;
            gameplayPos = transform.position;
            gameplayRot = transform.rotation;
        }

        private void OnEnable()
        {
            EventBus.GameReady += OnReady;
        }

        private void OnDisable()
        {
            EventBus.GameReady -= OnReady;
        }

        private void OnReady()
        {
            if (GameManager.I.State == GameState.MainMenu) SnapToMenu();
        }

        private Vector3 LookPoint
        {
            get
            {
                float h = Balance.B != null ? Balance.B.menu.cameraLookHeight : 3f;
                Vector3 baseP = lookTarget != null ? lookTarget.position : gameplayPos + gameplayRot * Vector3.forward * 20f;
                return baseP + Vector3.up * h;
            }
        }

        private void MenuPose(out Vector3 pos, out Quaternion rot)
        {
            MenuConfig m = Balance.B != null ? Balance.B.menu : new MenuConfig();
            float a = driftTime * m.driftSpeed * Mathf.PI * 2f;
            float yaw = m.cameraYaw + Mathf.Sin(a) * m.driftYaw;
            float pitch = m.cameraPitch + Mathf.Sin(a * 1.31f + 1f) * m.driftPitch;
            rot = Quaternion.Euler(pitch, yaw, 0f);
            pos = LookPoint - rot * Vector3.forward * m.cameraDistance;
        }

        public void SnapToMenu()
        {
            mode = Mode.Menu;
            MenuPose(out Vector3 p, out Quaternion r);
            transform.SetPositionAndRotation(p, r);
        }

        public void GoToGameplay()
        {
            Begin(false);
        }

        public void GoToMenu()
        {
            Begin(true);
        }

        private void Begin(bool toMenu)
        {
            transitionToMenu = toMenu;
            fromPos = transform.position;
            fromRot = transform.rotation;
            t = 0f;
            float seconds = Balance.B != null ? Balance.B.menu.transitionSeconds : 0.8f;
            duration = toMenu ? seconds * 1.25f : seconds;
            mode = Mode.Transition;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            switch (mode)
            {
                case Mode.Menu:
                    {
                        driftTime += dt;
                        MenuPose(out Vector3 p, out Quaternion r);
                        transform.SetPositionAndRotation(p, r);
                        break;
                    }
                case Mode.Cutscene:
                    {
                        t += dt / Mathf.Max(0.01f, duration);
                        float e = Juice.EaseInOut(Mathf.Clamp01(t));
                        transform.SetPositionAndRotation(Vector3.Lerp(fromPos, shotPos, e), Quaternion.Slerp(fromRot, shotRot, e));
                        break;
                    }
                case Mode.Transition:
                    {
                        t += dt / Mathf.Max(0.01f, duration);
                        float e = Juice.EaseInOut(Mathf.Clamp01(t));
                        Vector3 toPos;
                        Quaternion toRot;
                        if (transitionToMenu)
                        {
                            driftTime += dt;
                            MenuPose(out toPos, out toRot);
                        }
                        else
                        {
                            toPos = gameplayPos;
                            toRot = gameplayRot;
                        }
                        transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, e), Quaternion.Slerp(fromRot, toRot, e));
                        if (t >= 1f)
                        {
                            mode = transitionToMenu ? Mode.Menu : Mode.Gameplay;
                            if (BlobSpawner.I != null) BlobSpawner.I.RecomputeView();
                        }
                        break;
                    }
            }
        }
    }
}
