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

-- ink, dark, shade, base, light, bright, texture; colours follow the layer table in the design data
local LAYERS = {
  { "1a1210", "4a3122", "6b4630", "8c5f3f", "a87a52", "c49a6c", "speck" }, -- 表土 棕
  { "2a1e14", "4a3520", "94703c", "b8904c", "d4ae62", "e8cc84", "band" },  -- 砂岩 土黄
  { "1c1430", "3a2a5e", "54408a", "6e58a8", "8e7cc8", "b8a8e8", "crack" }, -- 溶洞 紫蓝
  { "0a1428", "142a50", "1e4070", "2a5a90", "4078b0", "70a8d8", "band" },  -- 暗河 深蓝
  { "241e14", "5a5040", "8a7c5c", "a8986c", "c8b478", "e8d490", "speck" }, -- 遗迹 金灰
  { "201c22", "4e4850", "7e7480", "a0949c", "c0aeb4", "dccad0", "speck" }, -- 花岗岩 灰粉
  { "0e0a0c", "2a1218", "4a1a20", "68242a", "8a3234", "b04a44", "crack" }, -- 莫霍 黑暗红
  { "10180a", "2a3a14", "46581e", "62782a", "82983a", "a8b858", "speck" }, -- 上地幔 橄榄绿
  { "08201e", "124a44", "1e6e64", "2e9484", "50b8a4", "88dcc8", "band" },  -- 过渡带 蓝绿
  { "200806", "561410", "8e2412", "bc3a14", "e06220", "f8983c", "crack" }, -- 下地幔 深红橙
  { "2c1c04", "7a5410", "b88a1c", "e0b43a", "f4d870", "fff4c0", "band" },  -- 外核 金白
  { "3a3448", "787088", "a8a2b8", "d0ccdc", "ece8f0", "ffffff", "speck" }, -- 内核 白金
}

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
  outline(g, INK)
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

-- drills, 8x12, 4 frames. Material sets the colours; the family sets the silhouette.
local MATERIALS = {
  { "c49a6c", "8c5f3f", "5a3a24" }, -- 木
  { "c0c0cc", "8a8a96", "5a5a64" }, -- 石
  { "f6b98a", "e08a4a", "a85a2a" }, -- 铜
  { "d0d4e0", "8a90a0", "5a6070" }, -- 铁
  { "a8bcd4", "6a84a4", "465a78" }, -- 钢
  { "f4f8ff", "c4cede", "8894ac" }, -- 银
  { "ffe27a", "f2b92e", "b0781a" }, -- 金
  { "d0f8ff", "90e0f0", "50a8d0" }, -- 水晶
  { "f0fcff", "b0ecf8", "70c8e8" }, -- 钻石
  { "b0ffd8", "60e0b0", "20a080" }, -- 秘银
  { "6a5a8a", "3a2e58", "1e1830" }, -- 黑曜
  { "fff0a0", "f89a28", "d0401a" }, -- 星核
}
local FAMILIES = { "std", "swift", "heavy", "blast", "magnet" }

local function drillFrame(family, m, f)
  local hi, mid, lo = hex(m[1]), hex(m[2]), hex(m[3])
  local g = canvas(8, 12)
  local glass = hex("4fb8e8")
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
    g:rect(3, 2, 4, 3, hex("e84a2e"))
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
  outline(g, INK)
  return g
end

for fi, family in ipairs(FAMILIES) do
  for ti, m in ipairs(MATERIALS) do
    local sheet = canvas(32, 12)
    for f = 0, 3 do sheet:blit(drillFrame(family, m, f), f * 8, 0) end
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

-- 12 guardian faces, 48x32 over the boss wall: eyes, mouth and horns vary, colours follow the layer
for layer = 1, 12 do
  local v = LAYERS[layer]
  local ink, dark, glow, tooth = hex(v[1]), hex(v[2]), hex(v[6]), hex(v[5])
  local g = canvas(48, 32)
  local eyes = (layer - 1) % 3 + 1
  local eyeR = 7 - eyes
  for e = 1, eyes do
    local ex = math.floor(48 * e / (eyes + 1))
    for y = 4, 16 do
      for x = ex - eyeR - 1, ex + eyeR + 1 do
        if ((x - ex) / (eyeR + 1)) ^ 2 + ((y - 10) / 5) ^ 2 <= 1 then g:set(x, y, dark) end
      end
    end
    g:rect(ex - 1, 8, ex + 1, 11, glow)
    g:set(ex, 9, hex("ffffff"))
  end

  local mouth = (layer - 1) % 4
  if mouth == 0 then -- jagged
    for x = 8, 39 do
      local jag = (x % 6 < 3) and 0 or 2
      g:rect(x, 21 + jag, x, 27 - jag, ink)
    end
    for x = 9, 38, 6 do g:rect(x, 21, x + 1, 23, tooth) end
  elseif mouth == 1 then -- round maw
    for y = 18, 30 do
      for x = 12, 35 do
        if ((x - 23.5) / 12) ^ 2 + ((y - 24) / 6) ^ 2 <= 1 then g:set(x, y, ink) end
      end
    end
    g:rect(16, 21, 31, 21, tooth)
  elseif mouth == 2 then -- mandibles
    for i = 0, 7 do
      g:rect(10 - i, 20 + i, 16 - i, 21 + i, tooth)
      g:rect(31 + i, 20 + i, 37 + i, 21 + i, tooth)
    end
  else -- grille
    g:rect(10, 21, 37, 27, ink)
    for x = 12, 36, 4 do g:rect(x, 21, x, 27, tooth) end
  end

  if layer % 2 == 0 then -- horns on every second guardian
    for i = 0, 3 do
      g:rect(4 + i, 3 - i, 6 + i, 4 - i, tooth)
      g:rect(41 - i, 3 - i, 43 - i, 4 - i, tooth)
    end
  end
  outline(g, ink)
  save(g, "Layers/boss_face_" .. (layer - 1))
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
