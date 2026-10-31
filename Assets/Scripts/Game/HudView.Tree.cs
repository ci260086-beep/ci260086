using System;
using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>The skill tree page: a fan of six branches on a starfield.</summary>
    public sealed partial class HudView
    {
        private const float TreeUnit = 8f;
        private const float MinZoom = 0.3f;
        private const float MaxZoom = 2f;
        private const float ZoomStep = 0.1f;
        private const float StartZoom = 0.62f;
        private const float FocusRate = 8f;
        private const float HoverScale = 1.15f;
        private const float BreathRate = 2.5f;
        private const float FlowSeconds = 1.4f;
        private const float PulseSeconds = 0.45f;
        private const float ChargeSeconds = 0.25f;
        private const float DashScale = 6.25f; // one art pixel to one stage pixel on the tiled dash
        private const int LevelDots = 5;
        private const int Stars = 18;
        private const int BurstBits = 24;
        private const int RootSize = 23;
        private const int KeySize = 17;
        private const int SmallSize = 13;
        private const int KeyRing = 10;

        private sealed class BranchStyle
        {
            public string Id;
            public string Title;
            public Color Bright;
            public Color Mid;
            public Color Dark;
            public float Degrees;
        }

        private static readonly Color LockedFill = new Color32(0x1c, 0x18, 0x26, 255);
        private static readonly Color LockedFrame = new Color32(0x34, 0x2e, 0x44, 255);
        private static readonly Color LineLocked = new Color32(0x2a, 0x26, 0x3a, 255);
        private static readonly Color PriceRed = new Color32(0xff, 0x60, 0x58, 255);

        // clockwise from the top, as the fan is laid out
        private static readonly BranchStyle[] Branches =
        {
            Branch("drill", "钻头", "FFBE5A", "E68228", "6E3714", 90),
            Branch("wealth", "财富", "FFEC78", "DCB428", "645014", 30),
            Branch("blast", "爆破", "FF7864", "D23232", "64141E", -30),
            Branch("hangar", "机库", "96D2FF", "468CDC", "1E3C6E", -90),
            Branch("frenzy", "狂热", "FF8CE6", "C83CB4", "5A195A", -150),
            Branch("strata", "地层", "96FFA0", "3CBE5A", "195528", 150),
        };

        private static readonly BranchStyle CoreStyle = Branch("core", "核心", "FFE27A", "E8B02A", "5A4418", 0);

        private static BranchStyle Branch(string id, string title, string bright, string mid, string dark, float degrees)
        {
            ColorUtility.TryParseHtmlString("#" + bright, out Color b);
            ColorUtility.TryParseHtmlString("#" + mid, out Color m);
            ColorUtility.TryParseHtmlString("#" + dark, out Color d);
            return new BranchStyle { Id = id, Title = title, Bright = b, Mid = m, Dark = d, Degrees = degrees };
        }

        private static BranchStyle StyleOf(string branch)
        {
            foreach (var b in Branches)
                if (b.Id == branch) return b;
            return CoreStyle;
        }

        private sealed class TreeNode
        {
            public SkillDef Skill;
            public BranchStyle Style;
            public RectTransform Root;
            public Image Glow, Frame, Fill, Charge, Icon;
            public Image[] Dots;
            public float Pop;
            public float Flash;
            public float Charging;
            public bool Hovered;
            public bool Breathing;
        }

        private sealed class TreeLink
        {
            public SkillDef From;
            public SkillDef To;
            public BranchStyle Style;
            public Image Line;
            public Image Glow;
            public Image Flow;
            public Vector2 A;
            public Vector2 B;
            public bool Lit;
            public float Offset;
        }

        private sealed class TreePulse
        {
            public Image Dot;
            public Vector2 From;
            public Vector2 To;
            public float Time;
        }

        private sealed class TreeBit
        {
            public Image Dot;
            public Vector2 Velocity;
            public float Life;
        }

        /// <summary>The tree's background: dragging pans, the wheel zooms, a double click recentres.</summary>
        private sealed class TreeCanvas : MonoBehaviour, IDragHandler, IScrollHandler, IPointerClickHandler
        {
            public HudView Hud;
            public void OnDrag(PointerEventData e) => Hud.PanTree(e.delta);
            public void OnScroll(PointerEventData e) => Hud.ZoomTree(e.scrollDelta.y);
            public void OnPointerClick(PointerEventData e)
            {
                if (e.clickCount == 2) Hud.FocusTree(Vector2.zero);
            }
        }

        private sealed class NodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action Entered;
            public Action Left;
            public void OnPointerEnter(PointerEventData e) => Entered?.Invoke();
            public void OnPointerExit(PointerEventData e) => Left?.Invoke();
        }

        private readonly List<TreeNode> treeNodes = new List<TreeNode>();
        private readonly List<TreeLink> treeLinks = new List<TreeLink>();
        private readonly List<TreePulse> treePulses = new List<TreePulse>();
        private readonly List<TreeBit> treeBits = new List<TreeBit>();
        private readonly List<Image> starDots = new List<Image>();
        private RectTransform treeContent;
        private Sprite dashSprite;
        private RectTransform tooltip;
        private Image tooltipFrame, tooltipIcon;
        private Text tooltipName, tooltipDesc, tooltipEffect, tooltipPrice;
        private float treeZoom = StartZoom;
        private float treeTime;
        private Vector2 treeTarget;
        private bool treeFocusing;
        private TreeNode hovered;

        private void BuildTreePage()
        {
            var page = Ui.Panel(stageRoot, "TreePage", 0, 0, Stage.Width, Stage.Height, Color.white);
            page.sprite = art.Get("tree_bg");
            pages[PageTree] = page.gameObject;
            page.gameObject.AddComponent<RectMask2D>();
            page.gameObject.AddComponent<TreeCanvas>().Hud = this;

            for (int i = 0; i < Stars; i++)
            {
                float x = (i * 73.3f) % Stage.Width, y = (i * 41.7f + 9) % Stage.Height;
                var star = Ui.Panel(page.transform, "Star", x, y, 1, 1, new Color(0.6f, 0.65f, 0.8f, 0.7f));
                star.raycastTarget = false;
                starDots.Add(star);
            }

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(page.transform, false);
            treeContent = (RectTransform)contentGo.transform;
            treeContent.anchorMin = treeContent.anchorMax = treeContent.pivot = new Vector2(0.5f, 0.5f);
            treeContent.sizeDelta = Vector2.zero;
            treeContent.localScale = Vector3.one * treeZoom;

            dashSprite = art.Get("tree_dash");
            var rings = Ui.Panel(treeContent, "Rings", 0, 0, 1, 1, Color.white);
            rings.sprite = art.Get("tree_rings");
            rings.raycastTarget = false;
            Center(rings.rectTransform, Vector2.zero, 640, 640);

            foreach (var skill in GameData.Skills)
                foreach (string parentId in skill.Prereq)
                    treeLinks.Add(BuildLink(GameData.Skill(parentId), skill));
            foreach (var skill in GameData.Skills) treeNodes.Add(BuildNode(skill));

            for (int i = 0; i < BurstBits; i++)
            {
                var dot = Ui.Panel(treeContent, "Bit", 0, 0, 2, 2, Color.white);
                dot.raycastTarget = false;
                dot.gameObject.SetActive(false);
                treeBits.Add(new TreeBit { Dot = dot });
            }

            BuildBranchButtons(page.transform);
            BuildTooltip(page.transform);

            var close = Ui.Button(page.transform, Font, "关闭", Stage.Width - 30, 2, 28, 12, 7, RailColor, out _);
            close.onClick.AddListener(() => ShowPage(PageHangar));
        }

        private void BuildBranchButtons(Transform parent)
        {
            for (int i = 0; i < Branches.Length; i++)
            {
                var style = Branches[i];
                var button = Ui.Button(parent, Font, style.Title, 4 + i * 40, 2, 38, 12, 7, style.Dark, out Text label);
                label.color = style.Bright;
                button.onClick.AddListener(() => FocusBranch(style));
            }
        }

        private void BuildTooltip(Transform parent)
        {
            tooltipFrame = Ui.Panel(parent, "Tooltip", 0, 0, 132, 48, Color.white);
            tooltipFrame.raycastTarget = false;
            tooltip = tooltipFrame.rectTransform;
            var inner = Ui.Panel(tooltip, "Inner", 1, 1, 130, 46, new Color32(0x14, 0x10, 0x20, 245));
            inner.raycastTarget = false;
            tooltipIcon = Ui.Icon(inner.transform, art.Get("ticon_drill"), 4, 4, 10);
            tooltipName = Ui.Label(inner.transform, Font, "", 18, 2, 110, 10, 8, TextAnchor.MiddleLeft);
            tooltipName.fontStyle = FontStyle.Bold;
            tooltipDesc = Ui.Label(inner.transform, Font, "", 4, 14, 124, 9, 6, TextAnchor.MiddleLeft);
            tooltipDesc.color = SoftText;
            tooltipEffect = Ui.Label(inner.transform, Font, "", 4, 23, 124, 9, 6, TextAnchor.MiddleLeft);
            tooltipPrice = Ui.Label(inner.transform, Font, "", 4, 33, 124, 10, 7, TextAnchor.MiddleLeft);
            tooltip.gameObject.SetActive(false);
        }

        private static void Center(RectTransform rt, Vector2 at, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Vector2 NodePosition(SkillDef s) => new Vector2((float)s.X * TreeUnit, (float)s.Y * TreeUnit);

        private static bool IsKey(SkillDef s) => s.MaxLevel == 1 || NodeRing(s) >= KeyRing;

        private static int NodeRing(SkillDef s) =>
            Mathf.RoundToInt((Mathf.Sqrt((float)(s.X * s.X + s.Y * s.Y)) - 4f) / 3f);

        private TreeLink BuildLink(SkillDef from, SkillDef to)
        {
            Vector2 a = NodePosition(from), b = NodePosition(to);
            var style = StyleOf(to.Branch);
            var link = new TreeLink { From = from, To = to, Style = style, A = a, B = b, Offset = (a.x + b.y) * 0.013f };

            link.Glow = Bar(a, b, 4f);
            link.Glow.material = GlowMaterial;
            link.Line = Bar(a, b, 1f);
            link.Line.pixelsPerUnitMultiplier = DashScale;
            link.Flow = Ui.Panel(treeContent, "Flow", 0, 0, 2, 2, style.Bright);
            link.Flow.raycastTarget = false;
            Center(link.Flow.rectTransform, a, 2, 2);
            link.Flow.gameObject.SetActive(false);
            return link;
        }

        private Image Bar(Vector2 a, Vector2 b, float thickness)
        {
            var bar = Ui.Panel(treeContent, "Link", 0, 0, 1, 1, LineLocked);
            bar.raycastTarget = false;
            Center(bar.rectTransform, (a + b) / 2f, (b - a).magnitude, thickness);
            bar.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            return bar;
        }

        private Material glowMaterial;

        // Glows add light rather than paint over what is behind them.
        private Material GlowMaterial
        {
            get
            {
                if (glowMaterial == null) glowMaterial = new Material(Shader.Find("DeepDrill/UiAdditive"));
                return glowMaterial;
            }
        }

        private Image Layer(Transform parent, string sprite, float size)
        {
            var image = Ui.Panel(parent, sprite, 0, 0, size, size, Color.white);
            image.sprite = art.Get(sprite);
            image.raycastTarget = false;
            Center(image.rectTransform, Vector2.zero, size, size);
            return image;
        }

        private TreeNode BuildNode(SkillDef skill)
        {
            bool root = skill.Id == GameSim.RootSkill;
            int size = root ? RootSize : (IsKey(skill) ? KeySize : SmallSize);
            var style = StyleOf(skill.Branch);

            var hit = Ui.Panel(treeContent, skill.Id, 0, 0, size, size, Color.clear);
            Center(hit.rectTransform, NodePosition(skill), size, size);

            var node = new TreeNode { Skill = skill, Style = style, Root = hit.rectTransform };
            node.Glow = Layer(hit.transform, "node_glow", size * 3f);
            node.Glow.material = GlowMaterial;
            node.Fill = Layer(hit.transform, $"node_fill_{size}", size);
            node.Charge = Layer(hit.transform, $"node_fill_{size}", size);
            node.Charge.type = Image.Type.Filled;
            node.Charge.fillMethod = Image.FillMethod.Radial360;
            node.Charge.fillOrigin = (int)Image.Origin360.Top;
            node.Charge.color = style.Mid;
            node.Charge.gameObject.SetActive(false);
            node.Frame = Layer(hit.transform, $"node_frame_{size}", size);
            string icon = root ? "tab_drill" : "ticon_" + skill.Branch;
            node.Icon = Layer(hit.transform, icon, root ? 16 : 5);

            int shown = Mathf.Min(LevelDots, skill.MaxLevel);
            node.Dots = new Image[shown];
            for (int i = 0; i < shown; i++)
            {
                var dot = Ui.Panel(hit.transform, "Dot", 0, 0, 1, 1, LockedFrame);
                dot.raycastTarget = false;
                Center(dot.rectTransform, new Vector2((i - (shown - 1) / 2f) * 2f, -size / 2f - 2f), 1, 1);
                node.Dots[i] = dot;
            }

            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (sim.Buy(skill.Id)) LightNode(node);
                ShowNodeInfo(node);
            });
            var hover = hit.gameObject.AddComponent<NodeHover>();
            hover.Entered = () =>
            {
                node.Hovered = true;
                hovered = node;
                ShowNodeInfo(node);
            };
            hover.Left = () =>
            {
                node.Hovered = false;
                if (hovered == node) HideNodeInfo();
            };
            return node;
        }

        // A bought node fills up, then flashes and bursts, and light runs down the lines to what it opens.
        /// <summary>Plays the purchase effect on a skill; for the demo driver's screenshot.</summary>
        public void BurstSkill(string id)
        {
            foreach (var node in treeNodes)
                if (node.Skill.Id == id) LightNode(node);
        }

        private void LightNode(TreeNode node)
        {
            node.Charging = 1f;
            node.Charge.fillAmount = 0f;
            node.Charge.gameObject.SetActive(true);
        }

        private void ReleaseNode(TreeNode node)
        {
            node.Charge.gameObject.SetActive(false);
            node.Pop = 1f;
            node.Flash = 1f;
            int given = 0;
            foreach (var bit in treeBits)
            {
                if (bit.Life > 0 || given >= 12) continue;
                given++;
                bit.Life = 0.5f;
                bit.Velocity = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(40f, 90f);
                bit.Dot.color = node.Style.Bright;
                bit.Dot.rectTransform.anchoredPosition = node.Root.anchoredPosition;
                bit.Dot.gameObject.SetActive(true);
            }
            foreach (var link in treeLinks)
            {
                if (link.From != node.Skill) continue;
                var spark = Ui.Panel(treeContent, "Pulse", 0, 0, 3, 3, link.Style.Bright);
                spark.raycastTarget = false;
                Center(spark.rectTransform, link.A, 3, 3);
                treePulses.Add(new TreePulse { Dot = spark, From = link.A, To = link.B });
            }
        }

        private void ShowNodeInfo(TreeNode node)
        {
            var skill = node.Skill;
            int level = sim.State.LevelOf(skill.Id);
            bool maxed = level >= skill.MaxLevel;
            tooltip.gameObject.SetActive(true);
            tooltipFrame.color = node.Style.Mid;
            tooltipIcon.sprite = art.Get(skill.Id == GameSim.RootSkill ? "tab_drill" : "ticon_" + skill.Branch);
            tooltipIcon.color = node.Style.Bright;
            tooltipName.text = $"{skill.Name}  Lv{level}/{skill.MaxLevel}";
            tooltipName.color = node.Style.Bright;
            tooltipDesc.text = skill.Desc;
            tooltipEffect.text = maxed ? $"当前 {FormatEffect(skill, level)}" : $"{FormatEffect(skill, level)} → {FormatEffect(skill, level + 1)}";

            if (!sim.LayerReached(skill))
            {
                tooltipPrice.text = $"需要到达{GameData.Layers[skill.RequiresLayer].Name}";
                tooltipPrice.color = PriceRed;
            }
            else if (maxed)
            {
                tooltipPrice.text = "已满级";
                tooltipPrice.color = SoftText;
            }
            else
            {
                double cost = sim.SkillCost(skill);
                tooltipPrice.text = "价格 " + NumberFormat.Short(cost);
                tooltipPrice.color = sim.State.coins >= cost ? CoinColor : PriceRed;
            }

            Vector2 stage = new Vector2(Stage.Width / 2f, Stage.Height / 2f)
                + new Vector2(1, -1) * (treeContent.anchoredPosition + node.Root.anchoredPosition * treeZoom);
            float x = Mathf.Clamp(stage.x + 12, 2, Stage.Width - tooltip.sizeDelta.x - 2);
            float y = Mathf.Clamp(stage.y + 12, 16, Stage.Height - tooltip.sizeDelta.y - 2);
            tooltip.anchoredPosition = new Vector2(x, -y);
            tooltip.SetAsLastSibling();
        }

        private void HideNodeInfo()
        {
            hovered = null;
            tooltip.gameObject.SetActive(false);
        }

        /// <summary>Shows the tooltip for a skill; for the demo driver's screenshot.</summary>
        public void HoverSkill(string id)
        {
            foreach (var node in treeNodes)
            {
                if (node.Skill.Id != id) continue;
                node.Hovered = true;
                hovered = node;
                ShowNodeInfo(node);
            }
        }

        private static string FormatEffect(SkillDef skill, int level)
        {
            if (skill.Id != GameSim.RootSkill) return $"+{skill.PerLevel * level:0.##}";
            if (level <= 0) return "手动挖掘";
            return level == 1 ? "自动钻探" : $"自动钻探，伤害 +{skill.PerLevel * (level - 1):0.##}%";
        }

        public void PanTree(Vector2 screenDelta)
        {
            treeFocusing = false;
            treeContent.anchoredPosition += screenDelta / Stage.Scale;
            HideNodeInfo();
        }

        public void ZoomTree(float amount)
        {
            treeZoom = Mathf.Clamp(treeZoom + amount * ZoomStep, MinZoom, MaxZoom);
            treeContent.localScale = Vector3.one * treeZoom;
        }

        /// <summary>Moves the tree's camera to a branch by id; for the demo driver's screenshot.</summary>
        public void FocusBranch(string id) => FocusBranch(StyleOf(id));

        private void FocusBranch(BranchStyle style)
        {
            float radians = style.Degrees * Mathf.Deg2Rad;
            FocusTree(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * -16f * TreeUnit * treeZoom);
        }

        /// <summary>Returns the tree's camera to the root; for the demo driver's screenshot.</summary>
        public void CenterTree() => FocusTree(Vector2.zero);

        private void FocusTree(Vector2 at)
        {
            treeTarget = at;
            treeFocusing = true;
        }

        // Per-frame motion: breathing nodes, flowing lights, sparks and the smooth camera.
        private void AnimateTree(float dt)
        {
            treeTime += dt;

            if (treeFocusing)
            {
                treeContent.anchoredPosition = Vector2.Lerp(treeContent.anchoredPosition, treeTarget, 1f - Mathf.Exp(-FocusRate * dt));
                if ((treeContent.anchoredPosition - treeTarget).sqrMagnitude < 0.25f) treeFocusing = false;
            }

            for (int i = 0; i < starDots.Count; i++)
            {
                if (i % 3 != 0) continue;
                var c = starDots[i].color;
                c.a = 0.35f + 0.35f * Mathf.Sin(treeTime * 1.3f + i);
                starDots[i].color = c;
            }

            float breath = 0.5f + 0.5f * Mathf.Sin(treeTime * BreathRate);
            foreach (var node in treeNodes)
            {
                if (node.Breathing)
                    node.Frame.color = Color.Lerp(node.Style.Dark, node.Style.Mid, breath);

                if (node.Charging > 0)
                {
                    node.Charging = Mathf.Max(0, node.Charging - dt / ChargeSeconds);
                    node.Charge.fillAmount = 1f - node.Charging;
                    if (node.Charging <= 0) ReleaseNode(node);
                }

                node.Pop = Mathf.Max(0, node.Pop - dt * 4f);
                node.Flash = Mathf.Max(0, node.Flash - dt * 6f);
                float scale = (node.Hovered ? HoverScale : 1f) + node.Pop * 0.4f;
                node.Root.localScale = new Vector3(scale, scale, 1);
                if (node.Flash > 0) node.Fill.color = Color.Lerp(node.Fill.color, Color.white, node.Flash);
            }

            foreach (var link in treeLinks)
            {
                if (!link.Lit) continue;
                float t = Mathf.Repeat(treeTime / FlowSeconds + link.Offset, 1f);
                link.Flow.rectTransform.anchoredPosition = Vector2.Lerp(link.A, link.B, t);
            }

            for (int i = treePulses.Count - 1; i >= 0; i--)
            {
                var p = treePulses[i];
                p.Time += dt / PulseSeconds;
                p.Dot.rectTransform.anchoredPosition = Vector2.Lerp(p.From, p.To, p.Time);
                if (p.Time < 1) continue;
                Destroy(p.Dot.gameObject);
                treePulses.RemoveAt(i);
            }

            foreach (var bit in treeBits)
            {
                if (bit.Life <= 0) continue;
                bit.Life -= dt;
                bit.Dot.rectTransform.anchoredPosition += bit.Velocity * dt;
                if (bit.Life <= 0) bit.Dot.gameObject.SetActive(false);
            }
        }

        // State colours; refreshed a few times a second since they only change on purchases.
        private void UpdateTree()
        {
            foreach (var node in treeNodes)
            {
                var skill = node.Skill;
                int level = sim.State.LevelOf(skill.Id);
                bool lit = level > 0;
                bool open = !lit && sim.IsUnlocked(skill);
                var style = node.Style;

                node.Breathing = open;
                node.Fill.color = lit ? style.Dark : (open ? new Color(0.09f, 0.07f, 0.13f) : LockedFill);
                if (!open) node.Frame.color = lit ? style.Bright : LockedFrame;
                node.Icon.color = lit ? style.Bright : (open ? style.Mid : LockedFrame);
                var glow = style.Bright;
                glow.a = lit ? 0.55f : 0f;
                node.Glow.color = glow;

                int filled = skill.MaxLevel <= node.Dots.Length ? level
                    : Mathf.CeilToInt(level * node.Dots.Length / (float)skill.MaxLevel);
                for (int i = 0; i < node.Dots.Length; i++)
                    node.Dots[i].color = i < filled ? style.Bright : LockedFrame;
            }

            foreach (var link in treeLinks)
            {
                bool lit = sim.State.LevelOf(link.From.Id) > 0 && sim.State.LevelOf(link.To.Id) > 0;
                bool open = !lit && sim.State.LevelOf(link.From.Id) > 0 && sim.LayerReached(link.To);
                link.Lit = lit;
                var size = link.Line.rectTransform.sizeDelta;
                link.Line.rectTransform.sizeDelta = new Vector2(size.x, lit ? 2f : 1f);
                link.Line.sprite = open ? dashSprite : null;
                link.Line.type = open ? Image.Type.Tiled : Image.Type.Simple;
                link.Line.color = lit || open ? link.Style.Mid : LineLocked;
                var glow = link.Style.Bright;
                glow.a = lit ? 0.3f : 0f;
                link.Glow.color = glow;
                link.Flow.gameObject.SetActive(lit);
            }
        }
    }
}
