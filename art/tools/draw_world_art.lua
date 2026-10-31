-- Draws the 12 layers, 24 ore overlays, 60 drill kinds and 36 rare items into Assets/Art.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> --script draw_world_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")

local function hex(s, a)
  return app.pixelColor.rgba(
    tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end

local seed = 7777
local function rnd(n)
  seed = (seed * 1103515245 + 12345) % 2147483648
  return (seed >> 16) % n
end

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c)
    if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end
  end
  function g:get(x, y) return self.px[y * w + x] end
  function g:rect(x0, y0, x1, y1, c)
    for y = y0, y1 do for x = x0, x1 do self:set(x, y, c) end end
  end
  function g:blit(o, ox, oy)
    for k, c in pairs(o.px) do self:set(k % o.w + (ox or 0), k // o.w + (oy or 0), c) end
  end
  return g
end

local function save(g, path)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/" .. path .. ".png")
end

local function outline(g, ink)
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) then
        local near = (x > 0 and g:get(x - 1, y)) or (x < g.w - 1 and g:get(x + 1, y))
          or (y > 0 and g:get(x, y - 1)) or (y < g.h - 1 and g:get(x, y + 1))
        if near then out[#out + 1] = { x, y } end
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], ink) end
end

-- Colour ramps with hue shifting: shadows lean towards blue-violet, lights towards warm yellow,
-- and saturation peaks in the mid tones. The same formula lives in Palette.cs for the back wall.
local function hsv(h, s, v)
  local c, x = v * s, v * s * (1 - math.abs((h / 60) % 2 - 1))
  local m = v - c
  local r, g, b
  if h < 60 then r, g, b = c, x, 0 elseif h < 120 then r, g, b = x, c, 0
  elseif h < 180 then r, g, b = 0, c, x elseif h < 240 then r, g, b = 0, x, c
  elseif h < 300 then r, g, b = x, 0, c else r, g, b = c, 0, x end
  return string.format("%02x%02x%02x", math.floor((r + m) * 255 + 0.5),
    math.floor((g + m) * 255 + 0.5), math.floor((b + m) * 255 + 0.5))
end

local function towards(h, target, amount)
  local d = (target - h + 540) % 360 - 180
  return (h + d * amount) % 360
end

local RAMP_V = { 0.14, 0.30, 0.46, 0.62, 0.78, 0.92 }
local RAMP_S = { 0.60, 0.68, 0.80, 0.70, 0.52, 0.34 }
local RAMP_DARK = { 0.45, 0.30, 0.14, 0, 0, 0 }
local RAMP_WARM = { 0, 0, 0, 0, 0.14, 0.28 }

local function ramp(hue, sat)
  local out = {}
  for i = 1, 6 do
    local h = towards(towards(hue, 250, RAMP_DARK[i]), 55, RAMP_WARM[i])
    out[i] = hsv(h, RAMP_S[i] * sat, RAMP_V[i])
  end
  return out
end

-- hue, saturation scale, texture; colours follow the layer table in the design data
local LAYER_HUES = {
  { 24, 0.75, "speck" },  -- 表土 棕
  { 38, 0.70, "band" },   -- 砂岩 土黄
  { 262, 0.65, "crack" }, -- 溶洞 紫蓝
  { 212, 0.85, "band" },  -- 暗河 深蓝
  { 42, 0.45, "speck" },  -- 遗迹 金灰
  { 345, 0.22, "speck" }, -- 花岗岩 灰粉
  { 356, 0.70, "crack" }, -- 莫霍 黑暗红
  { 74, 0.70, "speck" },  -- 上地幔 橄榄绿
  { 168, 0.75, "band" },  -- 过渡带 蓝绿
  { 16, 1.0, "crack" },   -- 下地幔 深红橙
  { 44, 0.85, "band" },   -- 外核 金白
  { 280, 0.18, "speck" }, -- 内核 白金
}

local LAYERS = {}
for i, v in ipairs(LAYER_HUES) do
  local r = ramp(v[1], v[2])
  LAYERS[i] = { r[1], r[2], r[3], r[4], r[5], r[6], v[3] }
end

