-- The combo flame that replaces the combo text: four heat levels, four flicker frames each,
-- 16x24, into Assets/Art/Items as flame_<level>_<frame>.
--   level 0  a low red ember: a chain has started but earns no bonus yet
--   level 1  an orange flame: some bonus
--   level 2  a tall yellow flame with a white heart: over half the bonus cap
--   level 3  a blue flame: the bonus is at its cap
-- Shapes are a teardrop that sways more towards its tip, with a second tongue that licks up on
-- alternate frames and a spark or two above. Bands of heat step from the edge to the heart with a
-- checker dither between them, like the light sprites. No outline: fire should not look cut out.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script art/tools/draw_combo_flame.lua
local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local W, H = 16, 24
local FRAMES = 4
local BASE = H - 2 -- the row the flame stands on

-- edge to heart; the darkest band only shows on the rim
local LEVELS = {
  { height = 11, width = 9,  sway = 0.8, colors = { "6e1c22", "b8322a", "e8642a", "f5a83a" } },
  { height = 15, width = 11, sway = 1.2, colors = { "8e2420", "e0521e", "f7962a", "fde27a" } },
  { height = 19, width = 13, sway = 1.6, colors = { "c03a1c", "f0821e", "fcd040", "fff8dc" } },
  { height = 21, width = 14, sway = 1.8, colors = { "1c3478", "2a68d8", "68c4ff", "eaf8ff" } },
}

local function canvas()
  local g = { px = {} }
  function g:set(x, y, c) if x >= 0 and y >= 0 and x < W and y < H then self.px[y * W + x] = c end end
  function g:get(x, y) return self.px[y * W + x] end
  return g
end

-- half width of the teardrop at height share t (0 base, 1 tip)
local function halfWidth(t, width)
  if t < 0 or t > 1 then return -1 end
  local swell = math.sin(math.pi * math.min(1, t * 1.6 + 0.12)) -- widest a third of the way up
  local taper = (1 - t) ^ 0.55
  return width / 2 * (0.35 + 0.65 * swell) * taper
end

local function flame(level, frame)
  local L = LEVELS[level + 1]
  local ramp = {}
  for i, c in ipairs(L.colors) do ramp[i] = P.hex(c) end
  local g = canvas()
  local phase = frame / FRAMES * 2 * math.pi
  local height = L.height + ((frame % 2 == 0) and 0 or -1)
  local cx = W / 2 - 0.5

  local function heat(x, y, h, w, sway, offset)
    local t = (BASE - y) / h
    local hw = halfWidth(t, w)
    if hw <= 0 then return nil end
    local c = cx + offset + sway * (t ^ 1.2) * math.sin(phase + t * 2.6)
    -- ragged edges that crawl upward from frame to frame
    hw = hw * (1 + 0.22 * math.sqrt(t) * math.sin(y * 1.9 - phase * 2 + offset))
    if hw < 0.6 then return nil end
    local d = math.abs(x - c) / hw
    if d > 1 then return nil end
    -- hotter at the middle and low down
    return (1 - d) * (1 - t * 0.55)
  end

  for y = 0, H - 1 do
    for x = 0, W - 1 do
      local v = heat(x, y, height, L.width, L.sway, 0)
      -- the second tongue, off to one side, rises and falls with the frames
      local side = (frame < 2) and -2.5 or 2.5
      local tongue = heat(x, y, height * (0.7 + 0.12 * math.sin(phase)), L.width * 0.5, L.sway * 1.5, side)
      if tongue then tongue = tongue * 0.7 end
      if tongue and (not v or tongue > v) then v = tongue end
      if v then
        local band = v * 4.2 + (((x + y) % 2 == 0) and 0.22 or -0.22)
        local i = math.max(1, math.min(4, math.floor(band) + 1))
        g:set(x, y, ramp[i])
      end
    end
  end

  -- the flame sits on a bed of embers, flat along the base
  for x = math.floor(cx - L.width / 2 + 2), math.ceil(cx + L.width / 2 - 2) do
    if g:get(x, BASE) then g:set(x, BASE + 1, ramp[(x + frame) % 3 == 0 and 3 or 2]) end
  end

  -- sparks drift up and away
  local sparks = level + 1
  for s = 1, sparks do
    local sx = math.floor(cx + math.sin(s * 2.1 + frame * 1.3) * (2 + s))
    local sy = BASE - height - 1 - ((frame + s * 2) % 4)
    if sy >= 0 and not g:get(sx, sy) then g:set(sx, sy, ramp[3]) end
  end
  return g
end

local sheets = {}
for level = 0, #LEVELS - 1 do
  sheets[level] = {}
  for frame = 0, FRAMES - 1 do
    local g = flame(level, frame)
    local img = Image(W, H, ColorMode.RGB)
    for y = 0, H - 1 do for x = 0, W - 1 do
      local c = g:get(x, y)
      if c then img:drawPixel(x, y, c) end
    end end
    img:saveAs(dir .. "/Items/flame_" .. level .. "_" .. frame .. ".png")
    sheets[level][frame] = g
  end
end

if previewDir then
  local k = 6
  local img = Image((FRAMES * (W + 2) + 2) * k, (#LEVELS * (H + 2) + 2) * k, ColorMode.RGB)
  img:clear(P.hex("241c26"))
  for level = 0, #LEVELS - 1 do
    for frame = 0, FRAMES - 1 do
      local g = sheets[level][frame]
      local ox, oy = 2 + frame * (W + 2), 2 + level * (H + 2)
      for y = 0, H - 1 do for x = 0, W - 1 do
        local c = g:get(x, y)
        if c then
          for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel((ox + x) * k + a, (oy + y) * k + b, c) end end
        end
      end end
    end
  end
  img:saveAs(previewDir .. "/combo_flame_preview.png")
end
