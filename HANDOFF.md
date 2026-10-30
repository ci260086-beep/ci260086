# 《深渊钻头》交接文档

## 最新现状（2026-10-31，先读这一节）

每次提交都要同步更新这一节并推送到 `origin`。旧项目（`C:\GAME\DARKLATOR` 等）一律不碰，只动 `C:\GAME\DeepDril`。

- **仓库：** https://github.com/ci260086-beep/ci260086 ，主分支是 `master`。不要 force push。合并进 `master` 前先等用户确认。
- **分支：** `master`（`de825d3`，第一版）；`v2`（`8f3b4c6`，第三阶段第 0 步）；`phase3-step2`（从 `v2` 分出，正在做第 2 步）；`terrain-pixel`（`e7d03fb`，已包含在 `v2` 里，检出在 `Builds/wtp`）；`wip-v2`（旧存档分支）。
- **第三阶段进度：** 计划在 `tools/Design/phase3_plan.md`。用户决定在 `phase3-step2` 这一条分支上把第 1 到第 8 步全部做完，每步只跑编译和单元测试，最后统一做完整测试和节奏调参。第 0、2、1、3、4、5、6、7、8 步已提交。第 2 步：`milestones.json`、`GameSim.MilestonesOf/MasteryOf`、`HudView.Mastery.cs`（图鉴的“里程碑”“精通”标签页，未实机看过）。
- **第四阶段计划：** https://claude.ai/artifact/CKUBNxSWKeDQZEUQSTgwkF
- **用户已定：** 横屏 320×180；逐像素地形；手动起步（解锁自动钻探前只能点击）；定价 6 元人民币；不做广告和付费加速。
- **节奏目标：** 第一次重铸约 30 到 45 分钟；第 1 局到第 4 到 5 层；之后每层约 8 到 15 分钟；10 到 15 局到地心。
- **代码结构：** 规则在 `Assets/Scripts/Core`（`GameSim.cs`、`GameSim.Pixels.cs`、`PixelWorld.cs`、`WorldGen.cs`、`DrillBody.cs`、`Balance.cs`、`GameData.cs`、`GameState.cs`），不能引用 UnityEngine。表现层在 `Assets/Scripts/Game`（`HudView*.cs` 是全部 UI）。测试在 `Assets/Tests/EditMode`（`CoreTests.cs`、`RuleTests.cs`、`PacingTests.cs`）。下面第 1 节的表写于第一版，以这里为准。
- **数据：** `Assets/Resources/Data/*.json`，由 `tools/Design/gen_design_data.py` 生成，改数值改这个脚本，运行方法见下一节。`GameData.Load` 按文件名读取。
- **编译和测试：** Unity 没开时，运行 `tools\check.ps1 -Stage 名字`（先跑 EditMode 测试，再试玩截图）。Unity 开着时用工程外命令：`dotnet build tools\outside\Check.csproj` 检查 Core 和 Game 能否编译；`dotnet build tools\outside\Run.csproj -p:NUnitDll=<nunit.framework.dll 路径>` 后运行 `tools\outside\out\run\Run.exe` 跑测试。跑 Unity 前先确认用户没开 Play 模式。列表里的 `Unity Hub\...\unity.exe serve` 是 Hub，不是编辑器，要看 `Temp\UnityLockfile`。
- **节奏模拟：** `dotnet build tools\outside\Pace.csproj -c Release`，再运行 `tools\outside\out\pace\Pace.exe Assets\Resources\Data [模拟小时] [真实秒数上限] [种子]`。
- **已知问题：** 第 3 至 12 层的特殊机制只有测试，没有实机看过；Windows 打包未解决（见下一节）；磁力钻的“吸取 3 格内掉落”没做；节奏调参留到全部做完后统一进行。
- **第 1 步：** 黄金事件和地核不稳：`GameSim.Events.cs`、`HudView.Events.cs`，数值在 `phase3.json` 的 events，未实机看过。
- **第 3 步：** 重铸里程碑和自动化规则：`GameSim.Meta.cs`；基地页 `HudView.Base.cs`（标签页加行的通用框架，后面各系统都放这里）；永久加成统一走 `AddPermanent`。
- **第 4 步：** 钻探流派：`GameSim.Schools.cs`，每局选一派，重铸后重置，通关该派给永久奖励，技能价格走 `SkillCost`。
- **第 5 步：** 地表加工厂和地心结晶：`GameSim.Factory.cs`，结晶按真实时间生长，离线也算，工厂花矿物，等级跨重铸保留。
- **第 6 步：** 研究所和探险队：`GameSim.Lab.cs`，同时只能研究一项，结晶可加速，探险员来自远古遗迹宝箱，失败也会回来。
- **第 7 步：** 挑战深井：`GameSim.Challenges.cs`，五条规则，到达目标深度给永久奖励；重置逻辑抽成 `ResetRun`。
- **第 8 步：** 星系跃迁、星座图和行星规则：`GameSim.Warp.cs`，五颗行星按跃迁次数轮换，规则都在 `phase3.json` 的 planets，星点买星座，界面在基地页。
- **节奏现状（调到一半）：** 种子 7、11、23、42 的第一次重铸在 21 到 36 分钟，第 1 局到第 4 层，第 6 层在 24 到 38 分钟，约 18 局以上才接近地心，HpGrowthDeep 1.012 尚未验证。HpRamp 到 4.5 以上时第一局会卡在第 2 层 Boss。旋钮：`Balance.HpRamp/HpGrowthDeep/OreValuePower/ShardBonus/BossHpScale`，生成器里的 `TIER_DMG_MULT`、`BUY_GROWTH`、圈价倍数和技能价格增长率。
- **本阶段修过的旧 bug：** 连锁炸药一帧炸穿全图（现在限 24 次、24 行内）；爆炸无视血量（现在按伤害预算，`blast_dmg` 生效）；钻头悬空导致 Boss 不触发；爆破钻溅射没实现。

