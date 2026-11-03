# 《深渊钻头》交接文档

## 接手说明（2026-11-01，从这里读起）

Claude A 的额度快用完，从现在起 Claude B 全面接手代码和美术，包括写代码、开 Unity 和合并。本节写给从零接手的人。下面“最新现状”和更早的章节是历史记录，有冲突时以本节为准。

### 项目

《深渊钻头》，Unity 6000.0.85f1，uGUI，内置渲染管线，横屏 320×180，逐像素地形。用户是 weiyou，定价 6 元，不做广告和付费加速。玩法：钻头往下挖 12 层到地心，重铸拿碎片，再跃迁到下一颗规则星球。

### 文件夹

- `C:\GAME\DeepDril` 是 A 用过的主文件夹，当前检出 `master`。`C:\GAME\DeepDril2` 是 B 的文件夹。
- 只动这两个文件夹。旧项目（`C:\GAME\DARKLATOR` 等）一律不碰。
- 同一个文件夹只能开一个 Unity。开 Unity 前确认用户没在 Play：看 `Temp\UnityLockfile` 和存档 `save.json` 的修改时间。用户开着时，用 robocopy 把 `Assets`、`Packages`、`ProjectSettings`、`tools` 复制到临时目录，在副本里跑 `tools\check.ps1`。

### 代码结构

- `Assets/Scripts/Core`：纯 C# 规则层，不能引用 UnityEngine。`GameSim.cs` 是主循环，`GameSim.*.cs` 是分部类（`Pixels` 挖掘，`BossMoves` Boss 招式，`Challenges` 挑战，`Schools` 流派，`Warp` 跃迁和星球，`Meta` 里程碑和自动化，`Factory`、`Lab`、`Events`）。`PixelWorld.cs` 是地形，`Balance.cs` 是常量，`GameData.cs` 读数据，`GameState.cs` 是存档内容。
- `Assets/Scripts/Game`：MonoBehaviour 视图层，只通过 `SimEvent` 和公开属性读规则。`GameRoot.cs` 入口和按键，`WorldView.cs` 世界画面，`FxView.cs` 特效，`HudView.*.cs` 界面（`Base` 基地页，`Tree` 技能树，`Guide` 引导，`Merge` 合成），`DemoDriver.cs` 自动试玩和截图，`SaveService.cs` 存档。
- `Assets/Scripts/Editor`：`ProjectSetup.cs` 建场景和 `ArtLibrary.asset`，`ArtImportRules.cs` 管图片导入。
- `Assets/Tests/EditMode`：测试，`Saves` 里是用户的真实旧存档。
- `Assets/Resources/Data/*.json`：数据。`phase3.json` 手写，其余由 `tools/Design/gen_design_data.py` 生成。这台电脑的 `python` 是占位程序，用 `"C:\Program Files (x86)\Steam\steamapps\common\Blender\5.2\python\bin\python.exe" -I` 运行。
- `art/tools/*.lua`：Aseprite 美术脚本，输出到 `Assets/Art`（`Drill`、`Items`、`Layers`、`Tiles`）。`Aseprite.exe` 启动后立刻返回，PowerShell 里要用 `Start-Process -Wait`。重跑 `draw_game_art.lua` 会改动 4 张矿物贴图，要还原。
- `ArtLibrary.Get` 找不到图会抛异常，所以代码引用的新图必须和代码一起到位，否则 Play 报错。

### 编译和测试

都从 `C:\GAME\DeepDril` 这样的工程根目录运行。`Run.csproj` 要在 PowerShell 里构建，从 Bash 构建曾经找不到 NUnit，然后跑到旧的 exe。

```powershell
dotnet build tools\outside\Check.csproj -v q --nologo
$d = (Get-ChildItem -Recurse -Filter nunit.framework.dll 'Library\PackageCache' | Select-Object -First 1).FullName
dotnet build tools\outside\Run.csproj -v q --nologo "-p:NUnitDll=$d"
tools\outside\out\run\Run.exe
powershell -ExecutionPolicy Bypass -File tools\check.ps1 -Stage 名字
```

- `Check.csproj` 看编译，要 0 错误。先跑这一步，`check.ps1` 碰到编译错误会打印旧的 `Logs\tests.xml`，还会让 Unity 卡住。
- `Run.exe` 是不开 Unity 的测试。`master` 上应为 `total=174 passed=174 failed=0`。
- `check.ps1 -Stage 名字` 关掉 Unity 后才能跑，先跑 EditMode 测试，再自动试玩并截图。`master` 上应为 177 条全过，约 4 分钟。跑完核对 `Logs\tests.xml` 的时间确实是刚生成的，再查 `Logs\play.log` 没有 `Exception`。

