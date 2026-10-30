-- Draws every in-game sprite into Assets/Art.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> --script draw_game_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")

local function hex(s, a)
  return app.pixelColor.rgba(
    tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end

-- topsoil palette
local INK, D1, D2, D3, D4, D5, D6, D7 = hex("1a1210"), hex("2e1f17"), hex("4a3122"), hex("6b4630"),
  hex("8c5f3f"), hex("a87a52"), hex("c49a6c"), hex("dfc08f")
local G1, G2, G3, G4 = hex("2f4a1f"), hex("4a7a2a"), hex("76a83a"), hex("a8d45a")
local O1, O2 = hex("b8693a"), hex("e08a4a")
local K1, K2 = hex("1e1e24"), hex("3a3a44")
-- extensions for things the palette cannot express (grey stone, metals, red, yellow, blue)
local S1, S2, S3 = hex("5c5c6a"), hex("8a8a98"), hex("b4b4c2")
local GOLD, GOLD_HI = hex("e8b02a"), hex("ffe27a")

local seed = 1616
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

-- Bevelled 16x16 block: top/left lit, bottom/right shaded, ink seam on the bottom and right
-- so neighbouring blocks share a single ink line.
local function bevel(g, hi, lo)
  for i = 0, 14 do
    g:set(i, 0, hi)
    g:set(0, i, hi)
    g:set(i, 14, lo)
    g:set(14, i, lo)
  end
  for i = 0, 15 do
    g:set(i, 15, INK)
    g:set(15, i, INK)
  end
end

local function dirt(n)
  local g = canvas(16, 16)
  g:rect(0, 0, 15, 15, D4)
  -- no bevel: ground tiles join into one continuous surface, edges only appear around holes
  for _ = 1, n do
    local x, y = rnd(16), rnd(16)
    local c = rnd(3) == 0 and D5 or D3
    g:set(x, y, c)
    if rnd(2) == 0 then g:set((x + 1) % 16, y, c) end
    if rnd(3) == 0 then g:set(x, (y + 1) % 16, D3) end
  end
  return g
end

-- One-pixel ink line plus an inner line on the side of a solid tile that faces a hole.
local function edge(name, inner, ink)
  local g = canvas(16, 16)
  for i = 0, 15 do
    ink(g, i)
    inner(g, i)
  end
  save(g, "Tiles/" .. name)
end
edge("edge_top", function(g, i) g:set(i, 1, D2) end, function(g, i) g:set(i, 0, INK) end)
edge("edge_bottom", function(g, i) g:set(i, 14, D6) end, function(g, i) g:set(i, 15, INK) end)
edge("edge_left", function(g, i) g:set(1, i, D5) end, function(g, i) g:set(0, i, INK) end)
edge("edge_right", function(g, i) g:set(14, i, D2) end, function(g, i) g:set(15, i, INK) end)

local dirtA, dirtB, dirtC = dirt(14), dirt(20), dirt(10)
save(dirtA, "Tiles/dirt_a")
save(dirtB, "Tiles/dirt_b")
save(dirtC, "Tiles/dirt_c")

local grass = dirt(12)
for x = 0, 15 do
  local h = 3 + (x * 7 % 3 == 0 and 1 or 0)
  for y = 0, h do
    local c = G3
    if y == 0 then c = G4 elseif y == h then c = G1 elseif y == h - 1 then c = G2 end
    grass:set(x, y, c)
  end
end
save(grass, "Tiles/grass")

local hard = dirt(8)
for y = 2, 13 do
  for x = 2, 13 do
    local dx, dy = (x - 7.5) / 6, (y - 7.5) / 5.6
    local d = dx * dx + dy * dy
    if d <= 1 then
      local c = S2
      if d > 0.72 then c = (dx + dy < 0) and S3 or K2
      elseif dx + dy < -0.5 then c = S3
      elseif dx + dy > 0.6 then c = S1 end
      hard:set(x, y, c)
    end
  end
end
hard:set(5, 8, K2) hard:set(6, 9, K2) hard:set(9, 5, S1) hard:set(10, 5, S1)
for y = 1, 14 do
  for x = 1, 14 do
    local inStone = hard:get(x, y) == S1 or hard:get(x, y) == S2 or hard:get(x, y) == S3 or hard:get(x, y) == K2
    if not inStone then
      local neighbour = false
      for _, o in ipairs({ { -1, 0 }, { 0, -1 } }) do
        local c = hard:get(x + o[1], y + o[2])
        if c == K2 or c == S1 or c == S2 or c == S3 then neighbour = true end
      end
      if neighbour then hard:set(x, y, INK) end
    end
  end
end
save(hard, "Tiles/hard_rock")

local NUGGET_LAYOUT = {
  { 2, 2, 3 }, { 9, 1, 2 }, { 11, 6, 3 }, { 4, 8, 3 }, { 9, 11, 2 }, { 1, 11, 2 },
}
local function ore(hi, mid, lo)
  local g = dirt(6)
  for _, n in ipairs(NUGGET_LAYOUT) do
    local x, y, s = n[1], n[2], n[3]
    g:rect(x, y, x + s - 1, y + s - 1, mid)
    g:set(x, y, hi)
    g:set(x + 1, y, hi)
    g:set(x, y + 1, hi)
    g:set(x + s - 1, y + s - 1, lo)
    for i = 0, s do
      g:set(x + s, y + i, INK)
      g:set(x + i, y + s, INK)
    end
    g:set(x - 1, y + 1, INK)
    g:set(x + 1, y - 1, INK)
  end
  return g
end

local ORE_COLORS = {
  copper = { hex("f6b98a"), O2, O1 },
  coal = { hex("6a6a78"), K2, K1 },
  iron = { hex("e4d6cc"), hex("b49c90"), hex("7a6660") },
  silver = { hex("f4f8ff"), hex("c4cede"), hex("8894ac") },
}
for name, c in pairs(ORE_COLORS) do
  save(ore(c[1], c[2], c[3]), "Tiles/ore_" .. name)
end

local tnt = canvas(16, 16)
local RED_HI, RED, RED_LO = hex("e0523e"), hex("b82c2c"), hex("7c1a22")
for y = 0, 15 do tnt:rect(0, y, 15, y, (y % 4 < 2) and RED or RED_LO) end
tnt:rect(0, 6, 15, 9, D7)
tnt:rect(0, 5, 15, 5, RED_LO)
tnt:rect(0, 10, 15, 10, RED_LO)
tnt:rect(7, 7, 9, 9, INK)
tnt:set(6, 8, INK) tnt:set(8, 6, INK) tnt:set(9, 6, D3) tnt:set(10, 6, O2) tnt:set(7, 7, K2)
bevel(tnt, RED_HI, RED_LO)
save(tnt, "Tiles/tnt")

local chest = dirt(8)
chest:rect(2, 5, 13, 12, O1)
chest:rect(2, 5, 13, 5, O2)
chest:rect(2, 6, 2, 12, O2)
chest:rect(13, 6, 13, 12, D3)
chest:rect(2, 12, 13, 12, D3)
chest:rect(2, 8, 13, 8, INK)
chest:rect(4, 6, 4, 12, D3)
chest:rect(11, 6, 11, 12, D3)
chest:rect(7, 7, 8, 10, GOLD)
chest:set(7, 7, GOLD_HI)
chest:rect(1, 4, 14, 4, INK)
chest:rect(1, 13, 14, 13, INK)
chest:rect(1, 4, 1, 13, INK)
chest:rect(14, 4, 14, 13, INK)
save(chest, "Tiles/chest")

local tunnel = canvas(16, 16)
tunnel:rect(0, 0, 15, 15, D1)
for _ = 1, 9 do
  local x, y = rnd(16), rnd(16)
  tunnel:set(x, y, rnd(2) == 0 and INK or D2)
end
save(tunnel, "Tiles/tunnel_bg")

-- crack overlay: each frame keeps the previous frame's cracks and adds more
local function line(pts, x0, y0, x1, y1)
  local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0))
  for i = 0, n do
    pts[#pts + 1] = { math.floor(x0 + (x1 - x0) * i / n + 0.5), math.floor(y0 + (y1 - y0) * i / n + 0.5) }
  end
end
local STAGES = {
  { { 8, 8, 6, 5 }, { 6, 5, 7, 2 }, { 7, 2, 5, 0 }, { 8, 8, 11, 9 }, { 11, 9, 13, 7 } },
  { { 8, 8, 9, 12 }, { 9, 12, 7, 15 }, { 11, 9, 12, 12 }, { 6, 5, 3, 6 } },
  { { 3, 6, 0, 8 }, { 9, 12, 12, 14 }, { 13, 7, 15, 5 }, { 7, 2, 10, 1 }, { 8, 8, 4, 11 }, { 4, 11, 2, 14 } },
}
local crack = canvas(48, 16)
local pts = {}
for stage, segs in ipairs(STAGES) do
  for _, s in ipairs(segs) do line(pts, s[1], s[2], s[3], s[4]) end
  local frame = canvas(16, 16)
  local rim = hex("dfc08f", 120)
  for _, p in ipairs(pts) do frame:set(p[1], p[2] + 1, rim) end
  for _, p in ipairs(pts) do frame:set(p[1], p[2], INK) end
  crack:blit(frame, (stage - 1) * 16, 0)
end
save(crack, "Tiles/crack_overlay")

local ICON = {
  "..xxxx..",
  ".xhhmmx.",
  "xhhmmmlx",
  "xhmmmmlx",
  "xmmmmllx",
  "xmmmlllx",
  ".xmlllx.",
  "..xxxx..",
}
for name, c in pairs(ORE_COLORS) do
  local g = canvas(8, 8)
  local map = { x = INK, h = c[1], m = c[2], l = c[3] }
  for y, row in ipairs(ICON) do
    for x = 1, 8 do
      local ch = row:sub(x, x)
      if map[ch] then g:set(x - 1, y - 1, map[ch]) end
    end
  end
  save(g, "Items/icon_" .. name)
end

local coin = canvas(8, 8)
local coinMap = { x = INK, h = GOLD_HI, m = GOLD, l = O1 }
for y, row in ipairs(ICON) do
  for x = 1, 8 do
    local c = coinMap[row:sub(x, x)]
    if c then coin:set(x - 1, y - 1, c) end
  end
end
save(coin, "Items/icon_coin")

-- drill: 32x32, 4 frames, nose down
local function drillFrame(f, frenzy)
  local g = canvas(32, 32)
  local CX = 15.5
  local dy = f % 2
  local H = frenzy
    and { hi = hex("ffa98a"), mid = hex("e8402e"), lo = hex("a01e24"), dk = hex("5a1020") }
    or { hi = hex("ffe27a"), mid = hex("f2b92e"), lo = hex("c07f1e"), dk = hex("7a4a18") }
  local GLASS = frenzy
    and { hi = hex("fff0c0"), mid = hex("ffb040"), lo = hex("c05a1a") }
    or { hi = hex("e8fbff"), mid = hex("4fb8e8"), lo = hex("1f6f9e") }
  local CONE = frenzy
    and { hi = hex("fff0d0"), mid = hex("ffb060"), lo = hex("d05a2a"), dk = hex("8a2a1e") }
    or { hi = hex("f4f8ff"), mid = hex("bcc6d6"), lo = hex("7e8ca2"), dk = hex("4a5468") }

  local function ramp(n, c)
    if n < -0.45 then return c.hi end
    if n < 0.4 then return c.mid end
    return c.lo
  end

  for _, x0 in ipairs({ 1, 27 }) do
    for y = 4, 15 do
      local bar = (y + f) % 3 == 0
      g:rect(x0, y + dy, x0 + 3, y + dy, bar and K1 or (x0 == 1 and S2 or S1))
    end
  end

  for y = 2, 16 do
    local inset = (y == 2 or y == 16) and 2 or ((y == 3 or y == 15) and 1 or 0)
    for x = 5 + inset, 26 - inset do
      local n = (x - CX) / 11 + ((y - 9) / 7) ^ 2 * 0.3
      local c = ramp(n, H)
      if y >= 14 then c = (n < 0.4) and H.lo or H.dk end
      if y == 3 and n < 0.4 then c = H.hi end
      g:set(x, y + dy, c)
    end
  end

  for y = 3, 14 do
    for x = 9, 22 do
      local dx, dyy = x - CX, y - 8.5
      local d = math.sqrt(dx * dx + dyy * dyy)
      if d <= 5.4 then
        local c
        if d > 4.3 then
          c = (dx + dyy < 0) and S3 or K2
        else
          c = (dx + dyy < -2) and GLASS.mid or GLASS.lo
          if dx + dyy < -4 then c = GLASS.hi end
        end
        g:set(x, y + dy, c)
      end
    end
  end

  for y = 17, 19 do
    for x = 7, 24 do
      local c = ramp((x - CX) / 9, { hi = S3, mid = S2, lo = S1 })
      if y == 19 then c = K2 end
      g:set(x, y, c)
    end
  end

  local top, tip = 20, 30
  for y = top, tip do
    local t = (y - top) / (tip - top)
    local hw = 8.2 * (1 - t) + 0.4
    for x = 0, 31 do
      local n = (x - CX) / hw
      if math.abs(n) <= 1 then
        local theta = math.asin(n) / math.pi * 2
        local phase = (theta * 0.9 + y / 5.5 - f / 4) % 1
        local c
        if phase < 0.34 then
          c = n < 0.4 and CONE.lo or CONE.dk
        else
          c = ramp(n, CONE)
        end
        g:set(x, y, c)
      end
    end
  end

  local out = {}
  for y = 0, 31 do
    for x = 0, 31 do
      if not g:get(x, y) then
        local near = (x > 0 and g:get(x - 1, y)) or (x < 31 and g:get(x + 1, y))
          or (y > 0 and g:get(x, y - 1)) or (y < 31 and g:get(x, y + 1))
        if near then out[#out + 1] = { x, y } end
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], INK) end
  return g
