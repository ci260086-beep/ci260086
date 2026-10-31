"""深渊钻头 第二版数值设计生成器（数据部分）。运行：python gen_design_data.py <输出目录>"""
import json, math, os, sys

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
os.makedirs(OUT, exist_ok=True)

HP_BASE, HP_GROWTH = 10.0, 1.03
VAL_BASE, VAL_GROWTH = 1.0, 1.028
WORLD_WIDTH = 40

def hp(row):  return HP_BASE * HP_GROWTH ** row
def val(row): return VAL_BASE * VAL_GROWTH ** row

LAYERS_RAW = [
    ("表土", 0, 100, 100, "棕/绿", [("煤", 1, .06), ("铜", 2, .06)], "无，教学层", "巨型木根"),
    ("砂岩", 100, 500, 150, "土黄", [("铁", 4, .06), ("银", 8, .03)], "沙块：下方挖空就塌落", "岩甲虫"),
    ("石灰岩溶洞", 500, 2_000, 180, "紫/蓝", [("水晶", 16, .05), ("金", 30, .02)], "水晶反射：击碎时弹射伤害周围 2 格", "水晶巨像"),
    ("地下暗河", 2_000, 5_000, 200, "深蓝", [("珍珠", 50, .04), ("蓝宝石", 90, .02)], "水流：挖开水块会冲走附近碎屑并推歪钻头", "盲眼巨鱼"),
    ("远古遗迹", 5_000, 10_000, 200, "金/灰", [("古币", 150, .05), ("红宝石", 260, .02)], "机关宝箱和隐藏房间", "石像守卫"),
    ("花岗岩地壳", 10_000, 35_000, 220, "灰/粉", [("石英", 400, .05), ("钛", 700, .025)], "超硬岩：只有重型钻能高效挖", "花岗岩魔像"),
    ("莫霍面", 35_000, 100_000, 220, "黑/暗红", [("玄武晶", 1.2e3, .05), ("铂", 2e3, .02)], "高压：钻头每 10 秒过热一次，需狂热散热", "莫霍守门人"),
    ("上地幔", 100_000, 410_000, 240, "橄榄绿", [("橄榄石", 3.5e3, .05), ("钻石", 6e3, .015)], "岩浆囊：碰到会连锁喷发", "熔岩巨蛇"),
    ("地幔过渡带", 410_000, 660_000, 240, "蓝绿", [("林伍德石", 1e4, .05), ("尖晶石", 1.8e4, .02)], "相变层：每 30 行岩块类型整体变化一次", "相变之灵"),
    ("下地幔", 660_000, 2_890_000, 260, "深红/橙", [("布里奇曼石", 3e4, .05), ("后钙钛矿", 5.5e4, .02)], "对流：整片地层缓慢横向流动", "地幔巨鲸"),
    ("外核", 2_890_000, 5_150_000, 260, "金/白", [("液态铁镍", 9e4, .05), ("地磁晶", 1.6e5, .02)], "液态：挖开的格子会被慢慢回填", "磁暴旋涡"),
    ("内核", 5_150_000, 6_371_000, 280, "白金", [("星核碎晶", 3e5, .05), ("地心之石", 6e5, .01)], "极高温：只有狂热时能造成全额伤害", "地核心脏"),
]

layers, row = [], 0
for i, (name, d0, d1, rows, color, ores, mech, boss) in enumerate(LAYERS_RAW):
    layers.append({
        "id": i + 1, "name": name, "depthStartM": d0, "depthEndM": d1,
        "rowStart": row, "rowEnd": row + rows, "rows": rows, "color": color,
        "ores": [{"name": o, "valueMult": m, "rate": r} for o, m, r in ores],
        "mechanic": mech,
        "boss": {"name": boss, "hpMult": 400 * (1.15 ** i), "timeLimitSec": 60},
    })
    row += rows
TOTAL_ROWS = row

