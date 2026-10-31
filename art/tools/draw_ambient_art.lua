-- Ambient dressing, into Assets/Art/Items:
--   mote_<layer>     one floating particle per layer (layer counts from 0), 4 looping frames of
--                    8x8 (32x8): dust, sand, spores, bubbles, gold dust, mica, embers, olivine
--                    spores, ice crystals, ash, metal droplets and motes of light
--   dig_edge_shade   64x64 black mask, clear in the middle and darker towards the edges in
--                    stepped dithered bands; stretch it over the dig area or slice it 16px in
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_ambient_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local WHITE = hex("ffffff")

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

local function save(g, name)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
end

local function plus(g, x, y, core, arm)
  g:set(x, y, core); g:set(x - 1, y, arm); g:set(x + 1, y, arm); g:set(x, y - 1, arm); g:set(x, y + 1, arm)
end

---------------------------------------------------------------------------------------------------
-- particles: f runs 0..3 and loops; most pulse in brightness or turn, none move far, since the
-- game drifts the sprite itself
---------------------------------------------------------------------------------------------------
local MOTES = {
  -- 表土 dust: a soft clump that thins and thickens
  function(g, f)
    local c, lo = hex("c8a080", 200), hex("8a6a50", 150)
    g:set(3, 3, c); g:set(4, 4, c)
    if f % 2 == 0 then g:set(4, 3, lo); g:set(3, 4, lo) else g:set(5, 3, lo) end
  end,
  -- 砂岩 sand: a grain that tumbles
  function(g, f)
    local c, hi = hex("e8c890"), hex("fff0c0")
    local pts = { { { 3, 3 }, { 4, 3 } }, { { 4, 3 }, { 4, 4 } }, { { 4, 4 }, { 3, 4 } }, { { 3, 4 }, { 3, 3 } } }
    g:set(pts[f + 1][1][1], pts[f + 1][1][2], hi); g:set(pts[f + 1][2][1], pts[f + 1][2][2], c)
  end,
  -- 溶洞 spore: a violet glow pulsing
  function(g, f)
    local core, glow = hex("e0c8ff"), hex("8a5ad8", ({ 120, 180, 230, 180 })[f + 1])
    plus(g, 3.5, 3.5, core, glow)
    if f == 2 then g:set(2, 2, hex("8a5ad8", 100)); g:set(5, 5, hex("8a5ad8", 100)) end
  end,
  -- 暗河 bubble: a round rim with a glint, squashing a little every other frame
  function(g, f)
    local rim = hex("8ac8f0", 230)
    local rows = (f % 2 == 0) and { ".##.", "#..#", "#..#", ".##." } or { ".##.", "#..#", ".##." }
    local top = (f % 2 == 0) and 2 or 3
    for y, row in ipairs(rows) do
      for x = 1, 4 do if row:sub(x, x) == "#" then g:set(1 + x, top + y - 1, rim) end end
    end
    g:set(3, top + 1, WHITE)
  end,
  -- 远古遗迹 gold dust: a twinkle that flares and fades
  function(g, f)
    local gold, hi = hex("e8b02a"), hex("ffe27a")
    if f == 1 then plus(g, 3.5, 3.5, WHITE, hi) elseif f == 2 then g:set(3, 3, hi); g:set(4, 3, gold)
    else g:set(3, 3, gold) end
  end,
  -- 花岗岩 mica: a flake catching the light as it turns
  function(g, f)
    local c = ({ hex("8a8a98"), hex("d8d8e8"), WHITE, hex("b4b4c2") })[f + 1]
    if f % 2 == 0 then g:set(2, 3, c); g:set(3, 3, c); g:set(4, 3, c) else g:set(3, 2, c); g:set(3, 3, c); g:set(3, 4, c) end
  end,
  -- 莫霍面 ember: hot core cooling at the edges, flickering
  function(g, f)
    local core = ({ hex("ffe27a"), hex("ffb040"), WHITE, hex("ff8a2a") })[f + 1]
    g:set(3, 3, core); g:set(4, 3, hex("e0401e")); g:set(3, 4, hex("e0401e"))
    if f % 2 == 1 then g:set(3, 2, hex("ff8a2a", 160)) end
  end,
  -- 上地幔 olivine spore: a green speck with a pale halo
  function(g, f)
    local core, glow = hex("d0f070"), hex("8ab82a", ({ 110, 170, 220, 170 })[f + 1])
    plus(g, 3.5, 3.5, core, glow)
  end,
  -- 地幔过渡带 ice crystal: a six-pointed flake turning a step each frame
  function(g, f)
    local c, hi = hex("a8f0e0"), WHITE
    if f % 2 == 0 then
      g:set(3, 1, c); g:set(3, 5, c); g:set(1, 2, c); g:set(5, 2, c); g:set(1, 4, c); g:set(5, 4, c)
    else
      g:set(1, 3, c); g:set(5, 3, c); g:set(2, 1, c); g:set(4, 1, c); g:set(2, 5, c); g:set(4, 5, c)
    end
    g:set(3, 3, hi); g:set(2, 3, c); g:set(4, 3, c); g:set(3, 2, c); g:set(3, 4, c)
  end,
  -- 下地幔 ash: a grey flake with a dying red spark
  function(g, f)
    g:set(3, 3, hex("6a5a5a")); g:set(4, 3, hex("4a3a3a"))
    if f < 2 then g:set(3, 4, ({ hex("ff6a2a"), hex("a83a20") })[f + 1]) end
  end,
  -- 外核 metal droplet: a bead of liquid metal with a moving highlight
  function(g, f)
    for y = 2, 5 do for x = 2, 5 do
      if (x - 3.5) ^ 2 + (y - 3.5) ^ 2 <= 2.6 then g:set(x, y, hex("a8a8b8")) end
    end end
    local hl = { { 2, 2 }, { 3, 2 }, { 4, 3 }, { 3, 3 } }
    g:set(hl[f + 1][1], hl[f + 1][2], WHITE); g:set(5, 5, hex("5a5a6a"))
  end,
  -- 内核 light mote: a white point breathing in a warm glow
  function(g, f)
    local glow = hex("fff0b0", ({ 90, 160, 230, 160 })[f + 1])
    plus(g, 3.5, 3.5, WHITE, glow)
    if f == 2 then g:set(1, 3, hex("fff0b0", 90)); g:set(6, 3, hex("fff0b0", 90)); g:set(3, 1, hex("fff0b0", 90)); g:set(3, 6, hex("fff0b0", 90)) end
  end,
}

