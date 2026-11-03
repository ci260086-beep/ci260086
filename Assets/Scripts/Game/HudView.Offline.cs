using DeepDrill.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>The welcome back screen: what the drills earned while the game was closed.</summary>
    public sealed partial class HudView
    {
        private const int OfflineWidth = 170;
        private const int OfflineHeight = 100;
        private const float OfflineCountSeconds = 0.8f;

        private GameObject offlineRoot;
        private Text offlineDetail, offlineCoins;
        private double offlineTotal;
        private float offlineShown;

        private void BuildOfflinePanel()
        {
            var dim = Ui.Panel(stageRoot, "OfflineDim", 0, 0, Stage.Width, Stage.Height, new Color(0, 0, 0, 0.6f));
            offlineRoot = dim.gameObject;

            float x = (Stage.Width - OfflineWidth) / 2f, y = (Stage.Height - OfflineHeight) / 2f;
            Ui.Panel(dim.transform, "Frame", x, y, OfflineWidth, OfflineHeight, CoinColor);
            var inner = Ui.Panel(dim.transform, "Inner", x + 1, y + 1, OfflineWidth - 2, OfflineHeight - 2, PanelColor);

            var title = Ui.Label(inner.transform, Font, "欢迎回来", 0, 3, OfflineWidth - 2, 12, 9, TextAnchor.MiddleCenter);
            title.color = CoinColor;
            offlineDetail = Ui.Label(inner.transform, Font, "", 8, 18, OfflineWidth - 18, 30, 7, TextAnchor.UpperLeft);
            offlineDetail.color = SoftText;
            offlineDetail.lineSpacing = 1.15f;
            offlineCoins = Ui.Label(inner.transform, Font, "", 0, 50, OfflineWidth - 2, 18, 12, TextAnchor.MiddleCenter);
            offlineCoins.color = CoinColor;

            var take = Ui.Button(inner.transform, Font, "收下", (OfflineWidth - 62) / 2f, 76, 60, 16, 8, CardBright, out _);
            take.onClick.AddListener(CloseOffline);
            offlineRoot.SetActive(false);
        }

        public void ShowOffline(OfflineReport report)
        {
            offlineTotal = report.Coins;
            offlineShown = 0;
            string paid = report.Capped ? $"结算 {FormatDuration(report.SecondsPaid)}（已达上限）" : "全部结算";
            offlineDetail.text = $"离开了 {FormatDuration(report.SecondsAway)}\n{paid}\n离线效率 {report.Rate * 100:0}%";
            offlineRoot.transform.SetAsLastSibling();
            offlineRoot.SetActive(true);
        }

        public void CloseOffline() => offlineRoot.SetActive(false);

        private void UpdateOffline(float dt)
        {
            if (!offlineRoot.activeSelf) return;
            offlineShown = Mathf.Min(1, offlineShown + dt / OfflineCountSeconds);
            offlineCoins.text = "+" + NumberFormat.Short(offlineTotal * offlineShown);
        }

        private static string FormatDuration(double seconds)
        {
            int minutes = (int)(seconds / 60);
            if (minutes < 1) return "不到 1 分钟";
            if (minutes < 60) return $"{minutes} 分钟";
            return minutes % 60 == 0 ? $"{minutes / 60} 小时" : $"{minutes / 60} 小时 {minutes % 60} 分钟";
        }
    }
}
