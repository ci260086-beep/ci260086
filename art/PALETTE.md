# 深渊钻头调色板和描边规范（美术 18）

所有新美术都按这份规范画，代码在 `art/tools/palette.lua`，预览在 `art/palette_preview.png`（`draw_palette_preview.lua` 生成）。

## 光照

- 光从左上来。亮面朝左上，暗面朝右下。
- 每种材质最多 5 级：深影、阴影、本色、亮面、高光（`P.ramp(本色)` 生成）。
- 阴影的色相往蓝紫偏（250°），亮面往暖黄偏（50°），不要只调明暗。灰色材质的阴影直接带一点蓝紫。
- 两级之间用棋盘格抖动过渡（`P.shade`），不用渐变。
- 金属和宝石的高光点用暖白 `fff6d8`（`P.GLINT`），不用纯白。

## 描边

- 世界、钻头、物品和界面的描边统一用 `1a1210`（`P.INK`）。画在太空里的东西（星球、星图）用 `0e0c16`（`P.INK_SPACE`）。不用纯黑。
- 16 像素及以下的图标：整圈描边（`P.outline`），保证缩小后还认得出形状。
- 大于 16 像素的精灵（钻头、Boss、星球、大件掉落）：选择性描边（`P.selout`）。右下的暗面用描边色，左上的亮面用相邻颜色的深影，边缘看起来是被光照亮，而不是剪纸。
- 描边只有 1 像素宽，不加双层描边。

## 材质色板

`P.RAMPS` 里有 10 组：泥土 `dirt`、石头 `stone`、木头 `wood`、铁 `iron`、金 `gold`、水晶 `crystal`、冰 `ice`、植物 `leaf`、岩浆 `lava`、界面 `ui`。新材质用 `P.ramp("本色")` 生成，不手挑 5 个颜色。

## 旧美术

美术 18 之前的脚本有 5 种描边色（`1a1210`、`140c12`、`0e0c12`、`120e18`、`0e0c16`），彼此只差几个色阶，画面上看不出区别，这次没有重画。以后重画哪张图，就改用 `palette.lua`。

## 怎么在脚本里用

```lua
local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
local stone = P.RAMPS.stone
g:set(x, y, P.shade(stone, light, x, y))
P.selout(g)
```

从命令行运行时工作目录是工程根目录，相对路径直接可用。在 Aseprite 界面里用 `deepdrill_job` 运行时，`DEEPDRILL_ROOT` 由它设好。