local strips = {}
for layer = 1, 12 do
  local strip = canvas(32, 8)
  for f = 0, 3 do
    local g = canvas(8, 8)
    MOTES[layer](g, f)
    for k, c in pairs(g.px) do strip.px[(k // 8) * 32 + f * 8 + k % 8] = c end
  end
  save(strip, "mote_" .. (layer - 1))
  strips[layer] = strip
end

---------------------------------------------------------------------------------------------------
-- edge shade: alpha rises in four steps towards the nearest edge, each step dithered into the
-- next so the bands read as pixel art rather than a smooth blur
---------------------------------------------------------------------------------------------------
local shade = canvas(64, 64)
local STEPS = { 0, 60, 110, 160, 210 }
for y = 0, 63 do
  for x = 0, 63 do
    local d = math.min(x, y, 63 - x, 63 - y)
    local t = math.max(0, (16 - d) / 16) * 4
    local step = math.floor(t + (((x + y) % 2 == 0) and 0.25 or -0.25))
    step = math.max(0, math.min(4, step))
    if STEPS[step + 1] > 0 then shade:set(x, y, pc.rgba(8, 6, 14, STEPS[step + 1])) end
  end
end
save(shade, "dig_edge_shade")

-- preview: every layer's four frames at 8x on that layer's dark tone, then the mask over grey
if previewDir then
  local k = 8
  local BG = { "2a1a12", "3a2a18", "1e1438", "0e1e3a", "2e2818", "2e2a2e", "2a0e10", "1e2a0e", "0e2a26", "3a120a", "3a2a0a", "2e2a3a" }
  local img = Image((4 * 9 + 1) * 3 * k + 2 * k, (4 * 9 + 1) * k + 66 * 4, ColorMode.RGB)
  img:clear(hex("2a2630"))
  for layer = 1, 12 do
    local col, row = (layer - 1) % 3, (layer - 1) // 3
    for f = 0, 3 do
      for y = 0, 7 do for x = 0, 7 do
        local c = hex(BG[layer])
        local e = strips[layer]:get(f * 8 + x, y)
        if e then
          local a = pc.rgbaA(e) / 255
          local function m(p, q) return math.floor(p + (q - p) * a + 0.5) end
          c = pc.rgba(m(pc.rgbaR(c), pc.rgbaR(e)), m(pc.rgbaG(c), pc.rgbaG(e)), m(pc.rgbaB(c), pc.rgbaB(e)))
        end
        local ox, oy = 1 + col * 37 + f * 9 + x, 1 + row * 9 + y
        for a2 = 0, k - 1 do for b = 0, k - 1 do img:drawPixel(ox * k + a2, oy * k + b, c) end end
      end end
    end
  end
  local top = 37 * k
  for y = 0, 63 do for x = 0, 63 do
    local base = hex(((x // 8 + y // 8) % 2 == 0) and "8c5f3f" or "a87a52")
    local e = shade:get(x, y)
    local c = base
    if e then
      local a = pc.rgbaA(e) / 255
      c = pc.rgba(math.floor(pc.rgbaR(base) * (1 - a)), math.floor(pc.rgbaG(base) * (1 - a)), math.floor(pc.rgbaB(base) * (1 - a)))
    end
    for a2 = 0, 3 do for b = 0, 3 do img:drawPixel(8 + x * 4 + a2, top + y * 4 + b, c) end end
  end end
  img:saveAs(previewDir .. "/ambient_preview.png")
end
