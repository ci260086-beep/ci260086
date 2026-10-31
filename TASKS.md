# 《深渊钻头》任务分工

两个 Claude 在同一台电脑上一起开发时，按这里的规则分工。详细现状看 `HANDOFF.md`。

## 分工

| | 文件夹 | 负责 | Unity | 分支 |
|---|---|---|---|---|
| Claude A | `C:\GAME\DeepDril` | 要跑 Unity 的工作：功能、测试、性能、合并 | 可以打开 | `master` 或功能分支 |
| Claude B | `C:\GAME\DeepDril2` | 不用开 Unity 的工作：`art\tools` 里的 Aseprite 脚本美术、数值数据、文档 | 不打开 | `art/*` 或 `data/*` |

Claude B 在自己的分支上提交并推送，由 Claude A 合并和验证。Claude B 不合并到 `master`。

## 开工流程

1. 先运行 `git pull`，再读 `HANDOFF.md` 和本文件。
2. 在下面的任务表里认领任务，写上谁、哪个分支、状态，提交并推送，再开始做。已被认领的任务不要动。
3. 做完后更新任务状态和 `HANDOFF.md`，再推送。

状态只用这四种：待办、进行中、待验证、完成。Claude B 做完的任务写成“待验证”，由 Claude A 合并并验证后改成“完成”。

## 任务表

计划来源：第四阶段计划 https://claude.ai/artifact/CKUBNxSWKeDQZEUQSTgwkF

| 任务 | 负责 | 认领人 | 分支 | 状态 |
|---|---|---|---|---|
| 12 个 Boss 的独立造型 | B | Claude B | `art/boss-designs` | 待验证 |
| 第三阶段各系统的图标 | B | Claude B | `art/system-icons` | 待验证 |
| 150 个技能的图标（按效果类型） | B | Claude B | `art/skill-icons` | 进行中 |
| 46 个成就和 36 件稀有物品的图标及剪影 | B | | | 待办 |
| 最慢一帧降到 20 毫秒以下 | A | | | 待办 |
| 新手引导 | A | | | 待办 |
| 设置菜单和新游戏按钮 | A | | | 待办 |
