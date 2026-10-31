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
    public sealed partial class HudView : MonoBehaviour
    {
        private const int FamilyCardHeight = 15;
        private const int HangarCellSize = 14;
        private const int HangarColumns = 5;
        private const int DrillIconWidth = 8;
        private const int DrillIconHeight = 12;
        private const float CellBackShare = 0.35f;
        private const int HangarVisibleCells = 180;
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
        private const int GalleryColumns = 22;
        private const int ComboFlashStep = 10;
        private const float TextSeconds = 0.1f;
        private const float MaxDarkness = 0.4f;
        private const int SpeedLines = 14;
        private const float BannerSeconds = 2.2f;
        private const float BannerBarHeight = 34f;

        private const int PageHangar = 0;
        private const int PageTree = 1;
        private const int PageGallery = 2;
        private const int PageStats = 3;
        private const int PageSettings = 4;
        private const int PageBase = 5;

        private static readonly Color PanelColor = new Color32(0x1a, 0x12, 0x10, 0xff);
        private static readonly Color RailColor = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color RailSelected = new Color32(0x6b, 0x46, 0x30, 0xff);
        private static readonly Color CardBright = new Color32(0x4a, 0x31, 0x22, 0xff);
        private static readonly Color CardDim = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color CoinColor = new Color32(0xff, 0xe2, 0x7a, 0xff);
        private static readonly Color FrenzyColor = new Color32(0xe8, 0x40, 0x2e, 0xff);
        private static readonly Color ArmorColor = new Color32(0xc0, 0xc0, 0xcc, 0xff);
        private static readonly Color LockedColor = new Color32(0x3a, 0x3a, 0x44, 0xff);
        private static readonly Color BuyColor = new Color32(0x4a, 0x7a, 0x2a, 0xff);
        private static readonly Color SoftText = new Color32(0xc4, 0x9a, 0x6c, 0xff);
        private static readonly Color DimText = new Color32(0x8c, 0x5f, 0x3f, 0xff);
        private static readonly Color SilhouetteColor = new Color(0, 0, 0, 1);
        private static readonly Color NotOwned = new Color(0.25f, 0.25f, 0.28f, 1f);
        private static readonly Color[] ComboColors =
        {
            Color.white, new Color32(0xff, 0xe2, 0x7a, 255), new Color32(0xff, 0x9a, 0x3c, 255), new Color32(0xff, 0x50, 0x70, 255),
        };

        private static readonly Dictionary<string, Color32> BranchColors = new Dictionary<string, Color32>
        {
            { "core", new Color32(0xff, 0xe2, 0x7a, 255) }, { "drill", new Color32(0xe0, 0x8a, 0x4a, 255) },
            { "wealth", new Color32(0xe8, 0xb0, 0x2a, 255) }, { "blast", new Color32(0xe8, 0x40, 0x2e, 255) },
            { "hangar", new Color32(0x50, 0xb8, 0xa4, 255) }, { "frenzy", new Color32(0xd0, 0x50, 0xd0, 255) },
            { "strata", new Color32(0x76, 0xa8, 0x3a, 255) },
        };

        private sealed class FamilyCard
        {
            public FamilyDef Family;
            public Image Background;
            public Text Price;
            public Text Name;
            public Image Icon;
        }

        /// <summary>Tells the page which element the pointer is on.</summary>
        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler
        {
            public Action Entered;
            public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        }

        /// <summary>One drill in the hangar grid: drag it onto a twin to merge them.</summary>
        private sealed class HangarCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
        {
            public HudView Hud;
            public Image Icon;
            public Image Back;
            public int Code = -1;

            public void OnBeginDrag(PointerEventData e) => Hud.BeginCellDrag(this, e);
            public void OnDrag(PointerEventData e) => Hud.MoveCellDrag(e);
            public void OnEndDrag(PointerEventData e) => Hud.EndCellDrag();
            public void OnDrop(PointerEventData e) => Hud.DropOnCell(this);
        }

        private readonly List<FamilyCard> familyCards = new List<FamilyCard>();
        private readonly List<HangarCell> hangarCells = new List<HangarCell>();
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<Image> railButtons = new List<Image>();
        private readonly List<string> pageTitles = new List<string>();
        private readonly List<Image> speedLines = new List<Image>();
        private readonly List<Image> achievementIcons = new List<Image>();
        private readonly List<Image> rareIcons = new List<Image>();
        private readonly List<Text> rareCounts = new List<Text>();
        private readonly List<int> sortedDrills = new List<int>();

        private GameSim sim;
        private ArtLibrary art;
        private CanvasScaler scaler;
        private RectTransform stageRoot;
        private GameObject sidePanel;
        private Text coinsLabel, incomeLabel, oresLabel, depthLabel, comboLabel, versionLabel, drillsLabel;
        private Text toastLabel, guideLabel, panelTitle, footerLabel, statsLabel, galleryInfo, galleryCount;
        private Image meterFill;
        private Image bossBar, bossFill, bossArmorFill;
        private Text bossLabel;
        private Button rebirthButton;
        private Text rebirthLabel;
        private bool rebirthHinted;
        private float rebirthArmed;
        private Button frenzyButton;
        private Image frenzyButtonImage;
        private Text frenzyButtonLabel;
        private Image dragGhost;
        private Image frenzyGlow, bannerTop, bannerBottom, darkness;
        private Text bannerLabel;
        private float bannerTime;
        private HangarCell dragSource;
        private float toastTime;
        private float fpsAverage = 60;
        private double coinsInFlight;
        private double oresInFlight;
        private float coinPulse;
        private float comboFlash;
        private float textClock;
        private int lastComboTier;
        private int comboStep;
        private int currentPage;
        private int lastDrillCount = -1;
        private int lastDrillHash;

        public Font Font { get; private set; }
        public RectTransform FxLayer { get; private set; }
        public Vector2 CoinTarget => new Vector2(9, 11);
        public Vector2 OreTarget => new Vector2(8, 36);

        public event Action SaveRequested;

        public static string KindName(int code) =>
            GameData.Materials[DrillCode.Tier(code)] + GameData.Families[DrillCode.Family(code)].Name;

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

            BuildDarkness();
            BuildFrenzyOverlay();
            BuildDigOverlay();
            BuildFrenzyMeter();
            BuildFrame();
            BuildSidePanel();
            BuildRail();
            BuildTreePage();
            BuildGalleryPage();
            BuildBasePage();

            var fxGo = new GameObject("Fx", typeof(RectTransform));
            fxGo.transform.SetParent(stageRoot, false);
            FxLayer = (RectTransform)fxGo.transform;
            Ui.TopLeft(FxLayer, 0, 0, Stage.DigWidth, Stage.Height);
            fxGo.AddComponent<RectMask2D>(); // effects belong to the dig area and never spill onto the frame

            dragGhost = Ui.Icon(stageRoot, art.Strip("drill_0_0", 8)[0], 0, 0, HangarCellSize);
            Ui.TopLeft(dragGhost.rectTransform, 0, 0, DrillIconWidth, DrillIconHeight);
            dragGhost.gameObject.SetActive(false);

            BuildBossBanner();
            BuildSurfaceEvent();
            BuildMergeCard();

            toastLabel = Floating("", 0, 140, Stage.DigWidth, 18, 13, TextAnchor.MiddleCenter);
            toastLabel.color = CoinColor;
            BuildOfflinePanel();
            guideLabel = Floating("", 0, 24, Stage.DigWidth, 18, 13, TextAnchor.MiddleCenter);
            guideLabel.fontStyle = FontStyle.Bold;

            var eventsGo = new GameObject("EventSystem");
            eventsGo.transform.SetParent(transform, false);
            eventsGo.AddComponent<EventSystem>();
            eventsGo.AddComponent<StandaloneInputModule>();

            ShowPage(PageHangar);
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
            coinPulse = 1f;
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
            bool fullScreen = index == PageTree || index == PageGallery || index == PageBase;
            sidePanel.SetActive(!fullScreen);
            FxLayer.gameObject.SetActive(!fullScreen); // digging carries on, but its numbers stay off these pages
            panelTitle.text = pageTitles[index];
            footerLabel.text = "";
        }

        public void ShowTreePage() => ShowPage(PageTree);

        public void ShowGalleryPage() => ShowPage(PageGallery);

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

            depthLabel = Floating("", Stage.DigWidth - 124, 2, 120, 14, 9, TextAnchor.MiddleRight);
            depthLabel.fontStyle = FontStyle.Bold;

            versionLabel = Floating("", 3, 171, 90, 8, 6, TextAnchor.MiddleLeft);
            versionLabel.color = SoftText;
        }

        // The deeper the layer the darker the view, with a soft shadow round the edge of the dig area.
        private void BuildDarkness()
        {
            darkness = Ui.Panel(stageRoot, "Darkness", 0, 0, Stage.DigWidth, Stage.Height, Color.clear);
            darkness.raycastTarget = false;
            var edge = Ui.Panel(stageRoot, "Vignette", 0, 0, Stage.DigWidth, Stage.Height, new Color(0.05f, 0.03f, 0.08f, 0.55f));
            edge.sprite = art.Get("fx_vignette");
            edge.raycastTarget = false;
        }

        private void UpdateDarkness()
        {
            int layer = GameData.LayerOfRow(sim.State.depth);
            Color tint = Palette.Dark(layer);
            tint.a = Mathf.Lerp(0f, MaxDarkness, layer / (GameData.Layers.Count - 1f));
            darkness.color = tint;
        }

        // Glowing edges and streaks of speed while frenzy runs.
        private void BuildFrenzyOverlay()
        {
            frenzyGlow = Ui.Panel(stageRoot, "FrenzyGlow", 0, 0, Stage.DigWidth, Stage.Height, Color.white);
            frenzyGlow.sprite = art.Get("fx_vignette");
            frenzyGlow.raycastTarget = false;
            for (int i = 0; i < SpeedLines; i++)
            {
                var line = Ui.Panel(stageRoot, "SpeedLine", 0, 0, 1, 18, new Color(1, 0.9f, 0.7f, 0.5f));
                line.raycastTarget = false;
                speedLines.Add(line);
            }
        }

        private void UpdateFrenzyOverlay()
        {
            bool on = sim.InFrenzy;
            frenzyGlow.gameObject.SetActive(on);
            if (on)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                frenzyGlow.color = new Color(1f, 0.35f, 0.2f, pulse);
            }
            for (int i = 0; i < speedLines.Count; i++)
            {
                var line = speedLines[i];
                line.gameObject.SetActive(on);
                if (!on) continue;
                float y = (Time.unscaledTime * (240f + i * 13f) + i * 37f) % (Stage.Height + 20f);
                float x = (i * 53.7f) % Stage.DigWidth;
                line.rectTransform.anchoredPosition = new Vector2(x, y - Stage.Height);
            }
        }

        // Black bars close in, the guardian is named, then they part again.
        private void BuildBossBanner()
        {
            bannerTop = Ui.Panel(stageRoot, "BannerTop", 0, 0, Stage.DigWidth, 0, Color.black);
            bannerBottom = Ui.Panel(stageRoot, "BannerBottom", 0, Stage.Height, Stage.DigWidth, 0, Color.black);
            bannerTop.raycastTarget = bannerBottom.raycastTarget = false;
            bannerLabel = Floating("", 0, 74, Stage.DigWidth, 26, 18, TextAnchor.MiddleCenter);
            bannerLabel.color = FrenzyColor;
            bannerLabel.fontStyle = FontStyle.Bold;
        }

        public void ShowBossBanner(string name)
        {
            bannerLabel.text = name;
            bannerTime = BannerSeconds;
        }

        private void UpdateBanner(float dt)
        {
            if (bannerTime <= 0) return;
            bannerTime -= dt;
            float t = Mathf.Clamp01(Mathf.Min(BannerSeconds - bannerTime, bannerTime) * 4f);
            float height = t * BannerBarHeight;
            bannerTop.rectTransform.sizeDelta = new Vector2(Stage.DigWidth, height);
            bannerBottom.rectTransform.sizeDelta = new Vector2(Stage.DigWidth, height);
            bannerBottom.rectTransform.anchoredPosition = new Vector2(0, -(Stage.Height - height));
            var c = bannerLabel.color;
            c.a = t;
            bannerLabel.color = c;
            if (bannerTime <= 0) bannerLabel.text = "";
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

        // One solid piece of UI for the rail and the panel; the dig area stops at its edge.
        private void BuildFrame()
        {
            var frame = Ui.Panel(stageRoot, "Frame", Stage.DigWidth, 0, Stage.Width - Stage.DigWidth, Stage.Height, Color.white);
            frame.sprite = art.Get("ui_frame");
        }

        // Hangar, tree, gallery on top; stats, settings and base at the bottom.
        private void BuildRail()
        {
            var rail = Ui.Panel(stageRoot, "Rail", Stage.DigWidth, 0, Stage.RailWidth, Stage.Height, Color.clear);
            rail.raycastTarget = false;
            string[] icons = { "tab_drill", "tab_tree", "tab_trophy", "tab_stats", "tab_settings", "tab_base" };
            string[] names = { "钻头机库", "技能树", "成就和收藏", "统计和重铸", "设置", "基地" };
            for (int i = 0; i < icons.Length; i++)
            {
                int index = i;
                int y = i < RailTopGroup
                    ? 3 + i * (RailButton + 2)
                    : Stage.Height - 3 - (icons.Length - i) * (RailButton + 2) + 2;
                var button = Ui.Button(rail.transform, Font, "", 1, y, RailButton, RailButton, 7, RailColor, out _);
                Ui.Icon(button.transform, art.Get(icons[i]), 1, 1, 16);
                button.onClick.AddListener(() => ShowPage(index));
                button.gameObject.AddComponent<HoverRelay>().Entered = () => footerLabel.text = names[index];
                railButtons.Add(button.GetComponent<Image>());
            }
        }

        private void BuildSidePanel()
        {
            var panel = Ui.Panel(stageRoot, "Panel", Stage.PanelX, 0, Stage.PanelWidth, Stage.Height, Color.clear);
            sidePanel = panel.gameObject;
            panelTitle = Ui.Label(panel.transform, Font, "", 0, 2, Stage.PanelWidth, 12, 8, TextAnchor.MiddleCenter);
            panelTitle.fontStyle = FontStyle.Bold;
            footerLabel = Ui.Label(panel.transform, Font, "", 3, Stage.Height - FooterHeight, Stage.PanelWidth - 6,
                FooterHeight - 2, 6, TextAnchor.MiddleLeft);
            footerLabel.color = SoftText;
            footerLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            var hangar = NewPage(panel.transform, "钻头机库");
            BuildHangar(hangar.transform);

            // the two full-screen pages keep their slots so rail indexes line up
            pages.Add(new GameObject("TreeSlot"));
            pageTitles.Add("技能树");
            pages.Add(new GameObject("GallerySlot"));
            pageTitles.Add("图鉴");

            var stats = NewPage(panel.transform, "统计");
            statsLabel = Ui.Label(stats.transform, Font, "", 4, PanelTitleHeight, Stage.PanelWidth - 8, 120, 7, TextAnchor.UpperLeft);
            statsLabel.lineSpacing = 1.2f;
            rebirthButton = Ui.Button(stats.transform, Font, "", 3, 144, Stage.PanelWidth - 6, 18, 7, FrenzyColor, out rebirthLabel);
            rebirthButton.onClick.AddListener(OnRebirthClicked);

            var settings = NewPage(panel.transform, "设置");
            pages.Add(new GameObject("BaseSlot"));
            pageTitles.Add("基地");
            var save = Ui.Button(settings.transform, Font, "立即保存", 4, PanelTitleHeight + 2, Stage.PanelWidth - 8, 16, 7,
                CardBright, out _);
            save.onClick.AddListener(() =>
            {
                SaveRequested?.Invoke();
                ShowToast("已保存", 1.5f);
            });

            var shake = Ui.Button(settings.transform, Font, "", 4, PanelTitleHeight + 20, Stage.PanelWidth - 8, 16, 7, CardBright, out Text shakeLabel);
            shakeLabel.text = "震屏：" + GameSettings.ShakeNames[GameSettings.ShakeLevel];
            shake.onClick.AddListener(() =>
            {
                GameSettings.ShakeLevel = (GameSettings.ShakeLevel + 1) % GameSettings.ShakeNames.Length;
                shakeLabel.text = "震屏：" + GameSettings.ShakeNames[GameSettings.ShakeLevel];
            });

            var particles = Ui.Button(settings.transform, Font, "", 4, PanelTitleHeight + 38, Stage.PanelWidth - 8, 16, 7, CardBright, out Text particleLabel);
            particleLabel.text = "粒子：" + GameSettings.ParticleNames[GameSettings.ParticleLevel];
            particles.onClick.AddListener(() =>
            {
                GameSettings.ParticleLevel = (GameSettings.ParticleLevel + 1) % GameSettings.ParticleNames.Length;
                particleLabel.text = "粒子：" + GameSettings.ParticleNames[GameSettings.ParticleLevel];
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

        // Family cards buy drills; the grid below shows what is owned, and drags merge.
        private void BuildHangar(Transform parent)
        {
            int y = PanelTitleHeight;
            foreach (var family in GameData.Families)
            {
                familyCards.Add(BuildFamilyCard(parent, family, y));
                y += FamilyCardHeight;
            }

            var viewportGo = new GameObject("Grid", typeof(RectTransform));
            viewportGo.transform.SetParent(parent, false);
            var viewport = (RectTransform)viewportGo.transform;
            Ui.TopLeft(viewport, 5, y + 1, HangarColumns * HangarCellSize, Stage.Height - y - FooterHeight - 2);
            viewportGo.AddComponent<RectMask2D>();
            viewportGo.AddComponent<Image>().color = Color.clear;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = (RectTransform)contentGo.transform;
            int rows = HangarVisibleCells / HangarColumns;
            Ui.TopLeft(content, 0, 0, HangarColumns * HangarCellSize, rows * HangarCellSize);

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = HangarCellSize;

            for (int i = 0; i < HangarVisibleCells; i++)
            {
                var bg = Ui.Panel(content, "cell", (i % HangarColumns) * HangarCellSize, (i / HangarColumns) * HangarCellSize,
                    HangarCellSize - 1, HangarCellSize - 1, RailColor);
                var icon = Ui.Icon(bg.transform, art.Strip("drill_0_0", 8)[0], 0, 0, HangarCellSize);
                Ui.TopLeft(icon.rectTransform, (HangarCellSize - 1 - DrillIconWidth) / 2, 0, DrillIconWidth, DrillIconHeight);
                var cell = bg.gameObject.AddComponent<HangarCell>();
                cell.Hud = this;
                cell.Icon = icon;
                cell.Back = bg;
                int index = i;
                bg.gameObject.AddComponent<HoverRelay>().Entered = () =>
                {
                    if (hangarCells[index].Code < 0) return;
                    int code = hangarCells[index].Code;
                    footerLabel.text = $"{KindName(code)}  每击 {NumberFormat.Short(sim.DamageOf(code))}";
                };
                bg.gameObject.SetActive(false);
                hangarCells.Add(cell);
            }
        }

        private FamilyCard BuildFamilyCard(Transform parent, FamilyDef family, int y)
        {
            var bg = Ui.Panel(parent, family.Id, 3, y, Stage.PanelWidth - 6, FamilyCardHeight - 1, CardBright);
            var card = new FamilyCard { Family = family, Background = bg };
            card.Icon = Ui.Icon(bg.transform, art.Strip($"drill_{family.Index}_0", 8)[0], 2, 1, 11);
            card.Name = Ui.Label(bg.transform, Font, family.Name, 15, 0, 56, 7, 6, TextAnchor.MiddleLeft);
            card.Price = Ui.Label(bg.transform, Font, "", 15, 7, 56, 7, 5, TextAnchor.MiddleLeft);

            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => sim.BuyDrill(family.Index));
            bg.gameObject.AddComponent<HoverRelay>().Entered = () =>
                footerLabel.text = sim.FamilyUnlocked(family.Index)
                    ? family.Trait
                    : $"到达{GameData.Layers[family.UnlockLayer].Name}解锁";
            return card;
        }

        private void BeginCellDrag(HangarCell cell, PointerEventData e)
        {
            if (cell.Code < 0) return;
            dragSource = cell;
            dragGhost.sprite = cell.Icon.sprite;
            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
            MoveCellDrag(e);
        }

        private void MoveCellDrag(PointerEventData e)
        {
            if (dragSource == null) return;
            Vector2 stage = Stage.ScreenToStage(e.position);
            dragGhost.rectTransform.anchoredPosition = new Vector2(stage.x - HangarCellSize / 2f, -stage.y + HangarCellSize / 2f);
        }

        private void EndCellDrag()
        {
            dragSource = null;
            dragGhost.gameObject.SetActive(false);
        }

        private void DropOnCell(HangarCell target)
        {
            if (dragSource == null || dragSource == target || target.Code != dragSource.Code) return;
            sim.Merge(target.Code);
        }

        // A collection grid: everything not yet earned shows as a silhouette.
        private void BuildGalleryPage()
        {
            var page = Ui.Panel(stageRoot, "GalleryPage", 0, 0, Stage.Width, Stage.Height, PanelColor);
            pages[PageGallery] = page.gameObject;

            galleryCount = Ui.Label(page.transform, Font, "", 6, 2, 122, 12, 7, TextAnchor.MiddleLeft);
            galleryCount.fontStyle = FontStyle.Bold;

            var close = Ui.Button(page.transform, Font, "关闭", Stage.Width - 34, 2, 30, 12, 7, CardBright, out _);
            close.onClick.AddListener(() => ShowPage(PageHangar));

            var collection = NewGalleryTab(page.transform, "收藏");
            for (int i = 0; i < GameData.Achievements.Count; i++)
            {
                var a = GameData.Achievements[i];
                var icon = GalleryCellAt(collection, i, 16, art.Get("badge"),
                    () => galleryInfo.text = $"{a.Name}  条件：{a.Cond}  奖励：{a.Reward}");
                achievementIcons.Add(icon);
            }

            int rareTop = 18 + (GameData.Achievements.Count + GalleryColumns - 1) / GalleryColumns * 14;
            Ui.Label(collection, Font, "稀有物品", 6, rareTop, 100, 10, 7, TextAnchor.MiddleLeft).color = SoftText;
            for (int i = 0; i < GameData.Rares.Count; i++)
            {
                var r = GameData.Rares[i];
                string file = "rare_" + r.Id.Substring("rare_".Length);
                var icon = GalleryCellAt(collection, i, rareTop + 10, art.Get(file), () => DescribeRare(r));
                rareIcons.Add(icon);
                var count = Ui.Label(icon.transform.parent, Font, "", 0, 9, 13, 5, 4, TextAnchor.MiddleRight);
                rareCounts.Add(count);
            }

            galleryInfo = Ui.Label(page.transform, Font, "", 6, 164, Stage.Width - 12, 14, 7, TextAnchor.MiddleLeft);
            galleryInfo.color = SoftText;

            BuildMilestoneTab(page.transform);
            BuildMasteryTab(page.transform);
            ShowGalleryTab(0);
        }

        private Image GalleryCellAt(Transform parent, int index, int top, Sprite sprite, Action hover)
        {
            const int size = 13;
            float x = 6 + (index % GalleryColumns) * (size + 1);
            float y = top + (index / GalleryColumns) * (size + 1);
            var box = Ui.Panel(parent, "cell", x, y, size, size, RailColor);
            var icon = Ui.Icon(box.transform, sprite, 0, 0, size);
            box.gameObject.AddComponent<HoverRelay>().Entered = hover;
            return icon;
        }

        private void DescribeRare(RareDef r)
        {
            int count = sim.State.RareCountOf(r.Id);
            galleryInfo.text = count > 0
                ? $"{r.Name}（{r.Rarity}）x{count}  {r.Bonus}  {GameData.Layers[r.Layer].Name}"
                : $"未发现（{r.Rarity}）  出自{GameData.Layers[r.Layer].Name}的宝箱";
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
            float dt = Time.unscaledDeltaTime;
            if (dt > 0) fpsAverage = Mathf.Lerp(fpsAverage, 1f / dt, 0.05f);

            // numbers refresh ten times a second: strings every frame are the main source of garbage
            textClock += dt;
            if (textClock >= TextSeconds)
            {
                textClock = 0;
                coinsLabel.text = NumberFormat.Short(s.coins - coinsInFlight);
                incomeLabel.text = $"{NumberFormat.Short(s.incomePerSecond)} 每秒金币";
                oresLabel.text = NumberFormat.Short(s.ores - oresInFlight);
                drillsLabel.text = $"钻头 {NumberFormat.Short(s.drills.Count)}";
                depthLabel.text = $"{GameData.Layers[GameData.LayerOfRow(s.depth)].Name} {GameData.FormatDepth(s.depth)}";
                versionLabel.text = $"v{Application.version}  {fpsAverage:0} FPS";
                UpdateCombo();
                if (currentPage == PageHangar) UpdateHangar();
                if (currentPage == PageTree) UpdateTree();
                if (currentPage == PageGallery) UpdateGallery();
                if (currentPage == PageStats) UpdateStats();
                if (currentPage == PageBase) UpdateBase();
            }

            UpdateCoinPulse();
            UpdateMeter();
            UpdateToast(dt);
            UpdateGuide();
            UpdateDarkness();
            UpdateFrenzyOverlay();
            UpdateBanner(dt);
            UpdateMergeCard(dt);
            if (currentPage == PageTree) AnimateTree(dt);
            UpdateBoss();
            UpdateRebirth(dt);
            UpdateOffline(dt);
            UpdateSurfaceEvent();
        }

        // The opening: tap the drill, then spend the first coins on auto drilling.
        private void UpdateGuide()
        {
            bool locked = !sim.AutoDrillUnlocked;
            guideLabel.gameObject.SetActive(locked && currentPage != PageTree);
            float pulse = Mathf.PingPong(Time.unscaledTime * 2f, 1f);
            if (locked)
            {
                bool earned = sim.State.coins >= 1;
                guideLabel.text = earned ? "打开技能树，解锁自动钻探" : "点击钻头开始挖掘！";
                guideLabel.color = Color.Lerp(CoinColor, Color.white, pulse);
            }

            bool flash = locked && sim.State.coins >= 1 && currentPage != PageTree;
            railButtons[PageTree].color = flash
                ? Color.Lerp(RailColor, CoinColor, pulse)
                : (currentPage == PageTree ? RailSelected : RailColor);
        }

        // The count grows with the chain and changes colour, with a flash, at each milestone.
        private void UpdateCombo()
        {
            int combo = sim.Combo;
            if (combo < 2)
            {
                comboLabel.text = "";
                lastComboTier = 0;
                return;
            }
            double bonus = (sim.ComboMultiplier - 1) * 100;
            comboLabel.text = bonus > 0 ? $"{combo} 连击 +{bonus:0}%" : $"{combo} 连击";

            int tier = combo >= 100 ? 3 : combo >= 50 ? 2 : combo >= 10 ? 1 : 0;
            if (tier != lastComboTier) comboFlash = 1f;
            if (combo / ComboFlashStep != comboStep) comboFlash = Mathf.Max(comboFlash, 0.6f);
            comboStep = combo / ComboFlashStep;
            lastComboTier = tier;

            comboFlash = Mathf.Max(0, comboFlash - Time.unscaledDeltaTime * 4f);
            comboLabel.fontSize = 8 + tier * 2;
            comboLabel.color = Color.Lerp(ComboColors[tier], Color.white, comboFlash);
            comboLabel.rectTransform.localScale = Vector3.one * (1 + comboFlash * 0.4f);
        }

        private void UpdateCoinPulse()
        {
            if (coinPulse <= 0) return;
            coinPulse = Mathf.Max(0, coinPulse - Time.unscaledDeltaTime * 6f);
            coinsLabel.rectTransform.localScale = Vector3.one * (1 + coinPulse * 0.25f);
            coinsLabel.color = Color.Lerp(CoinColor, Color.white, coinPulse);
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
            bossLabel.text = $"{sim.CurrentBoss.BossName}{armor} {NumberFormat.Short(sim.BossHp)}  " +
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
            bool advised = sim.RebirthAdvised;
            if (advised && !rebirthHinted) ShowToast("收益开始放缓，适合地核重铸", 3f);
            rebirthHinted = advised;
            string rate = advised ? "收益放缓，适合重铸" : $"每分钟 +{sim.ShardsPerMinute:0.0}";
            rebirthLabel.text = rebirthArmed > 0 ? "再点一次确认" : $"地核重铸 +{sim.ShardsOnRebirth}\n{rate}";
        }

        private void UpdateHangar()
        {
            foreach (var card in familyCards)
            {
                int f = card.Family.Index;
                bool unlocked = sim.FamilyUnlocked(f);
                bool can = sim.CanBuyDrill(f);
                card.Background.color = can ? CardBright : CardDim;
                card.Icon.color = unlocked ? Color.white : NotOwned;
                card.Name.color = can ? Color.white : SoftText;
                card.Price.text = unlocked ? NumberFormat.Short(sim.DrillPrice(f)) : "未解锁";
                card.Price.color = can ? CoinColor : DimText;
            }
            RefreshCells();
        }

        // Rebuilds the grid only when the owned list changes; strongest drills first.
        private void RefreshCells()
        {
            var drills = sim.State.drills;
            int hash = 17;
            foreach (int code in drills) hash = unchecked(hash * 31 + code);
            if (drills.Count == lastDrillCount && hash == lastDrillHash) return;
            lastDrillCount = drills.Count;
            lastDrillHash = hash;

            sortedDrills.Clear();
            sortedDrills.AddRange(drills);
            sortedDrills.Sort((a, b) => GameSim.KindDamage(b).CompareTo(GameSim.KindDamage(a)));
            for (int i = 0; i < hangarCells.Count; i++)
            {
                var cell = hangarCells[i];
                bool used = i < sortedDrills.Count;
                cell.gameObject.SetActive(used);
                cell.Code = used ? sortedDrills[i] : -1;
                if (used)
                {
                    cell.Icon.sprite = FrameOf(sortedDrills[i]);
                    cell.Back.color = Color.Lerp(RailColor, Palette.Tier(DrillCode.Tier(sortedDrills[i])), CellBackShare);
                }
            }
        }

        private readonly Dictionary<int, Sprite> kindIcons = new Dictionary<int, Sprite>();

        private Sprite FrameOf(int code)
        {
            if (kindIcons.TryGetValue(code, out var sprite)) return sprite;
            sprite = art.Strip($"drill_{DrillCode.Family(code)}_{DrillCode.Tier(code)}", 8)[0];
            kindIcons[code] = sprite;
            return sprite;
        }

        private void UpdateGallery()
        {
            var s = sim.State;
            int rares = 0;
            for (int i = 0; i < rareIcons.Count; i++)
            {
                int count = s.RareCountOf(GameData.Rares[i].Id);
                if (count > 0) rares++;
                rareIcons[i].color = count > 0 ? Color.white : SilhouetteColor;
                rareCounts[i].text = count > 1 ? "x" + count : "";
            }
            for (int i = 0; i < achievementIcons.Count; i++)
                achievementIcons[i].color = s.achievements.Contains(GameData.Achievements[i].Id) ? Color.white : SilhouetteColor;
            galleryCount.text = $"成就 {s.achievements.Count}/{GameData.Achievements.Count}   稀有物品 {rares}/{GameData.Rares.Count}";
            UpdateGalleryTabs();
        }

        private void UpdateStats()
        {
            var s = sim.State;
            statsLabel.text =
                $"最大深度\n{GameData.FormatDepth(s.maxDepthEver)}\n总收入\n{NumberFormat.Short(s.totalIncome)}\n" +
                $"每秒伤害\n{NumberFormat.Short(sim.TotalDps)}\n钻头数量\n{NumberFormat.Short(s.drills.Count)}\n" +
                $"星核碎片\n{s.shards}\n重铸次数\n{s.rebirths}";
        }
    }
}
