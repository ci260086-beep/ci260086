-- One-tap merge, into Assets/Art/Items:
--   merge_all_normal, merge_all_pressed, merge_all_disabled   16x16 button in three states:
--       three small drills sweeping into one star; pressed sits a pixel lower and darker,
--       disabled is greyed out
--   merge_burst        8 frames of 32x32 (256x32), played once: a flash, a ring, sparks, fade
--   merge_count_plate  24x11 plate for the game to write "xN" on
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_merge_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local INK, WHITE = hex("1a1210"), hex("ffffff")
local GOLD, GOLD_HI, O1 = hex("e8b02a"), hex("ffe27a"), hex("b8693a")

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

local function save(g, name)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
end

local function grey(c)
  local v = math.floor((pc.rgbaR(c) * 0.3 + pc.rgbaG(c) * 0.55 + pc.rgbaB(c) * 0.15) * 0.75 + 0.5)
  return pc.rgba(v, v, v + 6 > 255 and 255 or v + 6, pc.rgbaA(c))
end
local function darker(c, t)
  return pc.rgba(math.floor(pc.rgbaR(c) * t), math.floor(pc.rgbaG(c) * t), math.floor(pc.rgbaB(c) * t), pc.rgbaA(c))
end

---------------------------------------------------------------------------------------------------
-- the button: a raised green plate with an ink rim, the icon on top
---------------------------------------------------------------------------------------------------
local PLATE, PLATE_HI, PLATE_LO = hex("3a8a4a"), hex("6ac87a"), hex("1e4a2a")
local DRILL, DRILL_HI = hex("c8d8f0"), WHITE

local function button(state)
  local g = canvas(16, 16)
  local drop = state == "pressed" and 1 or 0
  -- rim and lip: the lip under the plate is what disappears when pressed
  g:rect(1, 1 + drop, 14, 14, INK); g:rect(0, 2 + drop, 15, 13, INK)
  g:rect(1, 2 + drop, 14, 13, PLATE)
  g:rect(2, 2 + drop, 13, 2 + drop, PLATE_HI); g:rect(1, 3 + drop, 1, 12, PLATE_HI)
  if drop == 0 then g:rect(1, 13, 14, 13, PLATE_LO); g:rect(14, 3, 14, 12, PLATE_LO) end
  -- three drills sweep together into a star
  g:pattern(3, 3 + drop, {
    "bbb.bbb.bbb",
    "bBb.bBb.bBb",
    ".t...t...t.",
    "..l..l..l..",
    "...l.l.l...",
    "....sSs....",
    "...sSWSs...",
    "....sSs....",
    ".....s.....",
  }, { b = DRILL, B = DRILL_HI, t = hex("8a9ab8"), l = GOLD_HI, s = GOLD, S = GOLD_HI, W = WHITE })
  if state == "pressed" then
    for k, c in pairs(g.px) do if c ~= INK then g.px[k] = darker(c, 0.82) end end
  elseif state == "disabled" then
    for k, c in pairs(g.px) do if c ~= INK then g.px[k] = grey(c) end end
  end
  return g
end

