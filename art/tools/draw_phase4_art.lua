-- Phase 4 art. Boss animations: each guardian's boss_face_<n> becomes a 12-frame strip,
-- 64x32 per frame (768x32): idle 2, hit 2, skill 4, death 4, in that order.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param bosses=0,3]
--      [--script-param preview=<dir>] --script draw_phase4_art.lua
-- bosses limits which guardians are drawn (default all twelve); preview also writes an
-- enlarged sheet of every frame on its wall for reviewing outside Unity.
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end

local W, H = 64, 32
local INK = hex("140c12") -- the outline colour draw_world_art.lua gives every guardian
local WHITE = hex("ffffff")

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c)
    x, y = math.floor(x), math.floor(y)
    if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end
  end
  function g:get(x, y)
    if x < 0 or y < 0 or x >= w or y >= h then return nil end
    return self.px[y * w + x]
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

local function load(path)
  local img = Image { fromFile = path }
  local g = canvas(img.width, img.height)
  for y = 0, img.height - 1 do
    for x = 0, img.width - 1 do
      local c = img:getPixel(x, y)
      if pc.rgbaA(c) > 0 then g.px[y * g.w + x] = c end
    end
  end
  return g
end

local function copy(g, dx, dy, keep)
  local out = canvas(g.w, g.h)
  for k, c in pairs(g.px) do
    local x, y = k % g.w, k // g.w
    if not keep or keep(x, y, c) then out:set(x + (dx or 0), y + (dy or 0), c) end
  end
  return out
end

local function over(base, top)
  for k, c in pairs(top.px) do base.px[k] = c end
  return base
end

local function mix(c, target, t)
  local function m(a, b) return math.floor(a + (b - a) * t + 0.5) end
  return pc.rgba(m(pc.rgbaR(c), pc.rgbaR(target)), m(pc.rgbaG(c), pc.rgbaG(target)),
    m(pc.rgbaB(c), pc.rgbaB(target)), pc.rgbaA(c))
end

-- recolours everything but the outline towards one colour
local function tint(g, target, t)
  for k, c in pairs(g.px) do
    if c ~= INK then g.px[k] = mix(c, target, t) end
  end
  return g
end

-- a deterministic per-pixel roll, so frames do not flicker between runs
local function roll(x, y, salt)
  local n = (x * 73856093 ~ y * 19349663 ~ salt * 83492791) & 0x7fffffff
  return (n % 1000) / 1000
end

-- a glow hugging the outside of the shape, `reach` pixels wide
local function aura(g, colour, reach)
  local out = canvas(g.w, g.h)
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) then
        for d = 1, reach do
          if g:get(x - d, y) or g:get(x + d, y) or g:get(x, y - d) or g:get(x, y + d) then
            out:set(x, y, pc.rgba(pc.rgbaR(colour), pc.rgbaG(colour), pc.rgbaB(colour), d == 1 and 230 or 120))
            break
          end
        end
      end
    end
  end
  return over(out, g)
end

-- the guardian's eyes are its accent colour; brighten them to white for the wind-up
local function eyesTo(g, accent, colour)
  for k, c in pairs(g.px) do
    if c == accent then g.px[k] = colour end
  end
  return g
end

-- the body breaks into 3x3 chunks that drift outwards and fall; keepShare of the chunks remain
local function scatter(g, spread, fall, keepShare, salt)
  local out = canvas(g.w, g.h)
  for k, c in pairs(g.px) do
    local x, y = k % g.w, k // g.w
    local bx, by = x // 3, y // 3
    if roll(bx, by, salt) < keepShare then
      local dx = (bx * 3 + 1 - W / 2) / (W / 2)
      local jitter = math.floor((roll(bx, by, salt + 1) - 0.5) * 3 + 0.5)
      out:set(x + math.floor(dx * spread + 0.5) + jitter, y + math.floor(fall * (0.5 + roll(bx, by, salt + 2)) + 0.5), c)
    end
  end
  return out
end

-- a jagged bright crack down the middle
local function crack(g, colour)
  local pts = { { 32, 0 }, { 30, 6 }, { 34, 11 }, { 29, 17 }, { 33, 23 }, { 31, 31 } }
  for i = 1, #pts - 1 do
    local a, b = pts[i], pts[i + 1]
    local n = math.max(math.abs(b[1] - a[1]), math.abs(b[2] - a[2]))
    for s = 0, n do
      local x = math.floor(a[1] + (b[1] - a[1]) * s / n + 0.5)
      local y = math.floor(a[2] + (b[2] - a[2]) * s / n + 0.5)
      if g:get(x, y) then g:set(x, y, colour) end
    end
  end
  g:line(30, 6, 24, 9, colour); g:line(29, 17, 22, 19, colour); g:line(34, 11, 41, 13, colour)
  return g
end

-- each guardian's eye or core colour, as drawn in draw_world_art.lua
local ACCENTS = {
  "ffb43a", "e84a2e", "c8fbff", "e4eef2", "ffd24a", "ff8a2a",
  "ff3a2a", "ffe27a", "e8ffff", "ffc050", "ffe27a", "ffffff",
}