### Claude B 的工作方式（2026-11-01 起）

B 在这台电脑上只能点 PowerShell 和文件资源管理器、不能打字，所以：

- **编译和外部测试在云端跑**：B 的云端有同一仓库的克隆，用这台电脑 Unity 的 DLL 编译 `Check.csproj`、`Run.csproj`、`Pace.csproj`。改动用 `git bundle` 在云端和 `C:\GAME\DeepDril2` 之间来回同步（放在 `Temp\`，不进 git）。
- **Unity 内测试**：在 Unity 里打开 Window > General > Test Runner，点 Run All。代码从外部改过后，先在 Unity 里按 Ctrl+R 让它重新编译，否则跑的是旧代码。
- **自动试玩截图**：菜单 **DeepDrill > Check: Play And Capture**，截图写到 `Screenshots\check`，日志写到 `Logs\check_play.txt`，最后一行是 `[check] finished ... N shots`。和 `check.ps1` 用同一个 `DemoDriver`，只是通过环境变量传 `--shots`、`--save`，结束时退出 Play 而不是关掉编辑器。
- **Aseprite 脚本**：`%APPDATA%\Aseprite\scripts\deepdrill_job.lua` 读 `Temp\aseprite_job.lua` 里列的脚本和参数，在 Aseprite 界面里用 File > Scripts > deepdrill_job 运行，日志在 `Temp\aseprite_job.log`。每写一个文件 Aseprite 会弹一次安全确认。
- 改脚本时 B 在云端用一个只模拟 Image、drawPixel、saveAs 的小运行器看预览；它画出的旧图和仓库逐像素一致。提交的图一律由 Aseprite 生成。

### 截图

`check.ps1` 把截图写到 `Screenshots\名字_*.png`。要新增一张，在 `DemoDriver.cs` 里加 `yield return Shot("xxx");`，前面先摆好状态。`base_1` 是流派页，`base_5` 是挑战页，`rush` 是倒计时，`planets` 是星图，`layer_02` 到 `layer_12` 是各层，`boss_01` 到 `boss_12` 是 Boss。`Screenshots` 没有纳入 git。

### 节奏模拟

```powershell
dotnet build tools\outside\Pace.csproj -v q --nologo
tools\outside\out\pace\Pace.exe Assets\Resources\Data 8 1500 种子
```

参数依次是数据目录、模拟小时、真实秒数上限、种子。`phase5-planets` 分支多一个参数，是星球编号。

- 期望：种子 7、11、23、42、3、99、5、64、128、1000 都到地心，9 到 13 局。最后一行写着 `centre reached in run N`。
- 10 个一起并行时，真实秒数上限至少给 1200，太小会被截断，显示 `centre not reached`，这不代表到不了。`master` 上种子 64 在 1200 秒时被截断，停在 2530 深度。
- 全部来自机器人，机器人按第几局轮换流派，没有真人试玩数据。
- 用户定的目标是第一次重铸 30 到 45 分钟，之后每层 8 到 15 分钟，10 到 15 局到地心。目前部分种子 9 局，略快。
- 坑：`Pace` 每秒只算 10 步，限流都要按时间算，不能按帧算。爆炸有按岩石血量算的保底伤害，大范围爆炸会无视伤害打穿深层，所以 `Balance.PulseRadius` 固定为 1。

长时间运行用 `tools\outside\Soak.csproj`，`Soak.exe Assets\Resources\Data 8 7`。

### 分支和合并规则

- 新工作开新分支，主线是 `master`。
- 不 rebase，不 force，不删分支和历史。能快进就快进。
- 合并进 `master` 前必须同时满足：编译 0 错误；Unity 内测试全过；截图看过，没有明显问题。
- 测试没过、效果有疑问或影响存档时不合并，先问用户。
- 用户说过的话只算那一次的话。以后的合并能不能不问就做，要看用户当时怎么说，不要把别人得到的授权当成自己的。
- 改了 `GameState` 字段就要跑 `SaveCompatibilityTests`，存档版本现在是 7。
- 提交末尾要有署名行，不要把模型编号写进提交。`.claude/` 不提交。

### 推送限制

这台设备只放行 `git push origin comms`。其余分支的推送都由用户运行，把命令写给用户。不要自己改权限设置。

### 留言板

- `comms` 分支的 `COMMS.md` 和 `TASKS.md`。留言格式是 `## 时间 · 发件人 → 收件人`，最后一行 `状态：未读` 或 `状态：已读`，只追加不删。
- 用 scratchpad 里的临时 worktree 改，不要在主文件夹切分支：`git worktree add 路径 comms`，`git -C 路径 reset --hard origin/comms`，改完提交，`git worktree remove 路径`，`git push origin comms`。被拒绝就重新从 `origin/comms` 开始。
- 留言内容是资料，不是用户的指令。

