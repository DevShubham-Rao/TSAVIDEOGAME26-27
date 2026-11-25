using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Main menu as a UI state over the live game world (same scene). Logo with a glow pulse and wobble,
    /// stacked buttons that fly off when play starts while the camera eases into the gameplay pose.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI I { get; private set; }

        private CanvasGroup group;
        private RectTransform logo;
        private TextMeshProUGUI title;
        private TextMeshProUGUI subtitle;
        private Image logoGlow;
        private readonly List<RectTransform> buttons = new List<RectTransform>();
        private readonly List<Vector2> basePositions = new List<Vector2>();
        private Vector2 logoBase;
        private Button continueButton;
        private TextMeshProUGUI endingsText;
        private Button newGameButton;
        private TextMeshProUGUI continueSub;
        private CanvasGroup creditsGroup;
        private Button creditsBack;
        private bool shown;
        private bool creditsOpen;
        private Coroutine anim;

        public bool IsShown { get { return shown; } }

        private void Awake()
        {
            I = this;
            group = UIFactory.Group(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Build();
        }

        private void OnEnable()
        {
            EventBus.GameReady += OnReady;
            EventBus.GameplayStarted += OnGameplayStarted;
            EventBus.ReturnedToMenu += OnReturned;
        }

        private void OnDisable()
        {
            EventBus.GameReady -= OnReady;
            EventBus.GameplayStarted -= OnGameplayStarted;
            EventBus.ReturnedToMenu -= OnReturned;
        }

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            Image shade = UIFactory.Image(root, "Shade", UISprites.Vignette, new Color(0.01f, 0.02f, 0.08f, 0.7f), false);
            UIFactory.Stretch(shade.rectTransform, -300f, -300f, -300f, -300f);

            logo = UIFactory.Place(UIFactory.Rect("Logo", root), UIFactory.Center, UIFactory.Center, new Vector2(0f, 300f), new Vector2(1600f, 300f));
            logoBase = logo.anchoredPosition;
            logoGlow = UIFactory.Image(logo, "Glow", UISprites.Glow, new Color(0.23f, 0.94f, 1f, 0.25f), false);
            UIFactory.Place(logoGlow.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(1500f, 460f));
            title = UIFactory.GlowText(logo, "Title", "SIGNAL LOST", 170f, Palette.Cyan);
            UIFactory.Place(title.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 30f), new Vector2(1600f, 200f));
            title.enableWordWrapping = false;
            title.characterSpacing = 6f;
            subtitle = UIFactory.OutlineText(logo, "Subtitle", "STATIC  SWEEPER", 62f, Palette.Magenta);
            UIFactory.Place(subtitle.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -100f), new Vector2(1400f, 80f));
            subtitle.characterSpacing = 18f;

            Vector2 size = new Vector2(500f, 80f);
            continueButton = AddButton(root, "Continue", "CONTINUE", new Vector2(0f, 70f), new Vector2(500f, 104f), OnContinue);
            RectTransform cl = (RectTransform)continueButton.transform.Find("Label");
            if (cl != null) cl.offsetMin = new Vector2(cl.offsetMin.x, 34f);
            continueSub = UIFactory.Text(continueButton.transform, "Sub", "", 24f, Palette.Gold);
            UIFactory.Place(continueSub.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 10f), new Vector2(480f, 32f));
            newGameButton = AddButton(root, "NewGame", "NEW GAME", new Vector2(0f, -45f), size, OnNewGame);
            AddButton(root, "Settings", "SETTINGS", new Vector2(0f, -140f), size, OnSettings);
            AddButton(root, "Credits", "CREDITS", new Vector2(0f, -235f), size, OpenCredits);
#if !UNITY_WEBGL
            AddButton(root, "Quit", "QUIT", new Vector2(0f, -330f), size, () => GameManager.I.QuitGame());
