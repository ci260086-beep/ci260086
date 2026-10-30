using System;
using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>
    /// Builds the whole UI from code: floating resources, the icon rail, the drill hangar on the
    /// right, and the full-screen skill tree and collection pages.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const int HangarCardHeight = 29;
        private const int CardWidth = Stage.PanelWidth - 6;
        private const int PanelTitleHeight = 15;
        private const int FooterHeight = 21;
        private const int RailButton = 18;
        private const int RailTopGroup = 3;
        private const int MeterWidth = 60;
        private const int MeterHeight = 7;
        private const int MeterY = 169;
        private const float ToastFadeSeconds = 0.6f;
        private const int BossBarWidth = 100;
        private const float RebirthConfirmSeconds = 4f;
        private const int NodeSize = 18;
        private const int TreeLeft = 28;
        private const int TreeTop = 34;
        private const int TreeColumnStep = 58;
        private const int TreeRowStep = 22;

        private const int PageHangar = 0;
        private const int PageTree = 1;
        private const int PageCollection = 2;
        private const int PageStats = 3;
        private const int PageSettings = 4;

        private static readonly Color PanelColor = new Color32(0x1a, 0x12, 0x10, 0xff);
        private static readonly Color RailColor = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color RailSelected = new Color32(0x6b, 0x46, 0x30, 0xff);
        private static readonly Color CardBright = new Color32(0x4a, 0x31, 0x22, 0xff);
        private static readonly Color CardDim = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color IconBoxColor = new Color32(0x1a, 0x12, 0x10, 0xff);
        private static readonly Color CoinColor = new Color32(0xff, 0xe2, 0x7a, 0xff);
        private static readonly Color FrenzyColor = new Color32(0xe8, 0x40, 0x2e, 0xff);
        private static readonly Color ArmorColor = new Color32(0xc0, 0xc0, 0xcc, 0xff);
        private static readonly Color LockedColor = new Color32(0x3a, 0x3a, 0x44, 0xff);
        private static readonly Color BuyColor = new Color32(0x4a, 0x7a, 0x2a, 0xff);
        private static readonly Color MergeColor = new Color32(0xb8, 0x69, 0x3a, 0xff);
        private static readonly Color WarmText = new Color32(0xe0, 0x8a, 0x4a, 0xff);
        private static readonly Color SoftText = new Color32(0xc4, 0x9a, 0x6c, 0xff);
        private static readonly Color DimText = new Color32(0x8c, 0x5f, 0x3f, 0xff);
        private static readonly Color LineOff = new Color32(0x4a, 0x31, 0x22, 0xff);
        private static readonly Color LineOn = new Color32(0xe8, 0xb0, 0x2a, 0xff);
        private static readonly Color NodeHidden = new Color(0.25f, 0.25f, 0.28f, 1f);

        private static readonly string[] TreeIcons = { "tab_drill", "icon_coin", "tab_blast", "tab_frenzy", "rare_23" };

        private sealed class HangarCard
        {
            public int Tier;
            public Image Background;
            public Text Name;
            public Text Count;
            public Text Price;
            public Image BuyImage;
            public Button Buy;
            public Image MergeImage;
            public Button Merge;
            public Image Icon;
        }

        private sealed class TreeNode
        {
            public SkillDef Skill;
            public Image Box;
            public Image Icon;
            public Text Level;
            public Image LineUp;
        }

        /// <summary>Tells the page which element the pointer is on.</summary>
        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler
        {
            public Action Entered;
            public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        }

        private readonly List<HangarCard> hangarCards = new List<HangarCard>();
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<Image> railButtons = new List<Image>();
        private readonly List<string> pageTitles = new List<string>();
        private readonly List<TreeNode> treeNodes = new List<TreeNode>();
        private readonly List<Image> treeTabs = new List<Image>();
        private readonly List<Image> rareIcons = new List<Image>();
        private readonly List<Text> achievementLabels = new List<Text>();

        private GameSim sim;
        private ArtLibrary art;
        private CanvasScaler scaler;
        private RectTransform stageRoot;
        private GameObject sidePanel;
        private Text coinsLabel, incomeLabel, oresLabel, depthLabel, comboLabel, versionLabel, drillsLabel;
        private Text toastLabel, panelTitle, footerLabel, statsLabel, treeInfo, collectionInfo, collectionTitle;
        private Image meterFill;
        private Image bossBar, bossFill, bossArmorFill;
        private Text bossLabel;
        private Button rebirthButton;
        private Text rebirthLabel;
        private float rebirthArmed;
        private Button frenzyButton;
        private Image frenzyButtonImage;
        private Text frenzyButtonLabel;
        private float toastTime;
        private float fpsAverage = 60;
        private double coinsInFlight;
        private double oresInFlight;
        private int currentTree;
        private int currentPage;

        public Font Font { get; private set; }
        public RectTransform FxLayer { get; private set; }
        public Vector2 CoinTarget => new Vector2(9, 11);
        public Vector2 OreTarget => new Vector2(8, 36);

        public event Action SaveRequested;

        public void Init(GameSim simulation, ArtLibrary library, RenderTexture stageTexture)
        {
            sim = simulation;
            art = library;
            Font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 16);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var rootGo = new GameObject("Stage", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            stageRoot = (RectTransform)rootGo.transform;
            stageRoot.anchorMin = stageRoot.anchorMax = stageRoot.pivot = new Vector2(0.5f, 0.5f);
            stageRoot.sizeDelta = new Vector2(Stage.Width, Stage.Height);

            var imageGo = new GameObject("StageImage", typeof(RectTransform));
            imageGo.transform.SetParent(stageRoot, false);
            var stageImage = imageGo.AddComponent<RawImage>();
            stageImage.texture = stageTexture;
            stageImage.raycastTarget = false;
            Ui.TopLeft(stageImage.rectTransform, 0, 0, Stage.Width, Stage.Height);

            BuildDigOverlay();
            BuildFrenzyMeter();
            BuildSidePanel();
            BuildTreePage();
            BuildCollectionPage();
            BuildRail();

            var fxGo = new GameObject("Fx", typeof(RectTransform));
            fxGo.transform.SetParent(stageRoot, false);
            FxLayer = (RectTransform)fxGo.transform;
            Ui.TopLeft(FxLayer, 0, 0, Stage.Width, Stage.Height);

            toastLabel = Floating("", 0, 140, Stage.DigWidth, 18, 13, TextAnchor.MiddleCenter);
            toastLabel.color = CoinColor;

            var eventsGo = new GameObject("EventSystem");
            eventsGo.transform.SetParent(transform, false);
            eventsGo.AddComponent<EventSystem>();
            eventsGo.AddComponent<StandaloneInputModule>();

            ShowPage(PageHangar);
            ShowTree(0);
        }

        public void AddInFlight(double coins, double ores)
        {
            coinsInFlight += coins;
            oresInFlight += ores;
        }

        public void Land(double coins, double ores)
        {
            coinsInFlight = Math.Max(0, coinsInFlight - coins);
            oresInFlight = Math.Max(0, oresInFlight - ores);
        }

        public void ClearInFlight() => coinsInFlight = oresInFlight = 0;

        /// <summary>Big outlined message at the bottom of the dig area; fades out by itself.</summary>
        public void ShowToast(string text, float seconds)
        {
            toastLabel.text = text;
            toastTime = seconds;
        }

        public void ShowPage(int index)
        {
            currentPage = index;
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == index);
                railButtons[i].color = i == index ? RailSelected : RailColor;
            }
            sidePanel.SetActive(index != PageTree && index != PageCollection);
            panelTitle.text = pageTitles[index];
            footerLabel.text = "";
        }

        /// <summary>Opens the skill tree page; used by the rail and the screenshot driver.</summary>
        public void ShowTreePage() => ShowPage(PageTree);

        public void ShowCollectionPage() => ShowPage(PageCollection);

        public void ShowHangarPage() => ShowPage(PageHangar);

        private Text Floating(string text, float x, float y, float w, float h, int size, TextAnchor anchor)
        {
            var label = Ui.Label(stageRoot, Font, text, x, y, w, h, size, anchor);
            label.gameObject.AddComponent<Outline>().effectColor = PanelColor;
            return label;
        }

        private void BuildDigOverlay()
        {
            coinsLabel = Floating("0", 4, 2, 110, 18, 14, TextAnchor.MiddleLeft);
            coinsLabel.color = CoinColor;
            coinsLabel.fontStyle = FontStyle.Bold;
            incomeLabel = Floating("", 4, 20, 110, 10, 7, TextAnchor.MiddleLeft);
            Ui.Icon(stageRoot, art.Get("icon_copper"), 4, 32, 8);
            oresLabel = Floating("0", 15, 31, 80, 10, 8, TextAnchor.MiddleLeft);
            drillsLabel = Floating("", 4, 42, 100, 10, 7, TextAnchor.MiddleLeft);
            comboLabel = Floating("", 4, 53, 100, 11, 8, TextAnchor.MiddleLeft);

            bossBar = Ui.Panel(stageRoot, "BossBar", (Stage.DigWidth - BossBarWidth) / 2f, 4, BossBarWidth, 7, PanelColor);
            bossBar.raycastTarget = false;
            bossFill = Ui.Panel(bossBar.transform, "Fill", 1, 1, 0, 5, FrenzyColor);
            bossFill.raycastTarget = false;
            bossArmorFill = Ui.Panel(bossBar.transform, "Armor", 1, 1, 0, 2, ArmorColor);
            bossArmorFill.raycastTarget = false;
            bossLabel = Floating("", (Stage.DigWidth - BossBarWidth) / 2f, 12, BossBarWidth, 10, 7, TextAnchor.MiddleCenter);

            depthLabel = Floating("", Stage.DigWidth - 104, 2, 100, 14, 10, TextAnchor.MiddleRight);
            depthLabel.fontStyle = FontStyle.Bold;

            versionLabel = Floating("", 3, 171, 90, 8, 6, TextAnchor.MiddleLeft);
            versionLabel.color = SoftText;
        }

        private void BuildFrenzyMeter()
        {
            float x = (Stage.DigWidth - MeterWidth - 28) / 2f;
            var track = Ui.Panel(stageRoot, "FrenzyMeter", x, MeterY, MeterWidth, MeterHeight, PanelColor);
            track.raycastTarget = false;
            meterFill = Ui.Panel(track.transform, "Fill", 1, 1, 0, MeterHeight - 2, FrenzyColor);
            meterFill.raycastTarget = false;
            frenzyButton = Ui.Button(stageRoot, Font, "狂热", x + MeterWidth + 2, MeterY - 4, 26, 13, 7,
                LockedColor, out frenzyButtonLabel);
            frenzyButtonImage = frenzyButton.GetComponent<Image>();
            frenzyButton.onClick.AddListener(() => sim.StartFrenzy());
        }

        // Hangar, tree, collection, stats, settings: the first three on top, the rest at the bottom.
        private void BuildRail()
        {
            var rail = Ui.Panel(stageRoot, "Rail", Stage.DigWidth, 0, Stage.RailWidth, Stage.Height, Color.clear);
            rail.raycastTarget = false;
            string[] icons = { "tab_drill", "tab_frenzy", "rare_0", "tab_stats", "tab_settings" };
            for (int i = 0; i < icons.Length; i++)
            {
                int index = i;
                int y = i < RailTopGroup
                    ? 3 + i * (RailButton + 2)
                    : Stage.Height - 3 - (icons.Length - i) * (RailButton + 2) + 2;
                var button = Ui.Button(rail.transform, Font, "", 1, y, RailButton, RailButton, 7, RailColor, out _);
                Ui.Icon(button.transform, art.Get(icons[i]), 1, 1, 16);
                button.onClick.AddListener(() => ShowPage(index));
                railButtons.Add(button.GetComponent<Image>());
            }
        }

        private void BuildSidePanel()
        {
            var panel = Ui.Panel(stageRoot, "Panel", Stage.PanelX, 0, Stage.PanelWidth, Stage.Height, PanelColor);
            sidePanel = panel.gameObject;
            panelTitle = Ui.Label(panel.transform, Font, "", 0, 2, Stage.PanelWidth, 12, 8, TextAnchor.MiddleCenter);
            panelTitle.fontStyle = FontStyle.Bold;
            footerLabel = Ui.Label(panel.transform, Font, "", 3, Stage.Height - FooterHeight, Stage.PanelWidth - 6,
                FooterHeight - 2, 6, TextAnchor.MiddleLeft);
            footerLabel.color = SoftText;

            var hangar = NewPage(panel.transform, "钻头机库");
            BuildHangar(hangar.transform);

            // the two full-screen pages keep their slots in the list so rail indexes line up
            pages.Add(new GameObject("TreeSlot"));
            pageTitles.Add("技能树");
            pages.Add(new GameObject("CollectionSlot"));
            pageTitles.Add("成就");

            var stats = NewPage(panel.transform, "统计");
            statsLabel = Ui.Label(stats.transform, Font, "", 4, PanelTitleHeight, Stage.PanelWidth - 8, 120, 7, TextAnchor.UpperLeft);
            statsLabel.lineSpacing = 1.2f;
            rebirthButton = Ui.Button(stats.transform, Font, "", 3, 144, Stage.PanelWidth - 6, 18, 7, FrenzyColor, out rebirthLabel);
            rebirthButton.onClick.AddListener(OnRebirthClicked);

            var settings = NewPage(panel.transform, "设置");
            var save = Ui.Button(settings.transform, Font, "立即保存", 4, PanelTitleHeight + 2, Stage.PanelWidth - 8, 16, 7,
                CardBright, out _);
            save.onClick.AddListener(() =>
            {
                SaveRequested?.Invoke();
                ShowToast("已保存", 1.5f);
            });
        }

        private GameObject NewPage(Transform parent, string title)
        {
            var page = new GameObject("Page", typeof(RectTransform));
            page.transform.SetParent(parent, false);
            Ui.TopLeft((RectTransform)page.transform, 0, 0, Stage.PanelWidth, Stage.Height);
            pages.Add(page);
            pageTitles.Add(title);
            return page;
        }

        private void BuildHangar(Transform parent)
        {
            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(parent, false);
            var viewport = (RectTransform)viewportGo.transform;
            Ui.TopLeft(viewport, 2, PanelTitleHeight, Stage.PanelWidth - 4, Stage.Height - PanelTitleHeight - FooterHeight);
            viewportGo.AddComponent<RectMask2D>();
            var hit = viewportGo.AddComponent<Image>();
            hit.color = Color.clear;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = (RectTransform)contentGo.transform;
            Ui.TopLeft(content, 0, 0, Stage.PanelWidth - 4, DrillTiers.Count * HangarCardHeight);

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = HangarCardHeight / 2f;

            for (int tier = 0; tier < DrillTiers.Count; tier++)
                hangarCards.Add(BuildHangarCard(content, tier));
        }

        private HangarCard BuildHangarCard(Transform parent, int tier)
        {
            var bg = Ui.Panel(parent, $"tier{tier}", 1, tier * HangarCardHeight, CardWidth, HangarCardHeight - 2, CardBright);
            var card = new HangarCard { Tier = tier, Background = bg };

            var box = Ui.Panel(bg.transform, "IconBox", 2, 4, 18, 18, IconBoxColor);
            box.raycastTarget = false;
            card.Icon = Ui.Icon(box.transform, art.Strip($"tier_{tier}", 16)[0], 1, 1, 16);

            card.Name = Ui.Label(bg.transform, Font, DrillTiers.Names[tier], 22, 1, CardWidth - 24, 10, 6, TextAnchor.MiddleLeft);
            card.Name.fontStyle = FontStyle.Bold;
            card.Count = Ui.Label(bg.transform, Font, "", 40, 1, CardWidth - 42, 10, 6, TextAnchor.MiddleRight);
            card.Count.color = CoinColor;
            card.Price = Ui.Label(bg.transform, Font, "", 22, 11, CardWidth - 24, 8, 5, TextAnchor.MiddleLeft);
            card.Price.color = SoftText;

            card.Buy = Ui.Button(bg.transform, Font, "买", 22, 19, 25, 8, 5, BuyColor, out _);
            card.BuyImage = card.Buy.GetComponent<Image>();
            card.Buy.onClick.AddListener(() => sim.BuyDrill(tier));
            card.Merge = Ui.Button(bg.transform, Font, "合成", 48, 19, 24, 8, 5, MergeColor, out _);
            card.MergeImage = card.Merge.GetComponent<Image>();
            card.Merge.onClick.AddListener(() => sim.Merge(tier));

            bg.gameObject.AddComponent<HoverRelay>().Entered = () =>
                footerLabel.text = $"每次 {NumberFormat.Short(sim.DamageOf(tier))} 伤害";
            return card;
        }

        // A drawn tree: a trunk across the top, one branch per effect, a node per rank with lines
        // joining each rank to the one before it. Lit lines show what has been bought.
        private void BuildTreePage()
        {
            var page = Ui.Panel(stageRoot, "TreePage", 0, 0, Stage.PanelX, Stage.Height, PanelColor);
            pages.Insert(0, page.gameObject); // placeholder swapped below
            pages.RemoveAt(0);
            pages[PageTree] = page.gameObject;

            var title = Ui.Label(page.transform, Font, "技能树", 0, 2, Stage.PanelX, 12, 9, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;

            for (int i = 0; i < SkillCatalog.TreeNames.Length; i++)
            {
                int index = i;
                var tab = Ui.Button(page.transform, Font, SkillCatalog.TreeNames[i], 4 + i * 47, 15, 45, 12, 7, RailColor, out _);
                tab.onClick.AddListener(() => ShowTree(index));
                treeTabs.Add(tab.GetComponent<Image>());
            }

            var trunk = Ui.Panel(page.transform, "Trunk", TreeLeft + NodeSize / 2f, TreeTop - 3,
                4 * TreeColumnStep + 1, 1, LineOff);
            trunk.raycastTarget = false;

            for (int column = 0; column < 5; column++)
            {
                for (int rank = 0; rank < SkillCatalog.RanksPerEffect; rank++)
                {
                    int slot = treeNodes.Count;
                    float x = TreeLeft + column * TreeColumnStep;
                    float y = TreeTop + rank * TreeRowStep;

                    var line = Ui.Panel(page.transform, "Line", x + NodeSize / 2f, y - 4, 1, 4, LineOff);
                    line.raycastTarget = false;

                    var box = Ui.Panel(page.transform, "Node", x, y, NodeSize, NodeSize, RailColor);
                    var icon = Ui.Icon(box.transform, art.Get("tab_drill"), 1, 1, 16);
                    var level = Ui.Label(page.transform, Font, "", x + NodeSize + 1, y + 5, 30, 8, 5, TextAnchor.MiddleLeft);
                    level.color = SoftText;

                    var button = box.gameObject.AddComponent<Button>();
                    button.targetGraphic = box;
                    button.transition = Selectable.Transition.None;
                    button.onClick.AddListener(() =>
                    {
                        var skill = treeNodes[slot].Skill;
                        sim.Buy(skill.Id);
                        ShowNodeInfo(skill);
                    });
                    box.gameObject.AddComponent<HoverRelay>().Entered = () => ShowNodeInfo(treeNodes[slot].Skill);

                    treeNodes.Add(new TreeNode { Box = box, Icon = icon, Level = level, LineUp = line });
                }
            }

            treeInfo = Ui.Label(page.transform, Font, "", 6, 166, Stage.PanelX - 12, 12, 7, TextAnchor.MiddleLeft);
            treeInfo.color = SoftText;
        }

        private void ShowTree(int tree)
        {
            currentTree = tree;
            for (int i = 0; i < treeTabs.Count; i++) treeTabs[i].color = i == tree ? RailSelected : RailColor;

            int slot = 0;
            foreach (var skill in SkillCatalog.All)
            {
                if (skill.Tree != tree) continue;
                var node = treeNodes[slot++];
                node.Skill = skill;
                node.Icon.sprite = art.Get(TreeIcons[tree]);
            }
            if (treeInfo != null) treeInfo.text = "";
        }

        private void ShowNodeInfo(SkillDef skill)
        {
            int level = sim.State.LevelOf(skill.Id);
            string cost = level >= skill.MaxLevel ? "已满级" : "价格 " + NumberFormat.Short(skill.CostAt(level));
            string gate = sim.SkillDepthReached(skill) ? "" : $"  需要到达 {skill.UnlockDepth} 米";
            treeInfo.text = $"{skill.Name}  {skill.EffectText}/级  等级 {level}/{skill.MaxLevel}  {cost}{gate}";
        }

        private void BuildCollectionPage()
        {
            var page = Ui.Panel(stageRoot, "CollectionPage", 0, 0, Stage.PanelX, Stage.Height, PanelColor);
            pages[PageCollection] = page.gameObject;

            collectionTitle = Ui.Label(page.transform, Font, "", 4, 2, Stage.PanelX - 8, 12, 8, TextAnchor.MiddleLeft);
            collectionTitle.fontStyle = FontStyle.Bold;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(page.transform, false);
            var viewport = (RectTransform)viewportGo.transform;
            Ui.TopLeft(viewport, 4, 16, 120, 160);
            viewportGo.AddComponent<RectMask2D>();
            viewportGo.AddComponent<Image>().color = Color.clear;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = (RectTransform)contentGo.transform;
            Ui.TopLeft(content, 0, 0, 120, AchievementCatalog.All.Count * 10);

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 10;

            for (int i = 0; i < AchievementCatalog.All.Count; i++)
            {
                var label = Ui.Label(content, Font, AchievementCatalog.All[i].Name, 0, i * 10, 120, 10, 6, TextAnchor.MiddleLeft);
                achievementLabels.Add(label);
            }

            var rareTitle = Ui.Label(page.transform, Font, "稀有物品", 130, 2, 110, 12, 8, TextAnchor.MiddleLeft);
            rareTitle.fontStyle = FontStyle.Bold;
            for (int i = 0; i < RareItems.All.Count; i++)
            {
                var item = RareItems.All[i];
                float x = 130 + (i % 6) * 19;
                float y = 18 + (i / 6) * 19;
                var box = Ui.Panel(page.transform, "Rare", x, y, 18, 18, RailColor);
                var icon = Ui.Icon(box.transform, art.Get($"rare_{i}"), 1, 1, 16);
                rareIcons.Add(icon);
                box.gameObject.AddComponent<HoverRelay>().Entered = () =>
                    collectionInfo.text = sim.State.rareItems.Contains(item.Id)
                        ? $"{item.Name}（{Strata.All[item.Stratum].Name}）"
                        : $"未发现（{Strata.All[item.Stratum].Name}）";
            }
            collectionInfo = Ui.Label(page.transform, Font, "", 130, 100, 112, 30, 7, TextAnchor.UpperLeft);
            collectionInfo.color = SoftText;
            collectionInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // Rebirth wipes the run, so it takes a second click within a few seconds.
        private void OnRebirthClicked()
        {
            if (rebirthArmed > 0)
            {
                rebirthArmed = 0;
                sim.Rebirth();
                return;
            }
            rebirthArmed = RebirthConfirmSeconds;
        }

        private void Update()
        {
            int scale = Stage.Scale;
            if (!Mathf.Approximately(scaler.scaleFactor, scale)) scaler.scaleFactor = scale;

            var s = sim.State;
            coinsLabel.text = NumberFormat.Short(s.coins - coinsInFlight);
            incomeLabel.text = $"{NumberFormat.Short(s.incomePerSecond)} 每秒金币";
            oresLabel.text = NumberFormat.Short(s.ores - oresInFlight);
            drillsLabel.text = $"钻头 {NumberFormat.Short(s.DrillsOwned)}";
            var stratum = Strata.All[Strata.OfDepth(s.depth)];
            depthLabel.text = $"{stratum.Name} {s.depth} 米";

            float dt = Time.unscaledDeltaTime;
            if (dt > 0) fpsAverage = Mathf.Lerp(fpsAverage, 1f / dt, 0.05f);
            versionLabel.text = $"v{Application.version}  {fpsAverage:0} FPS";

            UpdateCombo();
            UpdateMeter();
            UpdateToast(dt);
            UpdateBoss();
            UpdateRebirth(dt);

            if (currentPage == PageHangar) foreach (var card in hangarCards) UpdateHangarCard(card);
            if (currentPage == PageTree) foreach (var node in treeNodes) UpdateTreeNode(node);
            if (currentPage == PageCollection) UpdateCollection();
            if (currentPage == PageStats) UpdateStats();
        }

        private void UpdateCombo()
        {
            if (sim.Combo < 2)
            {
                comboLabel.text = "";
                return;
            }
            double bonus = (sim.ComboMultiplier - 1) * 100;
            comboLabel.text = bonus > 0 ? $"{sim.Combo} 连击 +{bonus:0}%" : $"{sim.Combo} 连击";
            comboLabel.color = bonus > 0 ? CoinColor : Color.white;
        }

        private void UpdateMeter()
        {
            double fill = sim.InFrenzy ? sim.FrenzyTimeLeft / sim.FrenzyDuration : sim.FrenzyEnergy;
            meterFill.rectTransform.sizeDelta = new Vector2((float)(fill * (MeterWidth - 2)), MeterHeight - 2);

            frenzyButton.interactable = sim.FrenzyReady;
            frenzyButtonImage.color = sim.FrenzyReady ? FrenzyColor : LockedColor;
            frenzyButtonLabel.text = sim.InFrenzy ? $"{sim.FrenzyTimeLeft:0}秒" : "狂热";
        }

        private void UpdateToast(float dt)
        {
            if (toastTime <= 0) return;
            toastTime -= dt;
            var c = toastLabel.color;
            c.a = Mathf.Clamp01(toastTime / ToastFadeSeconds);
            toastLabel.color = c;
            if (toastTime <= 0) toastLabel.text = "";
        }

        private void UpdateBoss()
        {
            bool show = sim.BossActive;
            bossBar.gameObject.SetActive(show);
            if (!show)
            {
                bossLabel.text = "";
                return;
            }
            string armor = sim.BossArmor > 0 ? " 护甲" : "";
            bossLabel.text = $"{sim.BossName}{armor} {NumberFormat.Short(sim.BossHp)}  " +
                $"{Mathf.CeilToInt((float)sim.BossTimeLeft)}秒";
            double fill = Math.Max(0, sim.BossHp / sim.BossMaxHp);
            bossFill.rectTransform.sizeDelta = new Vector2((float)(fill * (BossBarWidth - 2)), 5);
            double armorFill = sim.BossMaxArmor > 0 ? sim.BossArmor / sim.BossMaxArmor : 0;
            bossArmorFill.rectTransform.sizeDelta = new Vector2((float)(armorFill * (BossBarWidth - 2)), 2);
        }

        private void UpdateRebirth(float dt)
        {
            bool can = sim.CanRebirth;
            rebirthButton.gameObject.SetActive(can);
            if (rebirthArmed > 0) rebirthArmed -= dt;
            if (!can) return;
            rebirthLabel.text = rebirthArmed > 0 ? "再点一次确认" : $"地核重铸 +{sim.ShardsOnRebirth}";
        }

        private void UpdateHangarCard(HangarCard card)
        {
            int tier = card.Tier;
            bool unlocked = sim.DrillUnlocked(tier);
            bool canBuy = sim.CanBuyDrill(tier);
            bool canMerge = sim.CanMerge(tier);

            card.Background.color = canBuy || canMerge ? CardBright : CardDim;
            card.Icon.color = unlocked ? Color.white : NodeHidden;
            card.Name.color = unlocked ? Color.white : DimText;
            card.Count.text = "x" + NumberFormat.Short(sim.State.DrillCount(tier));
            card.Price.text = unlocked
                ? "价格 " + NumberFormat.Short(sim.DrillCost(tier))
                : $"{DrillTiers.UnlockDepth(tier)} 米解锁";
            card.Buy.interactable = canBuy;
            card.BuyImage.color = canBuy ? BuyColor : LockedColor;
            card.Merge.interactable = canMerge;
            card.MergeImage.color = canMerge ? MergeColor : LockedColor;
        }

        private void UpdateTreeNode(TreeNode node)
        {
            var skill = node.Skill;
            int level = sim.State.LevelOf(skill.Id);
            bool maxed = level >= skill.MaxLevel;
            bool can = sim.CanBuy(skill);
            bool open = sim.IsUnlocked(skill);

            node.Box.color = maxed ? LineOn : (can ? BuyColor : (open ? RailSelected : RailColor));
            node.Icon.color = open ? Color.white : NodeHidden;
            node.Level.text = open ? $"{level}/{skill.MaxLevel}" : "";
            node.LineUp.color = level > 0 ? LineOn : LineOff;
        }

        private void UpdateCollection()
        {
            var s = sim.State;
            collectionTitle.text = $"成就 {s.achievements.Count}/{AchievementCatalog.All.Count}";
            for (int i = 0; i < achievementLabels.Count; i++)
            {
                var a = AchievementCatalog.All[i];
                bool done = s.achievements.Contains(a.Id);
                achievementLabels[i].color = done ? CoinColor : DimText;
                achievementLabels[i].text = (done ? "★ " : "  ") + a.Name;
            }
            for (int i = 0; i < rareIcons.Count; i++)
                rareIcons[i].color = s.rareItems.Contains(RareItems.All[i].Id) ? Color.white : Color.black;
        }

        private void UpdateStats()
        {
            var s = sim.State;
            statsLabel.text =
                $"最大深度\n{s.maxDepthEver} 米\n总收入\n{NumberFormat.Short(s.totalIncome)}\n" +
                $"每秒伤害\n{NumberFormat.Short(sim.TotalDps)}\n钻头数量\n{NumberFormat.Short(s.DrillsOwned)}\n" +
                $"星核碎片\n{s.shards}\n重铸次数\n{s.rebirths}";
        }
    }
}