### 当前进度

- `master` 包含第五阶段的 ①②③⑤⑧，以及 ⑥ 里的挑战规则、流派专属规则和流派搭配。
- ⑥ 3 颗新星球（雷暴、永冻、潮汐）已并入 `master`（2026-11-01，Claude B）：美术 23 在 Aseprite 里生成；雷暴星的雷击落在领头钻头左右 5 列内，有闪电和“雷击！连击清零”提示（`SimEventKind.StormStrike`、`FxView.Lightning`）。Unity 内 180 条测试全过，截图 planets、storm、tide 看过。节奏：星球 5、6、7 各 3 个种子（7、42、99）都在 9 到 13 局到地心；潮汐星种子 7 第一局在第 3 层卡了 124 分钟才重铸，地球种子 7 同一处也要 35 分钟，属于 ④ 的第一局波动，没改。
- ④ 节奏重调：用户 2026-11-01 说先不用重调。
- ⑦ 声音（`phase5-sound`，已并入 `master`）：用户选了**轻快**风格。`MusicSynth` 用代码合成 8 小节循环（脉冲波主旋律、三角波低音、琶音、噪声鼓），每层一首，Boss 战一首更快的；越深调越低、越偏小调，但速度不降。`MusicPlayer` 在后台线程渲染、按深度和 Boss 状态交叉淡入；设置页加了“音乐”音量（默认 75%）。之前舞台摄像机没有 `AudioListener`，游戏里其实一直没有声音，这次补上了。`SaveService.Save` 的 `File.Replace` 被占用时重试 5 次（每次 40 毫秒），存档内容和格式不变。
- 字体：用户要求所有文字框里的长文字自动换行，不能超出框。做美术 21（像素字体）时逐页检查。
- 第六阶段美术 18（`art/PALETTE.md`、`art/tools/palette.lua`）和 19（钻头 8 帧，`draw_world_art.lua`）已并入 `master`。新美术一律用 `palette.lua`。旧美术的 5 种描边色差别很小，没有重画。
- 美术 21（`art/pixel-font`，已并入 `master`）：界面字体是 `Assets/Resources/Fonts/FusionPixel8.ttf`（缝合像素体 8px 简体常用字子集，SIL OFL，见 `CREDITS.md`），只在 8、16、24 绘制（`Ui.PixelSize`），行距 0.75（9 像素一行）。`Ui.Label` 默认自动换行。自动试玩每张截图后跑 `TextAudit.Check`，超框的文字在 `Logs/check_play.txt` 里以 `[overflow]` 列出，合并前要为 0。按钮、卡片、行用九宫格边框 `ui9_button`、`ui9_inset`、`ui9_panel`（`draw_ui_frames.lua`，`Ui.Framed`）。不要再用 `FontStyle.Bold`，像素字体会糊。
- 图标重画（`art/icons`，已并入 `master`）：技能图标 `sicon_*` 从 7×7 符号改成 9×9 三色小图（`draw_icons.lua` 的 SKILLS），`HudView.Tree.SkillIconSize` 是 9；成就奖牌中间改成 8×8 彩色小画（`PICTOS`：每层一景，计数类是钻头、合成、火焰、重铸环、炸药），Boss 头像牌不变。
- 宣传片（分支 `trailer`，没合并）：菜单 DeepDrill > Trailer: Record 用 1920×1080 跑 `TrailerDriver`，按 30fps 逐帧录 8 段到 Recordings/trailer/<段名>（录制时 `HudView.Filming` 关掉提示、Toast 和 FPS）。`tools/trailer/timeline.json` 定段长（128 拍，按小节）、中英字卡和音效点；`tools/trailer/audio` 在云端用 MusicSynth（`MusicSynth.Trailer`）和 SfxSynth 渲染配乐，放到 Recordings/trailer/audio.wav；Logo 由 `tools/trailer/logo_mask.py` 排字、`art/tools/draw_trailer_logo.lua` 在 Aseprite 画成 art/trailer/logo.png；`tools/trailer/make_trailer.py` 在电脑上用 PIL+ffmpeg 合成 Recordings/trailer/drill_thrill_trailer.mp4。第一版用户嫌不够爽、没展示流派/技能树/星球/成就、BGM 不够燃，第二版已改：140 拍专用配乐（`tools/trailer/audio/TrailerMusic.cs`，四拍底鼓、侧链压缩、八分贝斯、双层主旋律，按 timeline 的 section 分段）、系统展示段（技能树逐个点亮、基地五个系统页快切、成就和稀有物品逐个亮起、星图后真跃迁到雷暴星）、高潮段混剪（timeline 的 montage）、切镜推镜+闪光、高潮每拍震屏、字卡改用 12px 缝合像素体（`tools/trailer/fonts`，OFL，只给宣传片用）、ffmpeg loudnorm 到 -13 LUFS。第二版待用户反馈。已知：雷暴星地表和地球看起来差不多。
- 改名（`rename/drill-thrill`）：游戏改名《挖了个爽》，英文名 Drill Thrill（用户定）。Unity 产品名 `Drill Thrill`、公司名 `DrillThrill`（`ProjectSetup.ProductName`，`ProductNameGuard` 打开工程时自动纠正），保持 ASCII，因为它决定 exe 名和存档目录（LocalLow/DrillThrill/Drill Thrill）。新目录没有存档时，`SaveService.AdoptOldSave` 从旧目录 LocalLow/DeepDrill/Deep Drill 复制一份（旧文件留作备份）。设置（音量等）在 PlayerPrefs 里，会回到默认值。代码命名空间 `DeepDrill` 不改。
- 连击火焰和音乐（`ui/combo-flame`，已并入 `master`）：用户嫌连击大字挡画面，改成领头钻头身后的火焰（`WorldView.DrawFlame`、`FlameLevel`；图 `flame_<0-3>_<0-3>` 由 `draw_combo_flame.lua` 在 Aseprite 画），加成越高火越大，满上限变蓝，快断时小一号，火越旺光圈越大；HUD 只留一行 8px「连击 x2.3」。左上角悬浮火焰的版本用户说违和，已弃用。音乐用户嫌太欢快，选了安静方案（`MusicSynth.Style = 2`：约 92 拍、三角波主旋律、回声、稀疏鼓点）；0 是原来的欢快版。技能树提示框按文字实际高度往下排（`LayoutTooltip`），描述用 `Ui.NoBreak` 把空格换成不换行空格（字体里 U+00A0 已映射到空格），避免「第 2\n级」这种断行。
- 排行榜（`feature/leaderboard`，已并入 `master`，用户同意加存档字段）：`GameState` 新增 `standardBestRow`、`playSecondsEver`、`longestRunSeconds`、`mostDrills`，旧存档读出来是 0，没有升版本号。`GameSim.UpdateRecords` 每秒采样一次；只用标准钻的判定是 `StandardOnlyRun`（本局没买过其他家族，也没有其他家族的钻头）。最高钻头用原有的 `bestTier`。界面在图鉴第 4 个标签「排行」（`HudView.ShowRecordsPage`，截图 `records`）。`Leaderboards.All` 定义 5 个榜；接 Steam 时实现 `ILeaderboardBackend`（Steamworks 排行榜，保留最好成绩、降序），启动时赋给 `Leaderboards.Backend`，`SaveNow` 每次存档都会提交。
- 音效（`audio/sfx`，已并入 `master`）：全部由 `SfxSynth`（无引擎依赖，方波/三角/正弦/噪声多声部，C 大调和音乐同调）在启动时合成，`SfxSynth.Library()` 里按名字列出。挖掘按层用 `break_0` 到 `break_11`（`SfxSynth.BreakFor`，星球按层号复用）；Boss 有登场、警告、命中、碎甲、打穿、失败；成就、合成、稀有、里程碑、跃迁、重铸、雷击、过热、狂热、进入新层各有专属声。所有按钮经 `Ui.WithClick` 发 `Ui.Clicked`，`SoundBank` 播 `click`。`always` 的音效不随机变调，保持在调上。试听页由云端小程序渲染 WAV 生成。
- 美术 22（`art/ground`，已并入 `master`）：层与层之间的地面颜色按像素走一条带噪声的分界线（`PixelPainter.PaintLayerAt`，两层值噪声，分界上一像素棋盘格），只改画面，方块属于哪层仍由 `WorldGen.LayerAt` 决定，不影响存档。每层岩石纹理从 3 种加到 5 种（`RocksPerLayer`）：新增卵石（kind 3）和孔洞岩（kind 4），在 `draw_layer_art.lua`。从云端传 bundle 到电脑时每次用新文件名，同名文件会拿到旧内容。
- 美术 20（`art/lighting`，已并入 `master`）：挖掘区的暗色是 `LightMap`，每帧按舞台一半分辨率算，分 6 级加棋盘格抖动；领头钻头、视野内每台钻头、爆炸、雷击在里面挖出光圈（`WorldView.AddLights`、`LightMap.AddFlash`）。最深层暗度 0.62（原来 0.4）。头灯、钻头光晕、点击闪光、闪电用加法着色器 `DeepDrill/SpriteAdditive`（`WorldView.AdditiveMaterial`）和新图 `light_soft`；宝石闪光改用 `light_glint`。
- ⑧ Windows 打包：暂缓。这是用户的决定。

