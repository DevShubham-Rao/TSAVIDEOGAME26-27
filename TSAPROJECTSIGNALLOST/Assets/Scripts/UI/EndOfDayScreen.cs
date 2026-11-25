using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// The end-of-day screen, in a fixed order: Shift Report, then Mail (answer before moving on), then the
    /// Skill Tree with "Undo tonight" and "Start Day N+1". After a Rebroadcast it opens in Deep Signal mode
    /// (spend Echoes, then "Start Day 1").
    /// </summary>
    public class EndOfDayScreen : MonoBehaviour
    {
        public static EndOfDayScreen I { get; private set; }

        public enum Step { Report, Mail, Tree, Deep }

        public bool IsOpen { get; private set; }
        public Step Current { get; private set; }

        private CanvasGroup group;
        private TextMeshProUGUI title;
        private TextMeshProUGUI walletText;
        private TextMeshProUGUI endingsText;
        private readonly List<TextMeshProUGUI> stepLabels = new List<TextMeshProUGUI>();
        private RectTransform reportPage;
        private RectTransform mailPage;
        private RectTransform treePage;
        private TreeView tree;
        private TextMeshProUGUI earnedText;
        private TextMeshProUGUI statsText;
        private TextMeshProUGUI lostText;
        private TextMeshProUGUI mailStatus;
        private TextMeshProUGUI tomorrowText;
        private Button continueButton;
        private TextMeshProUGUI continueLabel;
        private Button openMailButton;
        private Button undoButton;
        private TextMeshProUGUI undoLabel;
        private Button startButton;
        private TextMeshProUGUI startLabel;
        private DayReport report;
        private Coroutine countUp;

        public RectTransform StartButtonRect { get { return startButton != null ? (RectTransform)startButton.transform : null; } }
        public RectTransform ContinueButtonRect { get { return continueButton != null ? (RectTransform)continueButton.transform : null; } }
        public RectTransform OpenMailRect { get { return openMailButton != null ? (RectTransform)openMailButton.transform : null; } }
        public TreeView Tree { get { return tree; } }

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
            EventBus.InboxChanged += RefreshMailStep;
            EventBus.DataSpent += RefreshWallet;
            EventBus.CoinLanded += OnCoin;
            EventBus.UpgradeBought += OnBought;
        }

        private void OnDisable()
        {
            EventBus.InboxChanged -= RefreshMailStep;
            EventBus.DataSpent -= RefreshWallet;
            EventBus.CoinLanded -= OnCoin;
            EventBus.UpgradeBought -= OnBought;
        }

        private void OnCoin(double a)
        {
            RefreshWallet();
        }

        private void OnBought(string id, int level)
        {
            RefreshWallet();
            RefreshUndo();
        }

        // ---------------- build ----------------

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            Image bg = UIFactory.Image(root, "Backdrop", UISprites.Square, new Color(0.015f, 0.025f, 0.07f, 1f), true);
            UIFactory.Stretch(bg.rectTransform);
            UIContrast.Tag(bg, UIContrast.Role.Backdrop);
            Image vig = UIFactory.Image(root, "Vignette", UISprites.Vignette, new Color(0.1f, 0.2f, 0.5f, 0.35f), false);
            UIFactory.Stretch(vig.rectTransform, -200f, -200f, -200f, -200f);

            title = UIFactory.GlowText(root, "Title", "END OF DAY 1", 64f, Color.white);
            UIFactory.Place(title.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -24f), new Vector2(1200f, 80f));

            string[] names = { "1  SHIFT REPORT", "2  MAIL", "3  SKILL TREE" };
            for (int i = 0; i < names.Length; i++)
            {
                TextMeshProUGUI s = UIFactory.Text(root, "Step" + i, names[i], 24f, Palette.Dim);
                UIFactory.Place(s.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2((i - 1) * 330f, -110f), new Vector2(320f, 34f));
                stepLabels.Add(s);
            }

            walletText = UIFactory.GlowText(root, "Wallet", "", 40f, Palette.Gold, TextAlignmentOptions.Right);
            UIFactory.Place(walletText.rectTransform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-40f, -34f), new Vector2(560f, 56f));

            endingsText = UIFactory.Text(root, "Endings", "", 24f, Palette.Gold, TextAlignmentOptions.Left);
            UIFactory.Place(endingsText.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(40f, -40f), new Vector2(480f, 34f));

            BuildReport(root);
            BuildMail(root);
            BuildTree(root);
        }

        private void BuildReport(RectTransform root)
        {
            reportPage = UIFactory.Stretch(UIFactory.Rect("Report", root), 0f, 160f, 0f, 0f);
            Image p = UIFactory.Panel(reportPage, "Panel", new Color(0.04f, 0.06f, 0.14f, 1f), Palette.Cyan, false);
            UIFactory.Place(p.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 40f), new Vector2(900f, 600f));
            TextMeshProUGUI head = UIFactory.Text(p.transform, "Head", "DATA EARNED TODAY", 26f, Palette.TextSoft);
            UIFactory.Place(head.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -30f), new Vector2(800f, 36f));
            earnedText = UIFactory.GlowText(p.transform, "Earned", "0", 110f, Palette.Gold);
            UIFactory.Place(earnedText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -70f), new Vector2(860f, 130f));
            statsText = UIFactory.Text(p.transform, "Stats", "", 28f, Color.white, TextAlignmentOptions.Top, FontStyles.Normal);
            UIFactory.Place(statsText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -220f), new Vector2(800f, 260f));
            statsText.lineSpacing = 14f;
            lostText = UIFactory.Text(p.transform, "Lost", "", 24f, Palette.Orange);
            UIFactory.Place(lostText.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 40f), new Vector2(820f, 64f));
            lostText.enableWordWrapping = true;

            continueButton = UIFactory.MenuButton(root, "Continue", "CONTINUE  >", 32f, Continue, new Vector2(420f, 84f));
            UIFactory.Place((RectTransform)continueButton.transform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-60f, 40f), new Vector2(420f, 84f));
            continueLabel = continueButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }

        private void BuildMail(RectTransform root)
        {
            mailPage = UIFactory.Stretch(UIFactory.Rect("MailStep", root), 0f, 160f, 0f, 0f);
            Image p = UIFactory.Panel(mailPage, "Panel", new Color(0.04f, 0.06f, 0.14f, 1f), Palette.Cyan, false);
            UIFactory.Place(p.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 40f), new Vector2(900f, 560f));
            Image icon = UIFactory.Icon(p.transform, "mail", Palette.Cyan, 110f);
            UIFactory.Place(icon.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -40f), new Vector2(110f, 110f));
            mailStatus = UIFactory.Text(p.transform, "Status", "", 34f, Color.white);
            UIFactory.Place(mailStatus.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -170f), new Vector2(820f, 90f));
            mailStatus.enableWordWrapping = true;
            openMailButton = UIFactory.MenuButton(p.transform, "OpenMail", "OPEN MAIL", 30f, OpenMail, new Vector2(380f, 80f));
            UIFactory.Place((RectTransform)openMailButton.transform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -280f), new Vector2(380f, 80f));
            tomorrowText = UIFactory.Text(p.transform, "Tomorrow", "", 24f, Palette.TextSoft, TextAlignmentOptions.Top, FontStyles.Normal);
            UIFactory.Place(tomorrowText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -390f), new Vector2(820f, 150f));
            tomorrowText.enableWordWrapping = true;
        }

        private void BuildTree(RectTransform root)
        {
            treePage = UIFactory.Stretch(UIFactory.Rect("TreeStep", root), 40f, 160f, 40f, 130f);
            tree = treePage.gameObject.AddComponent<TreeView>();

            undoButton = UIFactory.MenuButton(root, "Undo", "UNDO TONIGHT", 24f, () => { SkillTree.I.UndoTonight(); RefreshUndo(); }, new Vector2(360f, 70f), Palette.Orange);
            UIFactory.Place((RectTransform)undoButton.transform, UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(60f, 40f), new Vector2(360f, 70f));
            undoLabel = undoButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            startButton = UIFactory.MenuButton(root, "StartDay", "START DAY 2  >", 34f, StartDay, new Vector2(480f, 90f), Palette.Green);
            UIFactory.Place((RectTransform)startButton.transform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-60f, 36f), new Vector2(480f, 90f));
            startLabel = startButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }

        // ---------------- flow ----------------

        /// <summary>Opens at the report (or, after quitting at night, straight at mail / tree).</summary>
        public void Open(DayReport dayReport, bool resumed)
        {
            report = dayReport ?? new DayReport();
            Show();
            int day = DayManager.I != null ? DayManager.I.Day : 1;
            title.text = "END OF DAY " + day;
            startLabel.text = "START DAY " + (day + 1) + "  >";
            if (HUD.I != null) HUD.I.SetShown(false, false);
            if (resumed) GoTo(MailDirector.I.AnyNeedsReply ? Step.Mail : Step.Tree);
            else GoTo(Step.Report);
        }

        /// <summary>After a Rebroadcast: the Deep Signal tree, then Day 1.</summary>
        public void OpenDeep()
        {
            Show();
            title.text = "REBROADCAST";
            startLabel.text = "START DAY 1  >";
            if (HUD.I != null) HUD.I.SetShown(false, false);
            GoTo(Step.Deep);
        }

        private void Show()
        {
            IsOpen = true;
            group.blocksRaycasts = true;
            group.interactable = true;
            UIFactory.Fade(group, true, 0.3f);
            AudioManager.Play("arpeggio", 0.8f, 0.6f);
        }

        public void Close()
        {
            IsOpen = false;
            group.blocksRaycasts = false;
            group.interactable = false;
            UIFactory.Fade(group, false, 0.25f);
            if (HUD.I != null) HUD.I.SetShown(true, false);
        }

        public void GoTo(Step step)
        {
            Current = step;
            reportPage.gameObject.SetActive(step == Step.Report);
            mailPage.gameObject.SetActive(step == Step.Mail);
            treePage.gameObject.SetActive(step == Step.Tree || step == Step.Deep);
            continueButton.gameObject.SetActive(step == Step.Report || step == Step.Mail);
            undoButton.gameObject.SetActive(step == Step.Tree);
            startButton.gameObject.SetActive(step == Step.Tree || step == Step.Deep);
            for (int i = 0; i < stepLabels.Count; i++)
            {
                stepLabels[i].gameObject.SetActive(step != Step.Deep);
                bool on = i == (int)step;
                stepLabels[i].color = on ? Palette.Cyan : ((int)step > i ? Palette.TextSoft : Palette.Dim);
                stepLabels[i].transform.localScale = on ? Vector3.one * 1.1f : Vector3.one;
            }
            RefreshWallet();
            switch (step)
            {
                case Step.Report:
                    FillReport();
                    continueLabel.text = "CONTINUE  >";
                    Select(continueButton);
                    break;
                case Step.Mail:
                    RefreshMailStep();
                    if (MailDirector.I.AnyNeedsReply) Select(openMailButton);
                    else Select(continueButton);
                    break;
                case Step.Tree:
                    tree.Build(false);
                    RefreshUndo();
                    break;
                case Step.Deep:
                    tree.Build(true);
                    break;
            }
            EventBus.RaiseNightStep(step.ToString().ToLowerInvariant());
        }

        private void Continue()
        {
            if (Current == Step.Report)
            {
                if (countUp != null) Juice.Stop(countUp);
                GoTo(Step.Mail);
                // Nothing to answer tonight: go straight on to the tree.
                if (MailDirector.I.Inbox.Count == 0 || (!MailDirector.I.AnyNeedsReply && MailDirector.I.UnreadCount == 0)) GoTo(Step.Tree);
            }
            else if (Current == Step.Mail)
            {
                if (MailDirector.I.AnyNeedsReply)
                {
                    Juice.PunchScale(openMailButton.transform, 1.15f, 0.2f);
                    AudioManager.Play("boop");
                    return;
                }
                if (MailUI.I != null && MailUI.I.IsOpen) MailUI.I.Close();
                GoTo(Step.Tree);
            }
        }

        private void OpenMail()
        {
            if (MailUI.I == null) return;
            MailUI.I.Open();
        }

        private void StartDay()
        {
            if (MailUI.I != null && MailUI.I.IsOpen) MailUI.I.Close();
            if (Current == Step.Deep)
            {
                Close();
                DayManager.I.StartFirstDay();
                return;
            }
            Close();
            DayManager.I.StartNextDay();
        }

        // ---------------- content ----------------

        private void FillReport()
        {
            DayReport r = report ?? new DayReport();
            statsText.text =
                "Static popped   <color=#FFFFFF>" + r.staticPopped + "</color>\n" +
                "Bits sorted   <color=#7CFF3B>" + r.bitsSorted + "</color>" + (r.wrongSorts > 0 ? "     Wrong chute   <color=#FF8A3B>" + r.wrongSorts + "</color>" : "") + "\n" +
                "Crunches   <color=#3BF0FF>" + r.crunches + "</color>     Best combo   <color=#FF4BE1>" + r.bestCombo + "</color>";
            lostText.text = r.bitsLost > 0 ? r.bitsLost + " bits were left on the floor and lost." + (GameStats.NightStorage ? "" : "  (Night Storage keeps them.)") : "";
            if (countUp != null) Juice.Stop(countUp);
            countUp = Juice.Run(CountUp(r.dataEarned));
        }

        private IEnumerator CountUp(double target)
        {
            float t = 0f;
            float d = 1.2f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                double v = target * Juice.EaseOutCubic(Mathf.Clamp01(t / d));
                earnedText.text = "+" + NumberFormat.Format(v);
                if (Random.value < 0.3f) AudioManager.Play("coin", Random.Range(1.1f, 1.4f), 0.25f);
                yield return null;
            }
            earnedText.text = "+" + NumberFormat.Format(target);
            Juice.PunchScale(earnedText.transform, 1.15f, 0.2f);
        }

        private void RefreshMailStep()
        {
            if (mailStatus == null || MailDirector.I == null) return;
            int waiting = 0;
            foreach (MailEntry e in MailDirector.I.Inbox) if (e.NeedsReply) waiting++;
            int unread = MailDirector.I.UnreadCount;
            if (waiting > 0) mailStatus.text = waiting == 1 ? "1 message needs a reply before you continue." : waiting + " messages need a reply before you continue.";
            else if (unread > 0) mailStatus.text = "You have new mail.";
            else mailStatus.text = "All caught up.";
            tomorrowText.text = TomorrowSummary();
            if (continueLabel != null && Current == Step.Mail) continueLabel.text = waiting > 0 ? "REPLY FIRST" : "CONTINUE  >";
        }

        /// <summary>Tomorrow's conditions from tonight's replies, in plain words.</summary>
        public static string TomorrowSummary()
        {
            if (DayManager.I == null) return "";
            Dictionary<string, double> m = DayManager.I.TomorrowModifiers;
            if (m.Count == 0) return "";
            List<string> parts = new List<string>();
            if (m.TryGetValue("data_mult", out double dm)) parts.Add((dm >= 1d ? "+" : "") + Mathf.RoundToInt((float)((dm - 1d) * 100d)) + "% Data");
            if (m.TryGetValue("shift_seconds", out double ss) && System.Math.Abs(ss) > 0.1d) parts.Add((ss > 0 ? "+" : "") + Mathf.RoundToInt((float)ss) + " s shift");
            if (m.TryGetValue("golden_mult", out double gm)) parts.Add(gm <= 0.01d ? "no Gold Static" : (GameStats.GoldUnlockedByAntenna ? "x" + gm.ToString("0.#") + " Gold Static" : "a rare Gold Static"));
            if (m.TryGetValue("spawn_mult", out double sm)) parts.Add((sm >= 1d ? "+" : "") + Mathf.RoundToInt((float)((sm - 1d) * 100d)) + "% Static");
            if (m.TryGetValue("no_events", out double ne) && ne > 0d) parts.Add("no events");
            return parts.Count == 0 ? "" : "TOMORROW:  " + string.Join("   ·   ", parts);
        }

        private void RefreshWallet()
        {
            if (walletText == null || GameManager.I == null) return;
            walletText.text = Current == Step.Deep ? GameManager.I.Echoes + " ECHOES" : NumberFormat.Format(GameManager.I.Data) + " DATA";
            int found = GameManager.I.EndingsSeen.Count;
            if (endingsText != null) endingsText.text = found > 0 ? "ENDINGS FOUND  " + found + "/" + Mathf.Max(3, Balance.I.Mail.endings.Count) : "";
        }

        private void RefreshUndo()
        {
            if (undoButton == null || SkillTree.I == null) return;
            bool any = SkillTree.I.HasTonightPurchases;
            undoButton.interactable = any;
            undoLabel.text = any ? "UNDO TONIGHT  (+" + NumberFormat.Format(SkillTree.I.TonightSpent) + ")" : "UNDO TONIGHT";
            undoLabel.alpha = any ? 1f : 0.4f;
        }

        private static void Select(Selectable s)
        {
            if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Current == Step.Mail && continueLabel != null)
            {
                bool waiting = MailDirector.I.AnyNeedsReply;
                continueButton.interactable = true;
                continueLabel.alpha = waiting ? 0.5f : 1f;
            }
        }
    }
}
