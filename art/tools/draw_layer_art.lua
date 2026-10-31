-- Layer dressing: 4 decorations and 3 rock textures for each of the 12 layers, 16x16, into
-- Assets/Art/Layers as decal_layer_<layer>_<n> and rock_layer_<layer>_<n> (layer counts from 0).
-- Rock textures tile seamlessly and use the same ramp levels as PixelPainter's ground.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_layer_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local INK, WHITE = hex("1a1210"), hex("ffffff")

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c)
    x, y = math.floor(x), math.floor(y)
    if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end
  end
  -- wraps round the edges, for textures that must tile
  function g:wrap(x, y, c) self.px[(math.floor(y) % h) * w + math.floor(x) % w] = c end
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
    local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0))
    for i = 0, n do
      local t = n == 0 and 0 or i / n
      self:set(x0 + (x1 - x0) * t + 0.5, y0 + (y1 - y0) * t + 0.5, c)
    end
  end
  return g
end

local function tri(g, ax, ay, bx, by, cx, cy, c)
  local function side(px, py, qx, qy, x, y) return (qx - px) * (y - py) - (qy - py) * (x - px) end
  for y = math.floor(math.min(ay, by, cy)), math.ceil(math.max(ay, by, cy)) do
    for x = math.floor(math.min(ax, bx, cx)), math.ceil(math.max(ax, bx, cx)) do
      local d1, d2, d3 = side(ax, ay, bx, by, x, y), side(bx, by, cx, cy, x, y), side(cx, cy, ax, ay, x, y)
      if not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0)) then g:set(x, y, c) end
    end
  end
end

-- a round body lit from the upper left
local function ball(g, cx, cy, rx, ry, lo, mid, hi)
  for y = math.floor(cy - ry), math.ceil(cy + ry) do
    for x = math.floor(cx - rx), math.ceil(cx + rx) do
      local nx, ny = (x - cx) / rx, (y - cy) / ry
      if nx * nx + ny * ny <= 1 then g:set(x, y, nx + ny < -0.55 and hi or (nx + ny > 0.55 and lo or mid)) end
    end
  end
end