end

for _, v in ipairs({ { "drill_spin", false }, { "drill_frenzy", true } }) do
  local sheet = canvas(128, 32)
  for f = 0, 3 do sheet:blit(drillFrame(f, v[2]), f * 32, 0) end
  save(sheet, "Drill/" .. v[1])
end

-- explosion: 4 frames, 32x32, a hot core that turns into scattered smoke
local blast = canvas(128, 32)
local HOT, MID, COOL, SMOKE = hex("fff0c0"), O2, hex("b8432a"), K2
local RADII = { 7, 12, 15, 15 }
for f = 0, 3 do
  local r = RADII[f + 1]
  for y = 0, 31 do
    for x = 0, 31 do
      local d = math.sqrt((x - 15.5) ^ 2 + (y - 15.5) ^ 2)
      local speckle = (x * 13 + y * 7 + f * 5) % 5
      if d <= r and not (f >= 2 and speckle < f - 1) then
        local t = d / r
        local c
        if f == 3 then c = t < 0.6 and COOL or SMOKE
        elseif f == 2 then c = t < 0.45 and MID or (t < 0.8 and COOL or SMOKE)
        else c = t < 0.5 and HOT or (t < 0.8 and MID or COOL) end
        blast:set(x + f * 32, y, c)
      end
    end
  end