## 第二版现状（`v2` 分支）

主文件夹当前检出 `v2`（`bb14124` 之后）。下面第 1 至 9 节写于第一版，结构和流程仍有参考价值，第二版有差异的地方以本节为准。

- **数据：** `tools/Design/gen_design_data.py` 生成 `Assets/Resources/Data/*.json`，改数值改这个脚本。这台电脑的 `python` 是商店占位程序，用 Blender 自带的 Python 运行：`"C:\Program Files (x86)\Steam\steamapps\common\Blender\5.2\python\bin\python.exe" -I gen_design_data.py ..\..\Assets\Resources\Data`。
- **核心：** `MiniJson` + `GameData` 读数据，`GameSim` 管多钻头，`DrillBody` 是单台物理。`Strata`、`Skills` 等第一版的类已删除。
- **美术：** `art/tools/draw_world_art.lua` 生成 12 层、24 矿物、60 种钻头、36 稀有物品，输出到 `Assets/Art/Layers`、`Drill`、`Items`。`draw_game_art.lua` 仍负责炸药、裂纹、图标。
- **已在编辑器实测：** 200 台钻头 60 FPS（最差一帧约 24 毫秒）、机库、技能树、图鉴的显示，12 层画面，存档读回。Unity 内 58 个测试全过。
- **只有测试、没看过实际效果：** 第 3 至 12 层的特殊机制（水晶反射、水流、遗迹房间、超硬岩、过热、岩浆、相变、对流、回填、地核高温）。
- **没验证：** 真实鼠标的拖拽合成、拖动和缩放、悬停；正常数值下的难度曲线。
- **未完成：** 各层 Boss 只有两张脸循环加染色，没有 12 个独立外观；设计里的“吸取 3 格内掉落”（磁力钻）没做；`center` 成就的“星系跃迁”没有内容。
- **我定的：** 超过 300 台的钻头不做物理，威力计入 `SwarmMultiplier`；`hangar_slots` 暂时提高这个上限；Boss 护甲从第 2 层起占血量 25%；各机制的具体数值在 `Balance` 里。
- **脚本坑：** 不要用 `Select-Object -First` 接 `check.ps1` 的输出，管道提前结束会中止脚本，后面的截图复制不会执行。
- **`master` 的小问题：** 砂岩相关素材提交时没有 `.meta`，Unity 打开时会生成，切分支前要清掉。

