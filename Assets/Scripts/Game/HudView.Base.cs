using System;
using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>
    /// The base page: one full-screen page of tabs for the systems that outlive a run. Every tab is a
    /// list of rows, a live line of text with an optional button, so a new system only declares rows.
    /// </summary>
    public sealed partial class HudView
    {
        private const int BaseTabWidth = 38;
        private const int PlanetIcon = 22;
        private const int PlanetCellWidth = 39;
        private const int PlanetRuleIcon = 16;
        private const int PlanetNode = 12;
        private const int PlanetStripLeft = 5;
        private const int PlanetStripTop = 139;
        private const int BaseTabHeight = 18;
        private const int BaseTabIcon = 16;
        private const int BaseRowHeight = 13;
        private const int BaseTop = 21;
        private const int BaseButtonWidth = 54;

        private sealed class BaseRow
        {
            public Text Label, ButtonLabel;
            public Button Button;
            public Image ButtonImage;
            public Func<string> Text, ButtonText;
            public Func<bool> Enabled;
        }

        private readonly List<GameObject> baseTabs = new List<GameObject>();
        private readonly List<Image> planetMarks = new List<Image>();
        private readonly List<Image> planetNodes = new List<Image>();
        private readonly List<Image> baseTabButtons = new List<Image>();
        private readonly List<List<BaseRow>> baseRows = new List<List<BaseRow>>();
        private Transform basePage;
        private Text baseInfo;
        private int baseTab;
        private float warpArmed;

        private void BuildBasePage()
        {
            var page = Ui.Panel(stageRoot, "BasePage", 0, 0, Stage.Width, Stage.Height, PanelColor);
            pages[PageBase] = page.gameObject;
            basePage = page.transform;
            var close = Ui.Button(basePage, Font, "关闭", Stage.Width - 34, 2, 30, 12, 7, CardBright, out _);
            close.onClick.AddListener(() => ShowPage(PageHangar));
            baseInfo = Ui.Label(basePage, Font, "", 6, 166, Stage.Width - 12, 12, 7, TextAnchor.MiddleLeft);
            baseInfo.color = SoftText;

            BuildAutomationTab();
            BuildSchoolTab();
            BuildFactoryTab();
            BuildResearchTab();
            BuildExpeditionTab();
            BuildChallengeTab();
            BuildStarTab();
            ShowBaseTab(0);
        }

        private Transform NewBaseTab(string title, string icon = null)
        {
            int index = baseTabs.Count;
            var tab = new GameObject("Tab", typeof(RectTransform));
            tab.transform.SetParent(basePage, false);
            Ui.TopLeft((RectTransform)tab.transform, 0, 0, Stage.Width, Stage.Height);
            baseTabs.Add(tab);
            baseRows.Add(new List<BaseRow>());

            var button = Ui.Button(basePage, Font, title, 4 + index * (BaseTabWidth + 1), 2, BaseTabWidth, BaseTabHeight, 6, RailColor, out Text label);
            if (icon != null)
            {
                Ui.Icon(button.transform, art.Get(icon), 1, 1, BaseTabIcon);
                Ui.TopLeft(label.rectTransform, BaseTabIcon + 1, 0, BaseTabWidth - BaseTabIcon - 1, BaseTabHeight);
            }
            button.onClick.AddListener(() => ShowBaseTab(index));
            baseTabButtons.Add(button.GetComponent<Image>());
            return tab.transform;
        }

        /// <summary>Opens the base page on one of its tabs; for the demo driver's screenshots.</summary>
        public void ShowBasePage(int tab)
        {
            ShowPage(PageBase);
            ShowBaseTab(tab);
        }

        private void ShowBaseTab(int index)
        {
            baseTab = index;
            baseInfo.text = "";
            for (int i = 0; i < baseTabs.Count; i++)
            {
                baseTabs[i].SetActive(i == index);
                baseTabButtons[i].color = i == index ? RailSelected : RailColor;
            }
        }

        /// <summary>Adds a row to the newest tab, in one of two columns when <paramref name="half"/> is set.</summary>
        private void AddBaseRow(int row, Func<string> text, Func<string> buttonText = null, Action click = null,
            Func<bool> enabled = null, string info = null, int column = -1)
        {
            var tab = baseTabs[baseTabs.Count - 1].transform;
            float width = column < 0 ? Stage.Width - 8 : (Stage.Width - 8) / 2f - 1;
            float x = column <= 0 ? 4 : 4 + (Stage.Width - 8) / 2f + 1;
            float y = BaseTop + row * BaseRowHeight;
            var box = Ui.Panel(tab, "row", x, y, width, BaseRowHeight - 1, CardDim);
            var item = new BaseRow { Text = text, ButtonText = buttonText, Enabled = enabled };
            float textWidth = buttonText == null ? width - 4 : width - BaseButtonWidth - 5;
            item.Label = Ui.Label(box.transform, Font, "", 2, 0, textWidth, BaseRowHeight - 1, 6, TextAnchor.MiddleLeft);
            item.Label.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (info != null) box.gameObject.AddComponent<HoverRelay>().Entered = () => baseInfo.text = info;

            if (buttonText != null)
            {
                item.Button = Ui.Button(box.transform, Font, "", width - BaseButtonWidth - 1, 1, BaseButtonWidth, BaseRowHeight - 3, 6, CardBright, out item.ButtonLabel);
                item.ButtonImage = item.Button.GetComponent<Image>();
                item.Button.onClick.AddListener(() => click());
            }
            baseRows[baseRows.Count - 1].Add(item);
        }

        private void UpdateBase()
        {
            UpdatePlanetStrip();
            if (warpArmed > 0) warpArmed -= TextSeconds;
            foreach (var row in baseRows[baseTab])
            {
                row.Label.text = row.Text();
                if (row.Button == null) continue;
                bool on = row.Enabled == null || row.Enabled();
                row.ButtonLabel.text = row.ButtonText();
                row.Button.interactable = on;
                row.ButtonImage.color = on ? BuyColor : LockedColor;
            }
        }

        private static string OnOff(bool on) => on ? "开" : "关";

        private void BuildAutomationTab()
        {
            NewBaseTab("自动化", "sys_automation");
            for (int i = 0; i < GameData.RebirthMilestones.Count; i++)
            {
                var m = GameData.RebirthMilestones[i];
                AddBaseRow(i, () => $"{(sim.State.rebirths >= m.Count ? "已达成" : "未达成")}  重铸 {m.Count} 次：{m.Desc}");
            }

            int top = GameData.RebirthMilestones.Count;
            var s = sim.State;
            Func<bool> open = () => sim.AutomationUnlocked;
            AddBaseRow(top, () => sim.AutomationUnlocked ? "自动买技能" : "重铸 3 次后解锁自动化规则",
                () => OnOff(s.autoSkills), () => s.autoSkills = !s.autoSkills, open);
            AddBaseRow(top + 1, () => "自动买钻头", () => OnOff(s.autoDrills), () => s.autoDrills = !s.autoDrills, open);
            AddBaseRow(top + 2, () => "优先购买", () => s.autoDrillsFirst ? "先买钻头" : "先买技能", () => s.autoDrillsFirst = !s.autoDrillsFirst, open);
            AddBaseRow(top + 3, () => $"保留 {s.autoReservePct}% 金币不花", () => "调整",
                () => s.autoReservePct = (s.autoReservePct + 10) % 100, open);
            AddBaseRow(top + 4, () => s.autoRebirthRow > 0 ? $"到 {GameData.FormatDepth(s.autoRebirthRow)} 自动重铸" : "自动重铸：关闭",
                () => "下一层", CycleAutoRebirth, open);
        }

        private void BuildSchoolTab()
        {
            NewBaseTab("流派", "sys_school");
            AddBaseRow(0, () => !sim.SchoolsUnlocked ? "第一次重铸后解锁，每局选一派，重铸后可以重选"
                : sim.CurrentSchool != null ? $"本局流派：{sim.CurrentSchool.Name}" + (sim.SecondSchool != null ? $" + {sim.SecondSchool.Name}" : "，已通关的流派可以当第二流派")
                : "本局还没选流派");
            for (int i = 0; i < GameData.Schools.Count; i++)
            {
                int index = i;
                var school = GameData.Schools[i];
                AddBaseRow(1 + i * 2, () => $"{school.Name}：{school.Desc}", () => sim.State.school == index ? "本局" : "选择",
                    () => sim.ChooseSchool(index), () => sim.CanChooseSchool(index));
                AddBaseRow(2 + i * 2, () => $"  本派技能分支价格 -{school.BranchDiscount:0}%；{(sim.SchoolCleared(index) ? "已通关" : "通关奖励")}：{school.Reward.Desc}",
                    () => sim.State.secondSchool == index ? "搭配中" : "搭配", () => sim.ChooseSecond(index), () => sim.CanChooseSecond(index));
            }
        }

        private void BuildFactoryTab()
        {
            NewBaseTab("工厂", "sys_factory");
            AddBaseRow(0, () => $"矿物 {NumberFormat.Short(sim.State.ores)}   地心结晶 {sim.Crystals}/{GameData.CrystalCap:0}" +
                (sim.CrystalsFull ? "（已满）" : $"   下一颗 {sim.CrystalProgress * 100:0}%"));
            for (int i = 0; i < GameData.Factory.Count; i++)
            {
                int index = i;
                var def = GameData.Factory[i];
                AddBaseRow(1 + i, () => $"{def.Name} {sim.FactoryLevel(index)}/{def.MaxLevel}：{def.Desc}",
                    () => sim.FactoryLevel(index) >= def.MaxLevel ? "满级"
                        : $"{NumberFormat.Short(sim.FactoryOreCost(index))} 矿" + (sim.FactoryCrystalCost(index) > 0 ? " +结晶" : ""),
                    () => sim.UpgradeFactory(index), () => sim.CanUpgradeFactory(index));
            }
            AddBaseRow(2 + GameData.Factory.Count, () => $"结晶每 {GameData.CrystalSeconds / 3600:0} 小时长一颗，离线也在长，每 {GameData.FactoryCrystalEvery} 级工厂要用一颗");
        }

        private void BuildResearchTab()
        {
            NewBaseTab("研究", "sys_lab");
            AddBaseRow(0, () => $"地心结晶 {sim.Crystals}   " + (sim.ActiveResearch != null
                    ? $"研究中：{sim.ActiveResearch.Name}，还要 {FormatDuration(sim.State.researchLeft)}" : "没有进行中的研究"),
                () => "用结晶加速", () => sim.RushResearch(), () => sim.CanRushResearch);
            for (int i = 0; i < GameData.Research.Count; i++)
            {
                int index = i;
                var def = GameData.Research[i];
                AddBaseRow(1 + i, () => $"{def.Name}：{def.Desc}（{FormatDuration(def.Seconds)}，{def.Crystals} 结晶）",
                    () => sim.ResearchDone(index) ? "已完成" : "开始", () => sim.StartResearch(index), () => sim.CanResearch(index));
            }
        }

        private void BuildExpeditionTab()
        {
            NewBaseTab("探险", "sys_expedition");
            AddBaseRow(0, () => $"探险员 {sim.State.explorers}，空闲 {sim.ExplorersIdle}。远古遗迹的宝箱里可能救出探险员");
            for (int i = 0; i < GameData.Expeditions.Count; i++)
            {
                int index = i;
                var trip = GameData.Expeditions[i];
                AddBaseRow(1 + i, () => $"{trip.Name}：{FormatDuration(trip.Seconds)}，成功率 {trip.Success * 100:0}%，{trip.Shards} 碎片 {trip.Ores:0} 矿" + (trip.Crystals > 0 ? $" {trip.Crystals:0} 结晶" : ""),
                    () => "出发", () => sim.SendExpedition(index), () => sim.CanSendExpedition(index));
            }
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                AddBaseRow(6 + i, () => index < sim.State.expeditionTiers.Count
                    ? $"{GameData.Expeditions[sim.State.expeditionTiers[index]].Name}：还要 {FormatDuration(sim.State.expeditionLeft[index])}" : "");
            }
        }

        private void BuildChallengeTab()
        {
            NewBaseTab("挑战", "sys_challenge");
            AddBaseRow(0, () => sim.CurrentChallenge != null ? $"挑战中：{sim.CurrentChallenge.Name}，目标 {GameData.FormatDepth(sim.CurrentChallenge.TargetRow)}"
                    : sim.ChallengesUnlocked ? "进入挑战会放弃本局，不给碎片" : $"到达{GameData.Layers[Balance.ChallengeLayer].Name}后解锁",
                () => "放弃", () => sim.AbandonChallenge(), () => sim.CurrentChallenge != null);
            for (int i = 0; i < GameData.Challenges.Count; i++)
            {
                int index = i;
                var def = GameData.Challenges[i];
                AddBaseRow(1 + i / 2 * 2, () => $"{def.Name}：{def.Desc}",
                    () => sim.ChallengeDone(index) ? "已完成" : "进入", () => sim.StartChallenge(index), () => sim.CanStartChallenge(index), column: i % 2);
                AddBaseRow(2 + i / 2 * 2, () => $"  挖到 {GameData.FormatDepth(def.TargetRow)}：{def.Reward.Desc}", column: i % 2);
            }
        }

        private void BuildStarTab()
        {
            var tab = NewBaseTab("星图", "sys_constellation");
            var map = Ui.Panel(tab, "StarMap", 0, 0, Stage.Width, Stage.Height, Color.white);
            map.sprite = art.Get("star_map");
            map.raycastTarget = false;
            tab.SetAsFirstSibling(); // the picture stays behind the tab buttons and the close button
            BuildPlanetStrip(tab);
            AddBaseRow(0, () => $"当前行星：{sim.Planet.Name}。{sim.Planet.Desc}");
            AddBaseRow(1, () => sim.CanWarp ? $"跃迁得 {sim.StarPointsOnWarp} 星点，碎片和本星进度清零" : "挖到地心后可以跃迁",
                () => warpArmed > 0 ? "再点确认" : "星系跃迁", OnWarpClicked, () => sim.CanWarp);
            AddBaseRow(2, () => $"星点 {sim.State.starPoints}   已跃迁 {sim.State.warps} 次");
            for (int i = 0; i < GameData.Constellation.Count; i++)
            {
                int index = i;
                var star = GameData.Constellation[i];
                AddBaseRow(3 + i, () => $"{star.Name} {sim.StarLevel(index)}/{star.MaxLevel}：{star.Desc}",
                    () => sim.StarLevel(index) >= star.MaxLevel ? "满级" : $"{sim.StarCost(index)} 星点",
                    () => sim.BuyStar(index), () => sim.CanBuyStar(index));
            }
        }

        // Every planet in the order the warps visit them, with a light behind the current one.
        private void BuildPlanetStrip(Transform tab)
        {
            for (int i = 0; i < GameData.Planets.Count; i++)
            {
                float x = PlanetStripLeft + i * PlanetCellWidth;
                var mark = Ui.Panel(tab, "Mark", x, PlanetStripTop, PlanetCellWidth - 2, PlanetIcon + 13, new Color(1, 1, 1, 0));
                mark.raycastTarget = false;
                planetMarks.Add(mark);
                var icon = Ui.Icon(tab, art.Get("planet_" + i), x + 1, PlanetStripTop + 1, PlanetIcon);
                icon.raycastTarget = false;
                var name = Ui.Label(tab, Font, GameData.Planets[i].Name, x, PlanetStripTop + PlanetIcon + 2, PlanetCellWidth - 2, 11, 6, TextAnchor.MiddleCenter);
                name.raycastTarget = false;
                if (i > 0)
                {
                    var rule = Ui.Icon(tab, art.Get("planet_rule_" + i), x + PlanetIcon, PlanetStripTop + 4, PlanetRuleIcon);
                    rule.raycastTarget = false;
                }
                var node = Ui.Icon(tab, art.Get("star_node_locked"), x, PlanetStripTop, PlanetNode);
                node.raycastTarget = false;
                planetNodes.Add(node);
            }
        }

        private void UpdatePlanetStrip()
        {
            for (int i = 0; i < planetMarks.Count; i++)
            {
                planetMarks[i].color = i == sim.PlanetIndex ? new Color(CoinColor.r, CoinColor.g, CoinColor.b, 0.35f) : Color.clear;
                planetNodes[i].sprite = art.Get(i == sim.PlanetIndex ? "star_node_current" : (i <= sim.State.warps ? "star_node_reached" : "star_node_locked"));
            }
        }

        // A warp wipes a whole planet, so it takes a second click, like a rebirth.
        private void OnWarpClicked()
        {
            if (warpArmed > 0)
            {
                warpArmed = 0;
                sim.Warp();
                return;
            }
            warpArmed = RebirthConfirmSeconds;
        }

        // Steps through the layer bottoms, then off again.
        private void CycleAutoRebirth()
        {
            var s = sim.State;
            int next = 0;
            foreach (var layer in GameData.Layers)
            {
                if (layer.RowEnd <= s.autoRebirthRow) continue;
                next = layer.RowEnd;
                break;
            }
            s.autoRebirthRow = next;
        }
    }
}
