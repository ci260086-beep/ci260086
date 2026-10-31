-- Tutorial art: a 16x16 pointer arrow and a 48x32 picture for each system's explainer card,
-- into Assets/Art/Items as guide_arrow and guide_<system>, with the same system names as sys_.
-- Each picture is a small scene on its own dark card, so it reads without the UI around it.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_guide_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end

-- the base palette of draw_game_art.lua and draw_icons.lua
local INK, D2, D3, D5 = hex("1a1210"), hex("4a3122"), hex("6b4630"), hex("a87a52")
local G2, G3, G4 = hex("4a7a2a"), hex("76a83a"), hex("a8d45a")
local K1, K2 = hex("1e1e24"), hex("3a3a44")
local S1, S2, S3 = hex("5c5c6a"), hex("8a8a98"), hex("b4b4c2")
local GOLD, GOLD_HI, O1 = hex("e8b02a"), hex("ffe27a"), hex("b8693a")
local WHITE = hex("ffffff")
local RED, BLUE = hex("d8402e"), hex("3a7ad8")
local CARD, CARD_HI, CARD_EDGE = hex("221a2c"), hex("2e2440"), hex("120e18")

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c)
    x, y = math.floor(x + 0.5), math.floor(y + 0.5)
    if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end
  end
  function g:get(x, y)
    if x < 0 or y < 0 or x >= w or y >= h then return nil end
    return self.px[y * w + x]
  end
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
    local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0), 1)
    for i = 0, n do self:set(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n, c) end
  end
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

local function ball(g, cx, cy, rx, ry, lo, mid, hi)
  for y = math.floor(cy - ry), math.ceil(cy + ry) do
    for x = math.floor(cx - rx), math.ceil(cx + rx) do
      local nx, ny = (x - cx) / rx, (y - cy) / ry
      if nx * nx + ny * ny <= 1 then g:set(x, y, nx + ny < -0.55 and hi or (nx + ny > 0.55 and lo or mid)) end
    end
  end
end

local function save(g, name)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
end

-- the standard drill's first frame, in a mid tier, to stand in scenes
local drillImg = Image { fromFile = dir .. "/Drill/drill_0_4.png" }
local function drill(g, x, y)
  for dy = 0, 11 do
    for dx = 0, 7 do
      local c = drillImg:getPixel(dx, dy)
      if pc.rgbaA(c) > 0 then g:set(x + dx, y + dy, c) end
    end
  end
end

-- a dark card with a lighter glow behind the middle and a 1px dark edge
local function card()
  local g = canvas(48, 32)
  for y = 0, 31 do
    for x = 0, 47 do
      local d = ((x - 23.5) / 26) ^ 2 + ((y - 15.5) / 18) ^ 2
      g:set(x, y, (d < 0.55 and (x + y) % 2 == 0 or d < 0.35) and CARD_HI or CARD)
    end
  end
  for x = 0, 47 do g:set(x, 0, CARD_EDGE); g:set(x, 31, CARD_EDGE) end
  for y = 0, 31 do g:set(0, y, CARD_EDGE); g:set(47, y, CARD_EDGE) end
  return g
end

-- a strip of ground along the bottom of a card, with the grass of the surface on top
local function ground(g, top, grass)
  for y = top, 30 do
    for x = 1, 46 do g:set(x, y, ((x * 7 + y * 13) % 11 == 0) and D5 or (y == top and grass and G3 or D3)) end
  end
  if grass then for x = 1, 46 do g:set(x, top - 1, (x % 3 == 0) and G4 or G3) end end
end

---------------------------------------------------------------------------------------------------
-- the arrow: a gold down-pointer with an ink outline and a light edge, for the tutorial to bob
---------------------------------------------------------------------------------------------------
local arrow = canvas(16, 16)
arrow:pattern(1, 1, {
  "....######....",
  "....#hhyy#....",
  "....#hyyy#....",
  "....#hyyy#....",
  "....#hyyy#....",
  "#####hyyy#####",
  "#hhhhyyyyyyyo#",
  ".#hyyyyyyyyo#.",
  "..#hyyyyyyo#..",
  "...#hyyyyo#...",
  "....#hyyo#....",
  ".....#yo#.....",
  "......##......",
}, { ["#"] = INK, h = WHITE, y = GOLD_HI, o = GOLD })
save(arrow, "guide_arrow")