- **开局与表现（后续新增）：** 解锁自动钻探前全靠点击（`GameSim.AutoDrillUnlocked`，根节点第 1 级）。打击感在 `FxView`、`SoundBank`、`GameSettings`，色板公式同时存在于 `draw_world_art.lua` 和 `Palette.cs`，改一边要改另一边。
- **没用 URP：** 光照是贴图叠加（`HudView.BuildDarkness`、`WorldView` 的钻头光晕），不是 2D Light；像素对齐靠 320×180 的 RenderTexture，没有 Pixel Perfect Camera。要换 URP 需要改渲染管线和所有材质。
- **清单里没做的：** 背景音乐和狂热提速（项目里还没有 BGM）、合成时两台钻头飞到一起的动画（现在是闪光加火花）、技能节点“充能填满”（现在是弹跳加光点）、每种岩块 3–4 个变体（现在是地面 3 个变体加贴花）、爆炸照亮周围（现在是白闪）。
- **待查：** `v2_layer_03.png` 里隧道背景偏棕，不是该层的紫色；那张图处于狂热状态，原因没查清。

**Windows 打包（第 7 步）未解决，原因已基本定位：** 卡住的 `cmd.exe` 主线程状态是 `Suspended`（1 线程、3 句柄、CPU 0、无子进程），进程启动后就被冻结。这台电脑装有 Avast 和 Reason Cybersecurity，高度怀疑是它们拦截，比第 4 节的“令牌过期”更可信，但还没用排除规则验证过。下一步：让用户把 `C:\GAME\DeepDril` 和 `C:\Program Files\Unity\Hub\Editor\6000.0.85f1` 加入杀毒软件排除项，再点 DeepDrill > Build Windows。

写于 2026-10-30。工程在 `C:\GAME\DeepDril`（文件夹名就是一个 l，已确认不改）。
Unity 6000.0.85f1，内置渲染管线，uGUI，旧 Input Manager。git 主分支叫 `master`，不是 `main`。

先读这三条，它们都踩过：

- **用户开着 Unity 时，`tools\check.ps1` 跑不了。** 第二个 Unity 实例会报 "another Unity instance is running" 退出，而 `Logs\tests.xml` 还是旧文件，脚本会打印旧的“全部通过”。看结果前先核对 `tests.xml` 的时间。这时改用第 2 节的工程外命令。
- **工程外编译和测试不等于实机验证。** 第 3 节标了哪些功能只到这一步。
- **打包失败可能留下孤儿 `bee_backend.exe`。** 它会让之后的打包失败，也会让用户编辑器报 "Tundra build failed due to an internal error"。按 PID 结束，结束前先核对命令行和父进程。范围大的批量结束命令会被权限系统拦下。

## 1. 工程结构

玩法规则全部在 `DeepDrill.Core`，这个程序集设了 `noEngineReferences: true`，不能引用 UnityEngine。表现层调用 `GameSim.Tick` 和 `Click`，再读取 `GameSim.Events` 来绘制，核心不知道画面存在。场景是空的，所有对象都由代码创建。

| 程序集 | 目录 | 说明 |
|---|---|---|
| `DeepDrill.Core` | `Assets/Scripts/Core` | 纯 C# 规则，可在 Unity 外编译和测试 |
| `DeepDrill.Game` | `Assets/Scripts/Game` | MonoBehaviour 表现层，引用 Core 和 UnityEngine.UI |
| `DeepDrill.Editor` | `Assets/Scripts/Editor` | 只在编辑器中：导入规则、工程设置、打包入口 |
| `DeepDrill.Tests` | `Assets/Tests/EditMode` | EditMode 测试，全部在 `CoreTests.cs` |