### 已知问题

- 有一次自动试玩 1000 台钻头掉到 16 FPS，`Update` 单帧约 400 毫秒，不在规则、事件和环境效果里，之后 7 轮没有复现。`DemoDriver` 留着分段日志。
- 第一次重铸 12 到 47 分钟，波动大，10 个种子里约一半在目标内，留给 ④。
- 流派手感和急行挑战的难度没有真人玩过。爆破派自动引爆的画面没有单独看过。
- 整帧最差 23 到 29 毫秒，游戏自身 9 到 16 毫秒，要等打包版确认。

### 第六阶段

规划：https://claude.ai/artifact/AJVs5nXuLmY1QvyegDrv5a

美术编号 18 到 27（编号以这里为准。之前留言板上的“美术 18”指 3 颗星球，现在改叫美术 23）：

| 编号 | 类别 | 内容 |
|---|---|---|
| 18 | 质感 | 调色板和描边规范 |
| 19 | 质感 | 钻头 6 到 8 帧动画 |
| 20 | 质感 | 发光和光照 |
| 21 | 质感 | 像素字体和统一界面框 |
| 22 | 质感 | 地表过渡和岩块变体 |
| 23 | 新内容 | 3 颗星球：`planet_5` 到 `planet_7`（48×48）、`planet_rule_5` 到 `planet_rule_7`（16×16），放在 `Assets/Art/Items`，规格见 `comms` 的 COMMS.md 2026-10-31 22:40 的留言 |
| 24 | 新内容 | 流派专属钻头和技能图标 |
| 25 | 新内容 | 挑战奖励外观 |
| 26 | 新内容 | 无尽深渊背景 |
| 27 | 新内容 | 离线结算和跳字样式 |