-- what each guardian adds on top of its body while the skill is released; frame 1 to 3 of the skill
local SKILL_EXTRAS = {
  -- 巨型木根: roots whip up out of the wall on both sides
  [1] = function(g, f)
    local root, tip = hex("a87a52"), hex("dfc08f")
    local grow = ({ 3, 7, 4 })[f]
    for side = -1, 1, 2 do
      for i, base in ipairs({ 6, 13 }) do
        local x = side < 0 and base or W - 1 - base
        for s = 0, grow do
          local wave = math.floor(math.sin(s * 0.9 + i) * 1.5 + 0.5)
          for o = 0, 1 do
            g:set(x + wave * side + o, H - 1 - s * 2, root)
            g:set(x + wave * side + o, H - 2 - s * 2, root)
          end
        end
        g:set(x + side, H - 2 - grow * 2, tip)
      end
    end
    if f == 2 then
      for _, p in ipairs({ { 8, 4 }, { 55, 6 }, { 20, 1 }, { 44, 2 } }) do g:set(p[1], p[2], hex("76a83a")) end
    end
  end,
}

local function frames(face, layer)
  local accent = hex(ACCENTS[layer])
  local out = {}
  local function add(g) out[#out + 1] = g end

  -- idle: a slow breath, the second frame sinks a pixel and the eyes dim
  add(copy(face))
  add(eyesTo(copy(face, 0, 1), accent, mix(accent, INK, 0.35)))

  -- hit: a white flash knocked down a pixel, then a red flush jolted sideways
  add(tint(copy(face, 0, 1), WHITE, 0.85))
  add(tint(copy(face, 1, 0), hex("e84a2e"), 0.35))

  -- skill: crouch with eyes blazing, rise in a thin glow, release in a wide glow, settle
  local extra = SKILL_EXTRAS[layer]
  add(eyesTo(copy(face, 0, 1), accent, WHITE))
  local function skill(body, f, reach)
    if extra then extra(body, f) end
    return aura(body, accent, reach)
  end
  add(skill(eyesTo(copy(face, 0, -1), accent, WHITE), 1, 1))
  add(skill(eyesTo(copy(face, 0, -1), accent, WHITE), 2, 2))
  add(skill(copy(face), 3, 1))

  -- death: cracks open, the halves part, the body crumbles and falls away into dust
  add(crack(tint(copy(face), WHITE, 0.3), WHITE))
  local left = copy(face, -2, 1, function(x) return x < 32 end)
  local right = copy(face, 2, 2, function(x) return x >= 32 end)
  add(crack(over(left, right), accent))
  add(scatter(tint(copy(face), hex("5a4a50"), 0.25), 4, 3, 0.75, layer))
  add(scatter(tint(copy(face), hex("5a4a50"), 0.6), 9, 8, 0.3, layer))
  return out
end

local function save(g, path)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(path)
end

local chosen = {}
if app.params["bosses"] then
  for n in app.params["bosses"]:gmatch("%d+") do chosen[#chosen + 1] = tonumber(n) end
else
  for n = 0, 11 do chosen[#chosen + 1] = n end
end

for _, n in ipairs(chosen) do
  local face = load(dir .. "/Layers/boss_face_" .. n .. ".png")
  local list = frames(face, n + 1)
  local strip = canvas(W * #list, H)
  for i, f in ipairs(list) do
    for k, c in pairs(f.px) do strip.px[(k // W) * strip.w + (i - 1) * W + k % W] = c end
  end
  save(strip, dir .. "/Layers/boss_anim_" .. n .. ".png")

  -- preview: four rows (idle, hit, skill, death) on a dark wall, every frame enlarged 4 times
  if previewDir then
    local scale, pad = 4, 2
    local starts, counts = { 1, 3, 5, 9 }, { 2, 2, 4, 4 }
    local img = Image((W + pad) * 4 * scale + pad * scale, (H + pad) * 4 * scale + pad * scale, ColorMode.RGB)
    img:clear(hex("2a2630"))
    for row = 1, 4 do
      for j = 0, counts[row] - 1 do
        local f = list[starts[row] + j]
        local ox, oy = (pad + j * (W + pad)) * scale, (pad + (row - 1) * (H + pad)) * scale
        for y = 0, H - 1 do
          for x = 0, W - 1 do
            local c = f:get(x, y)
            local wall = (roll(x, y, 7) < 0.2) and hex("2c2026") or hex("1e161c")
            if c and pc.rgbaA(c) < 255 then c = mix(wall, c, pc.rgbaA(c) / 255) end
            c = c or wall
            for a = 0, scale - 1 do
              for b = 0, scale - 1 do img:drawPixel(ox + x * scale + a, oy + y * scale + b, c) end
            end
          end
        end
      end
    end
    img:saveAs(previewDir .. "/boss_anim_preview_" .. n .. ".png")
  end
end
