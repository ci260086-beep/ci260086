using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>Builds the whole UI from code: stage image, top bar and skill panel.</summary>
    public sealed class HudView : MonoBehaviour
    {
        private const int SkillRowHeight = 16;
        private const int SkillListTop = 14;

        private static readonly Color BarColor = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color PanelColor = new Color32(0x1a, 0x12, 0x10, 0xff);
        private static readonly Color RowColor = new Color32(0x4a, 0x31, 0x22, 0xff);
        private static readonly Color BuyColor = new Color32(0x4a, 0x7a, 0x2a, 0xff);
        private static readonly Color LockedColor = new Color32(0x3a, 0x3a, 0x44, 0xff);
        private static readonly Color CoinColor = new Color32(0xff, 0xe2, 0x7a, 0xff);

        private sealed class SkillRow
        {
            public SkillDef Skill;
            public Text Title;
            public Text Effect;
            public Button Buy;
            public Image BuyImage;
            public Text BuyLabel;
        }

        private readonly List<SkillRow> skillRows = new List<SkillRow>();

        private GameSim sim;
        private CanvasScaler scaler;
        private RectTransform stageRoot;
        private RawImage stageImage;
        private Text coinsLabel, oresLabel, depthLabel, incomeLabel;
        private Text noticeLabel;
        private float noticeTime;
        private double coinsInFlight;
        private double oresInFlight;

        public Font Font { get; private set; }
        public RectTransform FxLayer { get; private set; }
        public Vector2 CoinTarget => new Vector2(8, 9);
        public Vector2 OreTarget => new Vector2(100, 9);

        public void Init(GameSim simulation, ArtLibrary art, RenderTexture stageTexture)
        {
            sim = simulation;
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
            stageImage = imageGo.AddComponent<RawImage>();
            stageImage.texture = stageTexture;
            stageImage.raycastTarget = false;
            Ui.TopLeft(stageImage.rectTransform, 0, 0, Stage.Width, Stage.Height);

            BuildTopBar(art);
            BuildSkillPanel();

            var fxGo = new GameObject("Fx", typeof(RectTransform));
            fxGo.transform.SetParent(stageRoot, false);
            FxLayer = (RectTransform)fxGo.transform;
            Ui.TopLeft(FxLayer, 0, 0, Stage.Width, Stage.Height);

            noticeLabel = Ui.Label(stageRoot, Font, "", 0, 60, Stage.Width, 20, 9, TextAnchor.MiddleCenter);
            noticeLabel.color = CoinColor;
            noticeLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;

            var eventsGo = new GameObject("EventSystem");
            eventsGo.transform.SetParent(transform, false);
            eventsGo.AddComponent<EventSystem>();
            eventsGo.AddComponent<StandaloneInputModule>();
        }

        public void AddInFlight(double coins, double ores)
        {
            coinsInFlight += coins;
            oresInFlight += ores;
        }

        public void Land(double coins, double ores)
        {
            coinsInFlight = System.Math.Max(0, coinsInFlight - coins);
            oresInFlight = System.Math.Max(0, oresInFlight - ores);
        }

        public void ShowNotice(string text, float seconds)
        {
            noticeLabel.text = text;
            noticeTime = seconds;
        }

        private void BuildTopBar(ArtLibrary art)
        {
            var bar = Ui.Panel(stageRoot, "TopBar", 0, 0, Stage.Width, Stage.TopBarHeight, BarColor);
            Ui.Icon(bar.transform, art.Get("icon_coin"), 4, 4, 8);
            coinsLabel = Ui.Label(bar.transform, Font, "0", 15, 2, 75, 12, 9, TextAnchor.MiddleLeft);
            coinsLabel.color = CoinColor;
            Ui.Icon(bar.transform, art.Get("icon_copper"), 96, 4, 8);
            oresLabel = Ui.Label(bar.transform, Font, "0", 107, 2, 70, 12, 9, TextAnchor.MiddleLeft);
            depthLabel = Ui.Label(bar.transform, Font, "", 4, 17, 80, 12, 8, TextAnchor.MiddleLeft);
            incomeLabel = Ui.Label(bar.transform, Font, "", 96, 17, 80, 12, 8, TextAnchor.MiddleLeft);
        }

        private void BuildSkillPanel()
        {
            var panel = Ui.Panel(stageRoot, "SkillPanel", 0, Stage.BottomPanelTop, Stage.Width,
                Stage.Height - Stage.BottomPanelTop, PanelColor);
            var tab = Ui.Panel(panel.transform, "Tab", 2, 1, 48, 12, RowColor);
            Ui.Label(tab.transform, Font, "钻头", 0, 0, 48, 12, 8, TextAnchor.MiddleCenter);

            int y = SkillListTop;
            foreach (var skill in SkillCatalog.All)
            {
                skillRows.Add(BuildRow(panel.transform, skill, y));
                y += SkillRowHeight;
            }
        }

        private SkillRow BuildRow(Transform parent, SkillDef skill, int y)
        {
            var bg = Ui.Panel(parent, skill.Id, 2, y, Stage.Width - 4, SkillRowHeight - 1, RowColor);
            var row = new SkillRow { Skill = skill };
            row.Title = Ui.Label(bg.transform, Font, skill.Name, 3, 0, 70, 15, 7, TextAnchor.MiddleLeft);
            row.Effect = Ui.Label(bg.transform, Font, skill.Effect, 76, 0, 40, 15, 7, TextAnchor.MiddleLeft);
            row.Buy = Ui.Button(bg.transform, Font, "", 118, 1, 56, 13, 7, BuyColor, out row.BuyLabel);
            row.BuyImage = row.Buy.GetComponent<Image>();
            row.Buy.onClick.AddListener(() => sim.Buy(skill.Id));
            return row;
        }

        private void Update()
        {
            int scale = Stage.Scale;
            if (!Mathf.Approximately(scaler.scaleFactor, scale)) scaler.scaleFactor = scale;

            var s = sim.State;
            coinsLabel.text = NumberFormat.Short(s.coins - coinsInFlight);
            oresLabel.text = NumberFormat.Short(s.ores - oresInFlight);
            depthLabel.text = $"深度 {s.depth}m";
            incomeLabel.text = $"{NumberFormat.Short(s.incomePerSecond)}/秒";

            foreach (var row in skillRows) UpdateRow(row);

            if (noticeTime > 0)
            {
                noticeTime -= Time.unscaledDeltaTime;
                if (noticeTime <= 0) noticeLabel.text = "";
            }
        }

        private void UpdateRow(SkillRow row)
        {
            int level = sim.State.LevelOf(row.Skill.Id);
            row.Title.text = $"{row.Skill.Name} Lv{level}";

            bool unlocked = sim.IsUnlocked(row.Skill);
            bool maxed = level >= row.Skill.MaxLevel;
            bool canBuy = sim.CanBuy(row.Skill);
            row.Buy.interactable = canBuy;
            row.BuyImage.color = canBuy ? BuyColor : LockedColor;
            if (!unlocked) row.BuyLabel.text = "未解锁";
            else if (maxed) row.BuyLabel.text = "已满级";
            else row.BuyLabel.text = NumberFormat.Short(row.Skill.CostAt(level));
        }
    }
}
