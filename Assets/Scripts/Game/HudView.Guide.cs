using System;
using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>
    /// One-time explainer cards for each new thing, and a bouncing arrow for the first few steps.
    /// A card pauses the game until it is closed, so a guardian's clock never runs behind it.
    /// </summary>
    public sealed partial class HudView
    {
        private const int CardWidth = 196;
        private const int CardHeight = 96;
        private const int CardPicture = 2; // the 48x32 art at twice its size
        private const int ArrowSize = 16;
        private const int PlanetCardPicture = 64;
        private const string PlanetCard = "planet";
        private const float ArrowBounce = 2f;

        private sealed class GuideCard
        {
            public string Id;
            public string Title;
            public string Text;
            public Func<bool> Due;
        }

        private readonly List<GuideCard> guideCards = new List<GuideCard>();
        private GameObject cardRoot;
        private Image cardPicture;
        private Text cardTitle, cardText;
        private Image guideArrow;
        private string openCard;

        /// <summary>The demo driver plays through without stopping for cards.</summary>
        public bool GuidesOff { get; set; }

        public bool CardOpen => openCard != null;

        private void BuildGuide()
        {
            var dim = Ui.Panel(stageRoot, "CardDim", 0, 0, Stage.Width, Stage.Height, new Color(0, 0, 0, 0.6f));
            cardRoot = dim.gameObject;
            float x = (Stage.Width - CardWidth) / 2f, y = (Stage.Height - CardHeight) / 2f;
            Ui.Panel(dim.transform, "Frame", x, y, CardWidth, CardHeight, CoinColor);
            var inner = Ui.Panel(dim.transform, "Inner", x + 1, y + 1, CardWidth - 2, CardHeight - 2, PanelColor);

            cardPicture = Ui.Panel(inner.transform, "Picture", 3, 14, 48 * CardPicture, 32 * CardPicture, Color.white);
            cardTitle = Ui.Label(inner.transform, Font, "", 3, 1, CardWidth - 8, 12, 8, TextAnchor.MiddleLeft);
            cardTitle.color = CoinColor;
            cardText = Ui.Label(inner.transform, Font, "", 102, 15, CardWidth - 108, 60, 6, TextAnchor.UpperLeft);
            cardText.horizontalOverflow = HorizontalWrapMode.Wrap;
            cardText.color = Color.white;
            var ok = Ui.Button(inner.transform, Font, "知道了", CardWidth - 50, CardHeight - 18, 44, 14, 7, CardBright, out _);
            ok.onClick.AddListener(CloseCard);
            cardRoot.SetActive(false);

            guideArrow = Ui.Icon(stageRoot, art.Get("guide_arrow"), 0, 0, ArrowSize);
            guideArrow.raycastTarget = false;
            guideArrow.gameObject.SetActive(false);

            Card("tap", "点击挖掘", "点击钻头开始挖矿。攒够 5 金币，打开技能树解锁自动挖掘。之后继续点击可以让钻头临时加速。", () => true);
            Card("skilltree", "打开技能树", "攒够 5 金币了。点右边的树形按钮打开技能树，第一个技能就是自动挖掘。", () => !sim.AutoDrillUnlocked && sim.State.coins >= sim.SkillCost(GameData.Skill(GameSim.RootSkill)));
            Card("autodrill", "自动挖掘", "钻头现在会自己挖了。继续点击可以让它临时加速，停手就回到设定速度。", () => sim.AutoDrillUnlocked && sim.State.BoughtOf(0) == 0);
            Card("firstdrill", "第一台钻头", "赚到 6 金币了。在机库买一台新钻头，钻头越多挖得越快。", () => sim.State.BoughtOf(0) == 0 && sim.CanBuyDrill(0));
            Card("merge", "合成钻头", "把机库里两台相同的钻头拖到一起，就能合成一台更高级的。点“合成”或按 C 键一次合成全部。", HasTwins);
            Card("boss", "守层 Boss", "每层底部都有 Boss，要在时限内打穿。它闪红时要放招，被缠住就连点钻头。打不过也没关系，升级后再来。", () => sim.BossActive);
            Card("rebirth", "地核重铸", "重铸会失去金币、钻头和技能，换来星核碎片。碎片永久提高伤害，下一局会快很多。觉得慢了就重铸。", () => sim.CanRebirth);
            Card("event", "黄金事件", "地面偶尔出现发光的矿石，点一下有好处。别错过，它们很快会消失。", () => sim.ActiveEvent == SurfaceEvent.Golden);
            Card("unstable", "地核不稳", "红色的发光物是冒险：可能大赚，也可能短暂减益，点不点你决定。", () => sim.ActiveEvent == SurfaceEvent.Unstable);
            Card("school", "钻探流派", "每局选一个流派，改变玩法重点。通关一次还有永久奖励。在基地页选择。", () => sim.SchoolsUnlocked);
            Card("factory", "地表工厂", "矿物可以在工厂里换成永久加成，重铸也不会清零。在基地页升级。", () => sim.State.rebirths >= 1);
            Card("crystal", "地心结晶", "结晶按真实时间慢慢生长，离线也在长，用来升级工厂或加速研究。", () => sim.Crystals >= 1);
            Card("lab", "研究所", "用结晶开始研究，时间到了就得到永久加成。同时只能研究一项。", () => sim.Crystals >= 1);
            Card("expedition", "探险队", "救出的探险员可以派去探险，路线越远风险越大，奖励也越好。离线也在跑。", () => sim.State.explorers > 0);
            Card("automation", "自动化规则", "现在可以设定自动买技能、买钻头，甚至自动重铸。在基地页调整。", () => sim.AutomationUnlocked);
            Card("challenge", "挑战深井", "挑战有特殊限制，达到目标深度就有永久奖励。在基地页进入。", () => sim.ReachedLayer >= Balance.ChallengeLayer);
            Card("constellation", "星系跃迁", "挖到地心后可以跃迁到新星球，换来星点和星座图，每颗星球规则都不同。", () => sim.CanWarp);
        }

        private void Card(string id, string title, string text, Func<bool> due) =>
            guideCards.Add(new GuideCard { Id = id, Title = title, Text = text, Due = due });

        private bool HasTwins()
        {
            var seen = new HashSet<int>();
            foreach (int code in sim.State.drills)
                if (!seen.Add(code)) return true;
            return false;
        }

        /// <summary>Opens a card whatever its condition; for the demo driver's screenshot.</summary>
        public void ShowCard(string id)
        {
            foreach (var card in guideCards)
                if (card.Id == id) OpenCard(card);
        }

        private void OpenCard(GuideCard card)
        {
            Ui.TopLeft(cardPicture.rectTransform, 3, 14, 48 * CardPicture, 32 * CardPicture);
            ShowCardPage(card.Id, art.Get("guide_" + card.Id), card.Title, card.Text);
        }

        /// <summary>Welcomes the player to the planet a warp has just landed on.</summary>
        public void ShowPlanetCard(int starPoints)
        {
            var planet = sim.Planet;
            Ui.TopLeft(cardPicture.rectTransform, 19, 16, PlanetCardPicture, PlanetCardPicture);
            ShowCardPage(PlanetCard, art.Get("planet_" + sim.PlanetIndex), "抵达" + planet.Name,
                $"{planet.Desc}\n\n获得 {starPoints} 星点。\n到基地的星图升级星座。");
        }

        private void ShowCardPage(string id, Sprite picture, string title, string text)
        {
            openCard = id;
            cardPicture.sprite = picture;
            cardTitle.text = title;
            cardText.text = text;
            cardRoot.transform.SetAsLastSibling();
            cardRoot.SetActive(true);
            Time.timeScale = 0;
        }

        public void CloseCard()
        {
            if (openCard == null) return;
            if (openCard != PlanetCard && !sim.State.seenGuides.Contains(openCard)) sim.State.seenGuides.Add(openCard);
            openCard = null;
            cardRoot.SetActive(false);
            Time.timeScale = 1;
        }

        // Called a few times a second. One card at a time, and never over a full-screen page.
        private void UpdateGuideCards()
        {
            if (GuidesOff || openCard != null || currentPage == PageTree || currentPage == PageGallery || currentPage == PageBase) return;
            foreach (var card in guideCards)
            {
                if (sim.State.seenGuides.Contains(card.Id) || !card.Due()) continue;
                OpenCard(card);
                return;
            }
        }

        // Points at the next thing to press during the opening.
        private void UpdateGuideArrow()
        {
            bool show = false;
            Vector2 at = default;
            if (!sim.AutoDrillUnlocked && sim.State.coins >= sim.SkillCost(GameData.Skill(GameSim.RootSkill)))
            {
                show = true;
                at = currentPage == PageTree ? RootNodeArrow() : new Vector2(Stage.DigWidth + 2, RailArrowY);
            }
            else if (sim.AutoDrillUnlocked && sim.State.drills.Count == 1 && sim.CanBuyDrill(0) && currentPage == PageHangar)
            {
                show = true;
                at = new Vector2(Stage.PanelX + 30, FirstCardArrowY);
            }
            show &= openCard == null;
            guideArrow.gameObject.SetActive(show);
            if (!show) return;
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * ArrowBounce;
            guideArrow.rectTransform.anchoredPosition = new Vector2(at.x, -(at.y + bounce));
            guideArrow.transform.SetAsLastSibling();
        }

        // Just above the skill tree's first node, wherever the page has been panned to.
        private Vector2 RootNodeArrow() => new Vector2(
            Stage.Width / 2f + treeContent.anchoredPosition.x - ArrowSize / 2f,
            Stage.Height / 2f - treeContent.anchoredPosition.y - RootSize * treeZoom / 2f - ArrowSize);

        private const int RailArrowY = 7;
        private const int FirstCardArrowY = 2;
    }
}