local function ground(p, style, n)
  local g = canvas(16, 16)
  g:rect(0, 0, 15, 15, p.base)
  if style == "band" then
    g:rect(0, 5, 15, 6, p.shade)
    g:rect(0, 13, 15, 13, p.shade)
  elseif style == "crack" then
    for _ = 1, 3 do
      local x, y = rnd(16), rnd(16)
      for s = 0, 6 do g:set((x + s) % 16, (y + s // 2) % 16, p.shade) end
    end
  end
  for _ = 1, n do
    local x, y = rnd(16), rnd(16)
    local c = rnd(3) == 0 and p.light or p.shade
    g:set(x, y, c)
    if rnd(2) == 0 then g:set((x + 1) % 16, y, c) end
  end
  return g
end

for s, v in ipairs(LAYERS) do
  local i = s - 1
  local p = { ink = hex(v[1]), dark = hex(v[2]), shade = hex(v[3]), base = hex(v[4]), light = hex(v[5]), bright = hex(v[6]) }
  local style = v[7]
  save(ground(p, style, 12), "Layers/ground_" .. i .. "_a")
  save(ground(p, style, 18), "Layers/ground_" .. i .. "_b")
  save(ground(p, style, 8), "Layers/ground_" .. i .. "_c")

  -- loose block: paler and grainy, so what will fall stands out from the rock
  local loose = canvas(16, 16)
  loose:rect(0, 0, 15, 15, p.light)
  for y = 0, 15 do
    for x = 0, 15 do
      local n = (x * 7 + y * 13 + (x * y) % 5) % 9
      if n == 0 then loose:set(x, y, p.base) elseif n == 4 then loose:set(x, y, p.bright) end
    end
  end
  loose:rect(0, 15, 15, 15, p.base)
  save(loose, "Layers/loose_" .. i)

  local function edge(name, line)
    local g = canvas(16, 16)
    for k = 0, 15 do line(g, k) end
    save(g, "Layers/edge_" .. i .. "_" .. name)
  end
  edge("top", function(g, k) g:set(k, 0, p.ink) g:set(k, 1, p.dark) end)
  edge("bottom", function(g, k) g:set(k, 15, p.ink) g:set(k, 14, p.bright) end)
  edge("left", function(g, k) g:set(0, k, p.ink) g:set(1, k, p.light) end)
  edge("right", function(g, k) g:set(15, k, p.ink) g:set(14, k, p.dark) end)
end

-- 24 ores in design order: two per layer
local ORES = {
  { "6a6a78", "3a3a44", "1e1e24" }, { "f6b98a", "e08a4a", "b8693a" }, -- 煤 铜
  { "e8e8f0", "8a8a96", "5a5a64" }, { "fff4d8", "e8e8f0", "c0c0cc" }, -- 铁 银
  { "e0d0ff", "b090f0", "7050c0" }, { "ffe27a", "e8b02a", "a87818" }, -- 水晶 金
  { "f8f4f0", "e0d8e0", "b0a8c0" }, { "a0c0ff", "3060e8", "1830a0" }, -- 珍珠 蓝宝石
  { "e8c870", "b08c38", "705420" }, { "ff90a8", "e8305a", "a01440" }, -- 古币 红宝石
  { "f0f0f8", "c8c8d8", "9090a8" }, { "a8d8e8", "6aa8c0", "3a7088" }, -- 石英 钛
  { "a0a8c0", "606880", "383c50" }, { "f0f4ff", "c8d0e4", "98a2bc" }, -- 玄武晶 铂
  { "d0f070", "90c040", "508020" }, { "e8fbff", "90e8f8", "40b0d8" }, -- 橄榄石 钻石
  { "90a0ff", "5060e0", "2830a0" }, { "ff90c0", "e84080", "a01850" }, -- 林伍德石 尖晶石
  { "c0e890", "80b050", "488028" }, { "f0a0e0", "c050b0", "802080" }, -- 布里奇曼石 后钙钛矿
  { "fff0c0", "e8c870", "b09040" }, { "c0f0ff", "60b0f0", "2070c0" }, -- 液态铁镍 地磁晶
  { "ffffff", "ffe27a", "f08a4a" }, { "f4d0ff", "c05cff", "6a1ad0" }, -- 星核碎晶 地心之石
}
local INK = hex("1a1210")
local LAYOUT = { { 2, 2, 3 }, { 9, 1, 2 }, { 11, 6, 3 }, { 4, 8, 3 }, { 9, 11, 2 }, { 1, 11, 2 } }
for i, c in ipairs(ORES) do
  local g = canvas(16, 16)
  local hi, mid, lo = hex(c[1]), hex(c[2]), hex(c[3])
  for _, n in ipairs(LAYOUT) do
    local x, y, s = n[1], n[2], n[3]
    g:rect(x, y, x + s - 1, y + s - 1, mid)
    g:set(x, y, hi) g:set(x + 1, y, hi) g:set(x, y + 1, hi)
    g:set(x + s - 1, y + s - 1, lo)
  end
  outline(g, hex(c[3]))
  save(g, "Layers/ore_" .. (i - 1))

  local icon = canvas(8, 8)
  icon:rect(2, 1, 5, 6, mid) icon:rect(1, 2, 6, 5, mid)
  icon:rect(2, 2, 3, 3, hi) icon:rect(4, 4, 5, 5, lo)
  outline(icon, INK)
  save(icon, "Items/oreicon_" .. (i - 1))
end

local rock = canvas(16, 16)
local R1, R2, R3, R4 = hex("3a3a44"), hex("5c5c6a"), hex("8a8a98"), hex("b4b4c2")
for y = 2, 13 do
  for x = 2, 13 do
    local dx, dy = (x - 7.5) / 6, (y - 7.5) / 5.6
    local d = dx * dx + dy * dy
    if d <= 1 then
      local c = R3
      if d > 0.72 then c = (dx + dy < 0) and R4 or R1
      elseif dx + dy < -0.5 then c = R4
      elseif dx + dy > 0.6 then c = R2 end
      rock:set(x, y, c)
    end
  end
end
outline(rock, INK)
save(rock, "Layers/hard_overlay")

local chest = canvas(16, 16)
local W1, W2, W3, GOLD, GOLD_HI = hex("6b4630"), hex("b8693a"), hex("e08a4a"), hex("e8b02a"), hex("ffe27a")
chest:rect(2, 5, 13, 12, W2)
chest:rect(2, 5, 13, 5, W3)
chest:rect(2, 12, 13, 12, W1)
chest:rect(2, 8, 13, 8, INK)
chest:rect(4, 6, 4, 12, W1) chest:rect(11, 6, 11, 12, W1)
chest:rect(7, 7, 8, 10, GOLD) chest:set(7, 7, GOLD_HI)
outline(chest, INK)
save(chest, "Layers/chest_overlay")

-- blocks with their own behaviour, drawn to read at a glance
local superHard = canvas(16, 16)
local Q1, Q2, Q3 = hex("3a3648"), hex("6a6480"), hex("a8a0c0")
for y = 1, 14 do
  for x = 1, 14 do
    local c = ((x + y) % 5 == 0) and Q3 or (((x * 2 + y) % 7 == 0) and Q1 or Q2)
    superHard:set(x, y, c)
  end
end
superHard:rect(1, 1, 14, 1, Q3)
superHard:rect(14, 2, 14, 14, Q1)
outline(superHard, INK)
save(superHard, "Layers/superhard_overlay")

local water = canvas(16, 16)
water:rect(0, 0, 15, 15, hex("2a6ab0"))
for y = 0, 15 do
  for x = 0, 15 do
    if (x + y * 2) % 8 == 0 then water:set(x, y, hex("70b8e8")) end
    if (x * 2 + y) % 11 == 0 then water:set(x, y, hex("1a4a88")) end
  end
end
save(water, "Layers/water_block")

local magma = canvas(16, 16)
magma:rect(0, 0, 15, 15, hex("7a1a10"))
for y = 0, 15 do
  for x = 0, 15 do
    if (x * 3 + y * 2) % 7 < 2 then magma:set(x, y, hex("f07a20")) end
    if (x + y * 3) % 13 == 0 then magma:set(x, y, hex("ffe27a")) end
  end
end
save(magma, "Layers/magma_block")

-- neutral shells the game tints per guardian
local shell = canvas(16, 16)
shell:rect(0, 0, 15, 15, hex("9a9a9a"))
shell:rect(0, 0, 15, 2, hex("ffffff"))
shell:rect(0, 3, 15, 4, hex("c8c8c8"))
shell:rect(0, 12, 15, 13, hex("5a5a5a"))
shell:rect(0, 14, 15, 15, hex("3a3a3a"))
for x = 3, 15, 8 do shell:rect(x, 3, x, 11, hex("5a5a5a")) end
save(shell, "Layers/boss_shell")
local hide = canvas(16, 16)
hide:rect(0, 0, 15, 15, hex("b0b0b0"))
for y = 0, 15 do
  for x = 0, 15 do
    if (x + y * 3) % 7 == 0 then hide:set(x, y, hex("e0e0e0")) end
    if (x * 3 + y) % 11 == 0 then hide:set(x, y, hex("606060")) end
  end
end
save(hide, "Layers/boss_hide")

-- drills, 8x12, 4 frames. Material sets the colours; the family sets the silhouette. Each tier
-- has its own hue and brightness, and the same mid colours live in Palette.Tier.
-- hi, mid, lo, outline, strip, gem
local MATERIALS = {
  { "c8935a", "8f5a30", "5c3518", "2e1a0c" },                     -- 木
  { "8e8c88", "646260", "42413f", "201f1e" },                     -- 石，暗灰
  { "ffb070", "dc7030", "9c4018", "4a1c08" },                     -- 铜
  { "8fa6c4", "5c7696", "3a4e6c", "1a2436" },                     -- 铁，蓝灰
  { "d4ecff", "86b8ea", "3e70b4", "142a54", "f4fbff" },           -- 钢，亮银带蓝光，边条
  { "ffffff", "e4e9f2", "a4aec4", "4c546a", "b4c4e8" },           -- 银，亮白
  { "fff090", "ffc420", "c07a08", "5a3000", "fff8c8", "e8304a" }, -- 金，宝石
  { "fbd0ff", "d484ff", "8444d4", "f0a8ff", "ffffff", "ffffff" }, -- 水晶，紫，发光
  { "e8ffff", "70f0ff", "1ea4e4", "a8f8ff", "ffffff", "ff70c8" }, -- 钻石，青
  { "ccffdc", "50f0a0", "10a070", "90ffc8", "f0fff4", "ffe27a" }, -- 秘银，绿
  { "7a4ab0", "2e2248", "140f22", "c060ff", "e0a0ff", "ff4060" }, -- 黑曜，近黑带紫光
  { "fff8c0", "ffa020", "e03010", "ffe070", "ffffff", "ffffff" }, -- 星核，炽热
}
local BLUE_GLASS = "4fb8e8"
local STRIP_FROM, TWIN_STRIP_FROM, STAR_TIER = 5, 9, 12
local STRIP_ROW = { std = 4, swift = 3, heavy = 4, blast = 4, magnet = 4 }
-- spots just outside the body where a star core throws sparks, one set per frame
local SPARKS = { { { 0, 0 }, { 7, 9 } }, { { 7, 0 }, { 1, 10 } }, { { 0, 6 }, { 6, 11 } }, { { 7, 5 }, { 1, 0 } } }

local FAMILIES = { "std", "swift", "heavy", "blast", "magnet" }

local function drillFrame(family, m, f, tier)
  local hi, mid, lo = hex(m[1]), hex(m[2]), hex(m[3])
  local g = canvas(8, 12)
  local glass = hex(m[6] or BLUE_GLASS)
  if family == "swift" then
    -- slim with a long point
    g:rect(2, 1, 5, 4, mid) g:rect(2, 1, 5, 1, hi) g:rect(5, 2, 5, 4, lo)
    for y = 5, 11 do
      local half = (y < 8) and 1 or 0
      g:rect(3 - half, y, 4 + half, y, ((y + f) % 2 == 0) and hi or lo)
    end
    g:set(3, 2, glass)
  elseif family == "heavy" then
    -- wide and blunt
    g:rect(0, 1, 7, 5, mid) g:rect(0, 1, 7, 1, hi) g:rect(7, 2, 7, 5, lo)
    for y = 6, 10 do
      local half = 3 - (y - 6) // 2
      for x = 3 - half, 4 + half do
        g:set(x, y, ((x + y + f) % 3 == 0) and lo or hi)
      end
    end
    g:rect(3, 2, 4, 3, glass)
  elseif family == "blast" then
    -- round body with a hot nose
    g:rect(1, 1, 6, 5, mid) g:rect(2, 0, 5, 0, hi) g:rect(6, 2, 6, 5, lo)
    g:rect(3, 2, 4, 3, hex(m[6] or "e84a2e"))
    for y = 6, 10 do
      local half = (10 - y) // 2
      g:rect(3 - half, y, 4 + half, y, ((y + f) % 2 == 0) and hex("ffb040") or lo)
    end
  elseif family == "magnet" then
    -- two prongs either side of the bit
    g:rect(1, 1, 6, 4, mid) g:rect(1, 1, 6, 1, hi)
    g:rect(0, 3, 0, 7, hex("e84a2e")) g:rect(7, 3, 7, 7, hex("4a7ae8"))
    for y = 5, 10 do
      local half = (10 - y) // 3
      g:rect(3 - half, y, 4 + half, y, ((y + f) % 2 == 0) and hi or lo)
    end
    g:set(3, 2, glass)
  else
    g:rect(1, 1, 6, 4, mid) g:rect(1, 1, 6, 1, hi) g:rect(6, 2, 6, 4, lo)
    g:rect(3, 2, 4, 3, glass)
    for y = 5, 10 do
      local half = (10 - y) // 2
      for x = 3 - half, 4 + half do
        g:set(x, y, ((x + y + f) % 3 == 0) and lo or hi)
      end
    end
  end

  if tier >= STRIP_FROM then
    local strip = hex(m[5])
    for x = 0, 7 do
      if g:get(x, STRIP_ROW[family]) then g:set(x, STRIP_ROW[family], strip) end
      if tier >= TWIN_STRIP_FROM and g:get(x, 1) then g:set(x, 1, strip) end
    end
  end
  outline(g, hex(m[4]))
  if tier == STAR_TIER then
    for _, spot in ipairs(SPARKS[f + 1]) do g:set(spot[1], spot[2], hex("ffffff")) end
  end
  return g
end

for fi, family in ipairs(FAMILIES) do
  for ti, m in ipairs(MATERIALS) do
    local sheet = canvas(32, 12)
    for f = 0, 3 do sheet:blit(drillFrame(family, m, f, ti), f * 8, 0) end
    save(sheet, "Drill/drill_" .. (fi - 1) .. "_" .. (ti - 1))
  end
end

-- rare items: three per layer in the layer's colours (crystal, fossil, relic)
for layer = 1, 12 do
  local v = LAYERS[layer]
  local hi, mid, lo = hex(v[6]), hex(v[5]), hex(v[3])
  for j = 1, 3 do
    local g = canvas(16, 16)
    if j == 1 then
      for y = 1, 14 do
        local half = 6 - math.abs(y - 7.5) * 0.85
        for x = 2, 13 do
          local n = x - 7.5
          if math.abs(n) <= half then g:set(x, y, n < -1 and hi or (n < 2 and mid or lo)) end
        end
      end
    elseif j == 2 then
      for y = 2, 13 do
        for x = 2, 13 do
          local d = math.sqrt((x - 7.5) ^ 2 + (y - 7.5) ^ 2)
          if d <= 5.8 then
            local ring = math.floor(d * 1.2 + math.atan(y - 7.5, x - 7.5) * 0.9) % 3
            g:set(x, y, ring == 0 and hi or (ring == 1 and mid or lo))
          end
        end
      end
    else
      g:rect(3, 2, 12, 13, mid)
      g:rect(3, 2, 12, 3, hi)
      g:rect(11, 4, 12, 13, lo)
      g:rect(6, 6, 9, 9, hex("ffe27a"))
      g:rect(7, 7, 8, 8, hex("e84a2e"))
    end
    outline(g, INK)
    save(g, "Items/rare_" .. layer .. "_" .. j)
  end
end

-- 12 guardians, 64x32 over the two-row boss wall. Each has its own silhouette; the body uses the
-- layer's ramp (the wall itself is ramp 1-2, so bodies sit at 3-6) plus a few accents of its own.
local function ellipse(g, cx, cy, rx, ry, c)
  for y = math.floor(cy - ry), math.ceil(cy + ry) do
    for x = math.floor(cx - rx), math.ceil(cx + rx) do
      if ((x - cx) / rx) ^ 2 + ((y - cy) / ry) ^ 2 <= 1 then g:set(x, y, c) end
    end
  end
end

local function line(g, x0, y0, x1, y1, c, thick)
  local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0))
  for i = 0, n do
    local t = n == 0 and 0 or i / n
    local x, y = math.floor(x0 + (x1 - x0) * t + 0.5), math.floor(y0 + (y1 - y0) * t + 0.5)
    g:set(x, y, c)
    if thick then g:set(x + 1, y, c); g:set(x, y + 1, c) end
  end
