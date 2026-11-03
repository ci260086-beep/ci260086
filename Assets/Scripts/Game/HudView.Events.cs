using DeepDrill.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>The glowing nugget that surfaces in the dig area, and the buff line under the combo.</summary>
    public sealed partial class HudView
    {
        private const int EventIconSize = 32;
        private const int EventMarginX = 30;
        private const int EventMarginTop = 40;
        private const int EventMarginBottom = 50;
        private static readonly Color GoldenGlow = new Color32(0xff, 0xd8, 0x40, 0xff);
        private static readonly Color UnstableGlow = new Color32(0xff, 0x38, 0x28, 0xff);

        private Button eventButton;
        private Image eventIcon;
        private Text buffLabel;
        private SurfaceEvent shownEvent;

        private void BuildSurfaceEvent()
        {
            eventIcon = Ui.Icon(stageRoot, art.Get("sys_event"), 0, 0, EventIconSize);
            eventIcon.raycastTarget = true;
            eventButton = Ui.WithClick(eventIcon.gameObject.AddComponent<Button>());
            eventButton.targetGraphic = eventIcon;
            eventButton.transition = Selectable.Transition.None;
            eventButton.onClick.AddListener(OnEventClicked);
            eventIcon.gameObject.SetActive(false);
            buffLabel = Floating("", 4, 64, 120, 10, 7, TextAnchor.MiddleLeft);
            buffLabel.color = CoinColor;
        }

        private void OnEventClicked()
        {
            var outcome = sim.ClaimEvent();
            if (outcome != null) ShowToast(outcome.Desc, 3f);
        }

        private void UpdateSurfaceEvent()
        {
            var active = sim.ActiveEvent;
            if (active != shownEvent)
            {
                shownEvent = active;
                eventIcon.gameObject.SetActive(active != SurfaceEvent.None);
                if (active != SurfaceEvent.None) PlaceEventIcon();
            }
            if (active != SurfaceEvent.None)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 7f);
                eventIcon.color = Color.Lerp(active == SurfaceEvent.Golden ? GoldenGlow : UnstableGlow, Color.white, pulse * 0.4f);
                eventIcon.rectTransform.localScale = Vector3.one * (1 + 0.15f * pulse);
            }

            string buffs = "";
            if (sim.DamageBuffLeft > 0) buffs += $"伤害 ×{sim.DamageBuffMult:0.#} {sim.DamageBuffLeft:0}秒  ";
            if (sim.SpeedBuffLeft > 0) buffs += $"攻速 ×{sim.SpeedBuffMult:0.#} {sim.SpeedBuffLeft:0}秒";
            if (sim.Overheating) buffs += "过热：攻速减半，开狂热散热  ";
            if (sim.HeatWeakens) buffs += "高温：伤害只剩四分之一，开狂热";
            buffLabel.text = buffs;
        }

        private void PlaceEventIcon()
        {
            float x = Random.Range(EventMarginX, Stage.DigWidth - EventMarginX - EventIconSize);
            float y = Random.Range(EventMarginTop, Stage.Height - EventMarginBottom - EventIconSize);
            eventIcon.rectTransform.anchoredPosition = new Vector2(x, -y);
            eventIcon.transform.SetAsLastSibling();
        }
    }
}
