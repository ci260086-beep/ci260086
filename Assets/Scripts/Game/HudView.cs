using System;
using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>
    /// Builds the whole UI from code: floating resources over the dig area, the icon rail and the
    /// card panel on the right.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const int CardHeight = 25;
        private const int CardWidth = Stage.PanelWidth - 4;
        private const int SectionHeight = 10;
        private const int PanelTitleHeight = 15;
        private const int FooterHeight = 21;
        private const int RailButton = 18;
        private const int MeterWidth = 60;
        private const int MeterHeight = 7;
        private const int MeterY = 169;
        private const float ToastFadeSeconds = 0.6f;

        private static readonly Color PanelColor = new Color32(0x1a, 0x12, 0x10, 0xff);
        private static readonly Color RailColor = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color RailSelected = new Color32(0x6b, 0x46, 0x30, 0xff);
        private static readonly Color CardBright = new Color32(0x4a, 0x31, 0x22, 0xff);
        private static readonly Color CardDim = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color IconBoxColor = new Color32(0x1a, 0x12, 0x10, 0xff);
        private static readonly Color CoinColor = new Color32(0xff, 0xe2, 0x7a, 0xff);
        private static readonly Color FrenzyColor = new Color32(0xe8, 0x40, 0x2e, 0xff);
        private static readonly Color LockedColor = new Color32(0x3a, 0x3a, 0x44, 0xff);
        private static readonly Color WarmText = new Color32(0xe0, 0x8a, 0x4a, 0xff);
        private static readonly Color SoftText = new Color32(0xc4, 0x9a, 0x6c, 0xff);
        private static readonly Color DimText = new Color32(0x8c, 0x5f, 0x3f, 0xff);

        private static readonly (SkillTree tree, string title, string icon)[] SkillPages =
        {
            (SkillTree.Drill, "钻头升级", "tab_drill"),
            (SkillTree.Blast, "爆破升级", "tab_blast"),
            (SkillTree.Frenzy, "狂热升级", "tab_frenzy"),
        };

        private sealed class Card
        {
            public SkillDef Skill;
            public RectTransform Root;
            public Image Background;
            public Text Name;
            public Text Price;
            public Text Level;
            public Image PriceIcon;
            public bool WasUnlocked;
        }

        private sealed class SkillPage
        {
            public GameObject Root;
            public readonly List<Card> Cards = new List<Card>();
            public Text UnlockedHeading;
            public Text NewHeading;
        }

        /// <summary>Tells the panel which card the pointer is on.</summary>
        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler
        {
            public Action Entered;
            public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        }

        private readonly List<SkillPage> skillPages = new List<SkillPage>();
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<Image> railButtons = new List<Image>();
        private readonly List<string> pageTitles = new List<string>();

        private GameSim sim;
        private ArtLibrary art;
        private CanvasScaler scaler;
        private RectTransform stageRoot;
        private Text coinsLabel, incomeLabel, oresLabel, depthLabel, comboLabel, versionLabel;
        private Text toastLabel, panelTitle, footerLabel, statsLabel;
        private Image meterFill;
        private Button frenzyButton;
        private Image frenzyButtonImage;
        private Text frenzyButtonLabel;
        private float toastTime;
        private float fpsAverage = 60;
        private double coinsInFlight;
        private double oresInFlight;
        private bool cardsMoved;

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
            BuildPanel();
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

            ShowPage(0);
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

        /// <summary>Big outlined message at the bottom of the dig area; fades out by itself.</summary>
        public void ShowToast(string text, float seconds)
        {
            toastLabel.text = text;
            toastTime = seconds;
        }

        public void ShowPage(int index)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == index);
                railButtons[i].color = i == index ? RailSelected : RailColor;
            }
            panelTitle.text = pageTitles[index];
            footerLabel.text = "";
        }

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
            comboLabel = Floating("", 4, 43, 100, 11, 8, TextAnchor.MiddleLeft);

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

        private void BuildRail()
        {
            var rail = Ui.Panel(stageRoot, "Rail", Stage.DigWidth, 0, Stage.RailWidth, Stage.Height, PanelColor);
            string[] icons = { "tab_drill", "tab_blast", "tab_frenzy", "tab_stats", "tab_settings" };
            for (int i = 0; i < icons.Length; i++)
            {
                int index = i;
                var button = Ui.Button(rail.transform, Font, "", 1, 3 + i * (RailButton + 2), RailButton, RailButton, 7,
                    RailColor, out _);
                Ui.Icon(button.transform, art.Get(icons[i]), 1, 1, 16);
                button.onClick.AddListener(() => ShowPage(index));
                railButtons.Add(button.GetComponent<Image>());
            }
        }

        private void BuildPanel()
        {
            var panel = Ui.Panel(stageRoot, "Panel", Stage.PanelX, 0, Stage.PanelWidth, Stage.Height, PanelColor);
            panelTitle = Ui.Label(panel.transform, Font, "", 0, 2, Stage.PanelWidth, 12, 8, TextAnchor.MiddleCenter);
            panelTitle.fontStyle = FontStyle.Bold;
            footerLabel = Ui.Label(panel.transform, Font, "", 3, Stage.Height - FooterHeight, Stage.PanelWidth - 6,
                FooterHeight - 2, 6, TextAnchor.MiddleLeft);
            footerLabel.color = SoftText;

            foreach (var def in SkillPages)
            {
                var page = new SkillPage { Root = NewPage(panel.transform, def.title) };
                page.UnlockedHeading = Ui.Label(page.Root.transform, Font, "已解锁", 3, 0, 60, SectionHeight, 6, TextAnchor.MiddleLeft);
                page.UnlockedHeading.color = WarmText;
                page.NewHeading = Ui.Label(page.Root.transform, Font, "新技能", 3, 0, 60, SectionHeight, 6, TextAnchor.MiddleLeft);
                page.NewHeading.color = WarmText;
                foreach (var skill in SkillCatalog.All)
                {
                    if (skill.Tree == def.tree) page.Cards.Add(BuildCard(page.Root.transform, skill, def.icon));
                }
                skillPages.Add(page);
                Layout(page);
            }

            var stats = NewPage(panel.transform, "统计");
            statsLabel = Ui.Label(stats.transform, Font, "", 4, PanelTitleHeight, Stage.PanelWidth - 8, 140, 7, TextAnchor.UpperLeft);
            statsLabel.lineSpacing = 1.3f;

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

        private Card BuildCard(Transform parent, SkillDef skill, string iconName)
        {
            var bg = Ui.Panel(parent, skill.Id, 2, 0, CardWidth, CardHeight - 1, CardBright);
            var card = new Card { Skill = skill, Root = bg.rectTransform, Background = bg };

            var box = Ui.Panel(bg.transform, "IconBox", 2, 3, 18, 18, IconBoxColor);
            box.raycastTarget = false;
            Ui.Icon(box.transform, art.Get(iconName), 1, 1, 16);

            card.Name = Ui.Label(bg.transform, Font, skill.Name, 22, 1, CardWidth - 24, 11, 7, TextAnchor.MiddleLeft);
            card.Name.fontStyle = FontStyle.Bold;
            card.PriceIcon = Ui.Icon(bg.transform, art.Get("icon_coin"), 22, 14, 7);
            card.Price = Ui.Label(bg.transform, Font, "", 30, 13, 26, 9, 6, TextAnchor.MiddleLeft);
            card.Level = Ui.Label(bg.transform, Font, "", 40, 13, CardWidth - 42, 9, 6, TextAnchor.MiddleRight);
            card.Level.color = SoftText;

            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => sim.Buy(skill.Id));

            bg.gameObject.AddComponent<HoverRelay>().Entered = () => footerLabel.text = skill.Effect;
            card.WasUnlocked = sim.IsUnlocked(skill);
            return card;
        }

        // Bought or buyable skills first under one heading, still locked ones under the other.
        private void Layout(SkillPage page)
        {
            float y = PanelTitleHeight;
            bool anyUnlocked = page.Cards.Exists(c => c.WasUnlocked);
            bool anyLocked = page.Cards.Exists(c => !c.WasUnlocked);

            page.UnlockedHeading.gameObject.SetActive(anyUnlocked);
            if (anyUnlocked)
            {
                page.UnlockedHeading.rectTransform.anchoredPosition = new Vector2(3, -y);
                y += SectionHeight;
                foreach (var card in page.Cards)
                {
                    if (!card.WasUnlocked) continue;
                    card.Root.anchoredPosition = new Vector2(2, -y);
                    y += CardHeight;
                }
            }

            page.NewHeading.gameObject.SetActive(anyLocked);
            if (anyLocked)
            {
                page.NewHeading.rectTransform.anchoredPosition = new Vector2(3, -y);
                y += SectionHeight;
                foreach (var card in page.Cards)
                {
                    if (card.WasUnlocked) continue;
                    card.Root.anchoredPosition = new Vector2(2, -y);
                    y += CardHeight;
                }
            }
        }

        private void Update()
        {
            int scale = Stage.Scale;
            if (!Mathf.Approximately(scaler.scaleFactor, scale)) scaler.scaleFactor = scale;

            var s = sim.State;
            coinsLabel.text = NumberFormat.Short(s.coins - coinsInFlight);
            incomeLabel.text = $"{NumberFormat.Short(s.incomePerSecond)} 每秒金币";
            oresLabel.text = NumberFormat.Short(s.ores - oresInFlight);
            depthLabel.text = $"{s.depth} 米";

            float dt = Time.unscaledDeltaTime;
            if (dt > 0) fpsAverage = Mathf.Lerp(fpsAverage, 1f / dt, 0.05f);
            versionLabel.text = $"v{Application.version}  {fpsAverage:0} FPS";

            UpdateCombo();
            UpdateMeter();
            UpdateToast(dt);
            foreach (var page in skillPages) UpdatePage(page);
            if (statsLabel.isActiveAndEnabled) UpdateStats();
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

        private void UpdatePage(SkillPage page)
        {
            cardsMoved = false;
            foreach (var card in page.Cards) UpdateCard(card);
            if (cardsMoved) Layout(page);
        }

        private void UpdateCard(Card card)
        {
            var skill = card.Skill;
            int level = sim.State.LevelOf(skill.Id);
            bool unlocked = sim.IsUnlocked(skill);
            bool maxed = level >= skill.MaxLevel;
            bool canBuy = sim.CanBuy(skill);

            if (unlocked != card.WasUnlocked)
            {
                card.WasUnlocked = unlocked;
                cardsMoved = true;
                ShowToast($"解锁新技能：{skill.Name}", 2.5f);
            }

            card.Background.color = canBuy ? CardBright : CardDim;
            card.Name.color = canBuy ? Color.white : SoftText;
            card.Level.text = $"{level}/{skill.MaxLevel}";
            card.PriceIcon.enabled = unlocked && !maxed;
            card.Price.color = canBuy ? CoinColor : DimText;
            if (!unlocked) card.Price.text = "未解锁";
            else if (maxed) card.Price.text = "满级";
            else card.Price.text = NumberFormat.Short(skill.CostAt(level));
        }

        private void UpdateStats()
        {
            var s = sim.State;
            statsLabel.text =
                $"最大深度\n{s.maxDepth} 米\n总收入\n{NumberFormat.Short(s.totalIncome)}\n" +
                $"每击伤害\n{NumberFormat.Short(sim.DamagePerHit)}\n每秒击打\n{sim.HitsPerSecond:0.0}\n" +
                $"星核碎片\n{s.shards}";
        }
    }
}
