-- Planet rules and star map nodes, into Assets/Art/Items:
--   planet_rule_<n>   16x16 icon for each planet with a rule, n is its index in phase3.json's
--                     planets: 1 low gravity, 2 heat, 3 hard rich rock, 4 double shards,
--                     5 lightning, 6 ice crystal, 7 waves
--   star_node_reached, star_node_locked, star_node_current   12x12 markers for the star map
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_planet_rules.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local INK, WHITE = hex("1a1210"), hex("ffffff")
local GOLD, GOLD_HI = hex("e8b02a"), hex("ffe27a")

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

local function outline(g)
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) and (g:get(x - 1, y) or g:get(x + 1, y) or g:get(x, y - 1) or g:get(x, y + 1)) then
        out[#out + 1] = { x, y }
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], INK) end
end

local function save(g, name)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
end

---------------------------------------------------------------------------------------------------
-- rule icons
---------------------------------------------------------------------------------------------------
local RULES = {
  -- 1 月壤星 low gravity: a drill drifting down under a parachute of dust, a slow arrow beside
  [1] = function(g)
    local canopy, hi = hex("b4b4c2"), hex("e8e8f0")
    g:ellipse(6, 4, 5, 3, canopy)
    for y = 5, 7 do for x = 0, 12 do g:set(x, y, nil) end end
    g:rect(2, 2, 6, 2, hi)
    g:pattern(1, 5, { "#.........#", ".#.......#.", "..#.....#..", "...#...#..." }, { ["#"] = hex("8a8a98") })
    g:pattern(4, 9, {
      "#####",
      "#bbb#",
      ".#w#.",
      ".#w#.",
      "..#..",
    }, { ["#"] = hex("5c7696"), b = hex("4fb8e8"), w = hex("d4ecff") })
    g:pattern(12, 6, { ".#.", ".#.", ".#.", "###", ".#." }, { ["#"] = hex("8ad0ff") })
  end,
  -- 2 熔岩星 heat: a flame over a thermometer running hot
  [2] = function(g)
    g:pattern(1, 1, {
      "....#.......",
      "...##.......",
      "...###..#...",
      "..#####.##..",
      "..##yy####..",
      ".##yyyy###..",
      ".#yywwyy##..",
      ".#yywwyyy#..",
      "..#yyyyy#...",
      "...#####....",
    }, { ["#"] = hex("e0401e"), y = hex("ffb040"), w = WHITE })
    g:rect(12, 2, 13, 11, hex("e8e8f0")); g:rect(12, 5, 13, 11, hex("e0401e"))
    g:ellipse(12.5, 13, 2, 1.6, hex("e0401e")); g:set(12, 12, hex("ff9a7a"))
  end,
  -- 3 富矿星 hard rich rock: a grey block of hard rock with a gem set in it, a x2 spark
  [3] = function(g)
    g:rect(1, 4, 11, 14, hex("8a8a98")); g:rect(1, 4, 11, 4, hex("b4b4c2")); g:rect(1, 4, 1, 14, hex("b4b4c2"))
    g:rect(11, 5, 11, 14, hex("5c5c6a")); g:rect(2, 14, 11, 14, hex("5c5c6a"))
    g:pattern(3, 7, { "..#..", ".#w#.", "#ggg#", ".#g#.", "..#.." }, { ["#"] = hex("1e8a6a"), g = hex("4ae0c0"), w = WHITE })
    g:pattern(9, 0, { "#.#.###", ".#....#", "#.#.###", "....#..", "....###" }, { ["#"] = GOLD_HI })
  end,
  -- 4 碎晶星 double shards: two violet shards side by side with a x2
  [4] = function(g)
    local lo, mid, hi = hex("4a2a8a"), hex("a070e8"), hex("e0d0ff")
    local function shard(x, y)
      g:pattern(x, y, { "..h..", ".hhm.", ".hmm.", "hhmmm", "hhmml", ".hml.", ".hml.", "..l.." }, { h = hi, m = mid, l = lo })
    end
    shard(1, 6); shard(6, 4)
    g:pattern(9, 0, { "#.#.###", ".#....#", "#.#.###", "....#..", "....###" }, { ["#"] = GOLD_HI })
  end,
  -- 5 雷暴星 storm: a bold lightning bolt out of a dark cloud
  [5] = function(g)
    g:ellipse(7, 3.5, 6.5, 3, hex("3a2660"))
    g:ellipse(5, 2.5, 3.5, 2, hex("5e3e8e"))
    g:rect(2, 2, 4, 2, hex("9a7ac8"))
    g:pattern(4, 6, {
      "...####",
      "..####.",
      ".####..",
      "#######",
      "...###.",
      "..###..",
      "..##...",
      ".##....",
      ".#.....",
    }, { ["#"] = hex("ffe14a") })
    g:pattern(5, 6, { "..w", ".w.", "w.." }, { w = hex("fff6c8") })
  end,
  -- 6 永冻星 frost: a six-armed ice crystal
  [6] = function(g)
    g:pattern(1, 1, {
      ".....#.#.....",
      "......#......",
      "..#...#...#..",
      "...#..#..#...",
      "....#.#.#....",
      ".....###.....",
      "#############",
      ".....###.....",
      "....#.#.#....",
      "...#..#..#...",
      "..#...#...#..",
      "......#......",
      ".....#.#.....",
    }, { ["#"] = hex("9ad4f0") })
    g:rect(6, 6, 8, 8, hex("e0f6ff")); g:set(7, 7, WHITE)
    for _, p in ipairs({ { 7, 2 }, { 7, 12 }, { 2, 7 }, { 12, 7 } }) do g:set(p[1], p[2], hex("e0f6ff")) end
  end,
  -- 7 潮汐星 tide: two rolling waves, foam on the crests
  [7] = function(g)
    local function wave(y, deep, mid)
      g:pattern(0, y, {
        "..###.....###...",
        ".#mmm#...#mmm#..",
        "#mmmmm###mmmmm##",
        "dddddddddddddddd",
      }, { ["#"] = hex("ffffff"), m = mid, d = deep })
    end
    wave(3, hex("1a4e9a"), hex("6ab4f0"))
    wave(9, hex("0e2e66"), hex("2e78c8"))
    g:rect(0, 13, 15, 14, hex("0e2e66"))
  end,
}