end
save(blast, "Items/explosion")

-- 16x16 rail and card icons, each with an ink outline
local function icon(name, draw)
  local g = canvas(16, 16)
  draw(g)
  local out = {}
  for y = 0, 15 do
    for x = 0, 15 do
      if not g:get(x, y) then
        local near = (x > 0 and g:get(x - 1, y)) or (x < 15 and g:get(x + 1, y))
          or (y > 0 and g:get(x, y - 1)) or (y < 15 and g:get(x, y + 1))
        if near then out[#out + 1] = { x, y } end
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], INK) end
  save(g, "Items/tab_" .. name)
end

icon("drill", function(g)
  g:rect(4, 1, 11, 5, GOLD)
  g:rect(4, 1, 11, 1, GOLD_HI)
  g:rect(4, 2, 5, 5, GOLD_HI)
  for y = 6, 14 do
    local half = math.floor((14 - y) * 0.48 + 0.5)
    for x = 8 - half - 1, 7 + half do
      g:set(x, y, x < 8 and S3 or S2)
    end
  end
  g:set(7, 8, S1) g:set(8, 9, S1) g:set(9, 7, S1) g:set(6, 10, S1)
end)

icon("blast", function(g)
  for y = 4, 14 do
    for x = 2, 12 do
      local d = math.sqrt((x - 7) ^ 2 + (y - 9) ^ 2)
      if d <= 5.2 then g:set(x, y, (x + y < 13) and S1 or K2) end
    end
  end
  g:set(5, 7, S3) g:set(5, 6, S3) g:set(6, 6, S3)
  g:rect(9, 3, 10, 3, D6) g:set(11, 2, D6) g:set(12, 1, GOLD_HI) g:set(13, 2, O2) g:set(12, 3, O2)
end)

