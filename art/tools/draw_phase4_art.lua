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

  -- 岩甲虫: the shell seam glows shut and sand sprays off the sides
  [2] = function(g, f)
    local glow = ({ hex("f0d890"), WHITE, hex("f0d890") })[f]
    for y = 12, 31 do
      if g:get(31, y) then g:set(31, y, glow); g:set(32, y, glow) end
    end
    -- sand sprays up and out from both edges of the shell, further each frame
    local sand, grit = hex("fff0c0"), hex("c8a060")
    for i = 0, 4 do
      local out = i * 2 + f * 2
      local y = 22 - i * 2 - f + (i * i) // 3
      for side = -1, 1, 2 do
        local x = side < 0 and (11 - out) or (52 + out)
        g:set(x, y, sand); g:set(x + 1, y, sand); g:set(x, y + 1, grit)
      end
    end
  end,

  -- 水晶巨像: crystal shards circle the head, a step further round each frame
  [3] = function(g, f)
    local shard, light = hex("5ac8e0"), hex("c8fbff")
    for i = 0, 5 do
      local a = i * math.pi / 3 + f * 0.5
      local x, y = 32 + math.cos(a) * 27, 15 + math.sin(a) * 13
      g:set(x, y - 1, light); g:set(x, y, shard); g:set(x - 1, y, shard); g:set(x + 1, y, shard); g:set(x, y + 1, shard)
    end
  end,

  -- 盲眼巨鱼: water jets climb up on either side and spill over in drops
  [4] = function(g, f)
    local water, foam = hex("3a8ad8"), hex("c8f0ff")
    local height = ({ 6, 14, 10 })[f]
    for side = -1, 1, 2 do
      local x = side < 0 and 4 or W - 6
      for s = 0, height do
        local wob = math.floor(math.sin(s * 0.7 + f) + 0.5)
        g:set(x + wob, H - 1 - s, water); g:set(x + wob + 1, H - 1 - s, water)
      end
      g:set(x, H - 2 - height, foam); g:set(x + 1, H - 2 - height, foam)
      if f >= 2 then
        for d = 1, 3 do g:set(x - side * d * 2, H - height + d * 2, foam) end
      end
    end
  end,

  -- 石像守卫: the brow rune flares and grey stone rays fan out from it
  [5] = function(g, f)
    local gold, stone = hex("ffe27a"), hex("b0a890")
    local len = ({ 4, 9, 6 })[f]
    for _, d in ipairs({ { -1, 0 }, { 1, 0 }, { -1, -0.4 }, { 1, -0.4 }, { -0.6, 1 }, { 0.6, 1 } }) do
      for s = 3, len + 3 do g:set(32 + d[1] * s * 2, 4 + d[2] * s, s % 2 == 0 and stone or gold) end
    end
    g:set(31, 3, WHITE); g:set(32, 3, WHITE); g:set(31, 4, gold); g:set(32, 4, gold)
  end,

  -- 花岗岩魔像: chips of rock fly in from the edges and stick to the shoulders
  [6] = function(g, f)
    local chip, trail = hex("f4ecec"), hex("ff8a2a")
    for i = 0, 7 do
      local side = i % 2 == 0 and -1 or 1
      local dist = 34 - f * 7 + (i % 3) * 2
      local x, y = 32 + side * dist, 4 + i * 3
      g:set(x, y, chip); g:set(x + 1, y, chip); g:set(x, y + 1, chip); g:set(x + 1, y + 1, chip)
      g:set(x + side * 2, y, trail); g:set(x + side * 3, y + 1, trail)
    end
  end,

  -- 莫霍守门人: pressure waves press downwards from the helmet in red bands
  [7] = function(g, f)
    local red, hot = hex("ff3a2a"), hex("ff9a7a")
    for b = 0, 1 do
      local y = 2 + f * 3 + b * 9
      if y < H then
        local half = 10 + f * 3 + b * 4
        for x = 32 - half, 31 + half do
          if (x + y) % 4 ~= 0 then g:set(x, y, b == 0 and hot or red) end
        end
        g:set(32 - half, y + 1, red); g:set(31 + half, y + 1, red)
      end
    end
  end,

  -- 熔岩巨蛇: globs of magma arc out of the open mouth
  [8] = function(g, f)
    local magma, hot = hex("f07a20"), hex("ffe27a")
    for i, v in ipairs({ { -1.6, 1.1 }, { 1.6, 1.1 }, { -0.8, 1.6 }, { 0.8, 1.6 } }) do
      local t = f * 2.2 + (i % 2) * 0.6
      local x = 32 + v[1] * t * 3
      local y = 20 - v[2] * t * 3 + t * t * 0.7
      g:set(x, y, hot); g:set(x + 1, y, magma); g:set(x, y + 1, magma); g:set(x + 1, y + 1, magma)
    end
  end,

  -- 相变之灵: the whole body thins to half seen and sparkles flicker round it
  [9] = function(g, f)
    if f == 2 then
      for k, c in pairs(g.px) do
        g.px[k] = pc.rgba(pc.rgbaR(c), pc.rgbaG(c), pc.rgbaB(c), math.min(pc.rgbaA(c), 120))
      end
    end
    local spark = hex("e8ffff")
    for i = 0, 5 do
      local x, y = 12 + ((i * 17 + f * 9) % 40), 2 + ((i * 11 + f * 5) % 26)
      g:set(x, y, spark); g:set(x - 1, y, hex("5ad8c8")); g:set(x + 1, y, hex("5ad8c8"))
    end
  end,

  -- 地幔巨鲸: a red wave rolls along the bottom of the wall
  [10] = function(g, f)
    local red, crest = hex("c83a20"), hex("ff9a5a")
    for x = 0, W - 1 do
      local top = math.floor(H - 4 - 2.5 * math.sin(x / 5 + f * 1.4) + 0.5)
      for y = top, H - 1 do g:set(x, y, red) end
      g:set(x, top, crest)
    end
  end,

  -- 磁暴旋涡: lightning jumps from the two poles into the eye
  [11] = function(g, f)
    local arc = WHITE
    local bolt = { { 30, 0 }, { 34, 3 }, { 29, 6 }, { 33, 9 }, { 32, 13 } }
    if f == 2 then
      for i = 1, #bolt - 1 do
        g:line(bolt[i][1], bolt[i][2], bolt[i + 1][1], bolt[i + 1][2], hex("8ae8ff"))
        g:line(bolt[i][1] + 1, bolt[i][2], bolt[i + 1][1] + 1, bolt[i + 1][2], WHITE)
      end
    end
    for side = -1, 1, 2 do
      local x0 = side < 0 and 5 or W - 6
      local x, y = x0, 15
      for s = 1, 6 do
        local nx = x0 + (32 - x0) * s / 6
        local ny = 16 + ((s * 7 + f * 5 + (side + 1) * 3) % 9) - 4
        g:line(x, y, nx, ny, s == 6 and WHITE or arc)
        x, y = nx, ny
      end
    end
  end,

  -- 地核心脏: a heartbeat ring spreads outwards from the core
  [12] = function(g, f)
    local ring = ({ hex("fff0b0"), hex("ffc860"), hex("e8803a") })[f]
    local rx, ry = 10 + f * 7, 6 + f * 3.5
    for i = 0, 95 do
      local a = i / 96 * 2 * math.pi
      g:set(32 + math.cos(a) * rx, 15 + math.sin(a) * ry, ring)
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
