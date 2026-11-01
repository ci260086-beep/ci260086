-- Preview sheet for the shared palette (art 18): every material ramp, and the same two objects
-- drawn with a full ink outline and with the selective outline. Writes art/palette_preview.png.
-- Run: Aseprite.exe -b --script-param preview=<dir> --script art/tools/draw_palette_preview.lua
local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
local previewDir = app.params["preview"]
assert(previewDir, "pass --script-param preview=<dir>")

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
  return g
end

-- a lit ball of a ramp, light from the upper left like everything in the game
local function ball(ramp, r)
  local g = canvas(r * 2 + 4, r * 2 + 4)
  local c = r + 1.5
  for y = 0, g.h - 1 do for x = 0, g.w - 1 do
    local nx, ny = (x - c) / r, (y - c) / r
    local d = nx * nx + ny * ny
    if d <= 1 then
      local nz = math.sqrt(1 - d)
      local light = math.max(0, -0.55 * nx - 0.6 * ny + 0.58 * nz)
      g:set(x, y, P.shade(ramp, light, x, y))
    end
  end end
  g:set(c - r * 0.4, c - r * 0.45, P.GLINT)
  return g
end

-- a chunky rock with flat facets
local function rock(ramp)
  local g = canvas(22, 18)
  local poly = { { 4, 3 }, { 14, 1 }, { 20, 7 }, { 18, 15 }, { 6, 16 }, { 1, 10 } }
  for y = 0, 17 do for x = 0, 21 do
    local inside = true
    for i = 1, #poly do
      local a, b = poly[i], poly[i % #poly + 1]
      if (b[1] - a[1]) * (y - a[2]) - (b[2] - a[2]) * (x - a[1]) < 0 then inside = false break end
    end
    if inside then
      local facet = (x < 10 and y < 9) and 4 or (x >= 10 and y < 8) and 3 or (x < 9) and 2 or 1
      g:set(x, y, ramp[facet + ((x + y) % 7 == 0 and 1 or 0) > 5 and 5 or facet + ((x + y) % 7 == 0 and 1 or 0)])
    end
  end end
  return g
end

local function copy(g)
  local o = canvas(g.w, g.h)
  for k, v in pairs(g.px) do o.px[k] = v end
  return o
end

local names = { "dirt", "stone", "wood", "iron", "gold", "crystal", "ice", "leaf", "lava", "ui" }
local k = 4
local W, H = 300, 68
local sheet = canvas(W, H)
for y = 0, H - 1 do for x = 0, W - 1 do sheet:set(x, y, P.hex("241c26")) end end

-- swatches: one column per ramp, five shades down
for i, n in ipairs(names) do
  local ramp = P.RAMPS[n]
  for s = 1, 5 do
    for y = 0, 7 do for x = 0, 11 do sheet:set(8 + (i - 1) * 14 + x, 6 + (s - 1) * 9 + y, ramp[s]) end end
  end
end

-- objects: top row full ink, bottom row selective outline
local function put(g, ox, oy)
  for kk, c in pairs(g.px) do sheet:set(ox + kk % g.w, oy + kk // g.w, c) end
end
local samples = { ball(P.RAMPS.crystal, 9), ball(P.RAMPS.gold, 9), rock(P.RAMPS.stone), rock(P.RAMPS.dirt), ball(P.RAMPS.ice, 9) }
for i, s in ipairs(samples) do
  local a, b = copy(s), copy(s)
  P.outline(a)
  P.selout(b)
  put(a, 150 + (i - 1) * 29, 8)
  put(b, 150 + (i - 1) * 29, 40)
end

local img = Image(W * k, H * k, ColorMode.RGB)
for y = 0, H - 1 do for x = 0, W - 1 do
  local c = sheet:get(x, y)
  for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel(x * k + a, y * k + b, c) end end
end end
img:saveAs(previewDir .. "/palette_preview.png")
