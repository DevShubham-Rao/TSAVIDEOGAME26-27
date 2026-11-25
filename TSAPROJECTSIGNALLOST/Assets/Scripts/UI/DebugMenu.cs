using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Developer debug menu. Toggle with ~ (backquote) or F1, or the faint "DBG" tab in the bottom-right
    /// corner. It creates its own canvas, so it works in every scene and needs no scene setup.
    /// Set <see cref="Enabled"/> to false to compile it out of a release build.
    /// </summary>
    public class DebugMenu : MonoBehaviour
    {
        /// <summary>Flip to false to disable the menu entirely (hotkey and corner tab).</summary>
        public const bool Enabled = true;

        public static DebugMenu I { get; private set; }

        public bool IsOpen { get; private set; }

        private GameObject panel;
        private RectTransform rootRect;
        private bool built;
        private TextMeshProUGUI status;
        private TextMeshProUGUI revealLabel;
        private readonly List<Button> shiftOnly = new List<Button>();
        private float refreshTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Enabled || I != null) return;
            GameObject go = new GameObject("[DebugMenu]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugMenu>();
        }

        private void Awake()
        {
            I = this;
            Build();
        }

        // ---------------- build ----------------

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            RectTransform root = (RectTransform)transform;
            rootRect = root;

            // Faint corner tab (the panel itself is built on first open, once the game data has loaded).

            Button tab = UIFactory.Button(root, "Tab", "DBG", 16f, Toggle, new Color(0.05f, 0.07f, 0.14f, 0.35f), new Color(1f, 1f, 1f, 0.15f), new Color(1f, 1f, 1f, 0.45f));
            UIFactory.Place((RectTransform)tab.transform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-6f, 6f), new Vector2(54f, 28f));

        }

        private void BuildPanel()
        {
            built = true;
            RectTransform root = rootRect;
            Image p = UIFactory.Panel(root, "Panel", new Color(0.03f, 0.04f, 0.1f, 1f), Palette.Orange, true);
            panel = p.gameObject;
            UIFactory.Place(p.rectTransform, UIFactory.MidLeft, UIFactory.MidLeft, new Vector2(16f, 0f), new Vector2(560f, 900f));

            TextMeshProUGUI title = UIFactory.Text(panel.transform, "Title", "DEBUG MENU   (~ or F1)", 28f, Palette.Orange, TextAlignmentOptions.Left);
            UIFactory.Place(title.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(20f, -14f), new Vector2(520f, 38f));
            status = UIFactory.Text(panel.transform, "Status", "", 20f, Palette.TextSoft, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            UIFactory.Place(status.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(20f, -54f), new Vector2(520f, 66f));
            status.enableWordWrapping = true;

            float y = -130f;
            y = Section("FLOW", y);
            y = Row(y, new[] { "Skip Day", "Open Skill Tree", "Start Next Day" }, new Action[] { SkipDay, OpenTree, NextDay }, false);
            y = Section("CURRENCY", y);
            y = Row(y, new[] { "+1K Data", "+100K Data", "+10M Data" }, new Action[] { () => AddData(1e3), () => AddData(1e5), () => AddData(1e7) }, false);
            y = Row(y, new[] { "+5 Echoes", "+25 Echoes", "Reset Echoes" }, new Action[] { () => GameManager.I.AddEchoes(5), () => GameManager.I.AddEchoes(25), () => GameManager.I.AddEchoes(-GameManager.I.Echoes) }, false);
            y = Section("TRIGGER QTE  (needs a running shift)", y);
            List<string> names = new List<string>();
            List<Action> acts = new List<Action>();
            if (Balance.I != null)
            {
                foreach (EventDef e in Balance.I.Events)
                {
                    EventDef def = e;
                    names.Add(e.name);
                    acts.Add(() => TriggerEvent(def));
                }
            }
            for (int i = 0; i < names.Count; i += 3)
            {
                int n = Mathf.Min(3, names.Count - i);
                string[] ln = new string[n];
                Action[] la = new Action[n];
                for (int k = 0; k < n; k++)
                {
                    ln[k] = names[i + k];
                    la[k] = acts[i + k];
                }
                y = Row(y, ln, la, true);
            }
            y = Section("SPAWN STATIC  (needs a running shift)", y);
            y = Row(y, new[] { "Red", "Blue", "Green" }, new Action[] { () => SpawnStatic(BlobColor.Red), () => SpawnStatic(BlobColor.Blue), () => SpawnStatic(BlobColor.Green) }, true);
            y = Row(y, new[] { "GOLD", "Prism", "Spawn x10 Red" }, new Action[] { SpawnGold, () => SpawnStatic(BlobColor.Prism), () => { for (int i = 0; i < 10; i++) SpawnStatic(BlobColor.Red); } }, true);
            y = Row(y, new[] { "Bits (red)", "Bits (blue)", "Bits (green)" }, new Action[] { () => SpawnBits(BlobColor.Red), () => SpawnBits(BlobColor.Blue), () => SpawnBits(BlobColor.Green) }, true);
            y = Section("SKILL TREE", y);
            Button[] tb = RowButtons(y, new[] { "Reveal All: OFF", "Own All Nodes" }, new Action[] { ToggleReveal, OwnAll }, false);
            revealLabel = tb[0].GetComponentInChildren<TextMeshProUGUI>();
            y -= 56f;

            panel.SetActive(true);
        }

        private float Section(string label, float y)
        {
            TextMeshProUGUI t = UIFactory.Text(panel.transform, "Section", label, 18f, Palette.Orange, TextAlignmentOptions.Left);
            UIFactory.Place(t.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(20f, y), new Vector2(520f, 26f));
            return y - 30f;
        }

        private float Row(float y, string[] labels, Action[] actions, bool needsShift)
        {
            RowButtons(y, labels, actions, needsShift);
            return y - 56f;
        }

        private Button[] RowButtons(float y, string[] labels, Action[] actions, bool needsShift)
        {
            Button[] result = new Button[labels.Length];
            float w = 164f;
            for (int i = 0; i < labels.Length; i++)
            {
                Action a = actions[i];
                Button b = UIFactory.Button(panel.transform, "Btn", labels[i], 19f, () => { if (a != null) a(); Refresh(); },
                    new Color(0.07f, 0.1f, 0.2f, 1f), Palette.Cyan);
                UIFactory.Place((RectTransform)b.transform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(20f + i * (w + 8f), y), new Vector2(w, 46f));
                TextMeshProUGUI lbl = b.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.enableAutoSizing = true;
                    lbl.fontSizeMin = 12f;
                    lbl.fontSizeMax = 19f;
                    lbl.enableWordWrapping = false;
                }
                if (needsShift) shiftOnly.Add(b);
                result[i] = b;
            }
            return result;
        }

        // ---------------- actions ----------------

        private static bool Playing { get { return GameManager.I != null && GameManager.I.State == GameState.Playing; } }
        private static bool InShift { get { return Playing && DayManager.I != null && DayManager.I.InShiftOrLastCall; } }

        private void SkipDay()
        {
            if (DayManager.I == null) return;
            if (DayManager.I.Phase == DayPhase.Night) return;
            DayManager.I.DebugEndDay();
        }

        private void OpenTree()
        {
            if (DayManager.I == null) return;
            if (DayManager.I.Phase == DayPhase.Night && EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen) EndOfDayScreen.I.GoTo(EndOfDayScreen.Step.Tree);
            else DayManager.I.DebugOpenTree();
        }

        private void NextDay()
        {
            if (DayManager.I != null && DayManager.I.Phase == DayPhase.Night && EndOfDayScreen.I != null && EndOfDayScreen.I.IsOpen)
            {
                EndOfDayScreen.I.Close();
                DayManager.I.StartNextDay();
            }
        }

        private static void AddData(double amount)
        {
            if (GameManager.I != null) GameManager.I.Refund(amount);
        }

        private static void TriggerEvent(EventDef def)
        {
            if (!InShift || EventDirector.I == null) return;
            EventDirector.I.StartEvent(def);
        }

        private static Vector3 SpawnPoint()
        {
            return BlobSpawner.I.RandomInteriorPoint();
        }

        private static void SpawnStatic(BlobColor c)
        {
            if (!InShift) return;
            BlobSpawner.I.SpawnColor(c, SpawnPoint(), 1.1f, false);
        }

        private static void SpawnGold()
        {
            if (!InShift) return;
            BlobSpawner.I.SpawnGolden(SpawnPoint(), false);
        }

        private static void SpawnBits(BlobColor c)
        {
            if (!InShift) return;
            BlobTypeDef t = BlobSpawner.I.TypeForColor(c);
            double each = (t != null ? t.value / Mathf.Max(1, t.bits) : 1d) * Balance.B.dataValueScale;
            BlobSpawner.I.SpawnBits(SpawnPoint(), c, 6, each * 6d, false);
        }

        private void ToggleReveal()
        {
            if (SkillTree.I == null) return;
            SkillTree.I.RevealAll = !SkillTree.I.RevealAll;
            EventBus.RaiseUpgradeBought("debug", 0);
        }

        private static void OwnAll()
        {
            if (SkillTree.I != null) SkillTree.I.DebugOwnAll();
        }

        // ---------------- per frame ----------------

        public void Toggle()
        {
            if (!built)
            {
                if (Balance.I == null || Balance.I.Events == null) return;
                BuildPanel();
            }
            IsOpen = !IsOpen;
            panel.SetActive(IsOpen);
            if (IsOpen) Refresh();
        }

        private void Refresh()
        {
            if (status == null) return;
            DayManager dm = DayManager.I;
            GameManager gm = GameManager.I;
            if (dm == null || gm == null)
            {
                status.text = "Loading...";
                return;
            }
            status.text = "State " + gm.State + "   Day " + dm.Day + "   Phase " + dm.Phase + "   Clock " + dm.TimeLeft.ToString("0") + " s\n" +
                          "Data " + NumberFormat.Format(gm.Data) + "   Echoes " + gm.Echoes + "   Antenna " + GameStats.Antenna + "   Endings " + gm.EndingsSeen.Count + "/3";
            bool shift = InShift;
            foreach (Button b in shiftOnly) if (b != null) b.interactable = shift;
            if (revealLabel != null && SkillTree.I != null) revealLabel.text = "Reveal All: " + (SkillTree.I.RevealAll ? "ON" : "OFF");
        }

        private void Update()
        {
            InputReader input = InputReader.I;
            if (Enabled && input != null && input.DebugTogglePressed) Toggle();
            if (!IsOpen) return;
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.25f;
                Refresh();
            }
        }
    }
}
