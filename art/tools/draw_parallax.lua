-- Parallax backgrounds for the 12 layers, three depths each, 64x64 and seamless on both axes,
-- into Assets/Art/Layers as parallax_<layer>_far, _mid and _near (layer counts from 0).
-- Far is soft rock in the darkest ramp levels, mid carries the layer's own feature, near is
-- dark hanging silhouettes. Everything stays below ramp level 4 so the ground reads in front.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_parallax.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local N = 64

local function canvas()
  local g = { px = {} }
  -- every write wraps, so anything drawn near an edge continues on the other side
  function g:set(x, y, c) self.px[(math.floor(y + 0.5) % N) * N + math.floor(x + 0.5) % N] = c end
  function g:get(x, y) return self.px[(math.floor(y) % N) * N + math.floor(x) % N] end
  function g:fill(c) for k = 0, N * N - 1 do self.px[k] = c end end
  function g:disc(cx, cy, r, c)
    for y = -r, r do for x = -r, r do if x * x + y * y <= r * r then self:set(cx + x, cy + y, c) end end end
  end
  function g:line(x0, y0, x1, y1, c)
    local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0), 1)
    for i = 0, n do self:set(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n, c) end
  end
  return g
end

local seed = 4242
local function rnd(n)
  seed = (seed * 1103515245 + 12345) % 2147483648
  return (seed >> 16) % n
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
  -- r[0]: darker than the ramp, for near silhouettes
  local c = r[1]
  r[0] = pc.rgba(math.floor(pc.rgbaR(c) * 0.55), math.floor(pc.rgbaG(c) * 0.55), math.floor(pc.rgbaB(c) * 0.55))
  RAMPS[i] = r
end

-- a dim glow colour per layer for the few lit things in the mid ground
local GLOWS = {
  nil, nil, hex("8a5ad8"), hex("3a8ad8"), hex("c8a040"), nil,
  hex("c83a20"), hex("8ab82a"), hex("3ab8a8"), hex("e0501e"), hex("e8a030"), hex("e8e0f8"),
}

---------------------------------------------------------------------------------------------------
-- far: the same soft rock everywhere, lumps of the second level dithered into the first
---------------------------------------------------------------------------------------------------
local function far(r, layer)
  local g = canvas()
  g:fill(r[1])
  for _ = 1, 22 do
    local cx, cy, rad = rnd(N), rnd(N), 3 + rnd(6)
    for y = -rad, rad do
      for x = -rad, rad do
        local d = (x * x + y * y) / (rad * rad)
        if d <= 1 and (d < 0.6 or (x + y) % 2 == 0) then g:set(cx + x, cy + y, r[2]) end
      end
    end
  end
  local glow = GLOWS[layer]
  if glow then for _ = 1, 6 do g:set(rnd(N), rnd(N), r[3]) end end
  return g
end

---------------------------------------------------------------------------------------------------
-- mid: each layer's own feature on a transparent tile
---------------------------------------------------------------------------------------------------
local function spike(g, cx, base, tall, width, up, c, edge)
  for s = 0, tall do
    local half = math.floor(width * (1 - s / tall) + 0.5)
    local y = up and (base - s) or (base + s)
    for x = cx - half, cx + half do g:set(x, y, (x == cx - half and edge) or c) end
  end
end

local function roots(g, c, hi, hang)
  for n = 0, 4 do
    local x = 6 + n * 13 + rnd(4)
    local len = 18 + rnd(hang and 18 or 30)
    for y = 0, len do
      local wob = math.floor(math.sin(y * 0.25 + n) * 2 + 0.5)
      g:set(x + wob, y, c); g:set(x + wob + 1, y, c)
      if y % 9 == 4 then g:line(x + wob, y, x + wob + (n % 2 == 0 and 4 or -4), y + 4, c) end
    end
    g:set(x, 1, hi)
  end
end

