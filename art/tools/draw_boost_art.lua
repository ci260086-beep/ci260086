-- Tap boost feedback, into Assets/Art/Items:
--   boost_spark     6 looping frames of 16x16 (96x16), centred on a drill standing in the middle
--                   (the 8x12 drill covers x 4-11, y 2-13): sparks thrown off the bit, speed lines
--   boost_bar_empty and boost_bar_full, 24x6, drawn to line up so the game can draw the empty bar
--                   and clip the full one to the boost level
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_boost_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local INK, WHITE = hex("1a1210"), hex("ffffff")
local GOLD, GOLD_HI, ORANGE, RED = hex("e8b02a"), hex("ffe27a"), hex("ff8a2a"), hex("e0401e")
local BLUE, BLUE_HI = hex("3a8ae8"), hex("8ad0ff")

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
  return g
end

local function save(g, name)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
end

---------------------------------------------------------------------------------------------------
-- sparks: each spark leaves the bit tip (7.5, 13), flies out and falls, a step further each frame;
-- six sparks spaced one frame apart so the loop never empties
---------------------------------------------------------------------------------------------------
local FRAMES = 6
local SPARKS = { { -1.6, -0.6 }, { 1.7, -0.4 }, { -1.1, -1.3 }, { 1.2, -1.2 }, { -2.0, 0.1 }, { 2.0, 0.2 } }

local strip = canvas(16 * FRAMES, 16)
for f = 0, FRAMES - 1 do
  local g = canvas(16, 16)
  for i, v in ipairs(SPARKS) do
    local t = (f + i - 1) % FRAMES
    local x, y = 7.5 + v[1] * t, 13 + v[2] * t + 0.25 * t * t
    local c = t < 2 and WHITE or (t < 4 and GOLD_HI or (t < 5 and ORANGE or RED))
    g:set(x, y, c)
    if t < 4 then g:set(x - v[1] * 0.6, y - v[2] * 0.6, t < 2 and GOLD_HI or ORANGE) end
  end
  -- speed lines streaming up past both sides, shifted each frame
  for side = 0, 1 do
    local x = side == 0 and 2 or 13
    for k = 0, 1 do
      local y0 = (f * 3 + k * 8 + side * 4) % 16
      for s = 0, 2 do g:set(x - side * k + k * (1 - side), (y0 + s) % 16, hex("c8e8ff", 200 - s * 60)) end
    end
  end
  -- a hot flash at the bit tip every other frame
  if f % 2 == 0 then g:set(7, 14, WHITE); g:set(8, 14, GOLD_HI) else g:set(7, 14, GOLD_HI) end
  for k, c in pairs(g.px) do strip.px[(k // 16) * strip.w + f * 16 + k % 16] = c end
end
save(strip, "boost_spark")

---------------------------------------------------------------------------------------------------
-- speed bar: an ink frame with a dark well; full fills it with blue running to gold, a light
-- top edge and chevrons pointing the way the drill goes
---------------------------------------------------------------------------------------------------
local function bar(full)
  local g = canvas(24, 6)
  g:rect(1, 0, 22, 0, INK); g:rect(1, 5, 22, 5, INK); g:rect(0, 1, 0, 4, INK); g:rect(23, 1, 23, 4, INK)
  g:rect(1, 1, 22, 4, hex("241c2e")); g:rect(1, 4, 22, 4, hex("181220"))
  if full then
    for x = 1, 22 do
      local t = (x - 1) / 21
      local c = t < 0.45 and BLUE or (t < 0.75 and hex("a8c84a") or GOLD)
      local hi = t < 0.45 and BLUE_HI or (t < 0.75 and hex("e0f08a") or GOLD_HI)
      g:rect(x, 1, x, 4, c); g:set(x, 1, hi); g:set(x, 4, t < 0.45 and hex("1e5ab0") or ORANGE)
    end
    for x = 4, 20, 5 do g:set(x, 2, WHITE); g:set(x + 1, 3, WHITE) end
  else
    for x = 4, 20, 5 do g:set(x, 2, hex("3a3048")); g:set(x + 1, 3, hex("3a3048")) end
  end
  return g
end
local empty, full = bar(false), bar(true)
save(empty, "boost_bar_empty")
save(full, "boost_bar_full")

-- preview: the spark frames over a drill at 8x, and both bars plus a half-filled bar at 8x
if previewDir then
  local k = 8
  local drill = Image { fromFile = dir .. "/Drill/drill_0_4.png" }
  local img = Image((16 * FRAMES + FRAMES + 1) * k, (16 + 2 + 6 * 3 + 4) * k, ColorMode.RGB)
  img:clear(hex("2a2630"))
  local function put(px, py, c)
    for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel(px * k + a, py * k + b, c) end end
  end
  for f = 0, FRAMES - 1 do
    local ox = 1 + f * 17
    for y = 0, 15 do for x = 0, 15 do
      local c = hex("1e161c")
      if x >= 4 and x < 12 and y >= 2 and y < 14 then
        local d = drill:getPixel(x - 4, y - 2)
        if pc.rgbaA(d) > 0 then c = d end
      end
      local e = strip:get(f * 16 + x, y)
      if e then
        local a = pc.rgbaA(e) / 255
        local function m(p, q) return math.floor(p + (q - p) * a + 0.5) end
        c = pc.rgba(m(pc.rgbaR(c), pc.rgbaR(e)), m(pc.rgbaG(c), pc.rgbaG(e)), m(pc.rgbaB(c), pc.rgbaB(e)))
      end
      put(ox + x, 1 + y, c)
    end end
  end
  for row, fill in ipairs({ 0, 12, 22 }) do
    for y = 0, 5 do for x = 0, 23 do
      local c = (x >= 1 and x <= fill) and full:get(x, y) or empty:get(x, y)
      if c then put(1 + x, 18 + (row - 1) * 7 + y, c) end
    end end
  end
  img:saveAs(previewDir .. "/boost_preview.png")
end
