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