Core：

| 文件 | 职责 |
|---|---|
| `Balance.cs` | 所有可调数值和公式；`BossDef` 和 `Bosses` 表 |
| `NumberFormat.cs` | 大数字显示（K/M/B/T，1e15 起科学计数） |
| `WorldGen.cs` | `BlockType`、`Block`，按种子确定性生成地层 |
| `GameState.cs` | 存档数据，字段保持 public 供 JsonUtility 使用；`Upgrade()` 做版本迁移 |
| `Skills.cs` | `SkillDef`、`SkillCatalog`（目前三棵树共 12 个技能） |
| `DrillBody.cs` | 钻头物理：重力、双脚支撑、阻尼弹簧倾斜、横向碰撞 |
| `GameSim.cs` | 全部规则：挖掘、连击、狂热、炸药、沙块、Boss、重生、离线；事件队列 |

Game：

| 文件 | 职责 |
|---|---|
| `GameRoot.cs` | 启动入口（`RuntimeInitializeOnLoadMethod`）、存档计时、把事件分发给各视图 |
| `Stage.cs` | 320×180 舞台常量和坐标换算，整数倍放大 |
| `WorldView.cs` | 地面、边缘、裂纹、钻头、Boss 脸、摄像机跟随和震屏 |
| `FxView.cs` | 碎屑、爆炸、飞向顶部的矿物、飘字 |
| `HudView.cs` | 全部 UI：浮层、图标列、卡片面板、狂热条、Boss 血条、提示 |
| `Ui.cs` | uGUI 构建小工具 |
| `ArtLibrary.cs` | 名字到 Sprite 的查表（`Resources/ArtLibrary.asset`），切横条帧 |
| `SaveService.cs` | 读写存档，先写临时文件再替换 |
| `DemoDriver.cs` | 只在 `--shots` 参数下启动：自动试玩、截图、校验存档后退出 |

Editor：

| 文件 | 职责 |
|---|---|
| `ArtImportRules.cs` | `Assets/Art` 下图片统一 Point 过滤、无压缩、16 PPU；新素材导入后自动刷新 ArtLibrary |
| `ProjectSetup.cs` | 菜单命令、打包入口、`PlayAndCapture` |

世界坐标：格子 (row, col) 在世界坐标 (col, -row)。核心里 Y 向下为正。

## 2. 怎么运行

打开：用 Unity Hub 打开 `C:\GAME\DeepDril`，打开 `Assets/Scenes/Main.unity`，点 Play。空场景也能跑，游戏对象是启动时创建的。

菜单 DeepDrill：

- **Setup Project**：重新导入 `Assets/Art`、重建 ArtLibrary、创建空场景、写入窗口和产品设置。Play 时报找不到素材，先点这个。
- **Build Windows**：打包到 `Builds/Windows/DeepDrill.exe`。目前失败，见第 4 节。

用户存档：`C:\Users\Asus\AppData\LocalLow\DeepDrill\Deep Drill\save.json`。自动试玩用 `--save` 指向别处，不碰这份存档。

工程外验证（不需要关 Unity，已实跑；工作目录是工程根目录）：

```powershell
dotnet build tools\outside\Check.csproj -nologo -v q
```

```powershell
$nunit = (Get-ChildItem Library\PackageCache -Recurse -Filter nunit.framework.dll | Select-Object -First 1).FullName
dotnet build tools\outside\Run.csproj -nologo -v q "-p:NUnitDll=$nunit"
tools\outside\out\run\Run.exe
```

`Check.csproj` 用 Unity 的 DLL 编译 Core 和 Game，不含 Editor 程序集。`Run.csproj` 编译 Core 和测试，由 `TestRunner.cs` 反射运行。两者都依赖 `Library`，需要工程至少被 Unity 打开过一次。当前结果：`total=59 passed=59 failed=0`。

