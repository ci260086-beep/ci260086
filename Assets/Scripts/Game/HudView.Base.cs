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
        private const int BaseRowHeight = 16;
        private const int BaseTop = 17;
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
        private readonly List<Image> baseTabButtons = new List<Image>();
        private readonly List<List<BaseRow>> baseRows = new List<List<BaseRow>>();
        private Transform basePage;
        private Text baseInfo;
        private int baseTab;

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
            ShowBaseTab(0);
        }

        private Transform NewBaseTab(string title)
        {
            int index = baseTabs.Count;
            var tab = new GameObject("Tab", typeof(RectTransform));
            tab.transform.SetParent(basePage, false);
            Ui.TopLeft((RectTransform)tab.transform, 0, 0, Stage.Width, Stage.Height);
            baseTabs.Add(tab);
            baseRows.Add(new List<BaseRow>());

            var button = Ui.Button(basePage, Font, title, 4 + index * (BaseTabWidth + 1), 2, BaseTabWidth, 12, 7, RailColor, out _);
            button.onClick.AddListener(() => ShowBaseTab(index));
            baseTabButtons.Add(button.GetComponent<Image>());
            return tab.transform;
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
            NewBaseTab("自动化");
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
            NewBaseTab("流派");
            AddBaseRow(0, () => !sim.SchoolsUnlocked ? "第一次重铸后解锁，每局选一派，重铸后可以重选"
                : sim.CurrentSchool != null ? $"本局流派：{sim.CurrentSchool.Name}，下次重铸后可以换" : "本局还没选流派");
            for (int i = 0; i < GameData.Schools.Count; i++)
            {
                int index = i;
                var school = GameData.Schools[i];
                AddBaseRow(1 + i * 2, () => $"{school.Name}：{school.Desc}", () => sim.State.school == index ? "本局" : "选择",
                    () => sim.ChooseSchool(index), () => sim.CanChooseSchool(index));
                AddBaseRow(2 + i * 2, () => $"  本派技能分支价格 -{school.BranchDiscount:0}%；{(sim.SchoolCleared(index) ? "已通关" : "通关奖励")}：{school.Reward.Desc}");
            }
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
