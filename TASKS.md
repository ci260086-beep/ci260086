# 《深渊钻头》任务分工

两个 Claude 在同一台电脑上一起开发时，按这里的规则分工。详细现状看 `HANDOFF.md`。交流在 `comms` 分支的 `COMMS.md`。

## 分工

| | 文件夹 | 负责 | Unity | 分支 |
|---|---|---|---|---|
| Claude A | `C:\GAME\DeepDril` | 要跑 Unity 的工作：功能、测试、性能、合并 | 可以打开 | `master` 或功能分支 |
| Claude B | `C:\GAME\DeepDril2` | 不用开 Unity 的工作：`art\tools` 里的 Aseprite 脚本美术、数值数据、文档 | 不打开 | `art/*` 或 `data/*` |

Claude B 在自己的分支上提交并推送，由 Claude A 合并和验证。Claude B 不合并到 `master`。

## 开工流程

1. 先运行 `git pull`，再读 `HANDOFF.md` 和本文件。
2. 在下面的任务表里认领任务：Claude B 领“认领人是 B、状态是待做”的第一项，把认领人改成 Claude B，写上分支，状态改成进行中，提交并推送，再开始做。已经进行中的任务不要动。
3. 做完后更新任务状态和 `HANDOFF.md`，再推送。

状态只用这四种：待做、进行中、待验证、完成。Claude B 做完的任务写成“待验证”，由 Claude A 合并并验证后改成“完成”。

## 任务表

计划来源：第四阶段计划 https://claude.ai/artifact/CKUBNxSWKeDQZEUQSTgwkF

| 任务 | 负责 | 认领人 | 分支 | 状态 |
|---|---|---|---|---|
| 12 个 Boss 的独立造型 | B | Claude B | `art/boss-designs` | 完成 |
| 美术 1：Boss 的 4 组动画 | B | B |  | 待做 |
| 美术 2：第三阶段系统图标 | B | Claude B | `art/system-icons` | 完成 |
| 美术 2b：补结晶、自动化、地核不稳 3 张系统图标 | B | B | | 待做 |
| 美术 3：46 个成就图标 | B | Claude B | `art/collection-icons` | 完成 |
| 美术 4：技能效果图标 | B | Claude B | `art/skill-icons` | 完成 |
| 美术 5：12 层专属装饰和岩块变体 | B | B |  | 待做 |
| 美术 6：36 件稀有物品 | B | Claude B | `art/collection-icons` | 完成 |
| 美术 6b：稀有物品按稀有、史诗、传说加边框或光效 | B | B | | 待做 |
| 美术 7：钻头家族造型加强 | B | B |  | 待做 |
| 性能和手感：最慢一帧、磁力钻吸取、技能树收尾、8 小时挂机 | A | Claude A | `phase4-perf` | 完成 |
| 新手引导 | A | | | 待做 |
| 设置菜单和新游戏按钮 | A | | | 待做 |
| 接入成就、技能、系统图标 | A | Claude A | `phase4-icons` | 进行中 |
| 接入 Boss 动画和其余美术 | A | | | 待做 |
| 像素字体：换成开源中文像素字体，比如缝合像素体 | A | | | 待做 |
| 美术 8：游戏图标和 Steam 商店封面 | B | B | | 待做 |

合并 Claude B 的分支时，`TASKS.md` 里重复的条目合成一条。

## 美术任务说明

Claude B 按美术 1 到 8 的顺序做。**一次只做一项**，每项开一个 `art/*` 分支，比如 `art/boss-anim`，做完推送并把状态改成“待验证”，再开下一项。

共同要求：

- 全部写成 `art\tools` 里的 Aseprite Lua 脚本，不手工改图片。美术 1 到 8 的新内容建议放进新脚本 `art\tools\draw_phase4_art.lua`，不容易和别的修改冲突。
- 不改 `Assets\Scripts`。图片怎么进游戏由 Claude A 负责，B 只要按下面的文件名和尺寸输出。
- 不用提交 `.meta`，Claude A 在 Unity 里导入时生成。
- 色板沿用 `draw_world_art.lua`：阴影偏蓝紫，亮部偏暖黄，透明背景，带深色描边。
- 每项都输出一张总览图到 `art\`，方便用户看效果，比如 `art\boss_anim_preview.png`。
- 不要整个重跑 `draw_game_art.lua`：它会改动 `ore_coal`、`ore_silver` 和它们的 `_sand` 四张贴图。如果跑了，提交前要还原。
- `Aseprite.exe` 启动后马上返回，在 PowerShell 里用 `Start-Process -Wait`。

| 项 | 内容 | 输出文件 | 尺寸 |
|---|---|---|---|
| 美术 1 | 12 个 Boss 的动画，造型沿用 `boss_face_*`。待机 2 帧，受击 2 帧，放技能 4 帧，死亡 4 帧，横向排成一条，共 12 帧 | `Assets/Art/Layers/boss_anim_0.png` 到 `boss_anim_11.png` | 每帧 64×32，整条 768×32 |
| 美术 2 | 第三阶段系统各一张：工厂、结晶、研究所、探险队、挑战、星座、流派、自动化、黄金事件、地核不稳 | `Assets/Art/Items/sys_factory.png` 这样的名字 | 16×16 |
| 美术 3 | 46 个成就各一张。抵达地层用该层的岩块或矿物，Boss 用缩小的 Boss 头像，钻头数量、合成、连击、重铸、连锁各有一套图形，地心单独设计。再加铜、银、金边框表示难度。图形要能当剪影看：没拿到时游戏把它染成黑色 | `Assets/Art/Items/ach_reach_1.png` 这样的名字，后半段就是成就 id | 16×16 |
| 美术 4 | 按效果类型各一张，比如伤害、攻速、暴击、金币、矿物、宝箱、炸药、爆炸范围、机库、狂热、连击、离线、碎片。白色图形，游戏负责染色。效果列表见 `gen_design_data.py` 的 `EFFECTS` | `Assets/Art/Items/ticon_dmg_pct.png` 这样的名字，后半段就是效果名 | 7×7 |
| 美术 5 | 12 层每层 4 个专属装饰，比如溶洞晶簇、遗迹石柱、岩浆气泡。每层再加 3 个岩块纹理变体 | `Assets/Art/Layers/decal_layer_0_0.png`、`rock_layer_0_0.png` 这样的名字，前一个数字是地层，后一个是序号 | 16×16 |
| 美术 6 | 36 件稀有物品按名字重画，名字见 `gen_design_data.py` 的 `RARES`。稀有、史诗、传说三档用不同的边框或光效 | 沿用 `Assets/Art/Items/rare_1_1.png` 这些文件名 | 16×16 |
| 美术 7 | 5 个家族的轮廓再拉开差距，同时保留现在 12 级材质的配色、边条、宝石和发光规则。配色不要改，`Palette.Tier` 里有一份相同的颜色 | 沿用 `Assets/Art/Drill/drill_0_0.png` 这些文件名 | 每帧 8×12，4 帧 |
| 美术 8 | 游戏图标，和 Steam 商店封面。5 张商店截图要在 Unity 里截，由 Claude A 提供。风格先出两三种方案给用户选 | `art/store/icon.png`、`art/store/capsule.png` | 图标 512×512，封面 616×353 |
