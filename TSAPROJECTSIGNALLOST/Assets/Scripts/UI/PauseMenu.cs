using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Pause menu (Esc / P / pause button): freezes gameplay (Time.timeScale = 0; UI runs on unscaled time),
    /// blurs and dims the world. Resume, Settings, Main Menu (autosaves first), Quit.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu I { get; private set; }

        private CanvasGroup group;
        private RectTransform panel;
        private Button resume;
        private Button settings;
        private readonly List<Button> buttons = new List<Button>();
        private bool open;

        public bool IsOpen { get { return open; } }

        private void Awake()
        {
            I = this;
            group = UIFactory.Group(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Build();
        }

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            Image dim = UIFactory.Image(root, "Dim", UISprites.Square, new Color(0.01f, 0.02f, 0.07f, 0.55f), true);
            UIFactory.Stretch(dim.rectTransform);
            UIContrast.Tag(dim, UIContrast.Role.Backdrop);
            Image vig = UIFactory.Image(root, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0.02f, 0.8f), false);
            UIFactory.Stretch(vig.rectTransform, -200f, -200f, -200f, -200f);

            Image p = UIFactory.Panel(root, "Panel", new Color(0.03f, 0.05f, 0.13f, 1f));
            panel = p.rectTransform;
            UIFactory.Place(panel, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(620f, 620f));
            TextMeshProUGUI title = UIFactory.GlowText(panel, "Title", "PAUSED", 68f, Color.white);
            UIFactory.Place(title.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -30f), new Vector2(560f, 84f));

            Vector2 size = new Vector2(460f, 80f);
            resume = Add("Resume", "RESUME", new Vector2(0f, 110f), size, Close);
            settings = Add("Settings", "SETTINGS", new Vector2(0f, 10f), size, OpenSettings);
            Add("MainMenu", "MAIN MENU", new Vector2(0f, -90f), size, ToMainMenu);
#if !UNITY_WEBGL
            Add("Quit", "QUIT", new Vector2(0f, -190f), size, () => GameManager.I.QuitGame());
#endif
            TextMeshProUGUI hint = UIFactory.Text(panel, "Hint", "Esc / P to resume", 20f, Palette.TextSoft);
            UIFactory.Place(hint.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 18f), new Vector2(560f, 30f));
        }

        private Button Add(string name, string label, Vector2 pos, Vector2 size, System.Action onClick)
        {
            Button b = UIFactory.MenuButton(panel, name, label, 34f, onClick, size);
            UIFactory.Place((RectTransform)b.transform, UIFactory.Center, UIFactory.Center, pos, size);
            buttons.Add(b);
            return b;
        }

        public void Toggle()
        {
            if (open) Close();
            else Open();
        }

        public void Open()
        {
            GameManager gm = GameManager.I;
            if (open || gm == null || (gm.State != GameState.Playing && gm.State != GameState.Finale)) return;
            open = true;
            gm.SetPaused(true);
            transform.SetAsLastSibling();
            if (SettingsScreen.I != null) SettingsScreen.I.transform.SetAsLastSibling();
            if (ConfirmDialog.I != null) ConfirmDialog.I.transform.SetAsLastSibling();
            UIFactory.Fade(group, true, 0.15f);
            panel.localScale = Vector3.one * 0.9f;
            Juice.Run(Juice.Tween(0.22f, k => panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, Juice.EaseOutBack(k))));
            AudioManager.Play("whoosh", 0.8f, 0.5f);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resume.gameObject);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            UIFactory.Fade(group, false, 0.12f);
            if (GameManager.I != null) GameManager.I.SetPaused(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OpenSettings()
        {
            if (SettingsScreen.I != null) SettingsScreen.I.Open(() =>
            {
                if (open && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(settings.gameObject);
            });
        }

        private void ToMainMenu()
        {
            if (MailUI.I != null) MailUI.I.Close();
            if (EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen) EndOfDayScreen.I.Close();
            open = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            GameManager.I.ReturnToMenu();
        }

        private void Update()
        {
            if (!open || EventSystem.current == null) return;
            bool modal = (SettingsScreen.I != null && SettingsScreen.I.IsOpen) || (ConfirmDialog.I != null && ConfirmDialog.I.IsOpen);
            if (!modal && EventSystem.current.currentSelectedGameObject == null) EventSystem.current.SetSelectedGameObject(resume.gameObject);
        }
    }
}