local made = {}
for n = 1, 7 do
  local g = canvas(16, 16)
  RULES[n](g)
  outline(g)
  save(g, "planet_rule_" .. n)
  made[#made + 1] = g
end

---------------------------------------------------------------------------------------------------
-- star map nodes: reached is a lit gold star, locked a dim hollow ring with a keyhole dot,
-- current a bright white star inside a pulsing ring
---------------------------------------------------------------------------------------------------
local function star(g, cx, cy, outer, inner, c)
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      local dx, dy = x - cx, y - cy
      local a = math.atan(dy, dx) + math.pi / 2
      local r = math.sqrt(dx * dx + dy * dy)
      local t = (math.cos(a * 5) + 1) / 2
      if r <= inner + (outer - inner) * t then g:set(x, y, c) end
    end
  end
end

local reached = canvas(12, 12)
star(reached, 5.5, 6, 5.5, 2.4, GOLD); star(reached, 5.5, 6, 3.5, 1.6, GOLD_HI); reached:set(5, 5, WHITE)
outline(reached)
save(reached, "star_node_reached")

local locked = canvas(12, 12)
for i = 0, 63 do
  local a = i / 64 * 2 * math.pi
  locked:set(5.5 + math.cos(a) * 4.5, 5.5 + math.sin(a) * 4.5, hex("4a4a6a"))
end
locked:rect(5, 5, 6, 6, hex("6a6a8a"))
save(locked, "star_node_locked")

local current = canvas(12, 12)
for i = 0, 63 do
  local a = i / 64 * 2 * math.pi
  if i % 4 ~= 0 then current:set(5.5 + math.cos(a) * 5.4, 5.5 + math.sin(a) * 5.4, hex("8ad0ff")) end
end
star(current, 5.5, 6, 4, 1.8, WHITE); current:set(5, 6, hex("c8f0ff"))
save(current, "star_node_current")

-- preview: the seven rule icons and the three nodes at 8x on a night background
if previewDir then
  local k = 8
  local img = Image((7 * 18 + 3 * 14 + 4) * k, 20 * k, ColorMode.RGB)
  img:clear(hex("10102a"))
  local function put(ox, oy, g)
    for y = 0, g.h - 1 do for x = 0, g.w - 1 do
      local c = g:get(x, y)
      if c then for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel((ox + x) * k + a, (oy + y) * k + b, c) end end end
    end end
  end
  for i, g in ipairs(made) do put(2 + (i - 1) * 18, 2, g) end
  for i, g in ipairs({ reached, locked, current }) do put(2 + 7 * 18 + (i - 1) * 14, 4, g) end
  img:saveAs(previewDir .. "/planet_rules_preview.png")
end