local function outline(g, ink)
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) and (g:get(x - 1, y) or g:get(x + 1, y) or g:get(x, y - 1) or g:get(x, y + 1)) then
        out[#out + 1] = { x, y }
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

local function roll(x, y, salt)
  local n = (x * 73856093 ~ y * 19349663 ~ salt * 83492791) & 0x7fffffff
  return (n % 1000) / 1000
end

-- layer ramps, the same formula and table as draw_world_art.lua and Palette.cs
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
local RAMPS = {}
for i, v in ipairs(LAYER_HUES) do
  local r = {}
  for k = 1, 6 do
    local h = towards(towards(v[1], 250, RAMP_DARK[k]), 55, RAMP_WARM[k])
    r[k] = hex(hsv(h, RAMP_S[k] * v[2], RAMP_V[k]))
  end
  RAMPS[i] = r
end

---------------------------------------------------------------------------------------------------
-- Rock textures. Every layer gets the same three kinds so the ground reads the same way all the
-- way down: clumped specks, wavy strata and cracked plates. The layer adds its own glint.
---------------------------------------------------------------------------------------------------
-- a few bright pixels that belong to the layer: none, quartz, crystal, water, gold, mica, lava...
local GLINTS = {
  nil, hex("f8e8c0"), hex("c8a0ff"), hex("7ac8f0"), hex("ffd24a"), hex("f0f0f8"),
  hex("ff6a2a"), hex("b8e86a"), hex("7ae8d8"), hex("ffa040"), hex("ffe27a"), hex("ffffff"),
}
-- layers whose cracks glow from the heat below
local HOT = { [7] = hex("e0401e"), [8] = hex("e07a2a"), [10] = hex("ff6a2a"), [11] = hex("ffb040"), [12] = hex("fff0b0") }

local function rock(layer, kind)
  local r = RAMPS[layer]
  local g = canvas(16, 16)
  for y = 0, 15 do for x = 0, 15 do g.px[y * 16 + x] = r[4] end end
  local salt = layer * 10 + kind
  if kind == 0 then
    -- clumps of two by two specks, like the painted ground but heavier
    for y = 0, 15, 2 do
      for x = 0, 15, 2 do
        local v = roll(x, y, salt)
        local c = v < 0.16 and r[5] or (v < 0.34 and r[3] or nil)
        if c then g:wrap(x, y, c); g:wrap(x + 1, y, c); g:wrap(x, y + 1, c) end
      end
    end
  elseif kind == 1 then
    -- two wavy bands that repeat every 16 pixels
    for x = 0, 15 do
      local a = math.floor(4 + 1.5 * math.sin(x * math.pi / 8 + layer) + 0.5)
      local b = math.floor(11 + 1.5 * math.sin(x * math.pi / 8 + layer + 2) + 0.5)
      g:wrap(x, a, r[3]); g:wrap(x, a + 1, r[3]); g:wrap(x, a - 1, r[5])
      g:wrap(x, b, r[2]); g:wrap(x, b - 1, r[5])
      if roll(x, 7, salt) < 0.3 then g:wrap(x, 7 + (x % 2), r[3]) end
    end
  else
    -- plates split by dark cracks with a lit lower edge; hot layers glow in the cracks
    local crack = HOT[layer] or r[2]
    local function seam(points)
      for i = 1, #points - 1 do
        local a, b = points[i], points[i + 1]
        local n = math.max(math.abs(b[1] - a[1]), math.abs(b[2] - a[2]))
        for s = 0, n do
          local x = a[1] + (b[1] - a[1]) * s / n + 0.5
          local y = a[2] + (b[2] - a[2]) * s / n + 0.5
          g:wrap(x, y, crack); g:wrap(x, y + 1, r[5])
        end
      end
    end
    local o = layer % 4
    seam({ { 0, 3 + o }, { 5, 5 + o }, { 9, 2 + o }, { 16, 3 + o } })
    seam({ { 5, 5 + o }, { 7, 11 }, { 3, 16 } })
    seam({ { 9, 2 + o }, { 12, 9 }, { 16, 10 } })
    for y = 0, 15, 3 do
      for x = 1, 15, 4 do if roll(x, y, salt) < 0.25 then g:wrap(x, y, r[3]) end end
    end
  end
  local glint = GLINTS[layer]
  if glint then
    for i = 0, 2 do g:wrap(2 + i * 5 + kind * 3, 2 + ((i * 7 + kind * 5 + layer) % 13), glint) end
  end
  return g
end

---------------------------------------------------------------------------------------------------
-- Decorations: small things half buried in the ground, transparent around, with an ink outline.
-- Shared shapes first, then four picks per layer.
---------------------------------------------------------------------------------------------------
local function pebbles(g, lo, mid, hi)
  ball(g, 5, 10, 3, 2.5, lo, mid, hi)
  ball(g, 10.5, 11, 2.5, 2, lo, mid, hi)
  ball(g, 8, 6.5, 2, 1.8, lo, mid, hi)
end

local function bone(g, c, shade)
  g:line(4, 11, 11, 5, c); g:line(4, 12, 12, 5, c)
  for _, e in ipairs({ { 3, 11 }, { 4, 13 }, { 11, 4 }, { 13, 5 } }) do g:ellipse(e[1], e[2], 1.2, 1.2, c) end
  g:line(5, 12, 12, 6, shade)
end

local function crystals(g, lo, mid, hi)
  local function prism(x, w, top, base)
    local mx = x + w / 2
    g:rect(x, top + 2, x + w, base, mid)
    tri(g, x, top + 2, x + w, top + 2, mx, top, mid)
    g:rect(x, top + 2, math.floor(mx), base, hi)
    tri(g, x, top + 2, mx, top + 2, mx, top, hi)
    g:set(x + w, base, lo)
  end
  prism(3, 3, 6, 13); prism(7, 3, 2, 13); prism(11, 2, 7, 13)
end

local function bubble(g, lo, mid, hi)
  ball(g, 8, 9, 5, 4.5, lo, mid, hi)
  g:ellipse(6, 7, 1.5, 1, WHITE)
  ball(g, 13, 4, 1.5, 1.5, lo, mid, hi)
end

local function vein(g, c, shine)
  local pts = { { 1, 12 }, { 5, 9 }, { 8, 10 }, { 11, 6 }, { 14, 4 } }
  for i = 1, #pts - 1 do
    g:line(pts[i][1], pts[i][2], pts[i + 1][1], pts[i + 1][2], c)
    g:line(pts[i][1], pts[i][2] + 1, pts[i + 1][1], pts[i + 1][2] + 1, c)
  end
  g:set(8, 9, shine); g:set(12, 5, shine)
end

local function sprout(g, stem, leaf, leafHi)
  g:line(8, 14, 8, 7, stem); g:line(8, 10, 5, 8, stem)
  g:ellipse(10, 6, 2.5, 1.5, leaf); g:ellipse(4, 7, 2, 1.2, leaf)
  g:set(9, 5, leafHi); g:set(3, 6, leafHi)
end

local function shell(g, lo, mid, hi)
  g:ellipse(8, 9, 5.5, 4.5, mid)
  for i = 0, 4 do g:line(8, 13, 3 + i * 2.5, 5 + math.abs(i - 2), lo) end
  g:ellipse(6, 7, 1.5, 1, hi)
  g:rect(6, 13, 10, 14, lo)
end

local function skull(g, c, shade)
  g:ellipse(8, 7, 5, 4.5, c)
  g:rect(5, 10, 11, 13, c)
  g:ellipse(6, 7, 1.3, 1.5, INK); g:ellipse(10, 7, 1.3, 1.5, INK)
  g:set(8, 9, shade); g:set(6, 12, shade); g:set(8, 12, shade); g:set(10, 12, shade)
end

local function pillar(g, lo, mid, hi)
  g:rect(4, 2, 11, 3, hi); g:rect(5, 4, 10, 13, mid); g:rect(3, 13, 12, 14, lo)
  for x = 6, 9, 2 do g:rect(x, 5, x, 12, lo) end
  g:rect(5, 4, 5, 12, hi)
  tri(g, 10, 4, 11, 4, 11, 7, nil)
end

local function pot(g, lo, mid, hi, band)
  g:ellipse(8, 9.5, 5, 4.5, mid)
  g:rect(6, 3, 10, 5, mid); g:rect(5, 2, 11, 2, hi)
  g:rect(3, 9, 13, 9, band)
  g:ellipse(6, 8, 1.5, 2, hi)
  g:line(10, 6, 12, 12, lo)
end

local function coin(g)
  ball(g, 6, 9, 3.5, 3.5, hex("a8701a"), hex("e8b02a"), hex("ffe27a"))
  ball(g, 11, 11, 2.5, 2.5, hex("a8701a"), hex("e8b02a"), hex("ffe27a"))
  g:rect(6, 8, 6, 10, hex("a8701a"))
end

local function shard(g, lo, mid, hi)
  tri(g, 3, 13, 9, 2, 13, 12, mid)
  tri(g, 3, 13, 9, 2, 8, 13, hi)
  g:line(9, 2, 10, 12, lo)
end

local function ember(g, hot, glow, dark)
  g:ellipse(8, 9, 4, 3.5, dark)
  g:ellipse(8, 9, 2.5, 2, glow)
  g:set(8, 9, hot); g:set(7, 8, hot)
  for _, p in ipairs({ { 3, 4 }, { 12, 3 }, { 13, 7 }, { 5, 2 } }) do g:set(p[1], p[2], glow) end
end

local function droplets(g, lo, mid, hi)
  for _, d in ipairs({ { 5, 9, 3 }, { 11, 7, 2.5 }, { 9, 13, 1.5 }, { 13, 12, 1.2 } }) do
    ball(g, d[1], d[2], d[3], d[3], lo, mid, hi)
  end
end

local function fishbones(g, c)
  g:line(2, 8, 12, 8, c)
  for x = 4, 10, 2 do g:line(x, 5, x + 1, 8, c); g:line(x, 11, x + 1, 8, c) end
  tri(g, 12, 8, 15, 5, 15, 11, c)
  g:ellipse(2, 8, 1.5, 1.5, c); g:set(2, 8, INK)
end

local function mushroom(g, cap, capHi, stem, glow)
  g:rect(7, 9, 8, 14, stem)
  g:ellipse(7.5, 8, 5, 3, cap)
  g:rect(3, 8, 12, 8, cap)
  g:set(5, 6, capHi); g:set(9, 6, capHi); g:set(7, 7, glow); g:set(11, 7, glow)
  g:set(3, 3, glow); g:set(12, 2, glow)
end

local function weed(g, dark, mid, hi)
  for i, x in ipairs({ 4, 7, 10, 12 }) do
    local top = 3 + (i % 3) * 2
    for y = top, 14 do g:set(x + math.floor(math.sin(y * 0.6 + i) + 0.5), y, y < top + 3 and hi or mid) end
  end
  g:rect(3, 14, 13, 14, dark)
end

local function cube(g, lo, mid, hi)
  g:rect(3, 7, 9, 13, mid)
  for y = 4, 6 do g:rect(3 + (7 - y), y, 9 + (7 - y), y, hi) end
  for x = 10, 12 do g:rect(x, 7 - (x - 9), x, 13 - (x - 9), lo) end
end

local function spark(g, c, core)
  g:line(8, 2, 8, 14, c); g:line(2, 8, 14, 8, c)
  g:line(5, 5, 11, 11, c); g:line(11, 5, 5, 11, c)
  g:ellipse(8, 8, 1.5, 1.5, core)
end

local function lattice(g, c, node)
  for i = 3, 13, 5 do g:line(i, 3, i, 13, c); g:line(3, i, 13, i, c) end
  for x = 3, 13, 5 do for y = 3, 13, 5 do g:ellipse(x, y, 1, 1, node) end end
end

local function vent(g, rockC, steam, steamHi)
  g:ellipse(8, 13, 5, 2, rockC); g:ellipse(8, 12.5, 2, 1, INK)
  for i, p in ipairs({ { 8, 9 }, { 7, 6 }, { 9, 3 } }) do g:ellipse(p[1], p[2], 2 - i * 0.3, 1.5, i == 1 and steamHi or steam) end
end

local function swirl(g, c, hi)
  for i = 0, 40 do
    local t = i / 40 * 2.6 * math.pi
    local r = 1 + t * 0.8
    g:set(8 + math.cos(t) * r, 8 + math.sin(t) * r, i > 30 and hi or c)
    g:set(8 + math.cos(t) * r + 1, 8 + math.sin(t) * r, c)
  end
end

local function hexcrystal(g, lo, mid, hi)
  g:rect(5, 4, 10, 12, mid)
  tri(g, 5, 4, 10, 4, 7.5, 1, hi); tri(g, 5, 12, 10, 12, 7.5, 15, lo)
  g:rect(5, 4, 7, 12, hi); g:rect(10, 4, 10, 12, lo)
end

local function c3(a, b, c) return hex(a), hex(b), hex(c) end

-- four per layer, each a function of the layer ramp r
local DECALS = {
  { -- 表土
    function(g, r) pebbles(g, r[2], r[4], r[6]) end,
    function(g) bone(g, hex("e8dcc8"), hex("a89878")) end,
    function(g) sprout(g, hex("6b4630"), hex("76a83a"), hex("a8d45a")) end,
    function(g) local lo, mid, hi = c3("8a3a3a", "c86a6a", "f0a0a0")
      for i = 0, 10 do g:set(3 + i, 9 + math.floor(math.sin(i * 0.9) * 2 + 0.5), mid) end
      g:set(13, 8, hi); g:set(3, 9, lo) end,
  },
  { -- 砂岩
    function(g, r) shell(g, r[3], r[5], r[6]) end,
    function(g) skull(g, hex("f0e8d0"), hex("a89878")) end,
    function(g, r) for y = 4, 12, 4 do for x = 2, 13 do g:set(x, y + math.floor(math.sin(x * 0.8) + 0.5), y == 8 and r[6] or r[5]) end end end,
    function(g, r) pebbles(g, r[3], r[5], r[6]) end,
  },
  { -- 石灰岩溶洞
    function(g) crystals(g, c3("4a2a8a", "8a5ad8", "d0b0ff")) end,
    function(g, r) tri(g, 4, 14, 12, 14, 8, 2, r[5]); tri(g, 4, 14, 8, 14, 8, 2, r[6]); g:set(8, 3, hex("7ae8f0")) end,
    function(g) mushroom(g, hex("4a8ab8"), hex("8ac8f0"), hex("c8d8e0"), hex("7ae8f0")) end,
    function(g, r) local wing, body = r[2], r[3]
      g:ellipse(8, 8, 2, 2.5, body); tri(g, 7, 7, 1, 4, 3, 10, wing); tri(g, 9, 7, 15, 4, 13, 10, wing)
      g:set(7, 7, hex("ff6a6a")); g:set(9, 7, hex("ff6a6a")) end,
  },
  { -- 地下暗河
    function(g) bubble(g, c3("1e4a90", "3a8ad8", "a8d8f8")) end,
    function(g) fishbones(g, hex("d8e4ea")) end,
    function(g) weed(g, hex("1e4a3a"), hex("3a8a5a"), hex("8ad8a0")) end,
    function(g) shell(g, c3("8a6a88", "d0b0c8", "fae8f4")) end,
  },
  { -- 远古遗迹
    function(g, r) pillar(g, r[3], r[5], r[6]) end,
    function(g) pot(g, hex("6a3a18"), hex("b8693a"), hex("e8a070"), hex("2a4a9a")) end,
    function(g, r) g:rect(2, 4, 13, 13, r[5]); g:rect(2, 4, 13, 4, r[6]); g:rect(2, 13, 13, 13, r[3])
      for _, p in ipairs({ { 5, 7 }, { 8, 7 }, { 11, 7 }, { 6, 10 }, { 10, 10 } }) do g:set(p[1], p[2], hex("ffd24a")) end
      g:line(4, 6, 6, 12, r[3]) end,
    function(g) coin(g) end,
  },
  { -- 花岗岩地壳
    function(g) vein(g, hex("f0ece8"), WHITE) end,
    function(g) cube(g, c3("a86a5a", "e0a090", "f8d0c8")) end,
    function(g) for _, p in ipairs({ { 4, 5 }, { 10, 4 }, { 7, 9 }, { 12, 11 }, { 3, 12 } }) do
        g:rect(p[1], p[2], p[1] + 1, p[2], hex("e8e8f0")); g:set(p[1], p[2] + 1, hex("a8a8b8")) end end,
    function(g, r) ball(g, 8, 9, 6, 5, r[3], r[5], r[6])
      for _, p in ipairs({ { 6, 8 }, { 9, 10 }, { 11, 7 }, { 5, 11 } }) do g:set(p[1], p[2], INK) end end,
  },
  { -- 莫霍面
    function(g) vein(g, hex("e0401e"), hex("ffe27a")) end,
    function(g) shard(g, c3("0e0c14", "2a2838", "6a6a88")) end,
    function(g) ember(g, hex("ffe27a"), hex("ff6a2a"), hex("4a1a10")) end,
    function(g) ball(g, 8, 9, 6, 4.5, hex("1e1a22"), hex("3a3440"), hex("5a5468"))
      g:line(3, 9, 7, 8, hex("e0401e")); g:line(7, 8, 9, 11, hex("e0401e")); g:line(9, 11, 13, 9, hex("e0401e"))
      g:set(7, 8, hex("ffe27a")) end,
  },
  { -- 上地幔
    function(g) crystals(g, c3("4a7a10", "8ab82a", "d0f070")) end,
    function(g) bubble(g, c3("8a2a10", "f07a20", "ffc050")) end,
    function(g) droplets(g, c3("4a7a10", "8ab82a", "d0f070")) end,
    function(g, r) vent(g, r[3], hex("d8d0c8", 200), hex("ffffff", 220)) end,
  },
  { -- 地幔过渡带
    function(g) hexcrystal(g, c3("1e3a98", "3a5ad8", "a0b8ff")) end,
    function(g) droplets(g, c3("2a8ab0", "5ad8e8", "c8f8ff")) end,
    function(g) spark(g, hex("7ae8d8", 200), WHITE) end,
    function(g) crystals(g, c3("1e6a60", "4ab8a8", "a8f0e0")) end,
  },
  { -- 下地幔
    function(g) bubble(g, c3("a82a10", "ff6a2a", "ffc060")) end,
    function(g) ember(g, WHITE, hex("ffa040"), hex("6a1a10")) end,
    function(g) cube(g, c3("6a3a18", "a86a3a", "e0a870")) end,
    function(g) vein(g, hex("ff6a2a"), hex("ffe27a")) end,
  },
  { -- 外核
    function(g) droplets(g, c3("4a4a5a", "a8a8b8", "f0f0ff")) end,
    function(g) spark(g, hex("8ae8ff"), WHITE) end,
    function(g) bubble(g, c3("a8701a", "e8b02a", "ffe27a")) end,
    function(g) swirl(g, hex("8a8a98"), hex("f0f0ff")) end,
  },
  { -- 内核
    function(g) hexcrystal(g, c3("c8a040", "fff0b0", "ffffff")) end,
    function(g) spark(g, hex("ffe27a"), WHITE) end,
    function(g) lattice(g, hex("b4b4c2"), hex("f0f0ff")) end,
    function(g) ball(g, 8, 8, 5, 5, hex("e8a030"), hex("ffe8a0"), WHITE); g:ellipse(8, 8, 2, 2, WHITE) end,
  },
}

local rocks, decals = {}, {}
for layer = 1, 12 do
  rocks[layer], decals[layer] = {}, {}
  for kind = 0, 2 do
    local g = rock(layer, kind)
    save(g, "Layers/rock_layer_" .. (layer - 1) .. "_" .. kind)
    rocks[layer][kind] = g
  end
  for n = 1, 4 do
    local g = canvas(16, 16)
    DECALS[layer][n](g, RAMPS[layer])
    outline(g, INK)
    save(g, "Layers/decal_layer_" .. (layer - 1) .. "_" .. (n - 1))
    decals[layer][n] = g
  end
end

-- preview: one row per layer, the three rock textures tiled 2x2, then each decoration on rock 0
if previewDir then
  local k = 4
  local rowW = (32 * 3 + 16 * 4 + 7 * 2) * k
  local img = Image(rowW + 4 * k, 12 * (32 + 2) * k + 2 * k, ColorMode.RGB)
  img:clear(hex("2a2630"))
  local function put(px, py, c)
    for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel(px * k + a, py * k + b, c) end end
  end
  for layer = 1, 12 do
    local oy = 2 + (layer - 1) * 34
    for kind = 0, 2 do
      local ox = 2 + kind * 34
      for y = 0, 31 do for x = 0, 31 do put(ox + x, oy + y, rocks[layer][kind]:get(x % 16, y % 16)) end end
    end
    for n = 1, 4 do
      local ox = 2 + 3 * 34 + (n - 1) * 18
      for y = 0, 15 do
        for x = 0, 15 do
          local c = decals[layer][n]:get(x, y)
          if not c or pc.rgbaA(c) == 0 then c = rocks[layer][0]:get(x, y) end
          put(ox + x, oy + 8 + y, c)
        end
      end
    end
  end
  img:saveAs(previewDir .. "/layer_art_preview.png")
end
