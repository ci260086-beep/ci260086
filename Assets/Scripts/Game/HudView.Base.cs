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
        private const int BaseButtonWidth = 40; // "再点确认" is the longest button text: four characters

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
        private readonly List<List<BaseCard>> baseCards = new List<List<BaseCard>>();

        // A card on a base tab: a coloured header with an icon, a name and a status chip, a few
        // lines of text and up to two buttons along the bottom. Text and state are read live.
        private sealed class BaseCard
        {
            public Text Status, Body, Note, Foot;
            public Image Chip, Header, Progress;
            public Func<float> ProgressValue;
            public float ProgressWidth;
            public Func<string> StatusText, BodyText, NoteText, FootText;
            public Func<Color> StatusColor, HeaderColor;
            public readonly List<BaseRow> Buttons = new List<BaseRow>();
        }

        private const int CardHeader = 13;
        private const int CardButtonHeight = 11;
        private static readonly Color CardBody = new Color32(0x24, 0x1a, 0x16, 0xff);
        private static readonly Color ChipGold = new Color32(0xe8, 0xb0, 0x2a, 0xff);
        private static readonly Color ChipTeal = new Color32(0x50, 0xb8, 0xa4, 0xff);
        private static readonly Color ChipGreen = new Color32(0x76, 0xa8, 0x3a, 0xff);
        private static readonly Color ChipDim = new Color32(0x5a, 0x48, 0x40, 0xff);
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
            baseCards.Add(new List<BaseCard>());

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
            var box = Ui.Framed(Ui.Panel(tab, "row", x, y, width, BaseRowHeight - 1, CardDim), Ui.FrameKind.Inset);
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

        /// <summary>Adds a card to the newest tab at a stage position.</summary>
        private BaseCard AddBaseCard(float x, float y, float w, float h, Sprite icon, string title, Func<Color> headerColor,
            Func<string> body, Func<string> note = null, Func<string> status = null, Func<Color> statusColor = null,
            Func<string> foot = null)
        {
            var tab = baseTabs[baseTabs.Count - 1].transform;
            var frame = Ui.Framed(Ui.Panel(tab, "card", x, y, w, h, CardBody), Ui.FrameKind.Panel);
            var card = new BaseCard { BodyText = body, NoteText = note, StatusText = status, StatusColor = statusColor, HeaderColor = headerColor };
            card.Header = Ui.Panel(frame.transform, "header", 2, 2, w - 4, CardHeader, headerColor());
            card.Header.raycastTarget = false;
            if (icon != null) Ui.Icon(card.Header.transform, icon, 2, 2, SkillIconSize);
            var name = Ui.Label(card.Header.transform, Font, title, 14, 0, w - 60, CardHeader, 8, TextAnchor.MiddleLeft);
            name.color = Color.white;
            if (status != null)
            {
                card.Chip = Ui.Framed(Ui.Panel(card.Header.transform, "chip", w - 4 - 42, 1, 40, CardHeader - 2, ChipDim), Ui.FrameKind.Inset);
                card.Status = Ui.Label(card.Chip.transform, Font, "", 0, 0, 40, CardHeader - 2, 6, TextAnchor.MiddleCenter);
            }
            // two lines of body, one line of note, then a footer line shared with the buttons
            float textTop = CardHeader + 3;
            const float line = 11; // the 8-pixel font needs 21 pixels for two lines
            float buttonsTop = h - CardButtonHeight - 3;
            card.Body = Ui.Label(frame.transform, Font, "", 4, textTop, w - 8, line * 2, 6, TextAnchor.UpperLeft);
            card.Body.color = new Color32(0xf3, 0xe9, 0xdc, 0xff);
            if (note != null)
            {
                card.Note = Ui.Label(frame.transform, Font, "", 4, textTop + line * 2, w - 8, line, 6, TextAnchor.UpperLeft);
                card.Note.color = SoftText;
            }
            card.FootText = foot;
            if (foot != null)
            {
                card.Foot = Ui.Label(frame.transform, Font, "", 4, buttonsTop, w - 8 - 2 * (BaseButtonWidth + 2), CardButtonHeight, 6, TextAnchor.MiddleLeft);
                card.Foot.color = DimText;
            }
            baseCards[baseCards.Count - 1].Add(card);
            return card;
        }

        private const float ItemTop = BaseTop + BaseRowHeight + 2;
        private const float ItemBottom = Stage.Height - 2;
        private const float ItemGap = 2;
        private const int ItemIcon = 18; // a 9-pixel skill icon at twice its size

        /// <summary>
        /// A list card on a base tab: an accent bar, an icon, a bright title line, one or two soft
        /// lines under it, an optional progress bar and a button on the right. Placed on a grid of
        /// `cols` by `rows` filling the space under the tab's header row.
        /// </summary>
        private BaseCard AddBaseItem(int index, int cols, int rows, Sprite icon, Color accent, Func<string> title,
            Func<string> sub, Func<string> third = null, Func<string> buttonText = null, Action click = null,
            Func<bool> enabled = null, Func<float> progress = null)
        {
            var tab = baseTabs[baseTabs.Count - 1].transform;
            float w = (Stage.Width - 8 - ItemGap * (cols - 1)) / cols;
            float h = (ItemBottom - ItemTop - ItemGap * (rows - 1)) / rows;
            float x = 4 + index % cols * (w + ItemGap), y = ItemTop + index / cols * (h + ItemGap);
            var frame = Ui.Framed(Ui.Panel(tab, "item", x, y, w, h, CardBody), Ui.FrameKind.Inset);
            var bar = Ui.Panel(frame.transform, "accent", 1, 1, 2, h - 2, accent);
            bar.raycastTarget = false;
            if (icon != null) Ui.Icon(frame.transform, icon, 5, (h - ItemIcon) / 2, ItemIcon);
            float textX = icon != null ? 7 + ItemIcon : 6;
            float textW = w - textX - (buttonText != null ? BaseButtonWidth + 5 : 3);
            const float line = 11;
            int lines = third != null ? 3 : 2;
            float textY = Mathf.Max(0, (h - lines * line) / 2 - 1);
            var card = new BaseCard { BodyText = title, NoteText = sub, FootText = third, HeaderColor = () => accent };
            card.Header = bar;
            card.Body = Ui.Label(frame.transform, Font, "", textX, textY, textW, line, 8, TextAnchor.MiddleLeft);
            card.Body.color = Color.white;
            card.Note = Ui.Label(frame.transform, Font, "", textX, textY + line, textW, line, 6, TextAnchor.MiddleLeft);
            card.Note.color = new Color32(0xf3, 0xe9, 0xdc, 0xff);
            if (third != null)
            {
                card.Foot = Ui.Label(frame.transform, Font, "", textX, textY + line * 2, textW, line, 6, TextAnchor.MiddleLeft);
                card.Foot.color = SoftText;
            }
            if (progress != null)
            {
                var track = Ui.Panel(frame.transform, "track", textX, h - 4, textW, 2, PanelColor);
                track.raycastTarget = false;
                card.Progress = Ui.Panel(track.transform, "fill", 0, 0, 0, 2, accent);
                card.Progress.raycastTarget = false;
                card.ProgressValue = progress;
                card.ProgressWidth = textW;
            }
            if (buttonText != null)
            {
                var row = new BaseRow { ButtonText = buttonText, Enabled = enabled };
                row.Button = Ui.Button(frame.transform, Font, "", w - BaseButtonWidth - 3, (h - CardButtonHeight - 2) / 2, BaseButtonWidth, CardButtonHeight + 2, 6, CardBright, out row.ButtonLabel);
                row.ButtonImage = row.Button.GetComponent<Image>();
                row.Button.onClick.AddListener(() => click());
                card.Buttons.Add(row);
            }
            baseCards[baseCards.Count - 1].Add(card);
            return card;
        }

        private Sprite EffectIcon(SkillEffect effect) => art.Get("sicon_" + effect);

        /// <summary>Puts a button along a card's bottom edge, right to left.</summary>
        private void AddCardButton(BaseCard card, RectTransform frame, Func<string> text, Action click, Func<bool> enabled)
        {
            int index = card.Buttons.Count;
            float w = frame.sizeDelta.x, h = frame.sizeDelta.y;
            var row = new BaseRow { ButtonText = text, Enabled = enabled };
            row.Button = Ui.Button(frame, Font, "", w - 4 - (index + 1) * (BaseButtonWidth + 2), h - CardButtonHeight - 3, BaseButtonWidth, CardButtonHeight, 6, CardBright, out row.ButtonLabel);
            row.ButtonImage = row.Button.GetComponent<Image>();
            row.Button.onClick.AddListener(() => click());
            card.Buttons.Add(row);
        }

        private void UpdateBase()
        {
            UpdatePlanetStrip();
            if (warpArmed > 0) warpArmed -= TextSeconds;
            foreach (var row in baseRows[baseTab])
            {
                row.Label.text = row.Text();
                if (row.Button == null) continue;
                UpdateBaseButton(row);
            }
            foreach (var card in baseCards[baseTab])
            {
                card.Body.text = card.BodyText();
                if (card.Note != null) card.Note.text = card.NoteText();
                if (card.Foot != null) card.Foot.text = card.FootText();
                card.Header.color = card.HeaderColor();
                if (card.Progress != null)
                    card.Progress.rectTransform.sizeDelta = new Vector2(card.ProgressWidth * Mathf.Clamp01(card.ProgressValue()), 2);
                if (card.Status != null)
                {
                    string status = card.StatusText();
                    card.Chip.gameObject.SetActive(!string.IsNullOrEmpty(status));
                    card.Status.text = status;
                    card.Chip.color = card.StatusColor != null ? card.StatusColor() : ChipDim;
                }
                foreach (var button in card.Buttons) UpdateBaseButton(button);
            }
        }

        private void UpdateBaseButton(BaseRow row)
        {
            bool on = row.Enabled == null || row.Enabled();
            row.ButtonLabel.text = row.ButtonText();
            row.Button.interactable = on;
            row.ButtonImage.color = on ? BuyColor : LockedColor;
            row.Button.gameObject.SetActive(!string.IsNullOrEmpty(row.ButtonLabel.text));
        }

        private static string OnOff(bool on) => on ? "开" : "关";

        private void BuildAutomationTab()
        {
            NewBaseTab("自动", "sys_automation"); // two characters fit beside the icon in the pixel font
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

        // Four school cards, two by two: what the school changes, what clearing it earns, and
        // the buttons to take it for this run or pair it as a second school once cleared.
        private void BuildSchoolTab()
        {
            NewBaseTab("流派", "sys_school");
            AddBaseRow(0, () => !sim.SchoolsUnlocked ? "第一次重铸后解锁：每局选一派，重铸后可以重选"
                : sim.CurrentSchool != null ? $"本局流派：{sim.CurrentSchool.Name}" + (sim.SecondSchool != null ? $" + {sim.SecondSchool.Name}" : "　·　通关过的流派可以搭配成第二流派")
                : "本局还没选流派");
            const float top = BaseTop + BaseRowHeight + 2, gap = 3;
            float w = (Stage.Width - 8 - gap) / 2f, h = (Stage.Height - 14 - top - gap) / 2f;
            for (int i = 0; i < GameData.Schools.Count; i++)
            {
                int index = i;
                var school = GameData.Schools[i];
                Color32 branch = BranchColors.TryGetValue(school.Branch, out var c) ? c : (Color32)CoinColor;
                Color header = Color.Lerp(branch, Color.black, 0.45f);
                float x = 4 + i % 2 * (w + gap), y = top + i / 2 * (h + gap);
                string icon = school.Bonuses.Count > 0 ? "sicon_" + school.Bonuses[0].Effect : "sys_school";
                var card = AddBaseCard(x, y, w, h, art.Get(icon), school.Name,
                    () => sim.State.school == index || sim.State.secondSchool == index ? (Color)Color.Lerp(branch, Color.black, 0.2f) : header,
                    () => school.Desc,
                    () => $"{(sim.SchoolCleared(index) ? "已通关" : "通关奖励")}：{school.Reward.Desc}",
                    () => sim.State.school == index ? "本局" : sim.State.secondSchool == index ? "搭配中" : sim.SchoolCleared(index) ? "已通关" : "",
                    () => sim.State.school == index ? ChipGold : sim.State.secondSchool == index ? ChipTeal : ChipGreen,
                    () => $"分支技能 -{school.BranchDiscount:0}%");
                var frame = (RectTransform)card.Header.transform.parent;
                AddCardButton(card, frame, () => sim.State.school == index ? "" : "选择", () => sim.ChooseSchool(index), () => sim.CanChooseSchool(index));
                AddCardButton(card, frame, () => sim.SchoolCleared(index) && sim.State.school != index && sim.State.secondSchool != index ? "搭配" : "",
                    () => sim.ChooseSecond(index), () => sim.CanChooseSecond(index));
            }
        }

        private void BuildFactoryTab()
        {
            NewBaseTab("工厂", "sys_factory");
            AddBaseRow(0, () => $"矿物 {NumberFormat.Short(sim.State.ores)}　地心结晶 {sim.Crystals}/{GameData.CrystalCap:0}" +
                (sim.CrystalsFull ? "（已满）" : $"　下一颗 {sim.CrystalProgress * 100:0}%") + $"　·　每 {GameData.CrystalSeconds / 3600:0} 小时长一颗，每 {GameData.FactoryCrystalEvery} 级工厂用一颗",
                info: "结晶离线也在长");
            var accent = new Color32(0xe0, 0x8a, 0x4a, 0xff);
            for (int i = 0; i < GameData.Factory.Count; i++)
            {
                int index = i;
                var def = GameData.Factory[i];
                AddBaseItem(i, 1, GameData.Factory.Count, EffectIcon(def.Effect), accent,
                    () => $"{def.Name}　Lv {sim.FactoryLevel(index)}/{def.MaxLevel}",
                    () => def.Desc,
                    buttonText: () => sim.FactoryLevel(index) >= def.MaxLevel ? "满级"
                        : $"{NumberFormat.Short(sim.FactoryOreCost(index))} 矿" + (sim.FactoryCrystalCost(index) > 0 ? "+晶" : ""),
                    click: () => sim.UpgradeFactory(index), enabled: () => sim.CanUpgradeFactory(index),
                    progress: () => sim.FactoryLevel(index) / (float)def.MaxLevel);
            }
        }

        private void BuildResearchTab()
        {
            NewBaseTab("研究", "sys_lab");
            AddBaseRow(0, () => $"地心结晶 {sim.Crystals}　" + (sim.ActiveResearch != null
                    ? $"研究中：{sim.ActiveResearch.Name}，还要 {FormatDuration(sim.State.researchLeft)}" : "没有进行中的研究"),
                () => "用结晶加速", () => sim.RushResearch(), () => sim.CanRushResearch);
            var accent = new Color32(0x50, 0xb8, 0xa4, 0xff);
            int count = GameData.Research.Count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var def = GameData.Research[i];
                AddBaseItem(i, 2, (count + 1) / 2, EffectIcon(def.Effect), accent,
                    () => def.Name,
                    () => def.Desc,
                    () => $"{FormatDuration(def.Seconds)}　{def.Crystals} 结晶",
                    () => sim.ResearchDone(index) ? "已完成" : "开始", () => sim.StartResearch(index), () => sim.CanResearch(index));
            }
        }

        private void BuildExpeditionTab()
        {
            NewBaseTab("探险", "sys_expedition");
            AddBaseRow(0, () => $"探险员 {sim.State.explorers}，空闲 {sim.ExplorersIdle}　·　远古遗迹的宝箱里可能救出探险员");
            var accent = new Color32(0x76, 0xa8, 0x3a, 0xff);
            int count = GameData.Expeditions.Count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var trip = GameData.Expeditions[i];
                AddBaseItem(i, 2, (count + 1) / 2, art.Get("sys_expedition"), accent,
                    () => $"{trip.Name}　{FormatDuration(trip.Seconds)}",
                    () => $"成功率 {trip.Success * 100:0}%　{trip.Shards} 碎片 {trip.Ores:0} 矿" + (trip.Crystals > 0 ? $" {trip.Crystals:0} 晶" : ""),
                    () => TripsUnderway(index),
                    () => "出发", () => sim.SendExpedition(index), () => sim.CanSendExpedition(index));
            }
        }

        // How many teams are out on this trip, and when the first one is back.
        private string TripsUnderway(int trip)
        {
            int teams = 0;
            double soonest = double.MaxValue;
            for (int i = 0; i < sim.State.expeditionTiers.Count; i++)
            {
                if (sim.State.expeditionTiers[i] != trip) continue;
                teams++;
                soonest = Math.Min(soonest, sim.State.expeditionLeft[i]);
            }
            return teams == 0 ? "" : $"出发中 {teams} 队，最快 {FormatDuration(soonest)} 回来";
        }

        private void BuildChallengeTab()
        {
            NewBaseTab("挑战", "sys_challenge");
            AddBaseRow(0, () => sim.CurrentChallenge != null ? $"挑战中：{sim.CurrentChallenge.Name}，目标 {GameData.FormatDepth(sim.CurrentChallenge.TargetRow)}"
                    : sim.ChallengesUnlocked ? "进入挑战会放弃本局，不给碎片" : $"到达{GameData.Layers[Balance.ChallengeLayer].Name}后解锁",
                () => "放弃", () => sim.AbandonChallenge(), () => sim.CurrentChallenge != null);
            var accent = new Color32(0xe8, 0x40, 0x2e, 0xff);
            int count = GameData.Challenges.Count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var def = GameData.Challenges[i];
                AddBaseItem(i, 2, (count + 1) / 2, EffectIcon(def.Reward.Effect), accent,
                    () => def.Name,
                    () => def.Desc,
                    () => $"挖到 {GameData.FormatDepth(def.TargetRow)}：{def.Reward.Desc}",
                    () => sim.ChallengeDone(index) ? "已完成" : "进入", () => sim.StartChallenge(index), () => sim.CanStartChallenge(index));
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