---------------------------------------------------------------------------------------------------
-- the card pictures
---------------------------------------------------------------------------------------------------
local SCENES = {
  -- 黄金事件: a drill strikes a seam and gold bursts out of the ground
  { "event", function(g)
    ground(g, 20, true)
    drill(g, 20, 9)
    for _, n in ipairs({ { 12, 23 }, { 32, 24 }, { 24, 27 } }) do ball(g, n[1], n[2], 2.5, 2, O1, GOLD, GOLD_HI) end
    for i, p in ipairs({ { 10, 8 }, { 36, 6 }, { 14, 3 }, { 33, 13 }, { 40, 10 }, { 7, 14 } }) do
      ball(g, p[1], p[2], 1.5, 1.5, O1, GOLD, GOLD_HI)
      if i % 2 == 0 then g:set(p[1] + 2, p[2] - 2, WHITE) end
    end
  end },

  -- 地表加工厂: a factory on the surface with ore riding a belt into it
  { "factory", function(g)
    ground(g, 24, true)
    g:rect(22, 10, 42, 23, S2); g:rect(22, 10, 42, 11, S3)
    for i = 0, 2 do for s = 0, 3 do g:rect(22 + i * 7 + s, 10 - s, 22 + i * 7 + s, 10, S2) end end
    g:rect(36, 2, 39, 9, D3); g:rect(36, 2, 39, 2, RED)
    g:ellipse(40, 1, 2, 1, S3); g:ellipse(44, 0, 2, 1, S3)
    g:rect(25, 14, 27, 16, GOLD_HI); g:rect(37, 14, 39, 16, GOLD_HI)
    g:rect(30, 16, 34, 23, K2)
    g:rect(3, 21, 22, 22, K2); for x = 4, 21, 3 do g:set(x, 22, S1) end
    for _, x in ipairs({ 5, 11, 17 }) do g:rect(x, 18, x + 3, 20, D5); g:set(x + 1, 18, GOLD) end
  end },

  -- 研究所: a bench of flasks bubbling under a lamp, a crystal being studied
  { "lab", function(g)
    g:rect(4, 22, 43, 24, D3); g:rect(4, 22, 43, 22, D5); g:rect(6, 25, 7, 30, D2); g:rect(40, 25, 41, 30, D2)
    local glass, fluid, fluid2 = hex("c8e8f0"), hex("3ab86a"), hex("d85ab8")
    g:rect(10, 12, 11, 15, glass); g:ellipse(10.5, 18.5, 4, 3.5, glass); g:ellipse(10.5, 19.5, 3, 2.5, fluid)
    g:rect(18, 10, 20, 21, glass); g:rect(18, 15, 20, 21, fluid2)
    g:set(10, 9, WHITE); g:set(12, 7, fluid); g:set(19, 7, fluid2); g:set(20, 4, WHITE)
    g:line(32, 2, 32, 7, S2); g:ellipse(32, 9, 5, 2, S3); g:ellipse(32, 11, 3, 1, GOLD_HI)
    local c1, c2 = hex("8a5ad8"), hex("d0b0ff")
    g:rect(30, 15, 34, 21, c1); g:rect(30, 15, 31, 21, c2); g:set(32, 13, c2); g:set(32, 14, c1)
    for y = 12, 21, 3 do g:set(27, y, hex("fff0b0", 140)); g:set(37, y, hex("fff0b0", 140)) end
  end },

  -- 探险队: an explorer with a lantern heads down a tunnel towards a chest
  { "expedition", function(g)
    for y = 6, 30 do for x = 1, 46 do
      if ((x - 24) / 24) ^ 2 + ((y - 30) / 22) ^ 2 > 1 then g:set(x, y, ((x * 5 + y * 3) % 9 == 0) and D5 or D3) end
    end end
    g:rect(1, 28, 46, 30, D2)
    g:rect(10, 15, 13, 21, hex("4a6ab8")); g:ellipse(11.5, 13, 2, 2, hex("f0c8a0"))
    g:rect(9, 11, 14, 11, hex("c89a3a")); g:rect(10, 10, 13, 10, hex("c89a3a"))
    g:rect(10, 22, 10, 27, K2); g:rect(13, 22, 13, 27, K2)
    g:rect(14, 16, 16, 16, S2); g:rect(16, 17, 17, 19, GOLD_HI); g:set(18, 18, hex("fff0b0", 160))
    g:rect(8, 15, 9, 21, hex("8a5a3a"))
    g:rect(33, 21, 41, 27, hex("b8693a")); g:rect(33, 21, 41, 23, hex("8a4a2a")); g:rect(36, 23, 38, 24, GOLD)
    for i = 0, 2 do g:set(20 + i * 4, 25, D5) end
  end },

  -- 挑战深井: a drill plunging down a narrow shaft between hazard stripes, the bottom out of sight
  { "challenge", function(g)
    for y = 1, 30 do for x = 1, 46 do
      if x < 17 or x > 30 then g:set(x, y, ((x * 7 + y * 5) % 10 == 0) and S2 or S1) end
    end end
    for y = 1, 30 do
      g:set(17, y, ((y // 2) % 2 == 0) and GOLD or INK); g:set(30, y, ((y // 2) % 2 == 0) and GOLD or INK)
    end
    for y = 1, 30 do for x = 18, 29 do g:set(x, y, y > 22 and K1 or (y > 15 and K2 or CARD_HI)) end end
    drill(g, 20, 5)
    for _, y in ipairs({ 2, 3, 4 }) do g:set(21, y - 1, S3); g:set(26, y - 2, S3) end
    for y = 8, 26, 6 do g:rect(32, y, 34, y, WHITE) end
  end },

  -- 星座图: stars joined into a figure over a ringed planet
  { "constellation", function(g)
    local night = hex("141838")
    for y = 1, 30 do for x = 1, 46 do g:set(x, y, night) end end
    for i = 0, 22 do g:set(2 + (i * 17) % 44, 2 + (i * 11) % 28, hex("6a7ab0")) end
    ball(g, 38, 24, 6, 6, hex("6a3a8a"), hex("a86ad8"), hex("e0b0ff"))
    g:line(29, 25, 47, 21, hex("e8c8ff")); g:line(29, 26, 47, 22, hex("8a6ab0"))
    local stars = { { 5, 20 }, { 11, 8 }, { 20, 12 }, { 27, 4 }, { 18, 22 } }
    local thread = hex("6a8ad8")
    for i = 1, #stars - 1 do g:line(stars[i][1], stars[i][2], stars[i + 1][1], stars[i + 1][2], thread) end
    g:line(20, 12, 18, 22, thread)
    for i, s in ipairs(stars) do
      g:set(s[1], s[2], WHITE)
      if i % 2 == 0 then
        g:set(s[1] - 1, s[2], GOLD_HI); g:set(s[1] + 1, s[2], GOLD_HI); g:set(s[1], s[2] - 1, GOLD_HI); g:set(s[1], s[2] + 1, GOLD_HI)
      end
    end
  end },

  -- 钻探流派: three drills on pedestals under their own banners, pick one per run
  { "school", function(g)
    local banners = { RED, BLUE, hex("4aa84a") }
    for i = 0, 2 do
      local x = 6 + i * 14
      g:rect(x + 1, 2, x + 8, 9, banners[i + 1]); g:rect(x + 1, 2, x + 8, 2, WHITE)
      g:set(x + 1, 10, banners[i + 1]); g:set(x + 8, 10, banners[i + 1])
      g:rect(x, 1, x + 9, 1, D5)
      g:rect(x, 24, x + 9, 28, S2); g:rect(x, 24, x + 9, 24, S3)
      drill(g, x + 1, 12)
    end
    g:pattern(20, 3, { "..#..", ".###.", "#####" }, { ["#"] = GOLD_HI })
  end },

  -- 地心结晶: a core crystal growing on a stand while the clock runs
  { "crystal", function(g)
    g:rect(14, 25, 33, 28, S1); g:rect(16, 23, 31, 24, S2); g:rect(16, 23, 31, 23, S3)
    local lo, mid, hi = hex("c86a20"), hex("ffb040"), hex("fff0b0")
    g:pattern(17, 3, {
      ".......hh.....",
      "......hhmm....",
      "..h...hhmm....",
      ".hhm..hhmm..m.",
      ".hhm..hhmm.mm.",
      ".hhm..hhmm.mm.",
      ".hhm..hhmm.mm.",
      ".hhm..hhmm.ml.",
      ".hhl..hhml.ll.",
      ".hll..hhll.ll.",
      ".hll..hlll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
      ".lll..llll.ll.",
    }, { h = hi, m = mid, l = lo })
    ball(g, 40, 9, 5, 5, S1, S3, WHITE)
    g:ellipse(40, 9, 3.5, 3.5, hex("f4f0e0")); g:line(40, 9, 40, 6, INK); g:line(40, 9, 42, 10, INK)
    for _, p in ipairs({ { 12, 6 }, { 35, 2 }, { 10, 15 } }) do g:set(p[1], p[2], GOLD_HI) end
  end },

  -- 自动化: a turning gear drives a belt that carries drills along by themselves
  { "automation", function(g)
    for y = 0, 31 do for x = 0, 47 do
      local dx, dy = x - 13, y - 12
      local d = math.sqrt(dx * dx + dy * dy)
      if d <= 7 or (d <= 9 and math.cos(math.atan(dy, dx) * 8) > 0.2) then g:set(x, y, (dx + dy < -3) and S3 or S2) end
    end end
    g:ellipse(13, 12, 3, 3, K2)
    g:pattern(12, 10, { "g..", "gg.", "ggg", "gg.", "g.." }, { g = G4 })
    g:rect(4, 24, 44, 26, K2); g:rect(4, 24, 44, 24, S1)
    g:ellipse(5, 25, 2, 2, S2); g:ellipse(43, 25, 2, 2, S2)
    drill(g, 22, 12); drill(g, 33, 12)
    for x = 24, 40, 4 do g:set(x, 28, G3) end
    g:pattern(41, 28, { "g.", "gg", "g." }, { g = G3 })
  end },

  -- 地核不稳: the core glows red through cracks, rocks shake loose from the ceiling
  { "unstable", function(g)
    for y = 1, 6 do for x = 1, 46 do g:set(x, y, ((x * 3 + y * 7) % 8 == 0) and D5 or D3) end end
    ball(g, 24, 20, 12, 9, hex("8a1a10"), hex("e0401e"), hex("ff9a5a"))
    local crack = GOLD_HI
    g:line(24, 11, 22, 16, crack); g:line(22, 16, 26, 20, crack); g:line(26, 20, 23, 25, crack)
    g:line(26, 20, 31, 18, crack); g:line(22, 16, 17, 18, crack); g:set(24, 20, WHITE)
    for _, r in ipairs({ { 8, 10 }, { 38, 12 }, { 14, 14 }, { 42, 7 } }) do g:rect(r[1], r[2], r[1] + 1, r[2] + 1, D5) end
    for _, s in ipairs({ { 6, 20 }, { 41, 21 } }) do
      g:line(s[1], s[2] - 2, s[1], s[2] + 2, hex("ff9a5a")); g:line(s[1] + 2, s[2] - 1, s[1] + 2, s[2] + 1, hex("ff9a5a"))
    end
  end },
}

local made = {}
for _, scene in ipairs(SCENES) do
  local g = card()
  scene[2](g)
  save(g, "guide_" .. scene[1])
  made[#made + 1] = g
end

-- preview: the arrow, then every card, all 4x in a grid of three
if previewDir then
  local k, cols = 4, 3
  local rows = (#made + cols - 1) // cols + 1
  local img = Image((cols * 50 + 2) * k, (rows * 34 + 2) * k, ColorMode.RGB)
  img:clear(hex("3a3444"))
  local function put(ox, oy, g)
    for y = 0, g.h - 1 do for x = 0, g.w - 1 do
      local c = g:get(x, y)
      if c and pc.rgbaA(c) > 0 then
        for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel((ox + x) * k + a, (oy + y) * k + b, c) end end
      end
    end end
  end
  put(2, 2, arrow)
  for i, g in ipairs(made) do put(2 + ((i - 1) % cols) * 50, 36 + ((i - 1) // cols) * 34, g) end
  img:saveAs(previewDir .. "/guide_preview.png")
end