local MID = {
  -- 表土: roots hanging from above, a few stones bedded below
  function(g, r) roots(g, r[2], r[3]); for _ = 1, 6 do g:disc(rnd(N), 48 + rnd(14), 2, r[2]) end end,
  -- 砂岩: wavy strata with shell fossils caught in them
  function(g, r)
    for band = 0, 3 do
      for x = 0, N - 1 do
        local y = band * 16 + 6 + math.floor(2 * math.sin(x * math.pi / 16 + band) + 0.5)
        g:set(x, y, r[2]); g:set(x, y + 1, r[2]); g:set(x, y + 2, band % 2 == 0 and r[3] or r[2])
      end
    end
    for _ = 1, 4 do local x, y = rnd(N), rnd(N); g:disc(x, y, 2, r[3]); g:set(x, y, r[2]) end
  end,
  -- 溶洞: stalactites and stalagmites with crystal glints
  function(g, r, glow)
    for n = 0, 3 do
      spike(g, 8 + n * 16 + rnd(4), 0, 12 + rnd(14), 3, false, r[2], r[3])
      spike(g, 16 + n * 16 + rnd(4), 63, 10 + rnd(16), 3, true, r[2], r[3])
    end
    for _ = 1, 8 do g:set(rnd(N), rnd(N), glow) end
  end,
  -- 暗河: still water lines catching light, drips falling into them
  function(g, r, glow)
    for band = 0, 3 do
      local y = 10 + band * 15
      for x = 0, N - 1 do if (x + band * 7) % 11 < 8 then g:set(x, y, x % 3 == 0 and glow or r[2]) end end
    end
    for n = 0, 4 do local x = 5 + n * 13; local base = rnd(N); for y = 0, 5 do g:set(x, base + y, r[3]) end; g:set(x, base + 7, glow) end
  end,
  -- 远古遗迹: broken pillars and a fallen arch
  function(g, r, glow)
    for n = 0, 2 do
      local x, h = 6 + n * 22, 20 + rnd(24)
      for y = 63 - h, 63 do for dx = 0, 5 do g:set(x + dx, y, (dx == 0 or y % 8 == 0) and r[3] or r[2]) end end
      for dx = -1, 6 do g:set(x + dx, 63 - h, r[3]) end
    end
    for a = 0, 30 do
      local t = a / 30 * math.pi
      g:set(40 + math.cos(t) * 12, 30 - math.sin(t) * 10, r[2]); g:set(40 + math.cos(t) * 12, 31 - math.sin(t) * 10, r[2])
    end
    g:set(40, 20, glow)
  end,
  -- 花岗岩: stacked blocky boulders
  function(g, r)
    for _ = 1, 9 do
      local x, y, w, h = rnd(N), rnd(N), 6 + rnd(8), 5 + rnd(6)
      for dy = 0, h do for dx = 0, w do g:set(x + dx, y + dy, (dy == 0 or dx == 0) and r[3] or r[2]) end end
    end
  end,
  -- 莫霍面: dark rock split by faintly glowing lava veins
  function(g, r, glow)
    for n = 0, 3 do
      local x, y = rnd(N), 0
      while y < N do
        local nx, ny = x + rnd(9) - 4, y + 4 + rnd(4)
        g:line(x, y, nx, ny, glow); g:line(x + 1, y, nx + 1, ny, r[2])
        x, y = nx, ny
      end
    end
  end,
  -- 上地幔: olive crystal clusters growing up from below
  function(g, r, glow)
    for n = 0, 4 do
      local x = 4 + n * 13
      for k = 0, 2 do spike(g, x + k * 3, 63, 8 + rnd(14), 1, true, k == 1 and r[3] or r[2]) end
      g:set(x + 3, 63 - 12, glow)
    end
  end,
  -- 地幔过渡带: a faint honeycomb of crystal lattice
  function(g, r, glow)
    for row = 0, 4 do
      for col = 0, 4 do
        local cx, cy = col * 16 + (row % 2) * 8, row * 14
        for a = 0, 5 do
          local t0, t1 = a * math.pi / 3, (a + 1) * math.pi / 3
          g:line(cx + math.cos(t0) * 6, cy + math.sin(t0) * 6, cx + math.cos(t1) * 6, cy + math.sin(t1) * 6, r[2])
        end
        if (row + col) % 3 == 0 then g:set(cx, cy, glow) end
      end
    end
  end,
  -- 下地幔: slow magma flows winding across
  function(g, r, glow)
    for band = 0, 2 do
      for x = 0, N - 1 do
        local y = band * 21 + 8 + math.floor(4 * math.sin(x * math.pi / 32 + band * 2) + 0.5)
        g:set(x, y, r[2]); g:set(x, y + 1, (x + band) % 6 == 0 and glow or r[3]); g:set(x, y + 2, r[2])
      end
    end
  end,
  -- 外核: liquid metal swirling in eddies
  function(g, r, glow)
    for n = 0, 3 do
      local cx, cy = 16 + (n % 2) * 32, 16 + (n // 2) * 32
      for i = 0, 70 do
        local t = i / 70 * 3 * math.pi
        local rad = 2 + t * 1.2
        g:set(cx + math.cos(t + n) * rad, cy + math.sin(t + n) * rad, i > 60 and glow or r[2])
      end
    end
  end,
  -- 内核: a bright crystal lattice of iron, the lit joints standing out
  function(g, r, glow)
    for i = 0, 3 do
      for k = 0, N - 1 do
        g:set(i * 16 + 4, k, r[2]); g:set(k, i * 16 + 4, r[2])
        if k % 16 == 4 then g:disc(i * 16 + 4, k, 1, r[3]); g:set(i * 16 + 4, k, glow) end
      end
    end
    for k = 0, N - 1 do g:set(k, k, r[2]) end
  end,
}

---------------------------------------------------------------------------------------------------
-- near: dark things hanging into view from above, the shape suits the layer
---------------------------------------------------------------------------------------------------
local NEAR_KIND = { "roots", "spike", "spike", "drip", "chain", "spike", "spike", "spike", "crystal", "spike", "drip", "crystal" }

local function near(r, layer)
  local g = canvas()
  local c, edge = r[0], r[1]
  local kind = NEAR_KIND[layer]
  if kind == "roots" then
    roots(g, c, edge, true)
  elseif kind == "chain" then
    for n = 0, 2 do
      local x = 10 + n * 22
      for y = 0, 20 + n * 8, 4 do g:disc(x, y, 1, c); g:set(x, y, nil) end
    end
    for n = 0, 3 do spike(g, 4 + n * 16 + rnd(5), 0, 8 + rnd(10), 2, false, c, edge) end
  else
    for n = 0, 3 do
      local x, tall = 6 + n * 16 + rnd(6), 12 + rnd(18)
      local width = kind == "crystal" and 2 or 3
      spike(g, x, 0, tall, width, false, c, edge)
      if kind == "drip" then g:set(x, tall + 3 + rnd(6), r[2]) end
      if kind == "crystal" then g:set(x, tall - 2, r[2]) end
    end
  end
  return g
end

local function save(g, name)
  local img = Image(N, N, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % N, k // N, c) end
  img:saveAs(dir .. "/Layers/" .. name .. ".png")
end

local tiles = {}
for layer = 1, 12 do
  local r = RAMPS[layer]
  local glow = GLOWS[layer] or r[3]
  local m = canvas()
  MID[layer](m, r, glow)
  local set = { far(r, layer), m, near(r, layer) }
  for i, name in ipairs({ "far", "mid", "near" }) do save(set[i], "parallax_" .. (layer - 1) .. "_" .. name) end
  tiles[layer] = set
end

-- preview: each layer as the game stacks it, the three depths over each other, tiled 3x2 at 2x,
-- with a strip of the layer's ground along the bottom for contrast
if previewDir then
  local k, cols = 2, 4
  local W, H = N * 3, N * 2
  local img = Image((W * cols + 4 * (cols + 1)) * k, ((H + 8) * 3 + 4 * 4) * k, ColorMode.RGB)
  img:clear(hex("2a2630"))
  for layer = 1, 12 do
    local ox = 4 + ((layer - 1) % cols) * (W + 4)
    local oy = 4 + ((layer - 1) // cols) * (H + 12)
    for y = 0, H + 7 do
      for x = 0, W - 1 do
        local c
        if y >= H then
          c = RAMPS[layer][((x * 7 + y * 13) % 11 == 0) and 5 or 4]
        else
          for _, t in ipairs(tiles[layer]) do
            local p = t:get(x, y)
            if p then c = p end
          end
        end
        for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel((ox + x) * k + a, (oy + y) * k + b, c) end end
      end
    end
  end
  img:saveAs(previewDir .. "/parallax_preview.png")
end