MATERIALS = ["木", "石", "铜", "铁", "钢", "银", "金", "水晶", "钻石", "秘银", "黑曜", "星核"]
TIER_DMG_MULT = 2.0
FAMILIES = [
    ("std",   "标准钻", 1.0, 3.0, "均衡，没有特殊效果", 1, 6),
    ("swift", "疾风钻", 0.45, 8.0, "攻速快，连击积累 ×2", 2, 2.0e3),
    ("heavy", "重型钻", 4.5, 1.0, "对硬岩和超硬岩伤害 ×3，对 Boss ×1.5", 3, 6.0e4),
    ("blast", "爆破钻", 0.9, 3.0, "击碎岩块时爆炸，对周围 1 格造成 50% 伤害", 4, 1.5e6),
    ("magnet", "磁力钻", 0.7, 3.0, "它击碎的矿物价值 ×2，并吸取 3 格内掉落", 5, 4.0e7),
]
BUY_GROWTH = 1.11

drills = {"materials": MATERIALS, "tierDamageMult": TIER_DMG_MULT, "buyPriceGrowth": BUY_GROWTH,
          "mergeRule": "两台同家族、同材质的钻头合成一台高一级材质；星核级不能再合成", "families": [], "kinds": []}
for fid, fname, dmg, hps, trait, unlock, price in FAMILIES:
    drills["families"].append({"id": fid, "name": fname, "damage": dmg, "hitsPerSec": hps, "trait": trait,
                               "unlockLayer": unlock, "basePrice": price})
    for t, mat in enumerate(MATERIALS):
        drills["kinds"].append({"id": f"{fid}_{t+1}", "family": fid, "tier": t + 1, "name": f"{mat}{fname}",
                                "damage": dmg * TIER_DMG_MULT ** t, "hitsPerSec": hps,
                                "dps": dmg * hps * TIER_DMG_MULT ** t})

EFFECTS = {
    "dmg_pct": "全部钻头伤害 +%", "speed_pct": "全部钻头攻速 +%", "crit_chance": "暴击率 +%",
    "crit_mult": "暴击伤害 +%", "hard_pct": "对硬岩伤害 +%", "boss_pct": "对 Boss 伤害 +%",
    "pierce": "溢出伤害传给下一格（层数）", "swarm_pct": "每拥有 10 台钻头，伤害 +%",
    "ore_pct": "矿物价值 +%", "gold_pct": "全部收入 +%", "offline_pct": "离线收益效率 +%",
    "offline_hours": "离线时长上限 +小时", "chest_pct": "宝箱出现率 +%", "chest_gold": "宝箱金币 +%",
    "tnt_rate": "炸药块出现率 +%", "blast_radius": "爆炸半径 +格", "blast_ore": "爆炸矿物 +%",
    "chain": "爆炸可连锁引爆炸药块", "blast_dmg": "爆炸伤害 +%",
    "buy_discount": "钻头价格 -%", "buy_tier": "购买的钻头直接从更高材质开始（级）",
    "merge_lucky": "合成时跳 2 级的概率 +%", "auto_merge": "机库自动合成", "auto_buy": "自动购买最便宜的钻头",
    "hangar_slots": "机库格子 +",
    "frenzy_time": "狂热时长 +秒", "frenzy_charge": "狂热充能速度 +%", "frenzy_mult": "狂热伤害倍率 +%",
    "combo_cap": "连击加成上限 +%", "combo_window": "连击判定时间 +秒", "frenzy_quake": "狂热结束时全屏地震",
    "layer_pct": "在本分支对应地层内伤害 +%", "rare_pct": "稀有物品掉率 +%", "shard_pct": "地核重铸碎片 +%",
    "start_gold": "重生后起始金币 ×", "click_pct": "点击伤害 +%",
}

