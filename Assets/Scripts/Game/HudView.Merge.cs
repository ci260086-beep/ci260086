using DeepDrill.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>The new drill pops up large in its material's light, and its hangar cell jumps.</summary>
    public sealed partial class HudView
    {
        private const int MergeIconScale = 4;
        private const float MergeCardSeconds = 1.1f;
        private const float MergeGrowSeconds = 0.18f;
        private const float MergeFadeSeconds = 0.3f;
        private const float MergeOvershoot = 1.35f;
        private const float MergeBusySeconds = 0.5f;
        private const int MergeGlowSize = 64;
        private const int MergeCardRight = 40; // tucked into a corner, clear of the guardian and the banner
        private const int MergeCardBottom = 48;
        private const int BurstFrameWidth = 32;
        private const int BurstSize = 64;
        private const float BurstFrameSeconds = 0.08f;
        private const int CountPlateWidth = 48;
        private const int CountPlateHeight = 22;
        private const float CountPlateBelow = -34f;
        private const float CellPopSeconds = 0.35f;
        private const float CellPopScale = 0.5f;

        private RectTransform mergeCard;
        private Image mergeIcon, mergeGlow, burst, countPlate;
        private Text countLabel;
        private Sprite[] burstFrames;
        private float burstLeft;
        private float mergeTime;
        private float cellPop;
        private int mergedCode = -1;

        private void BuildMergeCard()
        {
            var go = new GameObject("MergeCard", typeof(RectTransform));
            go.transform.SetParent(FxLayer, false); // with the dig area's effects, so full-screen pages hide it
            mergeCard = (RectTransform)go.transform;
            Ui.TopLeft(mergeCard, Stage.DigWidth - MergeCardRight, Stage.Height - MergeCardBottom, 0, 0);

            mergeGlow = Layer(mergeCard, "node_glow", MergeGlowSize);
            mergeGlow.material = GlowMaterial;
            mergeIcon = Ui.Icon(mergeCard, art.Strip("drill_0_0", 8)[0], 0, 0, DrillIconWidth);
            mergeIcon.raycastTarget = false;
            Center(mergeIcon.rectTransform, Vector2.zero, DrillIconWidth * MergeIconScale, DrillIconHeight * MergeIconScale);
            burst = Ui.Icon(mergeCard, art.Get("pixel"), 0, 0, BurstSize);
            burst.raycastTarget = false;
            Center(burst.rectTransform, Vector2.zero, BurstSize, BurstSize);
            burstFrames = art.Strip("merge_burst", BurstFrameWidth);
            burst.gameObject.SetActive(false);

            countPlate = Ui.Panel(mergeCard, "CountPlate", 0, 0, 1, 1, Color.white);
            countPlate.sprite = art.Get("merge_count_plate");
            countPlate.raycastTarget = false;
            Center(countPlate.rectTransform, new Vector2(0, CountPlateBelow), CountPlateWidth, CountPlateHeight);
            countLabel = Ui.Label(countPlate.transform, Font, "", 6, 0, CountPlateWidth - 8, CountPlateHeight, 7, TextAnchor.MiddleCenter);
            countLabel.color = CoinColor;
            countPlate.gameObject.SetActive(false);
            mergeCard.gameObject.SetActive(false);
        }

        /// <summary>The single summary for a merge of everything: one burst and a count.</summary>
        public void ShowMergedAll(int code, int merges)
        {
            mergeTime = 0;
            ShowMerged(code);
            burstLeft = BurstFrameSeconds * burstFrames.Length;
            burst.gameObject.SetActive(true);
            countLabel.text = "×" + merges;
            countPlate.gameObject.SetActive(true);
        }

        /// <summary>Announces a merged drill; a burst of automatic merges keeps the one on show.</summary>
        public void ShowMerged(int code)
        {
            mergedCode = code;
            cellPop = 1f;
            if (mergeTime > MergeCardSeconds - MergeBusySeconds) return;

            mergeTime = MergeCardSeconds;
            mergeIcon.sprite = FrameOf(code);
            mergeGlow.color = Palette.Tier(DrillCode.Tier(code));
            mergeCard.gameObject.SetActive(true);
        }

        private void UpdateMergeCard(float dt)
        {
            UpdateCellPop(dt);
            if (burstLeft > 0)
            {
                burstLeft -= dt;
                int frame = Mathf.Clamp((int)((burstFrames.Length * BurstFrameSeconds - burstLeft) / BurstFrameSeconds), 0, burstFrames.Length - 1);
                burst.sprite = burstFrames[frame];
                if (burstLeft <= 0) burst.gameObject.SetActive(false);
            }
            if (mergeTime <= 0) return;
            mergeTime -= dt;
            if (mergeTime <= 0)
            {
                mergeCard.gameObject.SetActive(false);
                countPlate.gameObject.SetActive(false);
                return;
            }

            float shown = MergeCardSeconds - mergeTime;
            float grow = Mathf.Clamp01(shown / MergeGrowSeconds);
            float scale = shown < MergeGrowSeconds
                ? grow * MergeOvershoot
                : Mathf.Lerp(MergeOvershoot, 1f, Mathf.Clamp01((shown - MergeGrowSeconds) / MergeGrowSeconds));
            mergeCard.localScale = new Vector3(scale, scale, 1);

            float alpha = Mathf.Clamp01(mergeTime / MergeFadeSeconds);
            mergeIcon.color = new Color(1, 1, 1, alpha);
            var glow = mergeGlow.color;
            glow.a = alpha;
            mergeGlow.color = glow;
        }

        private void UpdateCellPop(float dt)
        {
            if (mergedCode < 0) return;
            cellPop = Mathf.Max(0, cellPop - dt / CellPopSeconds);
            float scale = 1f + cellPop * CellPopScale;
            bool found = false;
            foreach (var cell in hangarCells)
            {
                if (cell.Code != mergedCode || found)
                {
                    cell.transform.localScale = Vector3.one;
                    continue;
                }
                found = true;
                cell.transform.localScale = new Vector3(scale, scale, 1);
                cell.transform.SetAsLastSibling();
            }
            if (cellPop <= 0) mergedCode = -1;
        }
    }
}