完整验证（必须先关掉 Unity）：

```powershell
tools\check.ps1 -Stage 名字
```

它先跑 EditMode 测试，再启动图形界面编辑器进入 Play，`DemoDriver` 自动试玩，把截图以 `名字_xxx.png` 存到 `Screenshots`，并打印 `[demo]` 日志。Unity 窗口会在屏幕上出现几十秒。

注意：批处理模式下 `DemoDriver` 不能用，它依赖 Game 视图截图。

## 3. 功能清单

**编辑器实测过**（通过 `check.ps1` 实际运行并看过截图，分辨率 1280×720）：

- 钻头自动挖掘、点击伤害、3 段裂纹、碎屑、震屏、矿物飞向 UI 后入账、飘字
- 金币和矿物、三棵技能树的购买
- JSON 存档写入和读回、离线收益计算（这项是算出来的，不是真的离线等过）
- 连击、暴击、狂热、炸药连锁
- 横屏 320×180，摄像机跟随，40 列宽世界
- 钻头物理：向左倾斜滑入空洞、下落、落地，隧道拐弯
- 参考图布局：资源浮层、卡片面板、提示大字、版本号和 FPS
- Boss「巨型木根」和地核重铸的完整流程。**这段试玩把伤害和钻速调满并直接跳到 98 米，只验证流程，不代表真实难度。**

**只编译和测试过，没在游戏里看过：**

- 第 2 地层「砂岩」全部内容：地面、过渡、背景渐变、「进入砂岩层」提示、铁和银
- 沙块塌落（逻辑有测试，观感没看过）
- 岩甲虫和甲壳机制
- 图标列悬浮并分上下两组（`45ea5b7`）
- ArtLibrary 自动刷新
- 重生时不误弹“解锁新技能”的修复

**从未验证：**

- 正常数值下从 0 到 100 米的节奏和 Boss 难度
- 1920×1080（代码按整数倍放大，没跑过）
- 打包后的程序（一次也没成功打出来）

## 4. 已知问题

**Windows 打包失败（未解决）**

- 批处理模式：脚本编译成功后卡在 `BuildPlayerDataGenerator` 这一步。`bee_backend` 的子进程 `cmd.exe` 不退出，没有子进程，也没有输出，日志不再更新。`-nographics` 和不带它都一样。
- 图形界面模式：停在同一步后崩溃，退出码 `0xC0000005`，没有 `Build result` 日志。
- 试过：单独运行 `BuildPlayerDataGenerator.exe`，几秒成功，所以不是它本身的问题，也不是游戏代码的问题。清掉孤儿进程后重试，结果一样。
- 线索：`Logs\build4.log` 有 `Access token is unavailable`。**怀疑** Unity 账号令牌过期，没有证实。下一步是让用户在 Unity Hub 重新登录再试。

**其他**

- 中文字体用系统字体（微软雅黑优先），不是像素字体，也没有打进包里，换系统可能显示不同。
- 技能卡图标每棵树共用一张。
- 第 2 地层没有实机验证；`ef7a351` 之后用户开着 Unity，素材可能还没导入。Play 报 `KeyNotFoundException` 时点 Setup Project。
- 重生误弹提示的修复没有实机验证。
- 200 米之后仍是砂岩，master 上没有第 3 地层。
- JsonUtility 写 double 时末位会舍入（`18.982777777777766` 读回 `18.98277777777777`）。这不是丢数据，比较时要用容差，`DemoDriver.Close` 就是这么做的。
- `art/` 下保留了早期的 32×48 钻头和 `scene_mock`，游戏没有使用，是备选。
- `Assets/Art/Tiles/tunnel_bg.png`、`ore_iron.png`、`ore_silver.png`（泥土底）目前没有被使用。

## 5. 改过的设计决定