BRANCHES = [
    ("drill", "钻头", [
        ("锋利钻尖", "dmg_pct", 10, 25, 1, 0), ("高速马达", "speed_pct", 4, 20, 1, 1), ("手动助力", "click_pct", 25, 20, 1, -1),
        ("合金钻头", "dmg_pct", 15, 20, 2, 0), ("精准打击", "crit_chance", 1, 20, 2, 1), ("致命一击", "crit_mult", 25, 20, 2, -1),
        ("硬岩克星", "hard_pct", 20, 15, 3, 0), ("涡轮增压", "speed_pct", 6, 15, 3, 1), ("穿透钻", "pierce", 1, 3, 3, -1),
        ("群钻共振", "swarm_pct", 2, 20, 4, 0), ("碎岩冲击", "dmg_pct", 25, 15, 4, 1), ("弱点洞察", "crit_chance", 1.5, 10, 4, -1),
        ("屠魔钻头", "boss_pct", 30, 15, 5, 0), ("超频马达", "speed_pct", 8, 10, 5, 1), ("致命一击 II", "crit_mult", 40, 15, 5, -1),
        ("钨钢钻芯", "dmg_pct", 40, 15, 6, 0), ("群钻共振 II", "swarm_pct", 3, 15, 6, 1), ("硬岩克星 II", "hard_pct", 40, 10, 6, -1),
        ("深层穿透", "pierce", 1, 2, 7, 0), ("精准打击 II", "crit_chance", 2, 10, 7, 1), ("屠魔钻头 II", "boss_pct", 50, 10, 7, -1),
        ("等离子钻芯", "dmg_pct", 60, 15, 8, 0), ("量子马达", "speed_pct", 10, 10, 8, 1), ("致命一击 III", "crit_mult", 60, 10, 8, -1),
        ("群钻共振 III", "swarm_pct", 5, 10, 9, 0), ("反物质钻芯", "dmg_pct", 100, 10, 9, 1), ("屠魔钻头 III", "boss_pct", 80, 10, 9, -1),
        ("星核钻芯", "dmg_pct", 150, 10, 10, 0), ("绝对暴击", "crit_chance", 3, 5, 10, 1), ("钻头风暴", "swarm_pct", 10, 5, 10, -1),
    ]),
    ("wealth", "财富", [
        ("矿物鉴定", "ore_pct", 10, 25, 1, 0), ("精打细算", "gold_pct", 5, 20, 1, 1), ("挂机矿工", "offline_pct", 10, 10, 1, -1),
        ("宝箱嗅觉", "chest_pct", 10, 15, 2, 0), ("矿物鉴定 II", "ore_pct", 15, 20, 2, 1), ("夜班", "offline_hours", 1, 6, 2, -1),
        ("宝箱丰收", "chest_gold", 50, 15, 3, 0), ("商业头脑", "gold_pct", 10, 15, 3, 1), ("挂机矿工 II", "offline_pct", 10, 5, 3, -1),
        ("宝石切割", "ore_pct", 25, 15, 4, 0), ("寻宝罗盘", "rare_pct", 10, 10, 4, 1), ("宝箱嗅觉 II", "chest_pct", 15, 10, 4, -1),
        ("矿业公司", "gold_pct", 15, 15, 5, 0), ("考古学家", "rare_pct", 15, 10, 5, 1), ("通宵", "offline_hours", 2, 4, 5, -1),
        ("宝石切割 II", "ore_pct", 40, 15, 6, 0), ("宝箱丰收 II", "chest_gold", 100, 10, 6, 1), ("矿业集团", "gold_pct", 25, 10, 6, -1),
        ("点石成金", "ore_pct", 60, 10, 7, 0), ("寻宝罗盘 II", "rare_pct", 20, 10, 7, 1), ("永不停工", "offline_pct", 20, 5, 7, -1),
        ("跨国矿业", "gold_pct", 40, 10, 8, 0), ("宝石切割 III", "ore_pct", 80, 10, 9, 0), ("矿业帝国", "gold_pct", 60, 10, 9, 1),
        ("地心财富", "ore_pct", 150, 10, 10, 0),
    ]),
    ("blast", "爆破", [
        ("炸药储备", "tnt_rate", 10, 20, 1, 0), ("大号炸药", "blast_radius", 1, 3, 1, 1), ("爆破收集", "blast_ore", 20, 15, 1, -1),
        ("连锁反应", "chain", 1, 1, 2, 0), ("烈性炸药", "blast_dmg", 25, 20, 2, 1), ("炸药储备 II", "tnt_rate", 10, 15, 2, -1),
        ("爆破收集 II", "blast_ore", 30, 15, 3, 0), ("冲击波", "blast_dmg", 30, 15, 3, 1), ("定向爆破", "hard_pct", 25, 10, 3, -1),
        ("集束炸药", "blast_radius", 1, 2, 4, 0), ("烈性炸药 II", "blast_dmg", 40, 15, 4, 1), ("爆破矿工", "blast_ore", 40, 10, 4, -1),
        ("炸药储备 III", "tnt_rate", 15, 10, 5, 0), ("震荡波", "blast_dmg", 60, 10, 5, 1), ("爆破拆迁", "boss_pct", 30, 10, 5, -1),
        ("热核炸药", "blast_dmg", 80, 10, 6, 0), ("爆破收集 III", "blast_ore", 60, 10, 6, 1), ("集束炸药 II", "blast_radius", 1, 1, 6, -1),
        ("地震炸弹", "blast_dmg", 100, 10, 7, 0), ("炸药工厂", "tnt_rate", 20, 10, 7, 1), ("裂变爆破", "blast_ore", 80, 10, 8, 0),
        ("聚变爆破", "blast_dmg", 150, 10, 8, 1), ("反物质炸弹", "blast_dmg", 250, 5, 9, 0), ("星爆", "blast_radius", 1, 1, 9, 1),
        ("核心爆破", "blast_ore", 200, 5, 10, 0),
    ]),
    ("hangar", "机库", [
        ("批量采购", "buy_discount", 2, 15, 1, 0), ("扩建机库", "hangar_slots", 4, 10, 1, 1), ("幸运焊工", "merge_lucky", 1, 10, 1, -1),
        ("自动合成", "auto_merge", 1, 1, 2, 0), ("批量采购 II", "buy_discount", 2, 10, 2, 1), ("石器时代", "buy_tier", 1, 1, 2, -1),
        ("扩建机库 II", "hangar_slots", 6, 10, 3, 0), ("幸运焊工 II", "merge_lucky", 1.5, 10, 3, 1), ("自动采购", "auto_buy", 1, 1, 3, -1),
        ("青铜时代", "buy_tier", 1, 1, 4, 0), ("群钻共振", "swarm_pct", 2, 15, 4, 1), ("批量采购 III", "buy_discount", 2, 10, 4, -1),
        ("扩建机库 III", "hangar_slots", 8, 10, 5, 0), ("铁器时代", "buy_tier", 1, 1, 5, 1), ("精密焊接", "dmg_pct", 20, 15, 5, -1),
        ("幸运焊工 III", "merge_lucky", 2, 10, 6, 0), ("钢铁时代", "buy_tier", 1, 1, 6, 1), ("流水线", "buy_discount", 3, 5, 6, -1),
        ("白银时代", "buy_tier", 1, 1, 7, 0), ("群钻共振 II", "swarm_pct", 3, 10, 7, 1), ("黄金时代", "buy_tier", 1, 1, 8, 0),
        ("扩建机库 IV", "hangar_slots", 10, 10, 8, 1), ("水晶时代", "buy_tier", 1, 1, 9, 0), ("完美焊接", "merge_lucky", 3, 5, 9, 1),
        ("钻石时代", "buy_tier", 1, 1, 10, 0),
    ]),
    ("frenzy", "狂热", [
        ("持久狂热", "frenzy_time", 1, 10, 1, 0), ("快速充能", "frenzy_charge", 10, 15, 1, 1), ("连击专家", "combo_window", 0.5, 4, 1, -1),
        ("狂热之力", "frenzy_mult", 20, 15, 2, 0), ("连击上限", "combo_cap", 20, 10, 2, 1), ("快速充能 II", "frenzy_charge", 10, 10, 2, -1),
        ("余震", "frenzy_quake", 1, 1, 3, 0), ("持久狂热 II", "frenzy_time", 1, 10, 3, 1), ("狂热之力 II", "frenzy_mult", 30, 10, 3, -1),
        ("连击上限 II", "combo_cap", 30, 10, 4, 0), ("点击狂人", "click_pct", 50, 10, 4, 1), ("快速充能 III", "frenzy_charge", 15, 10, 4, -1),
        ("狂热之力 III", "frenzy_mult", 50, 10, 5, 0), ("持久狂热 III", "frenzy_time", 2, 5, 6, 0), ("连击上限 III", "combo_cap", 50, 10, 6, 1),
        ("狂热之力 IV", "frenzy_mult", 80, 10, 7, 0), ("快速充能 IV", "frenzy_charge", 20, 5, 8, 0), ("狂热之力 V", "frenzy_mult", 120, 10, 9, 0),
        ("永恒连击", "combo_cap", 100, 5, 9, 1), ("永恒狂热", "frenzy_time", 5, 3, 10, 0),
    ]),
    ("strata", "地层", [
        ("表土专精", "layer_pct", 50, 10, 1, 0), ("砂岩专精", "layer_pct", 50, 10, 2, 0), ("溶洞专精", "layer_pct", 50, 10, 3, 0),
        ("暗河专精", "layer_pct", 50, 10, 4, 0), ("遗迹专精", "layer_pct", 50, 10, 5, 0), ("地壳专精", "layer_pct", 50, 10, 6, 0),
        ("莫霍专精", "layer_pct", 50, 10, 7, 0), ("地幔专精", "layer_pct", 50, 10, 8, 0), ("过渡带专精", "layer_pct", 50, 10, 9, 0),
        ("下地幔专精", "layer_pct", 50, 10, 10, 0), ("外核专精", "layer_pct", 50, 10, 10, 1), ("内核专精", "layer_pct", 50, 10, 10, -1),
        ("地质学家", "rare_pct", 10, 10, 2, 1), ("Boss 猎人", "boss_pct", 25, 15, 3, 1), ("重铸大师", "shard_pct", 10, 10, 4, 1),
        ("启动资金", "start_gold", 2, 10, 5, 1), ("地质学家 II", "rare_pct", 15, 10, 6, 1), ("Boss 猎人 II", "boss_pct", 40, 10, 7, 1),
        ("重铸大师 II", "shard_pct", 15, 10, 8, 1), ("启动资金 II", "start_gold", 3, 10, 9, 1), ("深渊凝视", "dmg_pct", 30, 10, 2, -1),
        ("深渊凝视 II", "dmg_pct", 50, 10, 4, -1), ("深渊凝视 III", "dmg_pct", 80, 10, 6, -1), ("深渊凝视 IV", "dmg_pct", 120, 10, 8, -1),
        ("深渊之主", "dmg_pct", 200, 10, 10, 2),
    ]),
]