end

local function tri(g, ax, ay, bx, by, cx, cy, c)
  local function side(px, py, qx, qy, x, y) return (qx - px) * (y - py) - (qy - py) * (x - px) end
  for y = math.min(ay, by, cy), math.max(ay, by, cy) do
    for x = math.min(ax, bx, cx), math.max(ax, bx, cx) do
      local d1, d2, d3 = side(ax, ay, bx, by, x, y), side(bx, by, cx, cy, x, y), side(cx, cy, ax, ay, x, y)
      if not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0)) then g:set(x, y, c) end
    end
  end
end

-- copies the left half onto the right, so a face is drawn once
local function mirror(g)
  for y = 0, g.h - 1 do
    for x = 0, g.w // 2 - 1 do g.px[y * g.w + g.w - 1 - x] = g.px[y * g.w + x] end
  end
end

-- top edges of a colour catch the light, bottom edges fall into shade
local function rim(g, base, hi, lo)
  local paint = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if g:get(x, y) == base then
        if y == 0 or not g:get(x, y - 1) then paint[#paint + 1] = { x, y, hi }
        elseif y == g.h - 1 or not g:get(x, y + 1) then paint[#paint + 1] = { x, y, lo } end
      end
    end
  end
  for _, p in ipairs(paint) do g:set(p[1], p[2], p[3]) end
end

-- recolours pixels of one colour where a pattern says so: grain, speckle, stripes
local function texture(g, base, c, test)
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if g:get(x, y) == base and test(x, y) then g:set(x, y, c) end
    end
  end
end

local function eye(g, x, y, w, h, glow)
  g:rect(x, y, x + w - 1, y + h - 1, glow)
  g:set(x, y, hex("ffffff"))
end

local BOSS_INK = hex("140c12")
local BOSS_FACES = {}
local WHITE = hex("ffffff")

local GUARDIANS = {
  -- 巨型木根: a gnarled stump with knot-hole eyes, roots clawing out to both sides
  function(g, p)
    for i = 0, 1 do
      line(g, 16, 18 + i * 5, 3, 26 + i * 4, p[3], true)
      line(g, 16, 12 + i * 3, 4, 5 + i * 6, p[3], true)
    end
    line(g, 3, 26, 0, 22, p[3]); line(g, 4, 5, 1, 8, p[3])
    ellipse(g, 32, 17, 17, 14, p[4])
    texture(g, p[4], p[3], function(x, y) return (x + (y // 5) % 2) % 4 == 0 end)
    ellipse(g, 24, 14, 4, 3, p[1])
    eye(g, 23, 13, 3, 2, hex("ffb43a"))
    tri(g, 21, 23, 32, 21, 32, 28, p[1])
    for x = 22, 31, 3 do tri(g, x, 22, x + 2, 22, x + 1, 24, p[6]) end
    mirror(g)
    -- one broken branch and a few leaves keep it from looking carved
    line(g, 40, 4, 46, 0, p[3], true)
    ellipse(g, 30, 2, 3, 2, hex("76a83a")); g:set(29, 1, hex("a8d45a")); g:set(30, 1, hex("a8d45a"))
    ellipse(g, 47, 1, 2, 1, hex("76a83a"))
    rim(g, p[4], p[5], p[2])
  end,

  -- 岩甲虫: a domed beetle shell split down the middle, mandibles up towards the drill
  function(g, p)
    for i = 0, 2 do
      local y = 14 + i * 6
      line(g, 16, y, 6, y - 3 + i * 2, p[3], true)
      line(g, 6, y - 3 + i * 2, 1, y + 2 + i, p[3])
    end
    ellipse(g, 32, 21, 20, 10, p[4])
    texture(g, p[4], p[3], function(_, y) return y % 6 == 0 end)
    ellipse(g, 24, 15, 4, 2, p[6])
    ellipse(g, 32, 8, 9, 5, p[3])
    tri(g, 24, 6, 27, 4, 20, 0, p[6])
    tri(g, 24, 6, 20, 0, 22, 6, p[5])
    eye(g, 27, 7, 2, 2, hex("e84a2e"))
    mirror(g)
    g:rect(31, 12, 32, 31, p[2])
    rim(g, p[4], p[5], p[2])
    rim(g, p[3], p[4], p[2])
  end,

  -- 水晶巨像: a shield-shaped stone face crowned with cyan crystal spires
  function(g, p)
    local cdark, cmid, clight = hex("2a8ab8"), hex("5ac8e0"), hex("c8fbff")
    local function crystal(x0, x1, top, base)
      local mid = (x0 + x1) // 2
      tri(g, x0, base, x1, base, mid, top, cmid)
      tri(g, x0, base, mid, base, mid, top, clight)
      line(g, mid, top, mid, base, cdark)
    end
    crystal(4, 10, 14, 30)
    crystal(11, 18, 6, 26)
    g:rect(19, 9, 32, 20, p[3])
    tri(g, 19, 20, 32, 20, 32, 31, p[3])
    crystal(22, 28, 0, 10)
    crystal(29, 34, 0, 9)
    line(g, 20, 21, 27, 28, p[4])
    line(g, 21, 11, 26, 13, p[4])
    g:rect(23, 15, 28, 16, p[1])
    eye(g, 24, 15, 4, 1, clight)
    mirror(g)
    line(g, 27, 25, 31, 24, p[1]); line(g, 31, 24, 34, 26, p[1]); line(g, 34, 26, 37, 24, p[1])
    rim(g, p[3], p[5], p[2])
  end,

  -- 盲眼巨鱼: a wide gaping head with milky blind eyes, needle teeth and trailing barbels
  function(g, p)
    tri(g, 9, 9, 0, 1, 3, 18, p[3])
    tri(g, 9, 12, 2, 18, 1, 24, p[3])
    ellipse(g, 32, 15, 24, 14, p[4])
    texture(g, p[4], p[5], function(_, y) return y >= 20 end)
    for x = 22, 31, 4 do tri(g, x, 2, x + 3, 2, x + 2, -1, p[3]) end
    ellipse(g, 18, 11, 3.5, 3, hex("b0c0cc"))
    ellipse(g, 18, 11, 2, 2, hex("e4eef2"))
    ellipse(g, 32, 23, 16, 6, hex("0a0a1a"))
    for x = 18, 31, 3 do
      tri(g, x, 18, x + 1, 18, x, 21, p[6])
      tri(g, x + 1, 29, x + 2, 29, x + 1, 26, p[6])
    end
    line(g, 16, 23, 7, 28, p[5]); line(g, 7, 28, 4, 31, p[5])
    g:set(12, 6, hex("70e8f8")); g:set(9, 14, hex("70e8f8")); g:set(14, 4, hex("70e8f8"))
    mirror(g)
    rim(g, p[4], p[6], p[2])
  end,

  -- 石像守卫: a carved stone mask between two pillars, a gold rune burning on its brow
  function(g, p)
    local gold, goldDark = hex("ffd24a"), hex("b8862a")
    g:rect(7, 4, 13, 31, p[3])
    g:rect(6, 4, 14, 5, p[5]); g:rect(7, 11, 13, 11, gold); g:rect(7, 24, 13, 24, gold)
    g:rect(15, 2, 32, 31, p[4])
    tri(g, 15, 2, 18, 2, 15, 5, nil)
    g:rect(16, 3, 32, 3, p[3])
    g:rect(17, 8, 32, 10, p[5])
    g:rect(20, 12, 27, 17, p[1])
    g:rect(22, 14, 25, 15, gold); g:set(22, 14, WHITE)
    g:rect(30, 12, 32, 20, p[3]); g:rect(30, 12, 30, 20, p[5])
    g:rect(22, 23, 32, 27, p[1])
    for x = 23, 32, 3 do g:rect(x, 23, x, 27, p[4]) end
    mirror(g)
    -- the rune, and cracks that only run down one side
    g:set(32, 3, gold); g:set(31, 4, gold); g:set(32, 4, goldDark); g:set(33, 4, gold); g:set(32, 5, gold)
    g:set(32, 6, goldDark)
    line(g, 44, 3, 41, 9, p[2]); line(g, 41, 9, 43, 13, p[2])
    line(g, 18, 26, 21, 31, p[2])
  end,

  -- 花岗岩魔像: a small sunken head between huge speckled shoulders, fists planted at the sides
  function(g, p)
    ellipse(g, 15, 19, 13, 11, p[4])
    g:rect(0, 22, 9, 31, p[5])
    for y = 24, 30, 3 do g:rect(1, y, 8, y, p[3]) end
    g:rect(23, 6, 32, 25, p[4])
    g:rect(22, 9, 32, 11, p[5])
    g:rect(26, 13, 29, 15, p[1])
    eye(g, 27, 14, 2, 1, hex("ff8a2a"))
    g:rect(28, 20, 32, 20, p[1])
    line(g, 14, 9, 18, 16, p[2]); line(g, 18, 16, 14, 26, p[2])
    mirror(g)
    texture(g, p[4], p[6], function(x, y) return (x * 7 + y * 13) % 11 == 0 end)
    texture(g, p[4], p[2], function(x, y) return (x * 5 + y * 3) % 13 == 0 end)
    rim(g, p[4], p[5], p[3])
  end,

  -- 莫霍守门人: a horned helmet with a T-shaped visor standing in front of a portcullis
  function(g, p)
    local iron, ironHi, bone, boneLo = hex("4a4a5a"), hex("8a8a9a"), hex("e8dcc0"), hex("a89878")
    for x = 9, 32, 5 do g:rect(x, 18, x + 1, 31, iron); g:set(x, 18, ironHi) end
    g:rect(6, 24, 32, 25, iron); g:rect(6, 24, 32, 24, ironHi)
    g:rect(6, 29, 32, 30, iron)
    ellipse(g, 32, 12, 14, 11, p[4])
    tri(g, 19, 6, 22, 12, 3, 0, bone)
    tri(g, 19, 6, 3, 0, 8, 1, boneLo)
    g:rect(18, 21, 32, 23, p[5])
    g:rect(22, 12, 32, 14, BOSS_INK)
    g:rect(30, 14, 32, 22, BOSS_INK)
    eye(g, 24, 13, 3, 1, hex("ff3a2a"))
    g:set(20, 18, ironHi); g:set(20, 9, ironHi)
    mirror(g)
    rim(g, p[4], p[6], p[2])
    -- magma glowing through the gate behind
    g:set(14, 27, hex("f07a20")); g:set(15, 27, hex("ffc050")); g:set(48, 21, hex("f07a20"))
  end,

  -- 熔岩巨蛇: an arrow head rearing over its own coils, magma showing between the scales
  function(g, p)
    local magma, hot = hex("f07a20"), hex("ffe27a")
    for x = 0, 63 do
      local cy = math.floor(24 + 4 * math.sin(x / 6) + 0.5)
      g:rect(x, cy - 3, x, cy + 3, p[4])
      g:set(x, cy + 3, p[5])
      if x % 5 == 0 then g:rect(x, cy - 2, x, cy + 1, magma) end
    end
    g:rect(29, 14, 34, 22, p[4])
    ellipse(g, 32, 8, 12, 6, p[4])
    tri(g, 20, 8, 44, 8, 32, 20, p[4])
    for x = 22, 42, 4 do g:set(x, 2, magma); g:set(x + 1, 1, hot) end
    texture(g, p[4], p[3], function(x, y) return y < 18 and (x + y) % 4 == 0 end)
    g:rect(24, 6, 27, 8, hot); g:rect(26, 6, 26, 8, BOSS_INK)
    g:rect(37, 6, 40, 8, hot); g:rect(37, 6, 37, 8, BOSS_INK)
    g:set(29, 12, BOSS_INK); g:set(35, 12, BOSS_INK)
    tri(g, 27, 15, 28, 15, 28, 18, WHITE); tri(g, 36, 15, 37, 15, 36, 18, WHITE)
    line(g, 32, 19, 32, 24, hex("e84a2e")); g:set(31, 25, hex("e84a2e")); g:set(33, 25, hex("e84a2e"))
    rim(g, p[4], p[6], p[2])
  end,

  -- 相变之灵: one spirit caught mid-change, faceted solid on the left, melting liquid on the right
  function(g, p)
    ellipse(g, 32, 13, 17, 12, p[5])
    for x = 15, 49 do
      local tail = math.floor(26 + 3 * math.sin(x / 2.5) + 0.5)
      g:rect(x, 20, x, tail, p[5])
    end
    for y = 0, g.h - 1 do
      for x = 0, 31 do
        if g:get(x, y) == p[5] and (x + y) % 7 < 2 then g:set(x, y, p[6]) end
        if g:get(x, y) == p[5] and (x - y) % 9 == 0 then g:set(x, y, p[4]) end
      end
      for x = 32, 63 do
        if g:get(x, y) == p[5] then
          g:set(x, y, (y + math.floor(2 * math.sin(x / 3))) % 5 == 0 and hex("e8ffff", 200) or hex("5ad8c8", 190))
        end
      end
    end
    for _, x in ipairs({ 38, 44, 50 }) do g:rect(x, 22, x, 29, hex("5ad8c8", 190)); g:set(x, 31, hex("e8ffff")) end
    ellipse(g, 25, 12, 3, 4, BOSS_INK); g:set(24, 10, WHITE)
    ellipse(g, 39, 12, 3, 4, BOSS_INK); g:rect(39, 16, 39, 19, BOSS_INK)
    line(g, 26, 20, 30, 19, BOSS_INK); line(g, 31, 20, 34, 21, BOSS_INK); line(g, 35, 21, 38, 19, BOSS_INK)
    tri(g, 6, 8, 9, 5, 9, 11, p[6]); tri(g, 4, 20, 6, 18, 6, 22, p[6])
    ellipse(g, 57, 7, 2, 2, hex("5ad8c8", 190)); ellipse(g, 59, 18, 1, 1, hex("5ad8c8", 190))
  end,

  -- 地幔巨鲸: a vast head-on whale, pleated throat, twin blowholes spouting magma past the drill
  function(g, p)
    local spout, hot = hex("f07a20"), hex("ffc050")
    tri(g, 10, 24, 20, 28, 0, 31, p[3])
    ellipse(g, 32, 18, 30, 13, p[4])
    for y = 22, 31, 2 do
      for x = 0, 31 do if g:get(x, y) == p[4] then g:set(x, y, p[5]) end end
    end
    for x = 4, 31 do
      local y = math.floor(17 + 3 * ((x - 4) / 27) ^ 2 + 0.5)
      g:rect(x, y, x, y + 1, BOSS_INK)
    end
    ellipse(g, 11, 13, 3, 2.5, p[1])
    eye(g, 10, 13, 2, 1, hot)
    for _, b in ipairs({ { 18, 9 }, { 14, 11 }, { 21, 13 } }) do
      ellipse(g, b[1], b[2], 1.5, 1.5, p[6]); g:set(b[1], b[2], spout)
    end
    ellipse(g, 24, 6, 2, 1, BOSS_INK)
    for i, d in ipairs({ { 0, 4 }, { -1, 3 }, { 1, 3 }, { -2, 1 }, { 0, 2 }, { -3, 0 }, { -1, 0 } }) do
      g:set(24 + d[1], d[2], i % 2 == 0 and hot or spout)
    end
    g:set(24, 1, hex("ffe27a"))
    mirror(g)
    rim(g, p[4], p[6], p[2])
  end,

  -- 磁暴旋涡: a three-armed spiral round a storm eye, held between a north and a south pole
  function(g, p)
    ellipse(g, 32, 16, 25, 14, p[2])
    for arm = 0, 2 do
      for s = 0, 120 do
        local t = s / 10
        local r = 2 + t * 1.9
        local a = t + arm * 2.094
        local x, y = math.floor(32 + r * math.cos(a) * 1.6 + 0.5), math.floor(16 + r * math.sin(a) * 0.85 + 0.5)
        if g:get(x, y) then g:set(x, y, s < 50 and p[6] or p[5]) end
      end
    end
    ellipse(g, 32, 16, 4, 3, hex("ffe27a"))
    ellipse(g, 32, 16, 2.5, 2, WHITE)
    g:rect(32, 15, 32, 17, BOSS_INK)
    local red, blue = hex("e84a2e"), hex("3a8ae8")
    g:rect(0, 11, 5, 20, red); g:rect(0, 11, 5, 11, hex("ff9a7a")); g:rect(1, 14, 4, 14, WHITE)
    g:rect(58, 11, 63, 20, blue); g:rect(58, 11, 63, 11, hex("8ac8ff"))
    g:rect(59, 14, 62, 14, WHITE); g:rect(60, 13, 61, 15, WHITE)
    local arc = hex("c8f0ff")
    line(g, 6, 15, 9, 12, arc); line(g, 9, 12, 11, 17, arc); line(g, 11, 17, 14, 14, arc)
    line(g, 57, 16, 54, 19, arc); line(g, 54, 19, 52, 14, arc); line(g, 52, 14, 49, 18, arc)
    line(g, 30, 0, 33, 2, arc); line(g, 33, 2, 31, 4, arc)
  end,

  -- 地核心脏: a white-hot heart with arteries, veins of light running out through the wall
  function(g, p)
    local hot, mid, cool = hex("fff0b0"), hex("ffc860"), hex("e8803a")
    line(g, 15, 17, 6, 21, cool, true); line(g, 6, 21, 0, 19, cool)
    line(g, 6, 21, 3, 28, cool); line(g, 13, 9, 4, 5, cool); line(g, 4, 5, 1, 7, cool)
    line(g, 20, 25, 13, 31, cool)
    ellipse(g, 25, 12, 10, 9, cool)
    tri(g, 15, 14, 32, 14, 32, 31, cool)
    g:rect(26, 0, 30, 5, p[3]); g:rect(26, 0, 30, 0, p[5]); g:rect(27, 1, 29, 1, BOSS_INK)
    mirror(g)
    for y = 0, g.h - 1 do
      for x = 0, g.w - 1 do
        if g:get(x, y) == cool then
          local d = ((x - 32) / 14) ^ 2 + ((y - 15) / 11) ^ 2
          if d < 0.15 then g:set(x, y, WHITE) elseif d < 0.4 then g:set(x, y, hot)
          elseif d < 0.75 then g:set(x, y, mid) end
        end
      end
    end
    g:rect(32, 10, 32, 20, WHITE); g:rect(27, 15, 37, 15, WHITE)
    g:set(10, 3, WHITE); g:set(56, 26, WHITE); g:set(52, 4, hot); g:set(8, 27, hot)
  end,
}

for layer = 1, 12 do
  local v = LAYERS[layer]
  local p = {}
  for i = 1, 6 do p[i] = hex(v[i]) end
  local g = canvas(64, 32)
  GUARDIANS[layer](g, p)
  outline(g, BOSS_INK)
  save(g, "Layers/boss_face_" .. (layer - 1))
  BOSS_FACES[layer] = g
end

-- optional: --script-param preview=<dir> also writes each guardian on its wall as seen in game,
-- with a drill standing on top, for reviewing the designs outside Unity
local previewDir = app.params["preview"]
if previewDir then
  local drill = Image { fromFile = dir .. "/Drill/drill_0_4.png" }
  for layer = 1, 12 do
    local v = LAYERS[layer]
    local p = {}
    for i = 1, 6 do p[i] = hex(v[i]) end
    local g = canvas(112, 64)
    local wallTop = 24
    for y = 0, 63 do
      for x = 0, 111 do
        local n = (x * 73856093 ~ y * 19349663) % 97
        if y >= wallTop and y < wallTop + 32 then
          g:set(x, y, n % 5 == 0 and p[2] or p[1])
        elseif y < wallTop and x >= 44 and x < 68 then
          g:set(x, y, hex("1c1620"))
        else
          g:set(x, y, n % 14 == 0 and p[5] or n % 7 == 0 and p[2] or p[3])
        end
      end
    end
    -- the face is centred on the first wall row, as WorldView places it
    g:blit(BOSS_FACES[layer], 56 - 32, wallTop + 8 - 16)
    for y = 0, drill.height - 1 do
      for x = 0, 7 do
        local c = drill:getPixel(x, y)
        if app.pixelColor.rgbaA(c) > 0 then g:set(52 + x, wallTop - drill.height + y, c) end
      end
    end
    local img = Image(g.w, g.h, ColorMode.RGB)
    for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
    img:saveAs(previewDir .. "/boss_preview_" .. (layer - 1) .. ".png")
  end
end

-- effects: a soft flash disc, an expanding shock ring and drifting smoke
local disc = canvas(16, 16)
for y = 0, 15 do
  for x = 0, 15 do
    local d = math.sqrt((x - 7.5) ^ 2 + (y - 7.5) ^ 2)
    if d <= 7.8 then disc:set(x, y, hex("ffffff", d < 5 and 255 or 170)) end
  end
end
save(disc, "Items/fx_circle")

local ring = canvas(128, 32)
for f = 0, 3 do
  local r = 6 + f * 3
  for y = 0, 31 do
    for x = 0, 31 do
      local d = math.sqrt((x - 15.5) ^ 2 + (y - 15.5) ^ 2)
      if math.abs(d - r) < 1.2 - f * 0.2 then ring:set(x + f * 32, y, hex("fff0c0", 230 - f * 50)) end
    end
  end
end
save(ring, "Items/fx_ring")

local smoke = canvas(64, 16)
for f = 0, 3 do
  for i = 1, 5 do
    local cx, cy = 3 + rnd(10), 3 + rnd(10)
    local r = 2 + f * 0.6
    for y = 0, 15 do
      for x = 0, 15 do
        if math.sqrt((x - cx) ^ 2 + (y - cy) ^ 2) <= r and (x + y + f) % (f + 2) ~= 0 then
          smoke:set(x + f * 16, y, hex(i % 2 == 0 and "5a5660" or "3e3a44", 210 - f * 30))
        end
      end
    end
  end
end
save(smoke, "Items/fx_smoke")

-- a soft frame for screen-edge glows; white so the game can tint it
local vignette = canvas(112, 90)
for y = 0, 89 do
  for x = 0, 111 do
    local d = math.min(x, 111 - x, y, 89 - y)
    if d < 12 then
      local dither = (x + y) % 2 == 0 and 1 or 0
      local alpha = math.floor((12 - d) / 12 * 220)
      if d < 8 or dither == 1 then vignette:set(x, y, hex("ffffff", alpha)) end
    end
  end
end
save(vignette, "Items/fx_vignette")

-- parallax backdrops, 64x64 and seamless: far rock, a mid feature for the layer, near hanging things
local function backdrop(dark, mid, kind, hanging)
  local g = canvas(64, 64)
  if kind == "far" then
    g:rect(0, 0, 63, 63, dark)
    for _ = 1, 26 do
      local cx, cy, r = rnd(64), rnd(64), 2 + rnd(5)
      for y = -r, r do
        for x = -r, r do
          if x * x + y * y <= r * r then g:set((cx + x) % 64, (cy + y) % 64, mid) end
        end
      end
    end
    return g
  end
  for n = 0, 3 do
    local cx, tall = 8 + n * 16 + rnd(5), 14 + rnd(hanging and 12 or 26)
    for y = 0, tall do
      local half = math.max(0, math.floor((tall - y) * (kind == "column" and 0.12 or 0.28)) + (kind == "column" and 2 or 0))
      for x = cx - half, cx + half do
        local row = hanging and y or (63 - y)
        g:set(x % 64, row, (x + y) % 5 == 0 and mid or dark)
      end
    end
  end
  return g
end

local MID_KINDS = { "spike", "spike", "spike", "spike", "column", "spike", "spike", "column", "spike", "spike", "spike", "column" }
for i, v in ipairs(LAYERS) do
  local dark, shade = hex(v[1]), hex(v[2])
  save(backdrop(hex(v[1]), hex(v[2]), "far", false), "Layers/parallax_" .. (i - 1) .. "_far")
  save(backdrop(dark, shade, MID_KINDS[i], false), "Layers/parallax_" .. (i - 1) .. "_mid")
  save(backdrop(dark, shade, "spike", true), "Layers/parallax_" .. (i - 1) .. "_near")
end

-- small decals that break up plain ground: pebbles, a bone, a root, a crack of quartz
local DECAL = { "6a6a78", "e0d8c8", "6b4630", "c8d0e0" }
for d = 1, 4 do
  local g = canvas(16, 16)
  local c = hex(DECAL[d])
  if d == 1 then g:rect(4, 9, 6, 10, c) g:rect(10, 5, 11, 6, c) g:set(8, 12, c)
  elseif d == 2 then g:rect(4, 8, 11, 8, c) g:rect(3, 7, 4, 9, c) g:rect(11, 7, 12, 9, c)
  elseif d == 3 then for i = 0, 8 do g:set(3 + i, 4 + i // 2 + (i % 3 == 0 and 1 or 0), c) end
  else for i = 0, 5 do g:set(5 + i, 11 - i, c) end g:set(9, 9, c) end
  save(g, "Layers/decal_" .. (d - 1))
end

local plain = canvas(16, 16)
plain:rect(0, 0, 15, 15, hex("ffffff"))
save(plain, "Layers/plain_tile")

-- skill tree: pixel octagons in white for the game to tint, in three sizes
local function octagon(size, inset)
  local cut = math.floor(size * 0.3)
  local inside = function(x, y)
    local a, b = math.min(x, size - 1 - x), math.min(y, size - 1 - y)
    return a >= inset and b >= inset and a + b >= cut + inset
  end
  return inside
end

for _, size in ipairs({ 13, 17, 23 }) do
  local outer, inner = octagon(size, 0), octagon(size, 1)
  local fill, frame = canvas(size, size), canvas(size, size)
  local doubled = size > 13
  local second = octagon(size, 2)
  local third = octagon(size, 3)
  for y = 0, size - 1 do
    for x = 0, size - 1 do
      if inner(x, y) then fill:set(x, y, hex("ffffff")) end
      if outer(x, y) and not inner(x, y) then frame:set(x, y, hex("ffffff")) end
      if doubled and second(x, y) and not third(x, y) then frame:set(x, y, hex("ffffff", 150)) end
    end
  end
  save(fill, "Items/node_fill_" .. size)
  save(frame, "Items/node_frame_" .. size)
end

-- a soft round halo for the additive glow behind lit nodes
local halo = canvas(32, 32)
for y = 0, 31 do
  for x = 0, 31 do
    local d = math.sqrt((x - 15.5) ^ 2 + (y - 15.5) ^ 2) / 16
    if d < 1 then halo:set(x, y, hex("ffffff", math.floor((1 - d) ^ 2 * 255))) end
  end
end
save(halo, "Items/node_glow")

local TREE_ICONS = {
  drill = { "..x..", ".xxx.", "xxxxx", ".xxx.", "..x.." },
  wealth = { ".xxx.", "x.x.x", "x.x.x", "x.x.x", ".xxx." },
  blast = { "x.x.x", ".xxx.", "xx.xx", ".xxx.", "x.x.x" },
  hangar = { ".x.x.", "xxxxx", ".x.x.", "xxxxx", ".x.x." },
  frenzy = { "...x.", "..x..", ".xxx.", "..x..", ".x..." },
  strata = { "xxxxx", ".....", "xxxxx", ".....", "xxxxx" },
}
for name, rows in pairs(TREE_ICONS) do
  local g = canvas(5, 5)
  for y, row in ipairs(rows) do
    for x = 1, 5 do
      if row:sub(x, x) == "x" then g:set(x - 1, y - 1, hex("ffffff")) end
    end
  end
  save(g, "Items/ticon_" .. name)
end

-- a dash for the lines to skills that can be bought next; the game repeats it along the line
local dash = canvas(6, 1)
dash:rect(0, 0, 3, 0, hex("ffffff"))
save(dash, "Items/tree_dash")

-- the page behind the tree: a dithered radial gradient, centre 24183A to edge 0E0A1A
local function lerpHex(t)
  local function mix(a, b) return string.format("%02x", math.floor(a + (b - a) * t + 0.5)) end
  return mix(0x24, 0x0e) .. mix(0x18, 0x0a) .. mix(0x3a, 0x1a)
end
local bg = canvas(320, 180)
for y = 0, 179 do
  for x = 0, 319 do
    local d = math.sqrt(((x - 160) / 180) ^ 2 + ((y - 90) / 120) ^ 2)
    local band = math.min(1, d) * 6
    local step = math.floor(band + (((x + y) % 2 == 0) and 0.25 or 0))
    bg:set(x, y, hex(lerpHex(step / 6)))
  end
end
save(bg, "Items/tree_bg")

-- faint dotted guide circles at every ring of the fan, radius (4 + 3 * ring) * 8 pixels
local rings = canvas(640, 640)
for ring = 1, 10 do
  local r = (4 + 3 * ring) * 8
  local steps = math.floor(r * 2 * math.pi / 5)
  for i = 0, steps - 1 do
    local a = i / steps * 2 * math.pi
    rings:set(math.floor(320 + math.cos(a) * r), math.floor(320 + math.sin(a) * r), hex("6a70a0", 90))
  end
end
save(rings, "Items/tree_rings")

-- achievement badge: the page tints it, and shows a black silhouette until earned
local badge = canvas(16, 16)
for y = 2, 13 do
  local half = (y < 8) and (y - 2) // 2 + 2 or (13 - y) // 2 + 1
  badge:rect(8 - half, y, 7 + half, y, hex("ffe27a"))
end
badge:rect(5, 4, 10, 5, hex("fff4c0"))
outline(badge, INK)
save(badge, "Items/badge")

print("done")