local buttons = {}
for _, state in ipairs({ "normal", "pressed", "disabled" }) do
  buttons[#buttons + 1] = button(state)
  save(buttons[#buttons], "merge_all_" .. state)
end

---------------------------------------------------------------------------------------------------
-- the burst: frame 0 a white core, then a ring widens and thins while sparks fly out and
-- cool from white to gold to orange; the last frames leave a few embers
---------------------------------------------------------------------------------------------------
local FRAMES = 8
local burst = canvas(32 * FRAMES, 32)
local SPARK_DIRS = 10
for f = 0, FRAMES - 1 do
  local g = canvas(32, 32)
  local cx, cy = 15.5, 15.5
  if f <= 1 then
    local r = f == 0 and 4 or 6
    for y = 0, 31 do for x = 0, 31 do
      local d = math.sqrt((x - cx) ^ 2 + (y - cy) ^ 2)
      if d <= r then g:set(x, y, d < r - 2 and WHITE or GOLD_HI) end
    end end
  end
  if f >= 1 and f <= 5 then
    local r = 4 + f * 2.6
    local col = ({ WHITE, GOLD_HI, GOLD_HI, GOLD, O1 })[f]
    for i = 0, 127 do
      local a = i / 128 * 2 * math.pi
      g:set(cx + math.cos(a) * r, cy + math.sin(a) * r, col)
      if f <= 3 then g:set(cx + math.cos(a) * (r - 1), cy + math.sin(a) * (r - 1), col) end
    end
  end
  for i = 0, SPARK_DIRS - 1 do
    local a = i / SPARK_DIRS * 2 * math.pi + 0.3
    local r = 3 + f * 2.2 + (i % 2) * 1.5
    local col = f < 2 and WHITE or (f < 4 and GOLD_HI or (f < 6 and GOLD or O1))
    if f < 7 or i % 3 == 0 then
      local x, y = cx + math.cos(a) * r, cy + math.sin(a) * r + (f > 4 and (f - 4) or 0)
      g:set(x, y, col)
      if f < 5 then g:set(x - math.cos(a), y - math.sin(a), f < 3 and GOLD_HI or GOLD) end
    end
  end
  if f >= 2 and f <= 4 then
    for _, d in ipairs({ { 0, -1 }, { 1, 0 }, { 0, 1 }, { -1, 0 } }) do
      local len = 6 - f
      for s = 1, len do g:set(cx + d[1] * s, cy + d[2] * s, WHITE) end
    end
  end
  for k, c in pairs(g.px) do burst.px[(k // 32) * burst.w + f * 32 + k % 32] = c end
end
save(burst, "merge_burst")

---------------------------------------------------------------------------------------------------
-- the count plate: a dark rounded pill with a gold rim and a small spark on the left; the
-- game writes "xN" in the space from x 6 to x 22
---------------------------------------------------------------------------------------------------
local plate = canvas(24, 11)
plate:rect(2, 0, 21, 10, INK); plate:rect(0, 2, 23, 8, INK); plate:rect(1, 1, 22, 9, INK)
plate:rect(2, 1, 21, 9, GOLD); plate:rect(1, 2, 22, 8, GOLD)
plate:rect(2, 2, 21, 8, hex("2a1e14")); plate:rect(2, 2, 21, 2, hex("3e2c1e"))
plate:rect(2, 1, 21, 1, GOLD_HI)
plate:pattern(2, 3, { ".s.", "sWs", ".s." }, { s = GOLD_HI, W = WHITE })
save(plate, "merge_count_plate")

-- preview: the three buttons, the burst frames over a drill, the plate, at 6x
if previewDir then
  local k = 6
  local drill = Image { fromFile = dir .. "/Drill/drill_0_6.png" }
  local img = Image((32 * FRAMES + FRAMES + 1) * k, (16 + 32 + 4) * k, ColorMode.RGB)
  img:clear(hex("2a2630"))
  local function put(px, py, c)
    for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel(px * k + a, py * k + b, c) end end
  end
  for i, b in ipairs(buttons) do
    for y = 0, 15 do for x = 0, 15 do local c = b:get(x, y); if c then put(1 + (i - 1) * 18 + x, 1 + y, c) end end end
  end
  for y = 0, 10 do for x = 0, 23 do local c = plate:get(x, y); if c then put(60 + x, 3 + y, c) end end end
  for f = 0, FRAMES - 1 do
    for y = 0, 31 do for x = 0, 31 do
      local c = hex("1e161c")
      if x >= 12 and x < 20 and y >= 10 and y < 22 then
        local d = drill:getPixel(x - 12, y - 10)
        if pc.rgbaA(d) > 0 then c = d end
      end
      local e = burst:get(f * 32 + x, y)
      if e then c = e end
      put(1 + f * 33 + x, 19 + y, c)
    end end
  end
  img:saveAs(previewDir .. "/merge_preview.png")
end
