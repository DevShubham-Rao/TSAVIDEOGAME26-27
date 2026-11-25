using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Retro terminal mail window: inbox list left, message right, reply buttons bottom.
    /// Typewriter text (fast, click to skip). [CHARM] replies show in gold.
    /// </summary>
    public class MailUI : MonoBehaviour
    {
        public static MailUI I { get; private set; }

        private CanvasGroup group;
        private RectTransform window;
        private RectTransform inboxContent;
        private Image portrait;
        private TextMeshProUGUI senderText;
        private TextMeshProUGUI senderTitle;
        private TextMeshProUGUI subjectText;
        private TextMeshProUGUI bodyText;
        private RectTransform bodySymbols;
        private RectTransform bodyArea;
        private RectTransform replyArea;
        private TextMeshProUGUI repliedText;
        private TextMeshProUGUI emptyText;
        private readonly List<GameObject> inboxRows = new List<GameObject>();
        private readonly List<GameObject> replyButtons = new List<GameObject>();
        private MailEntry current;
        private bool open;
        private float typeProgress;
        private int typeTotal;
        private int lastTickChar;

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

        private void OnEnable()
        {
            EventBus.InboxChanged += OnInboxChanged;
            EventBus.EventStarted += OnEventStarted;
        }

        private void OnDisable()
        {
            EventBus.InboxChanged -= OnInboxChanged;
            EventBus.EventStarted -= OnEventStarted;
        }

        private void OnEventStarted(EventDef d)
        {
            if (open) Close();
        }

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            Image dim = UIFactory.Image(root, "Backdrop", UISprites.Square, new Color(0f, 0.01f, 0.04f, 0.55f), true);
            UIFactory.Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            Image win = UIFactory.Panel(root, "Window", new Color(0.015f, 0.035f, 0.08f, 0.97f), Palette.Cyan, true);
            window = win.rectTransform;
            UIFactory.Place(window, UIFactory.Center, UIFactory.Center, new Vector2(0f, -10f), new Vector2(1340f, 800f));

            Image titleBar = UIFactory.Image(window, "TitleBar", UISprites.Square, new Color(0.23f, 0.94f, 1f, 0.12f), false);
            UIFactory.Place(titleBar.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -4f), new Vector2(1320f, 56f));
            TextMeshProUGUI title = UIFactory.GlowText(window, "Title", "RELAY OMEGA // COMMS TERMINAL", 30f, Palette.Cyan, TextAlignmentOptions.Left);
            UIFactory.Place(title.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -12f), new Vector2(900f, 44f));
            Button close = UIFactory.IconButton(window, "Close", "close", 48f, Close, Color.white);
            UIFactory.Place((RectTransform)close.transform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-10f, -8f), new Vector2(48f, 48f));

            // Inbox list.
            Image inboxBg = UIFactory.Image(window, "InboxBg", UISprites.RoundedFill, new Color(0.03f, 0.06f, 0.13f, 1f), true);
            UIFactory.Place(inboxBg.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(16f, -72f), new Vector2(400f, 712f));
            RectTransform view = UIFactory.Rect("Viewport", inboxBg.transform);
            UIFactory.Stretch(view, 8f, 8f, 8f, 8f);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            inboxContent = UIFactory.Rect("Content", view);
            inboxContent.anchorMin = new Vector2(0f, 1f);
            inboxContent.anchorMax = new Vector2(1f, 1f);
            inboxContent.pivot = new Vector2(0.5f, 1f);
            inboxContent.sizeDelta = Vector2.zero;
            VerticalLayoutGroup v = inboxContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            inboxContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect sr = inboxBg.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view;
            sr.content = inboxContent;
            sr.horizontal = false;
            sr.scrollSensitivity = 40f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            // Message pane.
            RectTransform pane = UIFactory.Place(UIFactory.Rect("Message", window), UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(436f, -72f), new Vector2(888f, 712f));
            Image pbg = UIFactory.Image(pane, "Frame", UISprites.Circle, new Color(0.1f, 0.16f, 0.32f, 1f), false);
            UIFactory.Place(pbg.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(0f, 0f), new Vector2(150f, 150f));
            portrait = UIFactory.Image(pbg.transform, "Portrait", UISprites.Portrait("hale"), Color.white, false);
            UIFactory.Stretch(portrait.rectTransform, 6f, 6f, 6f, 6f);
            senderText = UIFactory.GlowText(pane, "Sender", "", 38f, Palette.Cyan, TextAlignmentOptions.Left);
            UIFactory.Place(senderText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(170f, -6f), new Vector2(700f, 50f));
            senderTitle = UIFactory.Text(pane, "SenderTitle", "", 22f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Italic);
            UIFactory.Place(senderTitle.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(170f, -56f), new Vector2(700f, 30f));
            subjectText = UIFactory.Text(pane, "Subject", "", 28f, Color.white, TextAlignmentOptions.Left);
            UIFactory.Place(subjectText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(170f, -94f), new Vector2(700f, 40f));

            Image bodyBg = UIFactory.Image(pane, "BodyBg", UISprites.RoundedFill, new Color(0.02f, 0.05f, 0.1f, 1f), true);
            bodyArea = bodyBg.rectTransform;
            UIFactory.Place(bodyArea, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(0f, -164f), new Vector2(888f, 208f));
            bodyBg.gameObject.AddComponent<Button>().onClick.AddListener(SkipTypewriter);
            bodyText = UIFactory.Text(bodyArea, "Body", "", 26f, Palette.Hex("#9CF7FF"), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            UIFactory.Stretch(bodyText.rectTransform, 24f, 20f, 24f, 20f);

            repliedText = UIFactory.Text(pane, "Replied", "", 24f, Palette.TextSoft, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            UIFactory.Place(repliedText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(4f, -378f), new Vector2(880f, 40f));
            replyArea = UIFactory.Place(UIFactory.Rect("Replies", pane), UIFactory.BottomLeft, UIFactory.BottomLeft, Vector2.zero, new Vector2(888f, 300f));
            VerticalLayoutGroup rv = replyArea.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 10f;
            rv.childAlignment = TextAnchor.LowerCenter;
            rv.childControlWidth = true;
            rv.childControlHeight = false;
            rv.childForceExpandWidth = true;
            rv.childForceExpandHeight = false;

            emptyText = UIFactory.Text(pane, "Empty", "NO MESSAGES.\nTHE STATIC HUMS QUIETLY.", 34f, Palette.Dim);
            UIFactory.Stretch(emptyText.rectTransform);

            Image scan = UIFactory.Image(window, "Scanlines", UISprites.Scanlines, new Color(0.6f, 1f, 1f, 0.5f), false);
            scan.type = Image.Type.Tiled;
            scan.pixelsPerUnitMultiplier = 0.4f;
            UIFactory.Stretch(scan.rectTransform, 4f, 4f, 4f, 4f);
        }

        public void Toggle()
        {
            if (open) Close();
            else Open();
        }

        public void Open()
        {
            if (open) return;
            open = true;
            UIFactory.Fade(group, true, 0.15f);
            window.localScale = Vector3.one * 0.92f;
            Juice.Run(Juice.Tween(0.2f, k => window.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Juice.EaseOutBack(k))));
            AudioManager.Play("type", 0.8f);
            EventBus.RaisePanelOpened("mail");
            RebuildInbox();
            MailEntry pick = null;
            foreach (MailEntry e in MailDirector.I.Inbox)
            {
                if (e.NeedsReply || !e.Read)
                {
                    pick = e;
                    break;
                }
            }
            if (pick == null && MailDirector.I.Inbox.Count > 0) pick = MailDirector.I.Inbox[0];
            Show(pick);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            UIFactory.Fade(group, false, 0.12f);
        }

        private void OnInboxChanged()
        {
            if (open) RebuildInbox();
        }

        private void RebuildInbox()
        {
            foreach (GameObject g in inboxRows) Destroy(g);
            inboxRows.Clear();
            foreach (MailEntry e in MailDirector.I.Inbox)
            {
                MailEntry entry = e;
                SenderDef s = Balance.I.Sender(e.From);
                bool selected = entry == current;
                Color fill = selected ? new Color(0.08f, 0.2f, 0.32f, 1f) : new Color(0.05f, 0.09f, 0.18f, 1f);
                Button b = UIFactory.Button(inboxContent, "Row", null, 0f, () => Show(entry), fill, selected ? Palette.Cyan : Palette.WithAlpha(Palette.Dim, 0.6f));
                RectTransform rt = (RectTransform)b.transform;
                rt.sizeDelta = new Vector2(370f, 84f);
                LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 84f;
                TextMeshProUGUI from = UIFactory.Text(rt, "From", s.name, 24f, Palette.Hex(s.color), TextAlignmentOptions.Left);
                UIFactory.Stretch(from.rectTransform, 40f, 8f, 10f, 44f);
                string subject = SymbolText.Plain(e.IsFollowup ? "> " + e.FollowupText : e.Subject);
                TextMeshProUGUI subj = UIFactory.Text(rt, "Subject", subject, 19f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Normal);
                UIFactory.Stretch(subj.rectTransform, 40f, 40f, 10f, 8f);
                subj.enableWordWrapping = false;
                subj.overflowMode = TextOverflowModes.Ellipsis;
                if (!e.Read || e.NeedsReply)
                {
                    Image dot = UIFactory.Image(rt, "Unread", UISprites.Circle, e.NeedsReply ? Palette.Red : Palette.Cyan, false);
                    UIFactory.Place(dot.rectTransform, UIFactory.MidLeft, UIFactory.Center, new Vector2(20f, 0f), new Vector2(16f, 16f));
                }
                inboxRows.Add(b.gameObject);
            }
            emptyText.gameObject.SetActive(MailDirector.I.Inbox.Count == 0);
        }

        private void Show(MailEntry e)
        {
            current = e;
            ClearReplies();
            if (bodySymbols != null)
            {
                Destroy(bodySymbols.gameObject);
                bodySymbols = null;
            }
            bool has = e != null;
            portrait.transform.parent.gameObject.SetActive(has);
            senderText.gameObject.SetActive(has);
            bodyArea.gameObject.SetActive(has);
            emptyText.gameObject.SetActive(!has);
            if (!has)
            {
                subjectText.text = "";
                senderTitle.text = "";
                repliedText.text = "";
                return;
            }

            SenderDef s = Balance.I.Sender(e.From);
            portrait.sprite = UISprites.Portrait(s.portrait);
            senderText.text = s.name;
            senderText.color = Palette.Hex(s.color);
            senderTitle.text = s.title;
            subjectText.text = e.IsFollowup ? "Re: your reply" : SymbolText.Plain(e.Def.subject);

            string body = e.IsFollowup ? e.FollowupText : e.Def.body;
            if (SymbolText.IsSymbolic(body))
            {
                bodyText.text = "";
                bodySymbols = SymbolText.Build(bodyArea, body, 72f);
                typeProgress = 0f;
                typeTotal = 0;
            }
            else
            {
                bodyText.text = body;
                bodyText.ForceMeshUpdate();
                typeTotal = bodyText.textInfo.characterCount;
                typeProgress = e.Read || GameSettings.MailSpeedMult <= 0f ? typeTotal : 0f;
                bodyText.maxVisibleCharacters = (int)typeProgress;
                lastTickChar = 0;
            }

            MailDirector.I.MarkRead(e);
            BuildReplies(e);
            RebuildInbox();
        }

        private void ClearReplies()
        {
            foreach (GameObject g in replyButtons) Destroy(g);
            replyButtons.Clear();
        }

        private void BuildReplies(MailEntry e)
        {
            repliedText.text = "";
            if (e.IsFollowup || e.Def.replies == null || e.Def.replies.Count == 0) return;
            if (e.Replied >= 0)
            {
                MailReply r = e.Def.replies[e.Replied];
                repliedText.text = "> YOU REPLIED: " + (r.symbols ? "(a pattern of shapes)" : r.text);
                return;
            }
            List<int> visible = MailDirector.I.VisibleReplyIndices(e.Def);
            foreach (int idx in visible)
            {
                MailReply r = e.Def.replies[idx];
                int replyIndex = idx;
                bool channel = r.NeedsOpenChannel;
                Color border = channel ? Palette.Magenta : Palette.Cyan;
                Color fill = channel ? new Color(0.2f, 0.05f, 0.2f, 1f) : new Color(0.05f, 0.1f, 0.2f, 1f);
                string label = r.symbols ? "" : (channel ? "[OPEN CHANNEL]  " + r.text : r.text);
                string hintText = GameStats.ResolveHint(r.hint);
                bool hasHint = !string.IsNullOrEmpty(hintText);
                float h = hasHint ? 96f : 74f;
                Button b = UIFactory.Button(replyArea, "Reply", label, 26f, () => OnReply(e, replyIndex, null), fill, border, channel ? Palette.Magenta : Color.white);
                RectTransform rt = (RectTransform)b.transform;
                rt.sizeDelta = new Vector2(888f, h);
                LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = h;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => OnReply(e, replyIndex, rt));
                TextMeshProUGUI lbl = b.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.alignment = TextAlignmentOptions.Left;
                    lbl.rectTransform.offsetMin = new Vector2(24f, hasHint ? 34f : lbl.rectTransform.offsetMin.y);
                }
                if (r.symbols)
                {
                    RectTransform sym = UIFactory.Place(UIFactory.Rect("Symbols", rt), UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(10f, 0f), new Vector2(400f, hasHint ? 60f : h));
                    SymbolText.Build(sym, r.text, 40f);
                }
                if (hasHint)
                {
                    // What this reply gives and costs.
                    TextMeshProUGUI hint = UIFactory.Text(rt, "Hint", hintText, 18f, Palette.TextSoft, TextAlignmentOptions.Left, FontStyles.Normal);
                    UIFactory.Stretch(hint.rectTransform, 26f, 58f, 20f, 6f);
                    hint.enableWordWrapping = true;
                    hint.enableAutoSizing = true;
                    hint.fontSizeMin = 13f;
                    hint.fontSizeMax = 18f;
                }
                replyButtons.Add(b.gameObject);
            }
            int hidden = e.Def.replies.Count - visible.Count;
            if (hidden > 0)
            {
                repliedText.text = "<color=#5A6488>(" + hidden + " more reply option" + (hidden > 1 ? "s" : "") + " needs Open Channel)</color>";
            }
        }

        private void OnReply(MailEntry e, int index, RectTransform button)
        {
            MailReply r = e.Def.replies[index];
            if (button != null)
            {
                Juice.PunchScale(button, 1.08f, 0.2f);
                UIFactory.Burst(button, r.NeedsOpenChannel ? Palette.Magenta : Palette.Cyan, 12);
            }
            MailDirector.I.Reply(e, index);
            if (open) Show(e);
        }

        private System.Collections.IEnumerator FlyIcon(string iconId, Color color, Vector3 from)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Image img = UIFactory.Image(canvas.transform, "FlyIcon", UISprites.Icon(iconId), color, false);
            RectTransform rt = img.rectTransform;
            rt.sizeDelta = new Vector2(110f, 110f);
            rt.position = from;
            Vector3 to = new Vector3(Screen.width - 80f, Screen.height - 170f, 0f);
            float t = 0f;
            const float d = 0.7f;
            while (t < d)
            {
                float k = t / d;
                float e = Juice.EaseInOut(k);
                Vector3 p = Vector3.Lerp(from, to, e);
                p.y += Mathf.Sin(k * Mathf.PI) * 160f;
                rt.position = p;
                rt.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.45f, e);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        private void SkipTypewriter()
        {
            typeProgress = typeTotal;
            bodyText.maxVisibleCharacters = typeTotal;
        }

        private void Update()
        {
            if (!open || typeProgress >= typeTotal) return;
            float cps = (Balance.B != null ? Balance.B.mail.typewriterCharsPerSecond : 90f) * Mathf.Max(0.05f, GameSettings.MailSpeedMult);
            if (GameSettings.MailSpeedMult <= 0f) cps = 100000f;
            typeProgress = Mathf.Min(typeTotal, typeProgress + Time.unscaledDeltaTime * cps);
            int shown = (int)typeProgress;
            bodyText.maxVisibleCharacters = shown;
            if (shown - lastTickChar >= 3)
            {
                lastTickChar = shown;
                AudioManager.PlayVaried("type", 0.9f, 1.2f, 0.35f);
            }
        }
    }
}