icon("frenzy", function(g)
  for y = 1, 14 do
    local t = (y - 1) / 13
    local half = math.floor(6.5 * math.sin(t ^ 1.6 * math.pi * 0.72) + 0.5)
    for x = 7 - half, 8 + half - 1 do g:set(x + (y < 6 and 1 or 0), y, hex("e8402e")) end
  end
  for y = 6, 14 do
    local half = math.floor(3.5 * math.sin(((y - 6) / 8) ^ 1.4 * math.pi * 0.75) + 0.5)
    for x = 7 - half, 8 + half - 1 do g:set(x, y, GOLD) end
  end
  g:rect(7, 11, 8, 13, GOLD_HI)
end)

icon("stats", function(g)
  g:rect(2, 9, 4, 14, G3)
  g:rect(7, 5, 9, 14, GOLD)
  g:rect(12, 2, 14, 14, O2)
  g:rect(2, 9, 2, 14, G4)
  g:rect(7, 5, 7, 14, GOLD_HI)
end)

icon("settings", function(g)
  for y = 1, 14 do
    for x = 1, 14 do
      local dx, dy = x - 7.5, y - 7.5
      local d = math.sqrt(dx * dx + dy * dy)
      local tooth = math.cos(math.atan(dy, dx) * 8) > 0
      if d > 2.6 and (d <= 4.8 or (d <= 6.8 and tooth)) then
        g:set(x, y, (dx + dy < 0) and S3 or S2)
      end
    end
  end
end)

local white = canvas(1, 1)
white:set(0, 0, hex("ffffff"))
save(white, "Items/pixel")
print("done")
