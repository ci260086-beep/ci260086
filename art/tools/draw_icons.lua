-- Icons for the phase 3 systems, the skill effects, achievements and rare items, into Assets/Art/Items.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_icons.lua
-- With preview set, it also writes one enlarged sheet per group for reviewing outside Unity.
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local function hex(s, a)
  return app.pixelColor.rgba(
    tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end

-- the same base palette as draw_game_art.lua
local INK, D2, D3, D5 = hex("1a1210"), hex("4a3122"), hex("6b4630"), hex("a87a52")
local G2, G3, G4 = hex("4a7a2a"), hex("76a83a"), hex("a8d45a")
local O1 = hex("b8693a")
local K1, K2 = hex("1e1e24"), hex("3a3a44")
local S1, S2, S3 = hex("5c5c6a"), hex("8a8a98"), hex("b4b4c2")
local GOLD, GOLD_HI = hex("e8b02a"), hex("ffe27a")
local WHITE = hex("ffffff")
local RED, RED_HI, RED_LO = hex("d8402e"), hex("ff7a5a"), hex("8a2420")
local BLUE, BLUE_HI, BLUE_LO = hex("3a7ad8"), hex("8ac0ff"), hex("1e3a80")

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c)
    x, y = math.floor(x), math.floor(y)
    if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end
  end
  function g:get(x, y) return self.px[y * w + x] end
  function g:rect(x0, y0, x1, y1, c)
    for y = y0, y1 do for x = x0, x1 do self:set(x, y, c) end end
  end
  function g:ellipse(cx, cy, rx, ry, c)
    for y = math.floor(cy - ry), math.ceil(cy + ry) do
      for x = math.floor(cx - rx), math.ceil(cx + rx) do
        if ((x - cx) / rx) ^ 2 + ((y - cy) / ry) ^ 2 <= 1 then self:set(x, y, c) end
      end
    end
  end
  function g:line(x0, y0, x1, y1, c)
    local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0))
    for i = 0, n do
      local t = n == 0 and 0 or i / n
      self:set(math.floor(x0 + (x1 - x0) * t + 0.5), math.floor(y0 + (y1 - y0) * t + 0.5), c)
    end
  end
  -- draws rows of characters: "." is empty, any other key looks up the colour table
  function g:pattern(x0, y0, rows, colours)
    for y, row in ipairs(rows) do
      for x = 1, #row do
        local c = colours[row:sub(x, x)]
        if c then self:set(x0 + x - 1, y0 + y - 1, c) end
      end
    end
  end
  return g
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

local function save(g, path)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/" .. path .. ".png")
end

