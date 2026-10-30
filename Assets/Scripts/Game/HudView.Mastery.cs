using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>The gallery's tabs: the collection, drill milestones and layer mastery.</summary>
    public sealed partial class HudView
    {
        private const int GalleryTabX = 130;
        private const int GalleryTabWidth = 34;
        private const int MilestoneRowHeight = 19;
        private const int MilestoneCellX = 70;
        private const int MilestoneCellWidth = 46;
        private const int MasteryRowHeight = 24;
        private const int MasteryColumnWidth = 154;
        private const int MasteryRowsPerColumn = 6;
        private const int MasteryBarWidth = 146;

        private sealed class MasteryRow
        {
            public Text Name, Level, Blocks;
            public Image Fill;
        }

        private readonly List<GameObject> galleryTabs = new List<GameObject>();
        private readonly List<Image> galleryTabButtons = new List<Image>();
        private readonly List<Text> familyBestLabels = new List<Text>();
        private readonly List<Image[]> milestoneCells = new List<Image[]>();
        private readonly List<MasteryRow> masteryRows = new List<MasteryRow>();
        private int galleryTab;

        private Transform NewGalleryTab(Transform page, string title)
        {
            int index = galleryTabs.Count;
            var tab = new GameObject("Tab", typeof(RectTransform));
            tab.transform.SetParent(page, false);
            Ui.TopLeft((RectTransform)tab.transform, 0, 0, Stage.Width, Stage.Height);
            galleryTabs.Add(tab);

            var button = Ui.Button(page, Font, title, GalleryTabX + index * (GalleryTabWidth + 2), 2, GalleryTabWidth, 12, 7, RailColor, out _);
            button.onClick.AddListener(() => ShowGalleryTab(index));
            galleryTabButtons.Add(button.GetComponent<Image>());
            return tab.transform;
        }

        private void ShowGalleryTab(int index)
        {
            galleryTab = index;
            galleryInfo.text = "";
            for (int i = 0; i < galleryTabs.Count; i++)
            {
                galleryTabs[i].SetActive(i == index);
                galleryTabButtons[i].color = i == index ? RailSelected : RailColor;
            }
        }

        // One row per family, one cell per owned-count milestone.
        private void BuildMilestoneTab(Transform page)
        {
            var tab = NewGalleryTab(page, "里程碑");
            for (int f = 0; f < GameData.Families.Count; f++)
            {
                int y = 18 + f * MilestoneRowHeight;
                var name = Ui.Label(tab, Font, GameData.Families[f].Name, 6, y, 62, 8, 7, TextAnchor.MiddleLeft);
                name.fontStyle = FontStyle.Bold;
                var best = Ui.Label(tab, Font, "", 6, y + 8, 62, 8, 6, TextAnchor.MiddleLeft);
                best.color = SoftText;
                familyBestLabels.Add(best);

                var cells = new Image[GameData.MilestoneCounts.Count];
                for (int i = 0; i < cells.Length; i++)
                {
                    var reward = GameData.Milestones[f][i];
                    int count = GameData.MilestoneCounts[i];
                    var cell = Ui.Panel(tab, "milestone", MilestoneCellX + i * (MilestoneCellWidth + 2), y, MilestoneCellWidth, MilestoneRowHeight - 3, CardDim);
                    Ui.Label(cell.transform, Font, $"{count} 台", 0, 0, MilestoneCellWidth, MilestoneRowHeight - 3, 7, TextAnchor.MiddleCenter);
                    cell.gameObject.AddComponent<HoverRelay>().Entered = () => galleryInfo.text = $"单局买到 {count} 台：{reward.Desc}";
                    cells[i] = cell;
                }
                milestoneCells.Add(cells);
            }
        }

        // Two columns of layers, each with its level and the way to the next one.
        private void BuildMasteryTab(Transform page)
        {
            var tab = NewGalleryTab(page, "精通");
            for (int layer = 0; layer < GameData.Layers.Count; layer++)
            {
                int index = layer;
                float x = 6 + layer / MasteryRowsPerColumn * MasteryColumnWidth;
                float y = 18 + layer % MasteryRowsPerColumn * MasteryRowHeight;
                var box = Ui.Panel(tab, "mastery", x, y, MasteryBarWidth, MasteryRowHeight - 2, CardDim);
                box.gameObject.AddComponent<HoverRelay>().Entered = () => DescribeMastery(index);

                var row = new MasteryRow
                {
                    Name = Ui.Label(box.transform, Font, "", 2, 1, 96, 10, 7, TextAnchor.MiddleLeft),
                    Level = Ui.Label(box.transform, Font, "", MasteryBarWidth - 50, 1, 48, 10, 7, TextAnchor.MiddleRight),
                    Blocks = Ui.Label(box.transform, Font, "", 2, 10, MasteryBarWidth - 4, 7, 6, TextAnchor.MiddleLeft),
                };
                row.Blocks.color = SoftText;
                var track = Ui.Panel(box.transform, "Track", 2, MasteryRowHeight - 6, MasteryBarWidth - 4, 3, PanelColor);
                track.raycastTarget = false;
                row.Fill = Ui.Panel(track.transform, "Fill", 0, 0, 0, 3, CoinColor);
                row.Fill.raycastTarget = false;
                masteryRows.Add(row);
            }
        }

        private void DescribeMastery(int layer)
        {
            if (!LayerSeen(layer))
            {
                galleryInfo.text = "还没有到过这一层";
                return;
            }
            galleryInfo.text = $"{GameData.Layers[layer].Name}  当前：本层伤害 +{sim.MasteryDamage(layer):0}%，本层价值 +{sim.MasteryValue(layer):0}%  " +
                $"每级 +{GameData.MasteryDamagePct:0}%";
        }

        private bool LayerSeen(int layer) => layer <= sim.ReachedLayer;

        private void UpdateGalleryTabs()
        {
            if (galleryTab == 1) UpdateMilestones();
            if (galleryTab == 2) UpdateMastery();
        }

        private void UpdateMilestones()
        {
            for (int f = 0; f < milestoneCells.Count; f++)
            {
                familyBestLabels[f].text = $"最多 {sim.State.FamilyBestOf(f)} 台";
                int reached = sim.MilestonesOf(f);
                for (int i = 0; i < milestoneCells[f].Length; i++)
                    milestoneCells[f][i].color = i < reached ? BuyColor : CardDim;
            }
        }

        private void UpdateMastery()
        {
            for (int layer = 0; layer < masteryRows.Count; layer++)
            {
                var row = masteryRows[layer];
                bool seen = LayerSeen(layer);
                int level = sim.MasteryOf(layer);
                bool maxed = level >= GameData.MasteryBlocks.Count;
                double dug = sim.MasteryBlocksDug(layer);
                double next = maxed ? dug : GameData.MasteryBlocks[level];
                row.Name.text = seen ? GameData.Layers[layer].Name : "？？？";
                row.Level.text = seen ? (maxed ? "精通满级" : $"精通 {level}") : "";
                row.Blocks.text = seen ? (maxed ? $"已挖 {NumberFormat.Short(dug)} 块" : $"{NumberFormat.Short(dug)} / {NumberFormat.Short(next)} 块") : "";
                double fill = seen && next > 0 ? System.Math.Min(1, dug / next) : 0;
                row.Fill.rectTransform.sizeDelta = new Vector2((float)(fill * (MasteryBarWidth - 4)), 3);
            }
        }
    }
}
