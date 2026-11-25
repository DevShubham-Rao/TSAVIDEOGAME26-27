using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost
{
    /// <summary>
    /// Draws the skill tree inside a pannable, zoomable viewport: drag to pan, mouse wheel (or the + / -
    /// buttons) to zoom. Fog of war: a node and its connector only appear once you own a node next to it,
    /// so you cannot see how big the tree is. Every node is a single purchase; former "ranks" are separate
    /// nodes further down the branch. Branch colors only label the branches (Navy Pop, Pink Sweep,
    /// Orange Station, Purple Signal); they have nothing to do with Static colors.
    /// </summary>
    public class TreeView : MonoBehaviour
    {
        public static readonly Color NavyBranch = Palette.Hex("#5B7CFF");
        public static readonly Color PinkBranch = Palette.Hex("#FF6FC8");
        public static readonly Color OrangeBranch = Palette.Hex("#FF9A45");
        public static readonly Color PurpleBranch = Palette.Hex("#A77BFF");

        private const float MinZoom = 0.3f;
        private const float MaxZoom = 1.8f;

        private class NodeView
        {
            public UpgradeDef Def;
            public RectTransform Root;
            public Image Fill;
            public Image Border;
            public Image Glow;
            public Image Icon;
            public TextMeshProUGUI Cost;
            public Button Button;
            public Color Color;
            public Vector2 Pos;
            public bool Visible;
            public bool Seen;
        }

        private class LineView
        {
            public Image Img;
            public string From;
            public string To;
        }

        private RectTransform viewport;
        private RectTransform content;
        private RectTransform details;
        private TextMeshProUGUI dName;
        private TextMeshProUGUI dBranch;
        private TextMeshProUGUI dDesc;
        private TextMeshProUGUI dRank;
        private TextMeshProUGUI dValue;
        private TextMeshProUGUI dCost;
        private TextMeshProUGUI dState;
        private Image dIcon;
        private readonly List<NodeView> nodes = new List<NodeView>();
        private readonly List<LineView> lines = new List<LineView>();
        private bool deep;
        private UpgradeDef focus;
        private float zoom = 1f;
        private Vector2 pan;
        private Canvas canvas;
        private bool viewFitted;

        public bool Deep { get { return deep; } }
        public float Zoom { get { return zoom; } }
        public int VisibleNodeCount
        {
            get
            {
                int n = 0;
                foreach (NodeView v in nodes) if (v.Visible) n++;
                return n;
            }
        }
        public int TotalNodeCount { get { return nodes.Count; } }

        public static Color BranchColor(string branch)
        {
            switch (branch)
            {
                case "pop": return NavyBranch;
                case "sweep": return PinkBranch;
                case "station": return OrangeBranch;
                case "signal": return PurpleBranch;
                case "deep": return Color.white;
                default: return Palette.Cyan;
            }
        }

        public static string BranchTitle(string branch)
        {
            switch (branch)
            {
                case "pop": return "NAVY: POP";
                case "sweep": return "PINK: SWEEP";
                case "station": return "ORANGE: STATION";
                case "signal": return "PURPLE: SIGNAL";
                case "deep": return "DEEP SIGNAL";
                default: return "";
            }
        }

        public static string BranchTheme(string branch)
        {
            switch (branch)
            {
                case "pop": return "Your clicks";
                case "sweep": return "The vacuum";
                case "station": return "Automation";
                case "signal": return "Economy and story";
                default: return "";
            }
        }

        private static float BranchX(string branch)
        {
            switch (branch)
            {
                case "pop": return -1.5f;
                case "sweep": return -0.5f;
                case "station": return 0.5f;
                case "signal": return 1.5f;
                default: return 0f;
            }
        }

        private void OnEnable()
        {
            EventBus.UpgradeBought += OnBought;
            EventBus.DataSpent += Refresh;
            EventBus.CoinLanded += OnCoin;
        }

        private void OnDisable()
        {
            EventBus.UpgradeBought -= OnBought;
            EventBus.DataSpent -= Refresh;
            EventBus.CoinLanded -= OnCoin;
        }

        private void OnCoin(double amount)
        {
            Refresh();
        }

        private void OnBought(string id, int level)
        {
            Refresh();
        }

        // ---------------- build ----------------

        /// <summary>Builds the view for the main tree or the Deep Signal tree.</summary>
        public void Build(bool deepMode)
        {
            deep = deepMode;
            foreach (Transform c in transform) Destroy(c.gameObject);
            nodes.Clear();
            lines.Clear();
            focus = null;
            viewFitted = false;
            canvas = GetComponentInParent<Canvas>();

            RectTransform root = (RectTransform)transform;
            Image vp = UIFactory.Image(root, "Viewport", UISprites.Square, new Color(0f, 0f, 0f, 0.001f), true);
            viewport = vp.rectTransform;
            UIFactory.Stretch(viewport, 0f, 0f, 450f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<TreeDrag>().Init(this);
            content = UIFactory.Rect("Content", viewport);
            content.anchorMin = content.anchorMax = UIFactory.Center;
            content.pivot = UIFactory.Center;
            content.sizeDelta = Vector2.zero;
            RectTransform lineLayer = UIFactory.Rect("Lines", content);
            RectTransform headLayer = UIFactory.Rect("Headers", content);
            RectTransform nodeLayer = UIFactory.Rect("Nodes", content);

            TreeConfig cfg = Balance.B.tree;
            Dictionary<string, Vector2> pos = new Dictionary<string, Vector2>();
            foreach (UpgradeDef u in Balance.I.Upgrades)
            {
                if (u.deep != deep) continue;
                Vector2 p;
                if (deep) p = new Vector2(u.col * cfg.laneSpacing * 1.7f, -(40f + u.row * cfg.rowSpacing * 1.3f));
                else if (u.branch == "center") p = Vector2.zero;
                else p = new Vector2(BranchX(u.branch) * cfg.columnSpacing + u.col * cfg.laneSpacing, -(190f + u.row * cfg.rowSpacing));
                pos[u.id] = p;
            }

            if (!deep)
            {
                foreach (string br in new[] { "pop", "sweep", "station", "signal" })
                {
                    float x = BranchX(br) * cfg.columnSpacing;
                    MakeHeader(headLayer, BranchTitle(br), BranchTheme(br), BranchColor(br), new Vector2(x, -96f));
                }
            }
            else
            {
                MakeHeader(headLayer, "DEEP SIGNAL", "Permanent upgrades for every run, bought with Echoes.", Color.white, new Vector2(0f, 60f));
            }

            foreach (UpgradeDef u in Balance.I.Upgrades)
            {
                if (u.deep != deep) continue;
                List<string> reqs = u.requires != null && u.requires.Count > 0 ? u.requires : (!deep && u.branch != "center" ? new List<string> { SkillTree.CenterId } : new List<string>());
                foreach (string r in reqs)
                {
                    if (!pos.ContainsKey(r)) continue;
                    lines.Add(new LineView { Img = MakeLine(lineLayer, pos[r], pos[u.id]), From = r, To = u.id });
                }
            }
            foreach (UpgradeDef u in Balance.I.Upgrades)
            {
                if (u.deep != deep) continue;
                nodes.Add(MakeNode(nodeLayer, u, pos[u.id]));
            }

            BuildDetails(root);
            BuildZoomButtons(root);
            Refresh();
            FitView();
            SelectDefault();
        }

        private void MakeHeader(RectTransform parent, string title, string theme, Color color, Vector2 p)
        {
            TextMeshProUGUI h = UIFactory.OutlineText(parent, "Head", title, 24f, color);
            UIFactory.Place(h.rectTransform, UIFactory.Center, UIFactory.Center, p, new Vector2(340f, 32f));
            TextMeshProUGUI th = UIFactory.OutlineText(h.transform, "Theme", theme, 17f, Palette.TextSoft);
            UIFactory.Place(th.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, -28f), new Vector2(520f, 24f));
        }

        private Image MakeLine(RectTransform parent, Vector2 a, Vector2 b)
        {
            Image img = UIFactory.Image(parent, "Line", UISprites.Square, new Color(1f, 1f, 1f, 0.15f), false);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = UIFactory.Center;
            rt.pivot = new Vector2(0f, 0.5f);
            Vector2 d = b - a;
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(d.magnitude, 5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return img;
        }

        private NodeView MakeNode(RectTransform parent, UpgradeDef u, Vector2 p)
        {
            NodeView v = new NodeView { Def = u, Color = BranchColor(u.branch), Pos = p };
            float size = Balance.B.tree.nodeSize * (u.star ? 1.15f : 1f) * (u.id == SkillTree.CenterId ? 1.3f : 1f);
            v.Root = UIFactory.Place(UIFactory.Rect(u.id, parent), UIFactory.Center, UIFactory.Center, p, new Vector2(size, size));
            v.Glow = UIFactory.Image(v.Root, "Glow", UISprites.Glow, new Color(v.Color.r, v.Color.g, v.Color.b, 0f), false);
            UIFactory.Place(v.Glow.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size * 2f, size * 2f));
            Sprite fillSprite = u.star ? UISprites.HexFill : UISprites.Circle;
            Sprite rimSprite = u.star ? UISprites.HexOutline : UISprites.Ring;
            v.Fill = UIFactory.Image(v.Root, "Fill", fillSprite, new Color(0.05f, 0.08f, 0.17f, 1f), true);
            UIFactory.Stretch(v.Fill.rectTransform);
            v.Border = UIFactory.Image(v.Root, "Border", rimSprite, v.Color, false);
            UIFactory.Stretch(v.Border.rectTransform);
            v.Icon = UIFactory.Image(v.Root, "Icon", UISprites.Icon(u.icon), Color.white, false);
            UIFactory.Place(v.Icon.rectTransform, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size * 0.46f, size * 0.46f));
            v.Cost = UIFactory.OutlineText(v.Root, "Cost", "", 16f, Palette.Gold);
            UIFactory.Place(v.Cost.rectTransform, UIFactory.BottomCenter, UIFactory.TopCenter, new Vector2(0f, -4f), new Vector2(150f, 22f));

            v.Button = v.Fill.gameObject.AddComponent<Button>();
            v.Button.transition = Selectable.Transition.None;
            v.Button.targetGraphic = v.Fill;
            UIFactory.EnableNav(v.Button);
            UpgradeDef def = u;
            NodeView view = v;
            v.Button.onClick.AddListener(() => Click(def, view));
            NodeHover hover = v.Fill.gameObject.AddComponent<NodeHover>();
            hover.Init(this, def);
            return v;
        }

        private void BuildDetails(RectTransform root)
        {
            Image p = UIFactory.Panel(root, "Details", new Color(0.04f, 0.06f, 0.14f, 1f), Palette.Cyan, false);
            details = p.rectTransform;
            UIFactory.Place(details, UIFactory.MidRight, UIFactory.MidRight, new Vector2(0f, 40f), new Vector2(430f, 560f));
            dIcon = UIFactory.Image(details, "Icon", UISprites.Icon("star"), Color.white, false);
            UIFactory.Place(dIcon.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -24f), new Vector2(72f, 72f));
            dName = UIFactory.Text(details, "Name", "", 32f, Color.white, TextAlignmentOptions.Left);
            UIFactory.Place(dName.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(110f, -22f), new Vector2(300f, 44f));
            dName.enableAutoSizing = true;
            dName.fontSizeMin = 18f;
            dName.fontSizeMax = 32f;
            dBranch = UIFactory.Text(details, "Branch", "", 18f, Palette.Cyan, TextAlignmentOptions.Left);
            UIFactory.Place(dBranch.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(110f, -66f), new Vector2(300f, 28f));
            dDesc = UIFactory.Text(details, "Desc", "", 22f, Palette.TextSoft, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            UIFactory.Place(dDesc.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -116f), new Vector2(382f, 130f));
            dDesc.enableWordWrapping = true;
            dRank = UIFactory.Text(details, "Rank", "", 20f, Palette.Dim, TextAlignmentOptions.Left);
            UIFactory.Place(dRank.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -262f), new Vector2(382f, 30f));
            dValue = UIFactory.Text(details, "Value", "", 30f, Palette.Cyan, TextAlignmentOptions.Left);
            UIFactory.Place(dValue.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -300f), new Vector2(382f, 44f));
            dCost = UIFactory.Text(details, "Cost", "", 30f, Palette.Gold, TextAlignmentOptions.Left);
            UIFactory.Place(dCost.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -360f), new Vector2(382f, 44f));
            dState = UIFactory.Text(details, "State", "", 22f, Palette.TextSoft, TextAlignmentOptions.TopLeft);
            UIFactory.Place(dState.rectTransform, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(24f, -420f), new Vector2(382f, 110f));
            dState.enableWordWrapping = true;
        }

        private void BuildZoomButtons(RectTransform root)
        {
            Color fill = new Color(0.05f, 0.08f, 0.17f, 1f);
            Button plus = UIFactory.Button(root, "ZoomIn", "+", 34f, () => ZoomBy(1.25f), fill, Palette.Cyan);
            UIFactory.Place((RectTransform)plus.transform, UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(10f, 70f), new Vector2(54f, 54f));
            Button minus = UIFactory.Button(root, "ZoomOut", "-", 38f, () => ZoomBy(0.8f), fill, Palette.Cyan);
            UIFactory.Place((RectTransform)minus.transform, UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(10f, 10f), new Vector2(54f, 54f));
            Button fit = UIFactory.Button(root, "Recenter", "FIT", 20f, FitView, fill, Palette.Cyan);
            UIFactory.Place((RectTransform)fit.transform, UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(72f, 10f), new Vector2(70f, 54f));
            TextMeshProUGUI hint = UIFactory.Text(root, "PanHint", "Drag to pan   Wheel to zoom", 18f, Palette.Dim, TextAlignmentOptions.Left);
            UIFactory.Place(hint.rectTransform, UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(156f, 24f), new Vector2(340f, 26f));
        }

        // ---------------- pan / zoom ----------------

        private float CanvasScale { get { return canvas != null ? canvas.scaleFactor : 1f; } }

        public void PanBy(Vector2 screenDelta)
        {
            pan += screenDelta / Mathf.Max(0.01f, CanvasScale);
            ApplyView();
        }

        public void ZoomBy(float factor)
        {
            ZoomAt(factor, Vector2.zero);
        }

        /// <summary>Zooms around a point in viewport-local space (the cursor, or zero for the view center).</summary>
        public void ZoomAt(float factor, Vector2 localPoint)
        {
            float newZoom = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(newZoom, zoom)) return;
            Vector2 contentPoint = (localPoint - pan) / zoom;
            zoom = newZoom;
            pan = localPoint - contentPoint * zoom;
            ApplyView();
        }

        public bool ScreenInViewport(Vector2 screen)
        {
            return viewport != null && RectTransformUtility.RectangleContainsScreenPoint(viewport, screen, null);
        }

        public Vector2 ScreenToViewport(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screen, null, out Vector2 local);
            return local;
        }

        private void GetVisibleBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            foreach (NodeView v in nodes)
            {
                if (!v.Visible) continue;
                any = true;
                min = Vector2.Min(min, v.Pos);
                max = Vector2.Max(max, v.Pos);
            }
            if (!any)
            {
                min = Vector2.zero;
                max = Vector2.zero;
            }
            float pad = 140f;
            float padX = 240f;
            min -= new Vector2(padX, pad);
            max += new Vector2(padX, pad + 70f);
        }

        /// <summary>Frames every visible node (never reveals nodes that are still hidden by the fog).</summary>
        public void FitView()
        {
            if (viewport == null) return;
            GetVisibleBounds(out Vector2 min, out Vector2 max);
            Vector2 size = max - min;
            Vector2 vp = viewport.rect.size;
            float fit = Mathf.Min(vp.x / Mathf.Max(1f, size.x), vp.y / Mathf.Max(1f, size.y));
            zoom = Mathf.Clamp(fit, MinZoom, 1.15f);
            Vector2 mid = (min + max) * 0.5f;
            pan = -mid * zoom;
            viewFitted = true;
            ApplyView();
        }

        private void ApplyView()
        {
            if (content == null) return;
            GetVisibleBounds(out Vector2 min, out Vector2 max);
            Vector2 vp = viewport.rect.size;
            // Keep the visible part of the tree in view: it may drift, but never fully off screen.
            Vector2 mid = (min + max) * 0.5f * zoom;
            Vector2 half = (max - min) * 0.5f * zoom;
            Vector2 limit = vp * 0.5f + half - new Vector2(120f, 120f);
            limit = Vector2.Max(limit, new Vector2(40f, 40f));
            Vector2 wantMid = pan + mid;
            wantMid.x = Mathf.Clamp(wantMid.x, -limit.x, limit.x);
            wantMid.y = Mathf.Clamp(wantMid.y, -limit.y, limit.y);
            pan = wantMid - mid;
            content.anchoredPosition = pan;
            content.localScale = Vector3.one * zoom;
        }

        /// <summary>Pans the view so a node (keyboard focus) is on screen.</summary>
        private void EnsureNodeVisible(NodeView v)
        {
            Vector2 p = pan + v.Pos * zoom;
            Vector2 half = viewport.rect.size * 0.5f - new Vector2(90f, 90f);
            Vector2 shift = Vector2.zero;
            if (p.x > half.x) shift.x = half.x - p.x;
            else if (p.x < -half.x) shift.x = -half.x - p.x;
            if (p.y > half.y) shift.y = half.y - p.y;
            else if (p.y < -half.y) shift.y = -half.y - p.y;
            if (shift != Vector2.zero)
            {
                pan += shift;
                ApplyView();
            }
        }

        // ---------------- interaction ----------------

        public void Focus(UpgradeDef u)
        {
            focus = u;
            RefreshDetails();
        }

        private void SelectDefault()
        {
            NodeView best = null;
            foreach (NodeView v in nodes)
            {
                if (!v.Visible || v.Def.id == SkillTree.CenterId) continue;
                if (SkillTree.I.CanBuy(v.Def)) { best = v; break; }
                if (best == null) best = v;
            }
            if (best == null && nodes.Count > 0) best = nodes[0];
            if (best == null) return;
            Focus(best.Def);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(best.Button.gameObject);
        }

        /// <summary>Rect of a node (tutorial spotlight). Null while the node is hidden by the fog.</summary>
        public RectTransform NodeRect(string id)
        {
            foreach (NodeView v in nodes) if (v.Def.id == id && v.Visible) return v.Root;
            return null;
        }

        private void Click(UpgradeDef u, NodeView v)
        {
            Focus(u);
            if (u.id == SkillTree.CenterId) return;
            if (SkillTree.I.TryBuy(u))
            {
                Juice.PunchScale(v.Root, 1.25f, 0.25f);
                UIFactory.Burst(v.Root, v.Color, u.star ? 24 : 12);
                if (u.star) AudioManager.Play("level_up", 1.1f, 0.6f);
            }
            else
            {
                Juice.PunchScale(v.Root, 0.9f, 0.15f);
                AudioManager.Play("boop", 1f, 0.5f);
            }
            RefreshDetails();
        }

        public void Refresh()
        {
            if (SkillTree.I == null || content == null) return;
            foreach (NodeView v in nodes)
            {
                UpgradeDef u = v.Def;
                bool vis = SkillTree.I.IsVisible(u);
                if (vis != v.Root.gameObject.activeSelf) v.Root.gameObject.SetActive(vis);
                bool newlyShown = vis && !v.Visible && viewFitted;
                v.Visible = vis;
                if (!vis) continue;
                if (newlyShown)
                {
                    v.Root.localScale = Vector3.one * 0.2f;
                    Juice.PunchScale(v.Root, 1.3f, 0.35f);
                    UIFactory.Burst(v.Root, v.Color, 8);
                }
                bool owned = SkillTree.I.Level(u.id) > 0;
                bool reach = SkillTree.I.IsReachable(u) && SkillTree.I.MeetsUnlock(u);
                bool afford = !owned && reach && SkillTree.I.CanAfford(u);
                Color c = v.Color;
                if (u.deep && u.id == "prism_static") c = Palette.Rainbow(Time.unscaledTime * 0.2f);
                float a = reach || owned ? 1f : 0.35f;
                v.Border.color = new Color(c.r, c.g, c.b, owned ? 1f : a * 0.85f);
                v.Fill.color = owned ? Color.Lerp(new Color(0.05f, 0.08f, 0.17f, 1f), c, 0.55f) : new Color(0.04f, 0.06f, 0.13f, 1f);
                v.Icon.color = new Color(1f, 1f, 1f, owned || reach ? 1f : 0.35f);
                bool showCost = u.id != SkillTree.CenterId && !owned;
                v.Cost.text = !showCost ? "" : (reach ? CostText(u) : (u.unlock != null && !string.IsNullOrEmpty(u.unlock.ending) ? "SEE ENDING" : "LOCKED"));
                v.Cost.color = afford ? Palette.Gold : new Color(0.7f, 0.7f, 0.8f, 0.9f);
                v.Glow.color = new Color(c.r, c.g, c.b, afford ? 0.35f : (owned ? 0.15f : 0f));
            }
            foreach (LineView l in lines)
            {
                UpgradeDef to = Balance.I.Upgrade(l.To);
                UpgradeDef from = Balance.I.Upgrade(l.From);
                bool show = to != null && from != null && SkillTree.I.IsVisible(to) && SkillTree.I.IsVisible(from)
                            && (SkillTree.I.Owns(l.From) || SkillTree.I.Owns(l.To) || SkillTree.I.RevealAll);
                if (l.Img.gameObject.activeSelf != show) l.Img.gameObject.SetActive(show);
                if (!show) continue;
                bool lit = SkillTree.I.Owns(l.From) && SkillTree.I.Owns(l.To);
                Color c = BranchColor(to.branch);
                l.Img.color = lit ? new Color(c.r, c.g, c.b, 0.85f) : new Color(c.r, c.g, c.b, 0.3f);
            }
            if (viewFitted) ApplyView();
            RefreshDetails();
        }

        private static string CostText(UpgradeDef u)
        {
            double cost = SkillTree.I.NextCost(u);
            return u.deep ? cost.ToString("0") + " ECHO" + (cost == 1 ? "" : "ES") : NumberFormat.Format(cost);
        }

        private static string TierText(UpgradeDef u)
        {
            int us = u.id.LastIndexOf('_');
            if (us < 0 || !int.TryParse(u.id.Substring(us + 1), out int tier)) return "";
            string baseId = u.id.Substring(0, us);
            int total = 0;
            foreach (UpgradeDef o in Balance.I.Upgrades) if (o.id.StartsWith(baseId + "_") && o.deep == u.deep) total++;
            return total > 1 ? "Step " + tier + " of " + total : "";
        }

        private void RefreshDetails()
        {
            if (details == null) return;
            UpgradeDef u = focus;
            details.gameObject.SetActive(u != null);
            if (u == null) return;
            Color c = BranchColor(u.branch);
            dIcon.sprite = UISprites.Icon(u.icon);
            dIcon.color = c;
            dName.text = u.name;
            dBranch.text = u.id == SkillTree.CenterId ? "CENTER" : (u.deep ? "DEEP SIGNAL" : BranchTitle(u.branch)) + (u.star ? "   BIG UNLOCK" : "");
            dBranch.color = c;
            dDesc.text = u.desc;
            bool owned = SkillTree.I.Level(u.id) > 0;
            dRank.text = u.id == SkillTree.CenterId ? "" : TierText(u);
            dValue.text = u.id == SkillTree.CenterId ? "" : ValueText(u, owned);
            bool reach = SkillTree.I.IsReachable(u);
            bool unlock = SkillTree.I.MeetsUnlock(u);
            if (u.id == SkillTree.CenterId) dCost.text = "";
            else dCost.text = owned ? "OWNED" : "Cost  " + CostText(u);
            string state;
            if (u.id == SkillTree.CenterId) state = "Every branch grows from here.";
            else if (owned) state = "Bought.";
            else if (!unlock && u.unlock != null && !string.IsNullOrEmpty(u.unlock.ending)) state = "Locked: see this ending first.";
            else if (!unlock) state = "Locked: needs Antenna " + u.unlock.antenna + ".";
            else if (!reach) state = "Locked: buy a node it connects to first.";
            else if (!SkillTree.I.ShopOpen(u)) state = "Buy between days.";
            else if (!SkillTree.I.CanAfford(u)) state = u.deep ? "Not enough Echoes." : "Not enough Data yet.";
            else state = "Click the node or press Enter to buy.";
            dState.text = state;
        }

        private static string ValueText(UpgradeDef u, bool owned)
        {
            if (u.display == "unlock") return owned ? "Unlocked" : "Not unlocked";
            string stat = u.PrimaryStat;
            double now = SkillTree.I.Stat(stat);
            if (owned) return Format(u.display, now);
            double next = SkillTree.I.StatPreview(u, stat, 1);
            return Format(u.display, now) + "  →  " + Format(u.display, next);
        }

        private static string Format(string display, double v)
        {
            switch (display)
            {
                case "meters": return v.ToString("0.00") + " m";
                case "mult": return "x" + v.ToString("0.00");
                case "bonus_pct": return "+" + Mathf.RoundToInt((float)(v * 100d)) + "%";
                case "percent": return Mathf.RoundToInt((float)(v * 100d)) + "%";
                case "seconds": return v.ToString("0.##") + " s";
                case "level": return "Antenna " + Mathf.RoundToInt((float)v);
                default: return NumberFormat.Format(System.Math.Round(v));
            }
        }

        private void Update()
        {
            if (nodes.Count == 0 || viewport == null) return;
            InputReader input = InputReader.I;
            if (input != null && Mathf.Abs(input.ScrollDelta) > 0.01f && ScreenInViewport(input.PointerPosition))
            {
                ZoomAt(Mathf.Pow(1.18f, input.ScrollDelta), ScreenToViewport(input.PointerPosition));
            }
            // Affordable nodes breathe; the focused one is outlined.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (NodeView v in nodes)
            {
                if (!v.Visible) continue;
                bool afford = SkillTree.I.CanBuy(v.Def);
                float s = afford ? 1f + 0.04f * pulse : 1f;
                bool selected = sel == v.Button.gameObject;
                if (selected) s *= 1.1f;
                v.Root.localScale = Vector3.Lerp(v.Root.localScale, Vector3.one * s, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                if (selected && focus != v.Def)
                {
                    Focus(v.Def);
                    EnsureNodeVisible(v);
                }
            }
        }
    }

    /// <summary>Drag on the tree background (or any node) to pan.</summary>
    public class TreeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private TreeView view;

        public void Init(TreeView v)
        {
            view = v;
        }

        public void OnBeginDrag(PointerEventData e)
        {
        }

        public void OnDrag(PointerEventData e)
        {
            if (view != null) view.PanBy(e.delta);
        }
    }

    /// <summary>Hovering a node shows its details.</summary>
    public class NodeHover : MonoBehaviour, IPointerEnterHandler
    {
        private TreeView view;
        private UpgradeDef def;

        public void Init(TreeView v, UpgradeDef d)
        {
            view = v;
            def = d;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (view != null) view.Focus(def);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