RING_ROW = {k: int(TOTAL_ROWS * (k - 1) / 10) for k in range(1, 11)}
def ring_base_cost(k):
    r = RING_ROW[k]
    return 60.0 if k == 1 else 60 * val(r) * (6.0 ** (k - 1))

skills = []
skills.append({"id": "root", "name": "深渊钻头", "branch": "core", "effect": "dmg_pct", "perLevel": 5, "maxLevel": 10,
               "baseCost": 5, "costGrowth": 1.5, "prereq": [], "x": 0.0, "y": 0.0, "ring": 0,
               "requiresLayer": 1, "desc": "第 1 级：自动钻探，所有钻头自动挖掘；第 2 级起全部钻头伤害 +5%/级"})
for b, (bid, bname, nodes) in enumerate(BRANCHES):
    ang = math.radians(90 - 60 * b)
    ux, uy = math.cos(ang), math.sin(ang)
    px, py = -uy, ux
    trunk = {}
    placed = {}
    for i, (name, eff, per, mx, ring, off) in enumerate(nodes):
        nid = f"{bid}_{i+1:02d}"
        rad = 4 + 3 * ring
        th = ang - math.radians(off * (22 - (ring - 1) * 1.4))
        x = round(rad * math.cos(th), 2)
        y = round(rad * math.sin(th), 2)
        if off == 0 and ring not in trunk:
            trunk[ring] = nid
        placed[nid] = (ring, off)
        growth = 1.33 if mx >= 15 else 1.43 if mx >= 8 else 1.75 if mx > 1 else 1.0
        layer_req = min(12, 1 + (ring - 1) * 11 // 9)
        node = {"id": nid, "name": name, "branch": bid, "effect": eff, "perLevel": per, "maxLevel": mx,
                "baseCost": float(f"{ring_base_cost(ring) * (1.0 if off == 0 else 0.7):.3g}"),
                "costGrowth": growth, "x": x, "y": y, "ring": ring, "requiresLayer": layer_req,
                "desc": EFFECTS[eff].replace("%", f"{per}%") if "%" in EFFECTS[eff] else f"{EFFECTS[eff]} {per}/级"}
        if eff == "layer_pct":
            node["layer"] = ["表土", "砂岩", "溶洞", "暗河", "遗迹", "地壳", "莫霍", "地幔", "过渡带", "下地幔", "外核", "内核"].index(name.replace("专精", "")) + 1
        skills.append(node)
    for n in skills:
        if n.get("branch") != bid:
            continue
        ring, off = placed[n["id"]]
        if off == 0 and trunk.get(ring) == n["id"]:
            prev = [trunk[r] for r in sorted(trunk) if r < ring]
            n["prereq"] = [prev[-1]] if prev else ["root"]
        else:
            same = trunk.get(ring)
            if same and same != n["id"]:
                n["prereq"] = [same]
            else:
                prev = [trunk[r] for r in sorted(trunk) if r < ring]
                n["prereq"] = [prev[-1]] if prev else ["root"]

assert len(skills) == 151, len(skills)

ach = []
for L in layers:
    ach.append({"id": f"reach_{L['id']}", "name": f"抵达{L['name']}", "cond": f"首次到达{L['name']}", "reward": "全部收入 +2%"})
    ach.append({"id": f"boss_{L['id']}", "name": f"击败{L['boss']['name']}", "cond": f"打败{L['name']}的守层 Boss", "reward": "全部伤害 +3%"})
for n, nm in ((10, "小队"), (50, "车队"), (200, "军团"), (1000, "钻头海")):
    ach.append({"id": f"drills_{n}", "name": f"钻头{nm}", "cond": f"同时拥有 {n} 台钻头", "reward": "钻头价格 -2%"})
for t in (3, 6, 9, 12):
    ach.append({"id": f"tier_{t}", "name": f"{MATERIALS[t-1]}匠", "cond": f"合成出{MATERIALS[t-1]}级钻头", "reward": "合成跳级概率 +1%"})
for n in (100, 1000, 10000):
    ach.append({"id": f"merge_{n}", "name": f"焊工 {n}", "cond": f"累计合成 {n} 次", "reward": "全部伤害 +2%"})
for n in (50, 200, 500):
    ach.append({"id": f"combo_{n}", "name": f"{n} 连击", "cond": f"达成 {n} 连击", "reward": "连击上限 +10%"})
for n in (1, 5, 25, 100):
    ach.append({"id": f"rebirth_{n}", "name": f"重铸 {n} 次", "cond": f"完成 {n} 次地核重铸", "reward": "碎片 +5%"})
for n in (10, 16, 24):
    ach.append({"id": f"chain_{n}", "name": f"连锁 {n}", "cond": f"一次连锁引爆 {n} 个炸药块", "reward": "爆炸伤害 +10%"})
ach.append({"id": "center", "name": "地心旅行", "cond": "到达 6371 千米的地心", "reward": "解锁星系跃迁"})

RARES = [
    ["三叶虫化石", "古老树根", "迷路的鼹鼠玩具"], ["沙漠玫瑰石", "沙虫牙", "埋藏的怀表"], ["发光钟乳石", "洞穴壁画碎片", "紫晶晶洞"],
    ["盲鱼鳞片", "沉船罗盘", "暗河珍珠母"], ["古城钥匙", "黄金面具", "失落的石板"], ["花岗岩球", "锆石晶粒", "地壳地图"],
    ["莫霍面样本", "玄武岩柱", "铂金徽章"], ["橄榄石原石", "金伯利岩管", "地幔羽碎片"], ["林伍德石晶体", "水之结晶", "相变核心"],
    ["布里奇曼石", "地幔鲸骨", "超深钻石"], ["磁极石", "液态金属珠", "地磁罗盘"], ["地心之石", "星核碎晶", "地球心跳"],
]
rare_items = []
for i, names in enumerate(RARES):
    for j, nm in enumerate(names):
        rare_items.append({"id": f"rare_{i+1}_{j+1}", "name": nm, "layer": i + 1, "rarity": ["稀有", "史诗", "传说"][j],
                           "dropChance": [0.02, 0.006, 0.0015][j], "bonus": ["全部伤害 +1%", "矿物价值 +3%", "全部收入 +5%"][j],
                           "dupeBonus": "重复获得时该加成再 +50%（最多 10 次）"})

def milestone(kind, amount, desc):
    return {"kind": kind, "amount": amount, "desc": desc}

def plain_rewards(name):
    return [milestone("damage", 15, f"{name}伤害 +15%"), milestone("speed", 10, f"{name}攻速 +10%"),
            milestone("crit", 2, "全部钻头暴击率 +2%"), milestone("damage", 30, f"{name}伤害 +30%")]

milestones = {
    "familyCounts": [10, 25, 50, 100, 200],
    "families": [
        {"id": "std", "rewards": plain_rewards("标准钻") + [milestone("damage", 100, "标准钻伤害 +100%")]},
        {"id": "swift", "rewards": [milestone("speed", 10, "疾风钻攻速 +10%"), milestone("damage", 15, "疾风钻伤害 +15%"),
                                    milestone("crit", 2, "全部钻头暴击率 +2%"), milestone("speed", 20, "疾风钻攻速 +20%"),
                                    milestone("trait", 2, "特性翻倍：连击积累 ×4")]},
        {"id": "heavy", "rewards": plain_rewards("重型钻") + [milestone("trait", 2, "特性翻倍：对硬岩 ×6，对 Boss ×3")]},
        {"id": "blast", "rewards": plain_rewards("爆破钻") + [milestone("trait", 2, "特性翻倍：爆炸溅射 100% 伤害")]},
        {"id": "magnet", "rewards": plain_rewards("磁力钻") + [milestone("trait", 2, "特性翻倍：矿物价值 ×4")]},
    ],
    "mastery": {"blocks": [150, 400, 900, 1800, 3500, 6500, 12000, 22000, 40000, 70000], "damagePct": 5, "valuePct": 5},
}

for name, data in (("layers", layers), ("drills", drills), ("skills", skills),
                   ("achievements", ach), ("rare_items", rare_items), ("milestones", milestones)):
    with open(os.path.join(OUT, f"{name}.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)

print("layers", len(layers), "rows", TOTAL_ROWS, "drill kinds", len(drills["kinds"]),
      "skills", len(skills) - 1, "achievements", len(ach), "rares", len(rare_items))