| 决定 | 原因 |
|---|---|
| 钻头每击同时打前方三格，基础频率 3 次/秒 | 逐块打时总是左边先空，钻头一路向左偏。同时打让匀质地面走直线，只有硬岩这类一侧留下时才拐弯 |
| 暴击每次攻击判定一次，不按格判定 | 一侧提前碎掉会让钻头无故倾斜 |
| 连击加成上限 +100% | 不设上限时试玩 44 秒到 315 米，经济失控 |
| 狂热：速度 ×5，伤害 ×2 | 设计只写了速度，总伤害公式又有“狂热倍率”，伤害 ×2 是我定的 |
| 狂热充能 0.008/次击破（约 40 米一次） | 原先 0.02 十几秒就充满 |
| 连击 2 起才开始充能 | “连击攒满能量条” |
| 暴击 10%，×3 | 设计未给，我定的 |
| 基础伤害 4，泥土价值 0.5 | 开局太慢，第一次升级要等 30 秒以上 |
| 双钻头改成每次打两轮 | 原来的“打两块”在三格同时打之后没有意义 |
| 炸药基础连锁上限 2 | 设计说“可连锁”，技能树又有“连锁”，所以基础给 2，技能加上限 |
| Boss 超时后回满，可重打，不惩罚 | 设计未写失败后果 |
| Boss 血量 = 当层 `BlockHp` ×60 | 设计未给 |
| 护甲吸收完整伤害，同时只让 50% 伤害打到血量；护甲 = 血量 25% | 设计只写“护甲期间伤害减半” |
| 重生同时清 `maxDepth` 和 `totalIncome` | 否则同样的深度和收入可以重复换碎片 |
| 技能卡有“已解锁/新技能”分组，重铸要连点两次确认 | 重铸不可撤销 |
| 炸药爆炸范围用 `dr²+dc² ≤ r²+1` | 半径 1 时包含对角格 |

## 6. 存档

- 格式：JsonUtility 输出的 `GameState`。
- 版本号：`GameState.CurrentVersion = 3`（`wip-v2` 里是 4）。
- 迁移：`GameState.Upgrade()`，由 `SaveService.Load` 调用。版本 2 的 `bossDefeated` 转成 `bossesDefeated`。
- 写入：每 30 秒、退出时、重铸时。先写 `.tmp` 再替换。
- 读取失败：把坏文件复制成 `save.json.bad`，开新档。
- 不保存的状态：连击、狂热能量、Boss 进度、被点击打出的洞。地形由 `seed` 重建，`GameSim.Row` 会重新打开钻头所在的通道。

## 7. 美术生成

脚本在 `art/tools`，都用 Aseprite Lua，由批处理模式运行：

| 脚本 | 产出 |
|---|---|
| `draw_game_art.lua` | **游戏实际用的全部素材**，输出到 `Assets/Art` |
| `draw_drill.lua` | 早期 32×48 钻头，备选 |
| `draw_tiles.lua` | 早期地层和合成图，备选 |

重新生成（工程根目录）：

```powershell
& 'C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe' -b --script-param dir=C:\GAME\DeepDril\Assets\Art --script art\tools\draw_game_art.lua
```

之后切回 Unity 窗口让它导入，或者点 DeepDrill > Setup Project。新素材按文件名进入 ArtLibrary，用 `art.Get("名字")` 取。缺名字会抛 `KeyNotFoundException`。

Aseprite 是 Steam 版 1.3.18.6。

## 8. Git 历史

`master`：

| 提交 | 内容 |
|---|---|
| `e28c775` | 第一阶段：可玩的挖掘循环 |
| `3ae5899` | 第二阶段：暴击、连击、狂热、炸药、三棵技能树 |
| `fcaea03` | 横屏和钻头物理，按参考图重做界面 |
| `2e3524f` | 存档校验逐字段打印 |
| `e770c45` | 第三阶段：巨型木根和地核重铸 |
| `ef7a351` | 第 2 地层：砂岩、沙块、岩甲虫 |
| `45ea5b7` | 图标列悬浮分组 |

`wip-v2`（`1eae936`）：未完成的第二版核心，见下一节。