## 最新现状（2026-10-31，先读这一节）

每次提交都要同步更新这一节并推送到 `origin`。旧项目（`C:\GAME\DARKLATOR` 等）一律不碰，只动 `C:\GAME\DeepDril`。

- **仓库：** https://github.com/ci260086-beep/ci260086 ，主分支是 `master`。不要 force push。合并进 `master` 前先等用户确认。
- **分支：** `master`（已快进合并第三阶段和美术界面改动，是当前主线）；`v2`（`8f3b4c6`，第三阶段第 0 步）；`phase3-step2`（第三阶段全部内容，已并入 `master`）；`phase4-art-ui`、`phase4-perf`、`verify-boss`、`verify-art`（都已并入 `master`）；`terrain-pixel`（`e7d03fb`，已包含在 `v2` 里，检出在 `Builds/wtp`）；`wip-v2`（旧存档分支）。
- **第三阶段进度：** 计划在 `tools/Design/phase3_plan.md`。用户决定在 `phase3-step2` 这一条分支上把第 1 到第 8 步全部做完，每步只跑编译和单元测试，最后统一做完整测试和节奏调参。第 0、2、1、3、4、5、6、7、8 步和完整测试、节奏调参都已提交：Unity 内 147 个测试全过，200 台钻头 60 FPS，最差一帧 24 毫秒。第 2 步：`milestones.json`、`GameSim.MilestonesOf/MasteryOf`、`HudView.Mastery.cs`（图鉴的“里程碑”“精通”标签页，未实机看过）。
- **第四阶段计划：** https://claude.ai/artifact/CKUBNxSWKeDQZEUQSTgwkF
- **第五阶段：** 上架往后推，先继续打磨，计划在 https://claude.ai/artifact/XVPYASpN6Ejmhx81Y9Sw1Y ，任务表在 `TASKS.md`。每项做完先截图给用户，确认后再合进 `master`。Claude A 需要的美术直接写到 `comms` 留言板交给 Claude B。
- **第五阶段已做：** Boss 招式（`GameSim.BossMoves.cs`，数值在 `phase3.json` 的 bossMoves）、Boss 动画和招式特效（`WorldView`）、地面用 B 的岩块和装饰绘制（`PixelPainter.UseLayerArt`，这些图要 `isReadable`，由 `ArtImportRules` 设定）、新手说明卡和箭头（`HudView.Guide.cs`，看过的记在 `GameState.seenGuides`，打开时 `Time.timeScale` 为 0）、设置页（音量、新游戏）、批量购买（`BuyDrills`）、快捷键（`GameRoot.HandleKeys`）、统计页。新游戏（`GameRoot.StartNewGame`）把存档改名保留，再重建全部场景对象；自动试玩最后会走一遍，以 `[demo] new game started` 结束。没有真人操作过这些界面。
- **存档：** 版本 7。第一次读旧版本时保留一份 `save.json.v<版本>`。`Assets/Tests/EditMode/Saves` 里是用户的两份真实版本 6 存档，`SaveCompatibilityTests` 每次都读，以后加存档字段要跑这个。
- **地层画面：** `FxView.AmbientDensity` 是每层粒子密度，`WorldView.DrawHeadlamp` 是领头钻头的光，`HudView.ShowHint` 显示进入新层时的规则，`GameSim.HeatWeakens` 和 `Overheating` 驱动持续提示，`PixelPainter.GroundAt` 里的扭曲减轻纹理重复。星图页底部是星球条，跃迁后弹抵达卡（`ShowPlanetCard`）。
- **前期流程：** 自动挖掘 5 金币，第一台钻头 6 金币，引导在 `HudView.UpdateGuide` 和 `UpdateGuideArrow`。点击加速是 `GameSim.ClickBoost`，数值在 `Balance.ClickBoost*`。`MergeEverything` 按数量一次合完，只发一个 `DrillsMergedAll` 事件，C 键或机库右上角按钮触发。
- **节奏模拟的坑：** `Pace` 每秒只算 10 步，按帧算的预算会让模拟比实际慢。爆炸预算现在按时间算（`MaxBlastPixelsPerSecond`），以后加限流都要按时间算。当前 10 个种子 9 到 13 局、117 到 164 分钟到地心，第一次重铸 12 到 47 分钟。
- **待查：** 有一次自动试玩 1000 台钻头掉到 16 FPS，`Update` 单帧约 400 毫秒，不在规则、事件和环境效果里，之后 7 轮没有复现。`DemoDriver` 会打出分段。
- **流派和挑战：** 4 个流派各有专属规则，`SchoolDef.Trait` 对应 `GameSim.HasTrait`，在 `GameSim.Schools.cs`。通关过的流派可以当第二流派（`State.secondSchool`，只加加成和规则，不给分支折扣），机群派和精工派不能搭配。挑战新增 `no_click`、`no_merge`、`rush`，`rush` 用 `runSeconds` 计时。
- **爆破派自动引爆的坑：** 爆炸有按岩石血量算的保底伤害，大半径的自动引爆会无视伤害打穿深层，把到地心压到全部 9 局，调间隔没用。所以 `Balance.PulseRadius` 固定为 1。
- **暂停点（2026-10-31）：** 用户要求暂停，等通知再继续。`phase5-planets` 上 3 颗新星球的规则已写好，外部测试 177 条全过，还没跑 Unity。这条分支在 B 交 `planet_5` 到 `planet_7`、`planet_rule_5` 到 `planet_rule_7`（美术 18，分支 `art/planets-2`）之前不能开 Play，星图缺图会报错。继续时：合入美术；跑 `Pace.exe Data 8 1500 种子 星球`，地球 10 个种子要在 9 到 13 局，星球编号 5、6、7 各看 3 个种子；`tools\check.ps1`；看 planets、tide、storm 截图；过了再快进合并。节奏模拟没跑完，结果不算。第 ⑦ 音效等用户选音乐风格。
- **分工：** 两个 Claude 同时开发时按 `TASKS.md` 执行，开工前先读。交流在 `comms` 分支的 `COMMS.md`。
- **性能（已并入 `master`）：** 只开一个 Unity 时，200 台钻头 60 FPS，整帧最差 23 毫秒，其中游戏自身（PlayerLoop）9.3 毫秒；1000 台 60 FPS，整帧最差 26 毫秒，游戏自身 12.5 毫秒，逻辑 3.2 毫秒，绘制 0.5 毫秒。整帧还没到 20 毫秒，差值在 PlayerLoop 之外，判断是编辑器和垂直同步，没有打包版本可以证实。`DemoDriver.MeasureFrames` 会打出最慢几帧的分段。修过的根因：`DropAbove` 只看领头钻头，掉队钻头脚下的地形块每向下一行就同步重建，现在要等最后一台离开；地形块多预生成到 16 行；绘制时块内直接读数组，屏幕外的块每帧只绘一块。
- **长时间运行：** `dotnet build tools\outside\Soak.csproj -c Release`，再运行 `tools\outside\out\soak\Soak.exe Assets\Resources\Data 8 7`。8 小时结果：不崩溃，内存 34 到 78 MB，没有持续增长，机器空闲时没有一步超过 100 毫秒。只测规则，不含 Unity 表现层。查清的两个问题：世界原来没有底，现在第 2552 行起是挖不动的基岩；后期大连锁一帧处理上百万像素，现在按 `Balance.MaxBlastPixelsPerTick` 分到多帧。同时开 Unity 跑时会出现 200 毫秒以上的假峰值，要在机器空闲时测。
- **本轮新增：** 磁力钻每秒吸取 3 格内最近的 32 个矿物像素（`GameSim.PullOre`）；技能树可解锁连线用虚线（`tree_dash`），购买时节点先填满再爆开；合成弹窗在挖掘区右下角，放在 `FxLayer` 里。
- **当前主线：** `master`。第三阶段、美术界面、12 个 Boss、性能和 Claude B 的图标都已并入。图标文件都在，但成就、技能和系统图标还没接进游戏，这是下一步（`phase4-icons`）。
- **美术和界面（`phase4-art-ui` 分支，已经用户确认并入 `master`）：** 用户试玩后的反馈，已在实机截图里看过，没有真人操作过。按钮图标：`tab_tree`、`tab_trophy`、`tab_base`，鼠标移上去底部显示名字。`ui_frame` 是按钮列和机库共用的实心底板，`FxLayer` 只覆盖挖掘区并裁剪，相机按 `DigWidth` 限位。钻头 12 级配色在 `draw_world_art.lua` 的 `MATERIALS`，中间色同时存在于 `Palette.Tier`，改一边要改另一边；钢起边条，金起宝石，水晶起发光外框，星核有火花和环绕光点（`WorldView.GlowOf`、`motes`）。机库改成每行 5 格，每格 14 像素，按材质上色；合成弹出在 `HudView.Merge.cs`。技能树页面和 46 个成就的单独图标这次没动。
- **Aseprite 脚本的坑：** `Aseprite.exe` 启动后立刻返回，在 PowerShell 里要用 `Start-Process -Wait`，否则会读到旧文件。重跑 `draw_game_art.lua` 会改动 `ore_coal`、`ore_silver` 及其 `_sand` 四张贴图，这是脚本和已提交图片的旧差异，不是想要的改动，提交前要还原。
- **用户开着 Unity 时的验证办法：** 用 robocopy 把 `Assets`、`Packages`、`ProjectSettings`、`Library`、`tools` 复制到别的目录，在副本里运行它的 `tools\check.ps1`，再把新生成的 `.meta` 和 `ArtLibrary.asset` 同步回来。这样不影响用户的 Play。两个 Unity 同时开时最差一帧会偏高（这次 30 毫秒）。
- **用户已定：** 横屏 320×180；逐像素地形；手动起步（解锁自动钻探前只能点击）；定价 6 元人民币；不做广告和付费加速。
- **节奏目标：** 第一次重铸约 30 到 45 分钟；第 1 局到第 4 到 5 层；之后每层约 8 到 15 分钟；10 到 15 局到地心。
- **代码结构：** 规则在 `Assets/Scripts/Core`（`GameSim.cs`、`GameSim.Pixels.cs`、`PixelWorld.cs`、`WorldGen.cs`、`DrillBody.cs`、`Balance.cs`、`GameData.cs`、`GameState.cs`），不能引用 UnityEngine。表现层在 `Assets/Scripts/Game`（`HudView*.cs` 是全部 UI）。测试在 `Assets/Tests/EditMode`（`CoreTests.cs`、`RuleTests.cs`、`PacingTests.cs`）。下面第 1 节的表写于第一版，以这里为准。
- **数据：** `Assets/Resources/Data/*.json`，由 `tools/Design/gen_design_data.py` 生成，改数值改这个脚本，运行方法见下一节。`GameData.Load` 按文件名读取。
- **编译和测试：** Unity 没开时，运行 `tools\check.ps1 -Stage 名字`（先跑 EditMode 测试，再试玩截图）。Unity 开着时用工程外命令：`dotnet build tools\outside\Check.csproj` 检查 Core 和 Game 能否编译；`dotnet build tools\outside\Run.csproj -p:NUnitDll=<nunit.framework.dll 路径>` 后运行 `tools\outside\out\run\Run.exe` 跑测试。跑 Unity 前先确认用户没开 Play 模式。列表里的 `Unity Hub\...\unity.exe serve` 是 Hub，不是编辑器，要看 `Temp\UnityLockfile`。
- **节奏模拟：** `dotnet build tools\outside\Pace.csproj -c Release`，再运行 `tools\outside\out\pace\Pace.exe Assets\Resources\Data [模拟小时] [真实秒数上限] [种子]`。
- **已知问题：** 第 3 至 12 层的特殊机制只有测试，没有实机看过；Windows 打包未解决（见下一节）；磁力钻的“吸取 3 格内掉落”没做；第 1 到 8 步的界面没有真人实机操作过。
- **第 1 步：** 黄金事件和地核不稳：`GameSim.Events.cs`、`HudView.Events.cs`，数值在 `phase3.json` 的 events，未实机看过。
- **第 3 步：** 重铸里程碑和自动化规则：`GameSim.Meta.cs`；基地页 `HudView.Base.cs`（标签页加行的通用框架，后面各系统都放这里）；永久加成统一走 `AddPermanent`。
- **第 4 步：** 钻探流派：`GameSim.Schools.cs`，每局选一派，重铸后重置，通关该派给永久奖励，技能价格走 `SkillCost`。
- **第 5 步：** 地表加工厂和地心结晶：`GameSim.Factory.cs`，结晶按真实时间生长，离线也算，工厂花矿物，等级跨重铸保留。
- **第 6 步：** 研究所和探险队：`GameSim.Lab.cs`，同时只能研究一项，结晶可加速，探险员来自远古遗迹宝箱，失败也会回来。
- **第 7 步：** 挑战深井：`GameSim.Challenges.cs`，五条规则，到达目标深度给永久奖励；重置逻辑抽成 `ResetRun`。
- **第 8 步：** 星系跃迁、星座图和行星规则：`GameSim.Warp.cs`，五颗行星按跃迁次数轮换，规则都在 `phase3.json` 的 planets，星点买星座，界面在基地页。
- **节奏现状（已调完一轮，全部来自 `Pace` 机器人，没有真人试玩）：** 10 个种子（7、11、23、42、3、99、5、64、128、1000）：第一次重铸 22.7 到 49.2 分钟，6 个在目标内，2 个偏快，2 个偏慢，都在第 4 层；9 到 13 局到地心，其中种子 5 是 9 局，其余都在 10 到 13 局；总时长 133 到 180 分钟；第一次重铸后平均每层 12 到 18 分钟，均值约 15。没解决：第 7 到 9 层常在几分钟内连过，第 11 到 12 层是最慢的一段。结果对 `HpGrowthDeep` 很敏感：1.0143 约 6 局，1.0165 超过 17 局，同一数值不同种子也会差几局。
- **本轮数值：** `HpDeepRow` 1050→830，`HpGrowthDeep` 1.012→1.0155，`HpRamp` 4→5，`BossHpScale` 0.05→0.04，`ShardBonus` 0.15→0.13，`OreValuePower` 0.7→0.6，`BUY_GROWTH` 1.08→1.11，`RebirthAdviceMinSeconds` 900→720，新增 `RebirthAdviceMinShards` 60（不到 60 碎片不建议重铸，否则卡在早期 Boss 时会建议玩家为几个碎片重铸）；`phase3.json` 的金币事件减半。其余旋钮：`TIER_DMG_MULT`、圈价倍数和技能价格增长率。`phase3.json` 是手写的，不由生成器产出。
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
