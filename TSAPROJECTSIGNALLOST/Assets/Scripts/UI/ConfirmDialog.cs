using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>Modal yes/no popup (New Game over a save, Skip tutorial). The safe choice is focused first.</summary>
    public class ConfirmDialog : MonoBehaviour
    {
        public static ConfirmDialog I { get; private set; }

        private CanvasGroup group;
        private RectTransform panel;
        private TextMeshProUGUI title;
        private TextMeshProUGUI body;
        private Button yes;
        private Button no;
        private TextMeshProUGUI yesLabel;
        private TextMeshProUGUI noLabel;
        private Action onYes;
        private Action onNo;
        private GameObject previousSelection;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            I = this;
            group = UIFactory.Group(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            RectTransform root = (RectTransform)transform;
            Image dim = UIFactory.Image(root, "Dim", UISprites.Square, new Color(0f, 0.01f, 0.04f, 0.8f), true);
            UIFactory.Stretch(dim.rectTransform);
            UIContrast.Tag(dim, UIContrast.Role.Backdrop);
            Image p = UIFactory.Panel(root, "Panel", new Color(0.03f, 0.05f, 0.13f, 1f));
            panel = p.rectTransform;
            UIFactory.Place(panel, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(860f, 420f));
            title = UIFactory.GlowText(panel, "Title", "", 50f, Color.white);
            UIFactory.Place(title.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -28f), new Vector2(800f, 64f));
            body = UIFactory.Text(panel, "Body", "", 28f, Palette.TextSoft, TextAlignmentOptions.Center, FontStyles.Normal);
            UIFactory.Place(body.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 20f), new Vector2(760f, 120f));
            yes = UIFactory.MenuButton(panel, "Yes", "YES", 30f, () => Answer(true), new Vector2(340f, 80f), Palette.Red);
            UIFactory.Place((RectTransform)yes.transform, UIFactory.BottomCenter, UIFactory.Center, new Vector2(-190f, 70f), new Vector2(340f, 80f));
            no = UIFactory.MenuButton(panel, "No", "NO", 30f, () => Answer(false), new Vector2(340f, 80f));
            UIFactory.Place((RectTransform)no.transform, UIFactory.BottomCenter, UIFactory.Center, new Vector2(190f, 70f), new Vector2(340f, 80f));
            yesLabel = yes.GetComponentInChildren<TextMeshProUGUI>();
            noLabel = no.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void Show(string titleText, string bodyText, string yesText, string noText, Action confirmed, Action cancelled = null)
        {
            title.text = titleText;
            body.text = bodyText;
            yesLabel.text = yesText;
            noLabel.text = noText;
            onYes = confirmed;
            onNo = cancelled;
            IsOpen = true;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            transform.SetAsLastSibling();
            UIFactory.Fade(group, true, 0.12f);
            Juice.StampIn(panel, 0.25f);
            AudioManager.Play("boop", 1.3f, 0.6f);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(no.gameObject);
        }

        public void Cancel()
        {
            if (IsOpen) Answer(false);
        }

        private void Answer(bool ok)
        {
            if (!IsOpen) return;
            IsOpen = false;
            UIFactory.Fade(group, false, 0.1f);
            Action cb = ok ? onYes : onNo;
            onYes = null;
            onNo = null;
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(previousSelection);
            }
            if (cb != null) cb();
        }
    }
}
