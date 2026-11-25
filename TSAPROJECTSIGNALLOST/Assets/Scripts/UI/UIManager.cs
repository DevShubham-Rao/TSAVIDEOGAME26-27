using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Routes Esc / P / M through the modal stack (confirm > settings > cutscene > menu > pause > panels),
    /// applies the UI Scale setting, and answers "is a modal open" for the event and tutorial systems.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager I { get; private set; }

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private CanvasScaler scaler;

        private void Awake()
        {
            I = this;
            scaler = GetComponent<CanvasScaler>();
            EnsureInputModule();
        }

        /// <summary>UI Scale 80%..130%: a smaller reference resolution makes everything bigger.</summary>
        public void ApplyUiScale(float scale)
        {
            if (scaler == null) return;
            scale = Mathf.Max(0.1f, scale);
            scaler.referenceResolution = ReferenceResolution / scale;
        }

        /// <summary>
        /// Makes sure the EventSystem uses the UI module that matches Active Input Handling
        /// (Input System package or legacy), so switching the setting never breaks the UI.
        /// </summary>
        private static void EnsureInputModule()
        {
            EventSystem es = EventSystem.current != null ? EventSystem.current : FindObjectOfType<EventSystem>();
            if (es == null)
            {
                es = new GameObject("EventSystem").AddComponent<EventSystem>();
            }
#if ENABLE_INPUT_SYSTEM
            StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
            UnityEngine.InputSystem.UI.InputSystemUIInputModule module = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module == null)
            {
                module = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
            else if (module.actionsAsset == null)
            {
                module.AssignDefaultActions();
            }
#else
            BaseInputModule other = es.GetComponent<BaseInputModule>();
            if (other != null && !(other is StandaloneInputModule)) Destroy(other);
            if (es.GetComponent<StandaloneInputModule>() == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>Events never fire while mail or another full-screen panel is open.</summary>
        public bool BlocksEvents
        {
            get
            {
                return (MailUI.I != null && MailUI.I.IsOpen)
                    || (SettingsScreen.I != null && SettingsScreen.I.IsOpen)
                    || (PauseMenu.I != null && PauseMenu.I.IsOpen)
                    || (ConfirmDialog.I != null && ConfirmDialog.I.IsOpen)
                    || (EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen)
                    || (CutscenePlayer.I != null && CutscenePlayer.I.IsPlaying);
            }
        }

        private void Update()
        {
            InputReader input = InputReader.I;
            GameManager gm = GameManager.I;
            if (input == null || gm == null || !gm.IsReady) return;
            bool cancel = input.CancelPressed;
            bool pause = input.PausePressed;

            if (ConfirmDialog.I != null && ConfirmDialog.I.IsOpen)
            {
                if (cancel) ConfirmDialog.I.Cancel();
                return;
            }
            if (SettingsScreen.I != null && SettingsScreen.I.IsOpen)
            {
                if (cancel && !SettingsScreen.I.BlocksEscape) SettingsScreen.I.Close();
                return;
            }
            if (gm.State == GameState.MainMenu)
            {
                if (cancel && MainMenuUI.I != null) MainMenuUI.I.Back();
                return;
            }
            if (gm.State == GameState.Ending || gm.State == GameState.Loading) return;
            // Cutscenes handle their own Esc (skip).
            if (CutscenePlayer.I != null && CutscenePlayer.I.IsPlaying) return;

            if (PauseMenu.I != null && PauseMenu.I.IsOpen)
            {
                if (cancel || pause) PauseMenu.I.Close();
                return;
            }
            if (pause)
            {
                if (PauseMenu.I != null) PauseMenu.I.Open();
                return;
            }
            if (cancel)
            {
                if (MailUI.I != null && MailUI.I.IsOpen) MailUI.I.Close();
                else if (PauseMenu.I != null) PauseMenu.I.Open();
                return;
            }
            // Mail lives on the end-of-day screen (and in the finale); M opens it there.
            bool night = DayManager.I != null && DayManager.I.Phase == DayPhase.Night;
            if (input.MailPressed && MailUI.I != null && (night || gm.State == GameState.Finale)) MailUI.I.Toggle();
        }
    }
}