## 9. 第二版改造需要动的代码

第二版的目标：钻头数量无上限；右侧栏改成钻头机库和合成；技能树是独立的树状图界面，每个技能可升级；成就和稀有物品页；150 个技能；12 个地层从地表到地心；5 个钻头家族 × 12 材质。

**数值以项目设计资料为准。**下面只讲代码。

### wip-v2 分支里有什么

只有 Core 和一个美术脚本，**不能编译**：Game 层和测试都还对着第一版的 API。里面的 20 种钻头、技能表、地层表都是我的占位数值，和设计资料冲突，**不要照抄数据**。

可以参考的是结构：

- `DrillBody`：改成单格宽的小钻头，支撑判定改成“正下方 + 左右”，物理 30Hz。
- `GameSim.RebuildUnits`：按拥有数量维护钻头实体，超出模拟上限的算进 `swarmMultiplier`。
- `GameSim.BringBackStragglers`：掉队的钻头传回领头钻头附近。
- `GameSim.effects[]`：技能效果汇总缓存。
- `Strata.cs` 和 `WorldGen.Fill`：按地层表生成，方块带 `Ore` 索引，矿物和硬岩改成叠加层。
- `draw_world_art.lua`：12 层调色板、矿物叠加层、批量钻头的画法。

这些都没运行过，一次测试也没跑。

### 要改的类

| 类 | 改动 |
|---|---|
| `Balance` | 钻头、地层、Boss 数值搬到数据表；保留公式和手感常量 |
| `Skills` | 换成 150 个技能的数据，加树状图坐标 |
| `WorldGen` | 地层查表替换写死的两层；`BlockType` 里的矿物改成带索引 |
| `GameState` | 加钻头数量、成就、稀有物品、终身记录，版本升到 4，写迁移 |
| `DrillBody` | 适配小钻头，每个钻头一个实例 |
| `GameSim` | 改动最大。`Drill` 单例变成列表，`Swing`、`DefeatBoss`、`FindTarget`、`StepSand` 都依赖单钻头 |
| `WorldView` | 钻头对象池；矿物叠加层；地层和边缘 Sprite 改成查表（现在写死 2 层）；`bossFaces` 写死 2 张；摄像机改成跟随领头钻头 |
| `HudView` | 重写右侧栏：可滚动的机库列表、购买、合成；图标列加入技能树和成就入口；两个全屏新界面 |
| `GameRoot` | `IconFor`、`BitsColor` 写死了矿物；钻头多了之后飘字、震屏、停顿都要限流（现在每次击破都触发） |
| `FxView` | 飘字池没有上限，要加；飞行矿物已有 40 个上限 |
| `DemoDriver` | 跟着 API 改；`Sandstone` 和 `BossAndRebirth` 里的固定流程要更新 |
| `CoreTests` | 物理和目标选择的测试要重写，`BodyOnFloor` 的支撑规则变了 |
| `draw_game_art.lua` | 地面和矿物部分要换；炸药、裂纹、图标可保留 |

### 可以直接保留

`NumberFormat`、`SaveService`、`Stage`、`Ui`、`ArtLibrary`、`ArtImportRules`、`ProjectSetup`、`tools\check.ps1`、`tools\outside`；`GameSim` 里的炸药连锁、连击、狂热、收入窗口、离线逻辑；`WorldGen.Hash`；裂纹和边缘的绘制方式。

### 容易踩的点

- 三格同时打是为了让单钻头走直线。第二版改成单格小钻头后，这条理由消失，要重新看隧道和倾斜的效果。
- 无上限的钻头不能都做物理。`wip-v2` 的思路是模拟上限加合力系数，要在游戏里实测性能。
- `Row()` 重建时只能重开钻头所在的一列，多钻头时被挖过的洞在读档后会丢。
- 改核心 API 时，先让 `tools\outside` 的两个工程编译通过，再切回 Unity，否则用户开着的编辑器会一直报错。