-- a review sheet: every icon enlarged on a dark and a light tile, in a grid
local function sheet(name, icons, scale, columns)
  if not previewDir then return end
  local w, h = icons[1].g.w, icons[1].g.h
  local cell = (w * 2 + 3) * scale
  local rows = (#icons + columns - 1) // columns
  local img = Image(columns * (cell + scale * 2) + scale * 2, rows * (h * scale + scale * 4) + scale * 2, ColorMode.RGB)
  img:clear(hex("2a2630"))
  for i, icon in ipairs(icons) do
    local ox = scale * 2 + ((i - 1) % columns) * (cell + scale * 2)
    local oy = scale * 2 + ((i - 1) // columns) * (h * scale + scale * 4)
    for half = 0, 1 do
      local bg = half == 0 and hex("1c1822") or hex("d8d0c4")
      local bx = ox + half * (w + 1) * scale
      for y = -1, h do
        for x = -1, w do
          local c = (x >= 0 and y >= 0 and x < w and y < h) and icon.g:get(x, y) or nil
          if c == nil or app.pixelColor.rgbaA(c) == 0 then c = bg end
          for a = 0, scale - 1 do
            for b = 0, scale - 1 do img:drawPixel(bx + (x + 1) * scale + a - scale, oy + (y + 1) * scale + b, c) end
          end
        end
      end
    end
  end
  img:saveAs(previewDir .. "/" .. name .. ".png")
end

---------------------------------------------------------------------------------------------------
-- Phase 3 systems, 16x16 in full colour with an ink outline, matching the tab_ rail icons.
---------------------------------------------------------------------------------------------------
local SYSTEMS = {
  -- 黄金事件: two gold nuggets and a sparkle
  { "event", function(g)
    g:ellipse(6.5, 9.5, 5, 4, GOLD)
    g:ellipse(11.5, 11.5, 3, 2.5, GOLD)
    g:ellipse(5, 8, 2.5, 1.5, GOLD_HI)
    g:set(11, 10, GOLD_HI); g:set(10, 10, GOLD_HI)
    g:rect(5, 13, 9, 13, O1); g:rect(9, 12, 10, 12, O1); g:rect(12, 13, 14, 13, O1)
    g:rect(12, 1, 12, 5, WHITE); g:rect(10, 3, 14, 3, WHITE); g:set(12, 3, GOLD_HI)
  end },

  -- 地表加工厂: a saw-tooth roofed hall with a smoking chimney
  { "factory", function(g)
    g:rect(10, 3, 12, 8, D3); g:rect(10, 3, 12, 3, RED); g:rect(10, 4, 10, 8, D5)
    g:ellipse(12, 1, 1.5, 1, S3); g:set(14, 0, S3)
    g:rect(1, 8, 14, 14, S2)
    for i = 0, 2 do
      local x = 1 + i * 4
      for s = 0, 3 do g:rect(x + s, 8 - s, x + s, 8, s == 3 and S3 or S2) end
    end
    g:rect(1, 9, 14, 9, S3)
    g:rect(3, 11, 4, 12, GOLD_HI); g:rect(10, 11, 11, 12, GOLD_HI)
    g:rect(6, 11, 8, 14, K2); g:rect(6, 11, 8, 11, S1)
  end },

  -- 研究所: a flask of bubbling green
  { "lab", function(g)
    local glass, glassHi = hex("a8c8d0"), hex("e0f4f8")
    local fluid, fluidHi = hex("3ab86a"), hex("8ae8a0")
    g:rect(5, 1, 10, 1, glassHi)
    g:rect(6, 2, 9, 5, glass)
    for y = 6, 14 do
      local half = math.floor((y - 5) * 0.75 + 1.5)
      g:rect(8 - half, y, 7 + half, y, y >= 9 and fluid or glass)
    end
    g:rect(2, 9, 13, 9, fluidHi)
    g:rect(6, 2, 6, 7, glassHi)
    g:set(7, 11, WHITE); g:set(9, 12, glassHi); g:set(10, 10, glassHi); g:set(8, 7, glassHi); g:set(8, 4, glassHi)
  end },

  -- 探险队: a red flag planted on a hill
  { "expedition", function(g)
    for y = 11, 14 do for x = 1, 14 do
      if ((x - 7.5) / 7) ^ 2 + ((y - 15.5) / 4.5) ^ 2 <= 1 then g:set(x, y, y <= 12 and G4 or G3) end
    end end
    g:set(10, 13, G2); g:set(3, 14, G2)
    g:rect(4, 1, 4, 12, D5)
    g:pattern(5, 1, {
      "rrrrrrh",
      "rRRrrr.",
      "rRrrrr.",
      "lrrrrrh",
      "llll...",
    }, { r = RED, R = RED_HI, l = RED_LO, h = RED })
    g:set(4, 0, GOLD_HI)
  end },

  -- 挑战深井: a stone-rimmed shaft falling away into the dark, an arrow pointing down
  { "challenge", function(g)
    g:ellipse(7.5, 7.5, 7, 7, S2)
    g:ellipse(7.5, 7, 7, 6.5, S3)
    g:ellipse(7.5, 8, 5.5, 5.5, K2)
    g:ellipse(7.5, 8.5, 4, 4, hex("2a2a32"))
    g:ellipse(7.5, 9, 2.5, 2.5, K1)
    g:pattern(5, 4, {
      ".yYy.",
      ".yYy.",
      ".yYy.",
      "yyYyy",
      ".yYy.",
      "..y..",
    }, { y = GOLD_HI, Y = GOLD })
    g:set(2, 4, S1); g:set(12, 11, S1); g:set(4, 12, S1)
  end },

  -- 星座图: a night disc with stars joined into a figure
  { "constellation", function(g)
    local night, nightHi, thread = hex("1e2450"), hex("2e3a78"), hex("6a8ad8")
    for y = 0, 15 do for x = 0, 15 do
      if ((x - 7.5) / 7) ^ 2 + ((y - 7.5) / 7) ^ 2 <= 1 then
        g:set(x, y, ((x - 6) / 4.5) ^ 2 + ((y - 5) / 4) ^ 2 <= 1 and nightHi or night)
      end
    end end
    local stars = { { 3, 9 }, { 6, 4 }, { 10, 6 }, { 12, 11 }, { 7, 12 } }
    for i = 1, #stars - 1 do g:line(stars[i][1], stars[i][2], stars[i + 1][1], stars[i + 1][2], thread) end
    g:line(10, 6, 7, 12, thread)
    for i, s in ipairs(stars) do
      g:set(s[1], s[2], WHITE)
      if i == 2 or i == 4 then
        g:set(s[1] - 1, s[2], GOLD_HI); g:set(s[1] + 1, s[2], GOLD_HI)
        g:set(s[1], s[2] - 1, GOLD_HI); g:set(s[1], s[2] + 1, GOLD_HI)
      end
    end
  end },

  -- 钻探流派: a split heraldic shield with a drill bit down the middle
  { "school", function(g)
    for y = 1, 14 do
      local half = y <= 8 and 6 or math.max(0, 6 - (y - 8))
      for x = 8 - half, 7 + half do
        g:set(x, y, x < 8 and (y == 1 and RED_HI or RED) or (y == 1 and BLUE_HI or BLUE))
      end
    end
    g:rect(2, 2, 2, 8, RED_HI)
    g:rect(13, 2, 13, 8, BLUE_LO)
    g:pattern(5, 3, {
      "yyyyyy",
      "YYYYYY",
      ".wwww.",
      ".wwSS.",
      "..wS..",
      "..wS..",
      "...S..",
    }, { y = GOLD_HI, Y = GOLD, w = WHITE, S = S3 })
  end },

  -- 地心结晶: a glowing core crystal growing out of dark rock
  { "crystal", function(g)
    local lo, mid, hi = hex("c86a20"), hex("ffb040"), hex("fff0b0")
    g:ellipse(7.5, 13, 7, 2.5, K2)
    g:rect(2, 12, 13, 14, K2); g:rect(2, 12, 13, 12, S1)
    g:pattern(2, 0, {
      "......hW....",
      ".....hhmm...",
      ".....hhmm...",
      "..h..hhmm...",
      ".hhm.hhmm.m.",
      ".hhm.hhmm.mm",
      ".hhm.hhmm.mm",
      ".hhm.hhmm.mm",
      ".hhm.hhml.ml",
      ".hhl.hhll.ll",
      ".hll.hlll.ll",
      ".lll.llll.ll",
    }, { h = hi, m = mid, l = lo, W = WHITE })
    g:set(1, 2, GOLD_HI); g:set(14, 3, GOLD_HI); g:set(13, 1, WHITE)
  end },

  -- 自动化: a gear turning with a green play arrow in its hub
  { "automation", function(g)
    for y = 0, 15 do
      for x = 0, 15 do
        local dx, dy = x - 7.5, y - 7.5
        local d = math.sqrt(dx * dx + dy * dy)
        local tooth = math.cos(math.atan(dy, dx) * 8) > 0.2
        if d <= 5.6 or (d <= 7.2 and tooth) then g:set(x, y, (dx + dy < -2) and S3 or S2) end
      end
    end
    g:ellipse(7.5, 7.5, 3.6, 3.6, K2)
    g:pattern(6, 5, { "g...", "gg..", "ggg.", "gg..", "g..." }, { g = G4 })
    g:set(6, 5, hex("d8f4a0"))
  end },

  -- 地核不稳: a cracked red core shaking, white-hot light leaking through
  { "unstable", function(g)
    local lo, mid, hi = hex("8a1a10"), hex("e0401e"), hex("ff9a5a")
    for y = 0, 15 do
      for x = 0, 15 do
        local nx, ny = (x - 7.5) / 5.5, (y - 8) / 5.5
        if nx * nx + ny * ny <= 1 then g:set(x, y, nx + ny < -0.6 and hi or (nx + ny > 0.6 and lo or mid)) end
      end
    end
    g:line(7, 3, 6, 6, GOLD_HI); g:line(6, 6, 9, 8, GOLD_HI); g:line(9, 8, 7, 11, GOLD_HI); g:line(7, 11, 8, 13, GOLD_HI)
    g:line(9, 8, 12, 7, GOLD_HI); g:set(8, 8, WHITE); g:set(6, 6, WHITE)
    g:rect(0, 6, 0, 9, hi); g:rect(15, 7, 15, 10, hi); g:set(1, 12, hi); g:set(14, 4, hi)
  end },
}

local systemIcons = {}
for _, entry in ipairs(SYSTEMS) do
  local g = canvas(16, 16)
  entry[2](g)
  outline(g, INK)
  save(g, "Items/sys_" .. entry[1])
  systemIcons[#systemIcons + 1] = { g = g, name = entry[1] }
end
sheet("sheet_systems", systemIcons, 8, 4)

---------------------------------------------------------------------------------------------------
-- Skill effects, 7x7, one per effect type in skills.json. White and two greys so the tree can tint
-- them by branch the way it tints ticon_ today. "#" white, "+" light grey, "-" dark grey.
---------------------------------------------------------------------------------------------------
local SKILL_INKS = { ["#"] = WHITE, ["+"] = hex("c0c0c0"), ["-"] = hex("808080") }

-- effect, branch (only for the preview tint), rows
local SKILLS = {
  { "dmg_pct", "drill", { "#######", ".#####.", ".##+##.", "..###..", "..#+#..", "...#...", "...#..." } },
  { "speed_pct", "drill", { ".......", "#..#...", ".#..#..", "..#..#.", ".#..#..", "#..#...", "......." } },
  { "click_pct", "drill", { "#......", "##.....", "#+#....", "#++#...", "#+++#..", "#+##...", "#.#...." } },
  { "crit_chance", "drill", { "...#...", ".#####.", ".#...#.", "###+###", ".#...#.", ".#####.", "...#..." } },
  { "crit_mult", "drill", { "#..#..#", ".#.#.#.", "..###..", "###+###", "..###..", ".#.#.#.", "#..#..#" } },
  { "hard_pct", "drill", { ".#####.", "##+####", "#+#-###", "##-####", "###-#+#", "####-##", ".#####." } },
  { "pierce", "drill", { "...#...", "...#...", "---#---", "...#...", "---#---", "..###..", "...#..." } },
  { "swarm_pct", "drill", { "###.###", ".#...#.", ".#...#.", ".......", "..###..", "...#...", "...#..." } },
  { "boss_pct", "drill", { ".#####.", "#######", "#..#..#", "#######", ".##.##.", ".#+#+#.", "......." } },
  { "ore_pct", "wealth", { ".......", ".#####.", "#+#+#+#", ".#####.", "..###..", "...#...", "......." } },
  { "gold_pct", "wealth", { ".#####.", "#+++++#", ".#####.", ".......", ".#####.", "#+++++#", ".#####." } },
  { "offline_pct", "wealth", { "..###..", ".##..#.", "##.....", "##.....", "##.....", ".##..#.", "..###.." } },
  { "offline_hours", "wealth", { ".#####.", "#++#++#", "#++#++#", "#++##+#", "#+++++#", "#+++++#", ".#####." } },
  { "chest_pct", "wealth", { "#......", ".#####.", "#+++++#", "#######", "#++#++#", "#+++++#", "#######" } },
  { "chest_gold", "wealth", { ".#.#.#.", "..###..", "#######", "#+++++#", "#++#++#", "#+++++#", "#######" } },
  { "rare_pct", "wealth", { ".#####.", "#++++##", "#+++#+#", "#++#++#", "#+#+++#", "##++++#", ".#####." } },
  { "tnt_rate", "blast", { ".....+.", "....#..", ".#.#.#.", ".#.#.#.", ".-----.", ".#.#.#.", ".#.#.#." } },
  { "blast_radius", "blast", { "#.#.#.#", ".......", "#.###.#", "..#+#..", "#.###.#", ".......", "#.#.#.#" } },
  { "blast_ore", "blast", { "#.....#", ".#####.", "#+#+#+#", ".#####.", "#.###.#", "...#...", "......." } },
  { "chain", "blast", { "##.....", "#.#....", ".###...", "..#.#..", "...###.", "....#.#", ".....##" } },
  { "blast_dmg", "blast", { ".....#.", "....#..", "..###..", ".#+###.", ".#####.", ".#####.", "..###.." } },
  { "buy_discount", "hangar", { "##...#.", "##..#..", "...#...", "..#....", ".#..##.", "#...##.", "......." } },
  { "hangar_slots", "hangar", { "###.###", "#.#.#.#", "###.###", ".......", "###..#.", "#.#.###", "###..#." } },
  { "merge_lucky", "hangar", { ".##.##.", "#######", ".##+##.", "#######", ".##.##.", "...#...", "...#..." } },
  { "auto_merge", "hangar", { "#.....#", ".#...#.", "..#.#..", "...#...", "...#...", "..###..", "...#..." } },
  { "buy_tier", "hangar", { "...#...", "..#+#..", ".#...#.", "...#...", "..#+#..", ".#...#.", "......." } },
  { "auto_buy", "hangar", { "##.....", ".######", ".#++++#", "..#++#.", "..####.", ".......", "..#..#." } },
  { "frenzy_time", "frenzy", { "#######", ".#+++#.", "..#+#..", "...#...", "..#.#..", ".#+++#.", "#######" } },
  { "frenzy_charge", "frenzy", { "....##.", "...##..", "..##...", ".#####.", "...##..", "..##...", ".##...." } },
  { "combo_window", "frenzy", { ".......", "#.....#", "##...##", "#######", "##...##", "#.....#", "......." } },
  { "frenzy_mult", "frenzy", { "...#...", "..##...", ".###.#.", ".#####.", "##+#+##", "##+++##", ".#####." } },
  { "combo_cap", "frenzy", { "#######", ".......", "##...##", ".##.##.", "..###..", ".##.##.", "##...##" } },
  { "frenzy_quake", "frenzy", { ".......", "..#....", "..##...", "#.#.#.#", "...##..", "....#..", "......." } },
  { "layer_pct", "strata", { "#######", ".......", "+++++++", ".......", "#######", ".......", "-------" } },
  { "shard_pct", "strata", { "...#...", "..##+..", "..#++..", ".##++#.", ".#+++#.", "..#+#..", "...#..." } },
  { "start_gold", "strata", { "..#.#..", "...#...", ".#####.", "##+++##", "#++#++#", "##+++##", ".#####." } },
}

-- preview only: roughly the branch colours the tree uses
local BRANCH_TINT = {
  drill = { 1, 0.62, 0.3 }, wealth = { 1, 0.85, 0.3 }, blast = { 1, 0.4, 0.35 },
  hangar = { 0.45, 0.7, 1 }, frenzy = { 1, 0.45, 0.8 }, strata = { 0.5, 0.9, 0.5 },
}

local skillIcons = {}
for _, entry in ipairs(SKILLS) do
  local g = canvas(7, 7)
  g:pattern(0, 0, entry[3], SKILL_INKS)
  save(g, "Items/sicon_" .. entry[1])
  local t = BRANCH_TINT[entry[2]]
  local tinted = canvas(7, 7)
  for k, c in pairs(g.px) do
    local pc = app.pixelColor
    tinted.px[k] = pc.rgba(math.floor(pc.rgbaR(c) * t[1]), math.floor(pc.rgbaG(c) * t[2]), math.floor(pc.rgbaB(c) * t[3]))
  end
  skillIcons[#skillIcons + 1] = { g = tinted, name = entry[1] }
end
sheet("sheet_skills", skillIcons, 8, 6)

---------------------------------------------------------------------------------------------------
-- Shared shapes for the collection icons below.
---------------------------------------------------------------------------------------------------
local function tri(g, ax, ay, bx, by, cx, cy, c)
  local function side(px, py, qx, qy, x, y) return (qx - px) * (y - py) - (qy - py) * (x - px) end
  for y = math.floor(math.min(ay, by, cy)), math.ceil(math.max(ay, by, cy)) do
    for x = math.floor(math.min(ax, bx, cx)), math.ceil(math.max(ax, bx, cx)) do
      local d1, d2, d3 = side(ax, ay, bx, by, x, y), side(bx, by, cx, cy, x, y), side(cx, cy, ax, ay, x, y)
      if not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0)) then g:set(x, y, c) end
    end
  end
end

-- a round body lit from the upper left, with a highlight dot
local function ball(g, cx, cy, rx, ry, lo, mid, hi, shine)
  for y = math.floor(cy - ry), math.ceil(cy + ry) do
    for x = math.floor(cx - rx), math.ceil(cx + rx) do
      local nx, ny = (x - cx) / rx, (y - cy) / ry
      if nx * nx + ny * ny <= 1 then
        local light = nx + ny
        g:set(x, y, light < -0.55 and hi or (light > 0.55 and lo or mid))
      end
    end
  end
  if shine then g:set(cx - rx * 0.45, cy - ry * 0.45, shine) end
end

local function thick(g, x0, y0, x1, y1, c, w)
  for o = 0, (w or 2) - 1 do
    g:line(x0 + o, y0, x1 + o, y1, c)
    g:line(x0, y0 + o, x1, y1 + o, c)
  end
end

local function ring(g, cx, cy, rx, ry, c)
  for i = 0, 63 do
    local a = i / 64 * 2 * math.pi
    g:set(math.floor(cx + math.cos(a) * rx + 0.5), math.floor(cy + math.sin(a) * ry + 0.5), c)
  end
end

local function star(g, cx, cy, outer, inner, points, c, turn)
  local pts = {}
  for i = 0, points * 2 - 1 do
    local r = i % 2 == 0 and outer or inner
    local a = (turn or -math.pi / 2) + i * math.pi / points
    pts[#pts + 1] = { cx + math.cos(a) * r, cy + math.sin(a) * r }
  end
  for i = 1, #pts do
    local p, q = pts[i], pts[i % #pts + 1]
    tri(g, cx, cy, p[1], p[2], q[1], q[2], c)
  end
end

-- 3x5 digits for layer numbers
local DIGITS = {
  ["0"] = { "###", "#.#", "#.#", "#.#", "###" }, ["1"] = { ".#.", "##.", ".#.", ".#.", "###" },
  ["2"] = { "##.", "..#", ".#.", "#..", "###" }, ["3"] = { "##.", "..#", ".#.", "..#", "##." },
  ["4"] = { "#.#", "#.#", "###", "..#", "..#" }, ["5"] = { "###", "#..", "##.", "..#", "##." },
  ["6"] = { ".##", "#..", "###", "#.#", "###" }, ["7"] = { "###", "..#", ".#.", ".#.", ".#." },
  ["8"] = { "###", "#.#", "###", "#.#", "###" }, ["9"] = { "###", "#.#", "###", "..#", "##." },
}
local function number(g, n, cx, y, c)
  local s = tostring(n)
  local x = math.floor(cx - (#s * 4 - 1) / 2 + 0.5)
  for i = 1, #s do
    g:pattern(x + (i - 1) * 4, y, DIGITS[s:sub(i, i)], { ["#"] = c })
  end
end

-- layer ramps, the same formula and table as draw_world_art.lua
local function hsv(h, s, v)
  local c, x = v * s, v * s * (1 - math.abs((h / 60) % 2 - 1))
  local m = v - c
  local r, gg, b
  if h < 60 then r, gg, b = c, x, 0 elseif h < 120 then r, gg, b = x, c, 0
  elseif h < 180 then r, gg, b = 0, c, x elseif h < 240 then r, gg, b = 0, x, c
  elseif h < 300 then r, gg, b = x, 0, c else r, gg, b = c, 0, x end
  return string.format("%02x%02x%02x", math.floor((r + m) * 255 + 0.5),
    math.floor((gg + m) * 255 + 0.5), math.floor((b + m) * 255 + 0.5))
end
local function towards(h, target, amount)
  local d = (target - h + 540) % 360 - 180
  return (h + d * amount) % 360
end
local RAMP_V = { 0.14, 0.30, 0.46, 0.62, 0.78, 0.92 }
local RAMP_S = { 0.60, 0.68, 0.80, 0.70, 0.52, 0.34 }
local RAMP_DARK = { 0.45, 0.30, 0.14, 0, 0, 0 }
local RAMP_WARM = { 0, 0, 0, 0, 0.14, 0.28 }
local LAYER_HUES = {
  { 24, 0.75 }, { 38, 0.70 }, { 262, 0.65 }, { 212, 0.85 }, { 42, 0.45 }, { 345, 0.22 },
  { 356, 0.70 }, { 74, 0.70 }, { 168, 0.75 }, { 16, 1.0 }, { 44, 0.85 }, { 280, 0.18 },
}
local LAYER_RAMP = {}
for i, v in ipairs(LAYER_HUES) do
  local r = {}
  for k = 1, 6 do
    local h = towards(towards(v[1], 250, RAMP_DARK[k]), 55, RAMP_WARM[k])
    r[k] = hex(hsv(h, RAMP_S[k] * v[2], RAMP_V[k]))
  end
  LAYER_RAMP[i] = r
end

-- the outline of the shape and nothing inside it, for things not yet found
local SIL_FILL, SIL_EDGE = hex("2e2a38"), hex("0e0c12")
local function silhouette(g)
  local s = canvas(g.w, g.h)
  for k in pairs(g.px) do
    local x, y = k % g.w, k // g.w
    local edge = x == 0 or y == 0 or x == g.w - 1 or y == g.h - 1
      or not g:get(x - 1, y) or not g:get(x + 1, y) or not g:get(x, y - 1) or not g:get(x, y + 1)
    s.px[k] = edge and SIL_EDGE or SIL_FILL
  end
  return s
end

local silIcons = {}
local function finish(g, name, list)
  outline(g, INK)
  save(g, "Items/" .. name)
  local s = silhouette(g)
  save(s, "Items/" .. name .. "_sil")
  list[#list + 1] = { g = g, name = name }
  silIcons[#silIcons + 1] = { g = s, name = name }
end

---------------------------------------------------------------------------------------------------
-- Achievements, 16x16, ach_<id> and ach_<id>_sil. Reaching a layer is a round medal in that
-- layer's colours with its number; beating a guardian is a plaque with a small portrait of it;
-- the counted ones are ribboned medals where the ribbon names the kind and the metal the rank.
---------------------------------------------------------------------------------------------------
local achievementIcons = {}

local function reachMedal(layer)
  local r = LAYER_RAMP[layer]
  local g = canvas(16, 16)
  ball(g, 7.5, 7.5, 7, 7, r[3], r[4], r[6])
  g:ellipse(7.5, 7.5, 5, 5, r[1])
  g:pattern(6, 3, { "####", ".##." }, { ["#"] = r[5] })
  number(g, layer, 7.5, 6, WHITE)
  return g
end

-- 12x10 portraits of the guardians. w light body, m body, d dark, e eyes, a and b accents, x white
local PORTRAITS = {
  { { ".....aa.....", "....mwwm....", "..mwwwwwwm..", ".mwwwwwwwwm.", ".wddwwwwddw.",
      ".wdewwwwedw.", "mwwwwwwwwwwm", ".wdwdwdwdww.", "m.wwwwwwww.m", "m..m....m..m" },
    { a = G3, e = hex("ffb43a") } },
  { { ".a........a.", "..a......a..", "...mmmmmm...", "..mmemmemm..", ".wwwwddwwww.",
      "m.wwwddwww.m", ".wwwwddwwww.", "m.wwwddwww.m", "..wwwddwww..", "...wwddww..." },
    { a = hex("f0e8d0"), e = hex("e84a2e") } },
  { { "....a..a....", "...ax..xa...", "a..mmmmmm..a", "ax.mmmmmm.xa", "a.meemmeem.a",
      "..mmmmmmmm..", "...mmddmm...", "...mmmmmm...", "....mmmm....", ".....mm....." },
    { a = hex("5ac8e0"), e = hex("c8fbff") } },
  { { ".....mm.....", "m..wwwwww..m", "mmwwwwwwwwmm", "mwxxwwwwxxwm", ".wxxwwwwxxw.",
      ".wwwwwwwwww.", ".wdxdxdxdxw.", ".wddddddddw.", ".wdxdxdxdxw.", "..wwwwwwww.." },
    {} },
  { { ".....aa.....", "mmwwwwwwwwmm", "mwddwwwwddwm", "mwdewwwwedwm", "mwwwwmmwwwwm",
      "mwwwwmmwwwwm", "mwddddddddwm", "mwdmdmmdmdwm", "mwwwwwwwwwwm", "mmmmmmmmmmmm" },
    { a = GOLD_HI, e = GOLD } },
  { { "....wwww....", "...wwwwww...", "mmmwdwwdwmmm", "mwwwewwewwwm", "mwwwwwwwwwwm",
      "mwwwwddwwwwm", "mmwwwwwwwwmm", "ww.mmmmmm.ww", "ww........ww", "ww........ww" },
    { e = hex("ff8a2a") } },
  { { "a..........a", "aa........aa", ".aa.mmmm.aa.", "..ammmmmma..", "..mmmmmmmm..",
      "..mdeddedm..", "..mmmddmmm..", "..mmmddmmm..", ".b.b.bb.b.b.", "bbbbbbbbbbbb" },
    { a = hex("e8dcc0"), b = hex("6a6a7a"), e = hex("ff3a2a") } },
  { { "...wwwwww...", "..wwwwwwww..", ".weewwwweew.", ".wedwwwwdew.", "..wwwwwwww..",
      "...wxwwxw...", "....wwww....", ".....aa.....", "....a..a....", "mwwb....bwwm" },
    { a = hex("e84a2e"), b = hex("f07a20"), e = hex("ffe27a") } },
  { { "...wwwaaa...", "..wwwwaaaa..", ".wwddwaddaa.", ".wwddwaddaa.", ".wwwwwaaaaa.",
      ".wwdddddaaa.", ".wwwwwaaaaa.", ".w.ww.aa.a..", "......a..a..", "......x..a.." },
    { a = hex("5ad8c8", 200) } },
  { { "...a....a...", "....a..a....", "..mmmmmmmm..", ".mmmmmmmmmm.", "mmemmmmmmemm",
      "mmmmmmmmmmmm", "dddddddddddd", "wwwwwwwwwwww", "wmwmwmwmwmwm", ".wwwwwwwwww." },
    { a = hex("f07a20"), e = hex("ffc050") } },
  { { "....wwww....", "..wwddddww..", ".wddwwwwddw.", "awdwddddwdwb", "awdwdxxdwdwb",
      "awdwdxxdwdwb", "awdwddwwwdwb", ".wddwwwwddw.", "..wwddddww..", "....wwww...." },
    { a = RED, b = BLUE } },
  { { ".aaa....aaa.", "abbba..abbba", "abbbbaabbbba", "abbbxxxxbbba", ".abbxxxxbba.",
      "..abbxxbba..", "...abbbba...", "....abba....", ".....aa.....", "............" },
    { a = hex("e8803a"), b = hex("ffc860") } },
}

local function bossPlaque(layer)
  local r = LAYER_RAMP[layer]
  local g = canvas(16, 16)
  g:rect(1, 0, 14, 15, r[4]); g:rect(0, 1, 15, 14, r[4])
  g:rect(1, 1, 14, 1, r[6]); g:rect(1, 14, 14, 14, r[3])
  g:rect(2, 2, 13, 13, r[1])
  local p = PORTRAITS[layer]
  local colours = { w = r[6], m = r[4], d = INK, e = WHITE, x = WHITE }
  for k, v in pairs(p[2]) do colours[k] = v end
  if layer >= 11 then colours.w, colours.m = GOLD_HI, GOLD end
  if layer == 12 then colours.w = WHITE end
  g:pattern(2, 3, p[1], colours)
  return g
end

local RANKS = {
  { hex("7a4a24"), hex("b87a3e"), hex("e8b070"), hex("3a2212") }, -- bronze
  { hex("6a6a7a"), hex("a8a8b8"), hex("e8e8f0"), hex("2a2a36") }, -- silver
  { hex("a8701a"), hex("e8b02a"), hex("ffe27a"), hex("4a300a") }, -- gold
  { hex("3a7a8a"), hex("8ad8e8"), hex("e8ffff"), hex("123a48") }, -- platinum
}

local function medal(rank, ribbon, emblem, inks)
  local m = RANKS[rank]
  local g = canvas(16, 16)
  tri(g, 3, 0, 7, 0, 7, 6, ribbon[1]); tri(g, 8, 0, 12, 0, 8, 6, ribbon[2])
  ball(g, 7.5, 9.5, 6, 6, m[1], m[2], m[3])
  g:ellipse(7.5, 9.5, 4.4, 4.4, m[4])
  g:pattern(4, 6, emblem, inks or { ["#"] = WHITE, ["+"] = m[3], ["-"] = m[2] })
  return g
end

local EMBLEM = {}
for _, entry in ipairs(SKILLS) do EMBLEM[entry[1]] = entry[3] end
EMBLEM.links = { ".......", ".###...", "#...#..", "#..###.", ".###..#", "...#..#", "....###" }
EMBLEM.rebirth = { "..####.", ".#...##", "#...###", "#.++...", "#.++..#", ".#...#.", "..###.." }

local RIBBONS = {
  drills = { hex("3a7ad8"), hex("1e3a80") }, tier = { hex("e08a3a"), hex("a8501a") },
  merge = { hex("4aa84a"), hex("2a6a2a") }, combo = { hex("e85aa8"), hex("a02a70") },
  rebirth = { hex("8a5ad8"), hex("4a2a90") }, chain = { hex("d8402e"), hex("8a2420") },
}
local TIER_TINTS = { [3] = hex("e08a4a"), [6] = hex("e8ecf4"), [9] = hex("8ae8f8"), [12] = hex("ffe27a") }

local COUNTED = {
  drills = { { 10, 1 }, { 50, 2 }, { 200, 3 }, { 1000, 4 } },
  tier = { { 3, 1 }, { 6, 2 }, { 9, 3 }, { 12, 4 } },
  merge = { { 100, 1 }, { 1000, 2 }, { 10000, 3 } },
  combo = { { 50, 1 }, { 200, 2 }, { 500, 3 } },
  rebirth = { { 1, 1 }, { 5, 2 }, { 25, 3 }, { 100, 4 } },
  chain = { { 10, 1 }, { 16, 2 }, { 24, 3 } },
}
local COUNTED_EMBLEM = { drills = "swarm_pct", tier = "dmg_pct", merge = "auto_merge", combo = "combo_cap",
  rebirth = "rebirth", chain = "links" }

-- the journey's end: the earth cut open to its glowing centre
local function centerIcon()
  local g = canvas(16, 16)
  ball(g, 7.5, 7.5, 7, 7, hex("1e4a9a"), hex("3a7ad8"), hex("8ac0ff"))
  g:ellipse(4, 5, 2, 1.5, G3); g:ellipse(5, 10, 1.5, 2, G3); g:set(3, 4, G4)
  local layers = { { 7, D3 }, { 5.5, hex("e07a2a") }, { 4, hex("ffb040") }, { 2.5, GOLD_HI }, { 1.2, WHITE } }
  for _, l in ipairs(layers) do
    for y = 0, 15 do
      for x = 8, 15 do
        if y <= 7 and (x - 7.5) ^ 2 + (y - 7.5) ^ 2 <= l[1] * l[1] then g:set(x, y, l[2]) end
      end
    end
  end
  return g
end

local data = {}
for layer = 1, 12 do
  data[#data + 1] = { "reach_" .. layer, reachMedal(layer) }
  data[#data + 1] = { "boss_" .. layer, bossPlaque(layer) }
end
for _, kind in ipairs({ "drills", "tier", "merge", "combo", "rebirth", "chain" }) do
  for _, step in ipairs(COUNTED[kind]) do
    local inks
    if kind == "tier" then inks = { ["#"] = TIER_TINTS[step[1]], ["+"] = WHITE } end
    data[#data + 1] = { kind .. "_" .. step[1], medal(step[2], RIBBONS[kind], EMBLEM[COUNTED_EMBLEM[kind]], inks) }
  end
end
data[#data + 1] = { "center", centerIcon() }
for _, d in ipairs(data) do finish(d[2], "ach_" .. d[1], achievementIcons) end
sheet("sheet_achievements", achievementIcons, 6, 8)

---------------------------------------------------------------------------------------------------
-- Rare items, 16x16, one drawing each, replacing the per-layer crystal/fossil/relic of
-- draw_world_art.lua under the same names (rare_<layer>_<n>), plus rare_<layer>_<n>_sil.
---------------------------------------------------------------------------------------------------
local function c3(a, b, c) return hex(a), hex(b), hex(c) end

local RARES = {
  -- 三叶虫化石
  ["1_1"] = function(g)
    ball(g, 7.5, 8, 7, 6.5, S1, S2, S3)
    g:ellipse(7.5, 4.5, 4.5, 2, D5)
    g:ellipse(7.5, 9, 3.5, 4.5, D5)
    for y = 6, 12, 2 do g:rect(5, y, 10, y, D3) end
    g:rect(7, 4, 8, 13, hex("dfc08f"))
    g:set(5, 4, D3); g:set(10, 4, D3)
  end,
  -- 古老树根
  ["1_2"] = function(g)
    local lo, mid, hi = c3("4a3122", "6b4630", "a87a52")
    thick(g, 3, 1, 11, 13, mid, 3)
    thick(g, 6, 6, 2, 11, mid, 2); thick(g, 9, 9, 13, 6, mid, 2); g:line(11, 12, 8, 14, lo)
    g:line(3, 1, 10, 12, hi); g:line(13, 6, 14, 4, lo); g:line(2, 11, 1, 13, lo)
    g:ellipse(5, 1, 1.5, 1, G3); g:set(4, 0, G4)
  end,
  -- 迷路的鼹鼠玩具
  ["1_3"] = function(g)
    ball(g, 7.5, 9, 6.5, 5.5, hex("3a3440"), hex("5a5468"), hex("8a84a0"))
    g:ellipse(3, 4.5, 1.5, 1.5, hex("5a5468")); g:ellipse(12, 4.5, 1.5, 1.5, hex("5a5468"))
    g:ellipse(7.5, 11, 2, 1.2, hex("f08aa0")); g:set(7, 10, hex("ffc0d0"))
    g:set(5, 8, INK); g:set(10, 8, INK); g:set(5, 7, WHITE)
    g:rect(2, 13, 3, 14, hex("f08aa0")); g:rect(12, 13, 13, 14, hex("f08aa0"))
    g:rect(10, 5, 12, 5, hex("c8a060")); g:rect(11, 4, 11, 6, hex("c8a060"))
  end,
  -- 沙漠玫瑰石: thin blades fanning out from one centre
  ["2_1"] = function(g)
    local lo, mid, hi = c3("8a5a3a", "c88a60", "f0c098")
    g:ellipse(7.5, 12, 6.5, 2.5, lo)
    for i = 0, 8 do
      local a = math.pi * (1.05 + i * 0.112)
      local function at(da, r) return 7.5 + math.cos(a + da) * r, 11 + math.sin(a + da) * r end
      local x1, y1 = at(-0.14, 7.5)
      local x2, y2 = at(0.14, 7.5)
      tri(g, 7.5, 11, x1, y1, x2, y2, i % 2 == 0 and mid or hi)
    end
    g:ellipse(7.5, 10.5, 2, 1.5, lo); g:set(7, 10, mid)
  end,
  -- 沙虫牙
  ["2_2"] = function(g)
    local lo, mid, hi = c3("a89878", "d8ccb0", "f8f0e0")
    for y = 1, 14 do
      local t = (y - 1) / 13
      local cx = 4 + t * t * 7
      local half = 3.6 * (1 - t) + 0.4
      for x = math.floor(cx - half), math.ceil(cx + half) do
        if math.abs(x - cx) <= half then g:set(x, y, x < cx - half / 3 and hi or (x > cx + half / 2 and lo or mid)) end
      end
    end
    g:rect(1, 1, 7, 3, hex("8a5a3a")); g:rect(1, 1, 7, 1, hex("b87a4a"))
  end,
  -- 埋藏的怀表
  ["2_3"] = function(g)
    for i = 0, 4 do g:set(2 + i, 3 - (i % 2), GOLD) end
    g:rect(7, 2, 8, 3, GOLD)
    ball(g, 7.5, 9.5, 5.5, 5.5, hex("a8701a"), GOLD, GOLD_HI)
    g:ellipse(7.5, 9.5, 4, 4, hex("f4f0e0"))
    g:line(7, 9, 7, 7, INK); g:line(8, 10, 10, 10, INK)
    g:set(7, 6, hex("a89878")); g:set(11, 9, hex("a89878")); g:set(4, 9, hex("a89878")); g:set(7, 13, hex("a89878"))
    g:set(11, 13, hex("8a5a3a")); g:set(12, 12, hex("8a5a3a"))
  end,
  -- 发光钟乳石
  ["3_1"] = function(g)
    local lo, mid, hi = c3("4a3a6a", "7a6a9a", "b0a0d0")
    g:rect(1, 0, 14, 2, lo); g:rect(1, 0, 14, 0, mid)
    tri(g, 3, 2, 12, 2, 7, 13, mid)
    tri(g, 3, 2, 7, 2, 7, 13, hi)
    g:line(9, 3, 8, 9, lo)
    g:rect(7, 10, 8, 13, hex("7ae8f0")); g:set(7, 14, WHITE)
    g:set(5, 13, hex("7ae8f0")); g:set(10, 12, hex("7ae8f0")); g:set(7, 15, hex("c8fbff"))
  end,
  -- 洞穴壁画碎片
  ["3_2"] = function(g)
    local stone, stoneLo, ochre = hex("c8b090"), hex("9a8468"), hex("b83a2a")
    tri(g, 1, 3, 13, 1, 12, 14, stone); tri(g, 1, 3, 12, 14, 2, 13, stone)
    g:line(2, 13, 12, 14, stoneLo); g:line(12, 14, 13, 2, stoneLo)
    g:rect(4, 7, 9, 8, ochre); g:rect(9, 5, 10, 6, ochre); g:set(11, 4, ochre); g:set(10, 4, ochre)
    g:line(4, 9, 3, 11, ochre); g:line(5, 9, 5, 11, ochre); g:line(8, 9, 7, 11, ochre); g:line(9, 9, 10, 11, ochre)
    g:ellipse(4, 4, 1, 1, hex("e8a03a"))
  end,
  -- 紫晶晶洞
  ["3_3"] = function(g)
    ball(g, 7.5, 8, 7, 6.5, S1, S2, S3)
    g:ellipse(7.5, 8, 5, 4.5, hex("3a1a5a"))
    local purple, light = hex("8a5ad8"), hex("d0b0ff")
    for _, t in ipairs({ { 3, 5, 6, 8 }, { 6, 4, 9, 7 }, { 10, 5, 8, 8 }, { 3, 11, 6, 8 }, { 7, 12, 8, 9 }, { 12, 10, 9, 8 } }) do
      tri(g, t[1], t[2], t[1] + (t[1] < 8 and 2 or -2), t[2], t[3], t[4], purple)
      g:set(t[3], t[4], light)
    end
    g:set(7, 8, WHITE)
  end,
  -- 盲鱼鳞片
  ["4_1"] = function(g)
    local lo, mid, hi = c3("2a4a90", "4a8ad8", "a8d8f8")
    tri(g, 3, 8, 12, 8, 7.5, 1, mid)
    ball(g, 7.5, 9.5, 5, 4.5, lo, mid, hi)
    tri(g, 4, 8, 7, 8, 7, 2, hi)
    ring(g, 7.5, 10, 3, 2.5, lo)
    g:set(9, 7, hex("f0a0d8")); g:set(5, 11, hex("8af0b8")); g:set(10, 11, hex("f0a0d8"))
  end,
  -- 沉船罗盘
  ["4_2"] = function(g)
    ball(g, 7.5, 7.5, 7, 7, hex("8a5a1a"), hex("c8902a"), hex("f0c860"))
    g:ellipse(7.5, 7.5, 5, 5, hex("e8dcc0"))
    tri(g, 6, 7, 9, 7, 7.5, 2, RED); tri(g, 6, 8, 9, 8, 7.5, 13, K2)
    g:set(7, 7, GOLD_HI); g:set(8, 8, GOLD_HI)
    g:set(1, 6, hex("5aa88a")); g:set(2, 10, hex("5aa88a")); g:set(12, 13, hex("5aa88a")); g:set(13, 3, hex("5aa88a"))
  end,
  -- 暗河珍珠母
  ["4_3"] = function(g)
    local lo, mid, hi = c3("a86a88", "e0b0c8", "fae8f4")
    g:ellipse(7.5, 4.5, 6.5, 3, lo); g:ellipse(7.5, 4, 5.5, 2.2, mid)
    for x = 3, 12, 2 do g:line(7.5, 6, x, 2, lo) end
    g:ellipse(7.5, 10.5, 7, 3.5, mid); g:ellipse(7.5, 10, 5.5, 2.5, hi)
    ball(g, 7.5, 9.5, 2.2, 2.2, hex("c8d0e0"), hex("f0f4ff"), WHITE)
  end,
  -- 古城钥匙
  ["5_1"] = function(g)
    local lo, mid, hi = c3("6a4018", "b87a3a", "f0c070")
    ball(g, 4, 4, 3.5, 3.5, lo, mid, hi)
    g:ellipse(4, 4, 1.2, 1.2, nil)
    thick(g, 6, 6, 13, 13, mid, 2); g:line(6, 6, 12, 12, hi)
    g:rect(10, 13, 11, 14, mid); g:rect(13, 10, 14, 11, mid); g:rect(12, 14, 12, 14, lo)
  end,
  -- 黄金面具
  ["5_2"] = function(g)
    local nemes = hex("2a4a9a")
    g:rect(1, 3, 14, 12, nemes)
    for y = 4, 12, 2 do g:rect(1, y, 2, y, GOLD); g:rect(13, y, 14, y, GOLD) end
    ball(g, 7.5, 7.5, 5, 6.5, hex("a8701a"), GOLD, GOLD_HI)
    g:rect(3, 1, 12, 3, GOLD); g:rect(3, 1, 12, 1, GOLD_HI); g:ellipse(7.5, 1, 1, 1, RED)
    g:rect(4, 6, 6, 6, INK); g:rect(9, 6, 11, 6, INK); g:set(5, 6, WHITE); g:set(10, 6, WHITE)
    g:rect(7, 7, 8, 9, hex("a8701a")); g:rect(6, 11, 9, 11, hex("8a4a1a"))
    g:rect(7, 14, 8, 15, nemes)
  end,
  -- 失落的石板
  ["5_3"] = function(g)
    local glow = hex("7ae8f0")
    g:ellipse(7.5, 4, 5.5, 3.5, S2)
    g:rect(2, 4, 13, 14, S2); g:rect(2, 4, 2, 14, S3); g:rect(13, 4, 13, 14, S1); g:rect(2, 14, 13, 14, S1)
    g:pattern(4, 3, {
      ".#.#..#.",
      "........",
      "#.#.##.#",
      "........",
      ".##.#.#.",
      "........",
      "#..#.##.",
      "........",
      ".#.##.#.",
    }, { ["#"] = glow })
    g:line(10, 9, 12, 12, S1)
  end,
  -- 花岗岩球
  ["6_1"] = function(g)
    ball(g, 7.5, 7.5, 6.5, 6.5, hex("6a5a5e"), hex("a8989a"), hex("d8c8c8"), WHITE)
    for y = 1, 14 do
      for x = 1, 14 do
        if g:get(x, y) and (x * 7 + y * 13) % 11 == 0 then g:set(x, y, hex("e0a0a0")) end
        if g:get(x, y) and (x * 5 + y * 3) % 13 == 0 then g:set(x, y, hex("2a2428")) end
      end
    end
  end,
  -- 锆石晶粒
  ["6_2"] = function(g)
    local lo, mid, hi = c3("8a2a10", "e0602a", "ffb080")
    tri(g, 3, 6, 12, 6, 7.5, 1, mid); g:rect(3, 6, 12, 10, mid); tri(g, 3, 10, 12, 10, 7.5, 14, lo)
    tri(g, 3, 6, 7, 6, 7, 2, hi); g:rect(3, 6, 5, 10, hi)
    g:rect(10, 6, 12, 10, lo); g:line(7, 1, 7, 14, lo)
    g:set(5, 4, WHITE)
  end,
  -- 地壳地图: mountains, a river and a dotted way to the X
  ["6_3"] = function(g)
    local paper, edge, ink = hex("e8d4a8"), hex("c8a870"), hex("8a5a3a")
    g:rect(1, 3, 14, 12, paper)
    g:rect(1, 3, 1, 12, edge); g:rect(14, 3, 14, 12, edge); g:rect(1, 12, 14, 12, edge)
    g:pattern(2, 4, { "..#......", ".#.#..#..", "#...##.#." }, { ["#"] = ink })
    g:line(2, 10, 5, 8, BLUE); g:line(5, 8, 8, 11, BLUE)
    for i = 0, 2 do g:set(7 + i * 2, 8 - i, RED) end
    g:line(11, 4, 13, 6, RED); g:line(13, 4, 11, 6, RED)
  end,
  -- 莫霍面样本
  ["7_1"] = function(g)
    local crustLo, crust, mantleLo, mantle = hex("8a6a50"), hex("c8a890"), hex("2e3a22"), hex("5a6a3a")
    g:rect(5, 2, 10, 7, crust); g:rect(5, 8, 10, 13, mantle)
    g:rect(10, 2, 10, 7, crustLo); g:rect(10, 8, 10, 13, mantleLo)
    g:ellipse(7.5, 2, 2.5, 1, hex("e0c8b0")); g:ellipse(7.5, 13.5, 2.5, 1, mantleLo)
    g:rect(5, 7, 10, 8, hex("e07a2a"))
    g:set(6, 4, crustLo); g:set(8, 10, hex("8a9a5a")); g:set(7, 12, hex("8a9a5a"))
  end,
  -- 玄武岩柱
  ["7_2"] = function(g)
    local lo, mid, hi = c3("22222e", "3e3e4e", "6a6a80")
    local cols = { { 1, 5, 6 }, { 5, 10, 2 }, { 10, 14, 8 } }
    for _, c in ipairs(cols) do
      g:rect(c[1], c[3], c[2], 14, mid)
      g:rect(c[1], c[3], c[2], c[3] + 1, hi)
      g:rect(c[2], c[3] + 2, c[2], 14, lo)
    end
    g:line(5, 2, 5, 14, lo); g:line(10, 8, 10, 14, lo)
  end,
  -- 铂金徽章
  ["7_3"] = function(g)
    star(g, 7.5, 8, 7.5, 3.2, 5, hex("8aa0b0"))
    star(g, 7.5, 7.5, 6.5, 2.6, 5, hex("d8e8f0"))
    ball(g, 7.5, 8, 2.2, 2.2, BLUE_LO, BLUE, BLUE_HI)
    g:set(7, 2, WHITE)
  end,
  -- 橄榄石原石
  ["8_1"] = function(g)
    local lo, mid, hi = c3("4a7a10", "8ab82a", "d0f070")
    g:ellipse(7.5, 12.5, 7, 2.5, D3)
    tri(g, 2, 12, 7, 12, 4, 4, mid); tri(g, 2, 12, 4, 12, 4, 4, hi)
    tri(g, 6, 12, 12, 12, 9, 1, mid); tri(g, 6, 12, 9, 12, 9, 1, hi)
    tri(g, 10, 13, 14, 13, 12, 7, lo)
    g:set(9, 4, WHITE)
  end,
  -- 金伯利岩管
  ["8_2"] = function(g)
    g:rect(1, 1, 14, 2, G3); g:rect(1, 3, 14, 3, D3)
    tri(g, 2, 3, 13, 3, 7.5, 15, hex("4a5a6a"))
    tri(g, 4, 3, 11, 3, 7.5, 12, hex("6a7a8a"))
    g:set(6, 6, hex("c8f0ff")); g:set(9, 8, WHITE); g:set(7, 10, hex("c8f0ff")); g:set(8, 5, hex("c8f0ff"))
  end,
  -- 地幔羽碎片
  ["8_3"] = function(g)
    local lo, mid, hi = c3("8a2a10", "f07a20", "ffc050")
    for y = 1, 14 do
      local t = (y - 1) / 13
      local cx = 9 - t * 3
      local half = 3.6 * math.sin(t * math.pi) + 0.6
      for x = math.floor(cx - half), math.ceil(cx + half) do
        if math.abs(x - cx) <= half then g:set(x, y, x < cx and hi or mid) end
      end
    end
    g:line(9, 1, 6, 14, lo)
    for y = 4, 11, 3 do g:line(8 - y / 4, y, 5 - y / 4, y + 2, lo) end
    g:set(10, 3, GOLD_HI)
  end,
  -- 林伍德石晶体
  ["9_1"] = function(g)
    local lo, mid, hi = c3("1e3a98", "3a5ad8", "a0b8ff")
    tri(g, 1, 8, 14, 8, 7.5, 1, mid); tri(g, 1, 8, 14, 8, 7.5, 14, lo)
    tri(g, 1, 8, 7, 8, 7, 1, hi); tri(g, 1, 8, 7, 8, 7, 14, mid)
    g:line(1, 8, 14, 8, hi); g:set(5, 5, WHITE)
  end,
  -- 水之结晶
  ["9_2"] = function(g)
    local lo, mid, hi = c3("2a8ab0", "5ad8e8", "c8f8ff")
    tri(g, 3, 9, 12, 9, 7.5, 1, mid)
    ball(g, 7.5, 10, 5, 4.5, lo, mid, hi)
    g:line(7, 3, 4, 9, hi); g:line(4, 10, 6, 13, hi)
    g:line(7, 2, 7, 14, lo); g:line(3, 10, 12, 10, lo)
    g:set(5, 7, WHITE)
  end,
  -- 相变核心
  ["9_3"] = function(g)
    ball(g, 7.5, 7.5, 6.5, 6.5, hex("2a7a70"), hex("4ab8a8"), hex("a8f0e0"))
    for y = 0, 15 do
      for x = 8, 15 do
        if g:get(x, y) then g:set(x, y, (y + x // 3) % 4 == 0 and hex("c8fff4", 210) or hex("5ad8c8", 200)) end
      end
    end
    g:rect(9, 14, 9, 15, hex("5ad8c8", 200)); g:set(12, 15, hex("5ad8c8", 200))
    g:line(2, 5, 7, 10, hex("2a7a70")); g:line(3, 10, 7, 5, hex("a8f0e0"))
    ball(g, 7.5, 7.5, 1.8, 1.8, GOLD_HI, WHITE, WHITE)
  end,
  -- 布里奇曼石
  ["10_1"] = function(g)
    local lo, mid, hi = c3("6a3a18", "a86a3a", "e0a870")
    g:rect(2, 6, 9, 13, mid)
    for y = 2, 5 do g:rect(2 + (6 - y), y, 9 + (6 - y), y, hi) end
    for x = 10, 13 do g:rect(x, 6 - (x - 9), x, 13 - (x - 9), lo) end
    g:line(2, 6, 9, 6, hex("f8d0a0")); g:set(4, 9, hi); g:set(7, 11, hi)
  end,
  -- 地幔鲸骨
  ["10_2"] = function(g)
    local lo, mid, hi = c3("a07058", "e0c0a0", "fae8d8")
    thick(g, 4, 10, 10, 4, mid, 2)
    g:line(4, 10, 10, 4, hi)
    for _, e in ipairs({ { 2, 11 }, { 4, 13 }, { 11, 2 }, { 13, 4 } }) do ball(g, e[1], e[2], 1.8, 1.8, lo, mid, hi) end
    g:set(7, 8, lo); g:set(9, 6, lo)
  end,
  -- 超深钻石
  ["10_3"] = function(g)
    local lo, mid, hi = c3("4a8ac8", "a8d8f0", "eefaff")
    for y = 3, 6 do g:rect(4 - (y - 3), y, 11 + (y - 3), y, y == 3 and hi or mid) end
    tri(g, 1, 7, 14, 7, 7.5, 14, mid)
    tri(g, 1, 7, 7, 7, 7, 14, hi)
    g:line(1, 7, 14, 7, lo); g:line(4, 3, 5, 6, lo); g:line(11, 3, 10, 6, lo); g:line(10, 7, 8, 13, lo)
    g:set(6, 4, WHITE); g:set(13, 1, WHITE); g:set(12, 2, hi); g:set(14, 2, hi); g:set(13, 3, hi)
  end,
  -- 磁极石
  ["11_1"] = function(g)
    for y = 1, 14 do
      for x = 1, 14 do
        local d = math.sqrt((x - 7.5) ^ 2 + (y - 7) ^ 2)
        if (y <= 7 and d >= 2.5 and d <= 6.5) or (y > 7 and y <= 11 and ((x >= 1 and x <= 4) or (x >= 11 and x <= 14))) then
          g:set(x, y, x < 8 and RED or BLUE)
        end
      end
    end
    g:rect(1, 12, 4, 14, S3); g:rect(11, 12, 14, 14, S3)
    g:rect(2, 4, 2, 9, RED_HI); g:rect(12, 4, 12, 9, BLUE_HI)
  end,
  -- 液态金属珠
  ["11_2"] = function(g)
    ball(g, 7.5, 7.5, 5.5, 5.5, hex("4a4a5a"), hex("a8a8b8"), hex("f0f0ff"), WHITE)
    g:rect(3, 8, 12, 8, hex("4a4a5a")); g:rect(4, 9, 11, 9, hex("e0e0f0"))
    ball(g, 2.5, 13, 1.5, 1.5, hex("4a4a5a"), hex("a8a8b8"), hex("f0f0ff"))
    ball(g, 13, 13.5, 1, 1, hex("4a4a5a"), hex("a8a8b8"), hex("f0f0ff"))
  end,
  -- 地磁罗盘: the earth inside its field lines, north red and south blue
  ["11_3"] = function(g)
    ring(g, 4, 7.5, 3.5, 6, GOLD); ring(g, 11, 7.5, 3.5, 6, GOLD)
    ball(g, 7.5, 7.5, 5, 5, hex("1e4a9a"), hex("3a7ad8"), hex("8ac0ff"))
    g:ellipse(6, 6, 1.5, 1, G3); g:set(9, 9, G3); g:set(10, 8, G3)
    g:ellipse(7.5, 1.5, 1.2, 1, RED); g:ellipse(7.5, 13.5, 1.2, 1, BLUE)
  end,
  -- 地心之石
  ["12_1"] = function(g)
    for _, p in ipairs({ { 1, 4 }, { 14, 9 }, { 3, 13 }, { 12, 2 }, { 7, 0 } }) do g:set(p[1], p[2], hex("ffe8a0", 160)) end
    ball(g, 7.5, 7.5, 5.5, 5, hex("e8a030"), hex("ffe8a0"), WHITE)
    g:rect(3, 6, 3, 7, hex("e8a030")); g:rect(11, 9, 12, 9, hex("e8a030"))
    g:ellipse(7.5, 8, 2, 1.5, WHITE)
  end,
  -- 星核碎晶
  ["12_2"] = function(g)
    star(g, 7.5, 7.5, 7.4, 2.2, 4, hex("8ad8e8"))
    star(g, 7.5, 7.5, 5.5, 1.6, 4, hex("e8ffff"))
    g:ellipse(7.5, 7.5, 1.2, 1.2, WHITE)
    g:set(2, 2, GOLD_HI); g:set(13, 12, GOLD_HI); g:set(12, 3, WHITE)
  end,
  -- 地球心跳
  ["12_3"] = function(g)
    local lo, mid, hi = c3("a82a2a", "e84a3a", "ff9a7a")
    g:ellipse(4.5, 5, 3.5, 3.5, mid); g:ellipse(10.5, 5, 3.5, 3.5, mid)
    tri(g, 1, 6, 14, 6, 7.5, 14, mid)
    g:ellipse(4, 4, 1.5, 1, hi); tri(g, 9, 13, 14, 6, 13, 9, lo)
    local pulse = { { 0, 8 }, { 4, 8 }, { 5, 6 }, { 7, 11 }, { 9, 3 }, { 10, 8 }, { 15, 8 } }
    for i = 1, #pulse - 1 do g:line(pulse[i][1], pulse[i][2], pulse[i + 1][1], pulse[i + 1][2], WHITE) end
  end,
}

local rareIcons = {}
for layer = 1, 12 do
  for n = 1, 3 do
    local key = layer .. "_" .. n
    local g = canvas(16, 16)
    RARES[key](g)
    finish(g, "rare_" .. key, rareIcons)
  end
end
sheet("sheet_rares", rareIcons, 6, 6)
sheet("sheet_silhouettes", silIcons, 4, 12)