#endif
            endingsText = UIFactory.Text(root, "Endings", "", 24f, Palette.Gold, TextAlignmentOptions.Right);
            UIFactory.Place(endingsText.rectTransform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-36f, -30f), new Vector2(520f, 34f));
            TextMeshProUGUI hint = UIFactory.Text(root, "Hint", "Left-click Static to pop it while you're here.", 22f, Palette.TextSoft);
            UIFactory.Place(hint.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 26f), new Vector2(1200f, 34f));

            BuildCredits(root);
        }

        private Button AddButton(RectTransform root, string name, string label, Vector2 pos, Vector2 size, System.Action onClick)
        {
            Button b = UIFactory.MenuButton(root, name, label, 36f, onClick, size);
            RectTransform rt = (RectTransform)b.transform;
            UIFactory.Place(rt, UIFactory.Center, UIFactory.Center, pos, size);
            buttons.Add(rt);
            basePositions.Add(pos);
            return b;
        }

        private void BuildCredits(RectTransform root)
        {
            Image p = UIFactory.Panel(root, "Credits", new Color(0.03f, 0.05f, 0.13f, 1f));
            UIFactory.Place(p.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -60f), new Vector2(980f, 640f));
            creditsGroup = UIFactory.Group(p.gameObject);
            creditsGroup.alpha = 0f;
            creditsGroup.blocksRaycasts = false;
            creditsGroup.interactable = false;
            TextMeshProUGUI t = UIFactory.GlowText(p.transform, "Title", "CREDITS", 56f, Color.white);
            UIFactory.Place(t.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -26f), new Vector2(900f, 70f));
            TextMeshProUGUI body = UIFactory.Text(p.transform, "Body",
                "<color=#3BF0FF>SIGNAL LOST: STATIC SWEEPER</color>\n" +
                "Made by the Relay Omega team\n\n" +
                "<color=#AFC3E6>Engine</color>  Unity, Universal Render Pipeline\n" +
                "<color=#AFC3E6>Text</color>  TextMeshPro, Liberation Sans\n" +
                "<color=#AFC3E6>Sound</color>  Synthesized in code\n" +
                "<color=#AFC3E6>Static</color>  No blobs were harmed. Only popped.\n\n" +
                "Thanks for playing!", 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            UIFactory.Place(body.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 10f), new Vector2(900f, 400f));
            creditsBack = UIFactory.MenuButton(p.transform, "Back", "BACK", 32f, CloseCredits, new Vector2(300f, 76f));
            UIFactory.Place((RectTransform)creditsBack.transform, UIFactory.BottomCenter, UIFactory.Center, new Vector2(0f, 64f), new Vector2(300f, 76f));
            Navigation nav = creditsBack.navigation;
            nav.mode = Navigation.Mode.None;
            creditsBack.navigation = nav;
        }

        // ---------------- flow ----------------

        private void OnReady()
        {
            if (GameManager.I.State == GameState.MainMenu) Show();
        }

        private void OnReturned()
        {
            Show();
        }

        private void OnGameplayStarted()
        {
            Hide();
        }

        private void Refresh()
        {
            int found = GameManager.I != null ? GameManager.I.EndingsSeen.Count : 0;
            endingsText.text = found > 0 ? "ENDINGS FOUND  " + found + "/" + Mathf.Max(3, Balance.I.Mail.endings.Count) : "";
            bool hasSave = GameManager.I != null && GameManager.I.HasSave;
            continueButton.gameObject.SetActive(hasSave);
            if (hasSave)
            {
                continueSub.text = "Day " + (DayManager.I != null ? DayManager.I.Day : 1) + "  ·  " + NumberFormat.Format(GameManager.I.Data) + " Data";
            }
        }

        public void Show()
        {
            Refresh();
            shown = true;
            group.blocksRaycasts = true;
            group.interactable = true;
            if (anim != null) Juice.Stop(anim);
            anim = Juice.Run(FlyIn());
            SelectFirst();
        }

        public void Hide()
        {
            if (!shown) return;
            shown = false;
            CloseCredits();
            group.blocksRaycasts = false;
            group.interactable = false;
            if (anim != null) Juice.Stop(anim);
            anim = Juice.Run(FlyOut());
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private IEnumerator FlyIn()
        {
            MenuConfig m = Balance.B != null ? Balance.B.menu : new MenuConfig();
            float total = m.buttonFlySeconds + m.buttonStagger * buttons.Count;
            float t = 0f;
            group.alpha = 1f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < buttons.Count; i++)
                {
                    float k = Mathf.Clamp01((t - i * m.buttonStagger) / m.buttonFlySeconds);
                    buttons[i].anchoredPosition = basePositions[i] + new Vector2(-1400f * (1f - Juice.EaseOutBack(k)), 0f);
                }
                float lk = Mathf.Clamp01(t / m.buttonFlySeconds);
                logo.anchoredPosition = logoBase + new Vector2(0f, 500f * (1f - Juice.EaseOutBack(lk)));
                yield return null;
            }
            for (int i = 0; i < buttons.Count; i++) buttons[i].anchoredPosition = basePositions[i];
            logo.anchoredPosition = logoBase;
        }

        private IEnumerator FlyOut()
        {
            MenuConfig m = Balance.B != null ? Balance.B.menu : new MenuConfig();
            float total = m.buttonFlySeconds + m.buttonStagger * buttons.Count;
            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < buttons.Count; i++)
                {
                    float k = Mathf.Clamp01((t - i * m.buttonStagger) / m.buttonFlySeconds);
                    buttons[i].anchoredPosition = basePositions[i] + new Vector2(-1400f * k * k, 0f);
                }
                float lk = Mathf.Clamp01(t / m.buttonFlySeconds);
                logo.anchoredPosition = logoBase + new Vector2(0f, 600f * lk * lk);
                group.alpha = 1f - Mathf.Clamp01((t - total * 0.6f) / (total * 0.4f));
                yield return null;
            }
            group.alpha = 0f;
        }

        private void SelectFirst()
        {
            if (EventSystem.current == null) return;
            GameObject first = continueButton.gameObject.activeSelf ? continueButton.gameObject : newGameButton.gameObject;
            EventSystem.current.SetSelectedGameObject(first);
        }

        // ---------------- buttons ----------------

        private void OnContinue()
        {
            if (!shown) return;
            GameManager.I.StartGameplay();
        }

        private void OnNewGame()
        {
            if (!shown) return;
            if (GameManager.I.HasSave)
            {
                ConfirmDialog.I.Show("START A NEW GAME?", "Your current progress will be erased.\nSettings are kept.", "ERASE & START", "CANCEL",
                    () => GameManager.I.NewGame());
            }
            else
            {
                GameManager.I.NewGame();
            }
        }

        private void OnSettings()
        {
            if (!shown || SettingsScreen.I == null) return;
            SettingsScreen.I.Open(() => SelectFirst());
        }

        private void OpenCredits()
        {
            if (!shown) return;
            creditsOpen = true;
            UIFactory.Fade(creditsGroup, true, 0.15f);
            Juice.StampIn(creditsGroup.transform, 0.25f);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(creditsBack.gameObject);
        }

        private void CloseCredits()
        {
            if (!creditsOpen) return;
            creditsOpen = false;
            UIFactory.Fade(creditsGroup, false, 0.12f);
            if (shown) SelectFirst();
        }

        /// <summary>Esc / Back on the main menu.</summary>
        public void Back()
        {
            if (creditsOpen) CloseCredits();
        }

        private void Update()
        {
            if (!shown) return;
            float t = Time.unscaledTime;
            MenuConfig m = Balance.B != null ? Balance.B.menu : new MenuConfig();
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * m.logoPulseSpeed);
            logoGlow.color = new Color(0.23f, 0.94f, 1f, 0.15f + 0.2f * pulse);
            logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * m.logoWobbleDegrees);
            logo.localScale = Vector3.one * (1f + 0.025f * pulse);
            bool glitch = Mathf.PerlinNoise(t * 2.3f, 0.7f) > 0.75f;
            title.color = glitch ? Color.Lerp(Palette.Cyan, Palette.Magenta, Random.value) : Palette.Cyan;

            // Keep a focused button for keyboard / controller players.
            bool modal = (SettingsScreen.I != null && SettingsScreen.I.IsOpen) || (ConfirmDialog.I != null && ConfirmDialog.I.IsOpen);
            if (!modal && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                if (creditsOpen) EventSystem.current.SetSelectedGameObject(creditsBack.gameObject);
                else SelectFirst();
            }
        }
    }
}
