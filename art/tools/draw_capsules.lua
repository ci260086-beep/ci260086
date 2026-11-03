-- Steam store art for 挖了个爽 / Drill Thrill: every capsule built from one underground scene.
-- The scene is drawn at pixel-art resolution from the game's own sprites, then scaled by a whole
-- number and cropped to Steam's exact sizes, so every poster stays on the pixel grid:
--   strata    the twelve layers' rock textures in bands, darker with depth, wavy where they meet
--   shaft     a ragged tunnel down the middle, lit from inside
--   swarm     drills of every material filing down the shaft
--   hero      the star-core drill at the bottom of the shaft in a blue combo flame, with light
--   burst     coins, ores and glints flying out of the rock it is breaking
--   guardian  a layer guardian waiting below, half hidden in the rock
-- Writes art/capsules/*.png and art/capsules_preview.png.
-- Run: Aseprite.exe -b --script-param preview=<art dir> --script art/tools/draw_capsules.lua
local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
local root = DEEPDRILL_ROOT or ""
local previewDir = app.params["preview"]
local pc = app.pixelColor
local ART = root .. "Assets/Art/"

---------------------------------------------------------------------------------------------------
-- small canvas with alpha
---------------------------------------------------------------------------------------------------
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
  return g
end

local cache = {}
local function load(path)
  if cache[path] then return cache[path] end
  local img = Image { fromFile = path }
  local g = canvas(img.width, img.height)
  for y = 0, img.height - 1 do for x = 0, img.width - 1 do
    local c = img:getPixel(x, y)
    if pc.rgbaA(c) > 127 then g.px[y * img.width + x] = pc.rgba(pc.rgbaR(c), pc.rgbaG(c), pc.rgbaB(c), 255) end
  end end
  cache[path] = g
  return g
end

-- the first frame of a strip of equal frames
local function frame(path, fw)
  local strip = load(path)
  local g = canvas(fw, strip.h)
  for y = 0, strip.h - 1 do for x = 0, fw - 1 do g.px[y * fw + x] = strip:get(x, y) end end
  return g
end

local function scaled(src, k)
  local g = canvas(src.w * k, src.h * k)
  for kk, c in pairs(src.px) do
    local x, y = kk % src.w, kk // src.w
    for a = 0, k - 1 do for b = 0, k - 1 do g.px[(y * k + b) * g.w + x * k + a] = c end end
  end
  return g
end

local function stamp(dst, src, ox, oy)
  for kk, c in pairs(src.px) do dst:set(ox + kk % src.w, oy + kk // src.w, c) end
end

local function mixc(a, b, t)
  local function m(x, y) return math.floor(x + (y - x) * t + 0.5) end
  return pc.rgba(m(pc.rgbaR(a), pc.rgbaR(b)), m(pc.rgbaG(a), pc.rgbaG(b)), m(pc.rgbaB(a), pc.rgbaB(b)), 255)
end

-- adds light in a colour: a round pool, stepped in bands with a checker between them
local function glow(dst, cx, cy, radius, tint, strength)
  local tr, tg, tb = pc.rgbaR(tint), pc.rgbaG(tint), pc.rgbaB(tint)
  for y = math.floor(cy - radius), math.ceil(cy + radius) do
    for x = math.floor(cx - radius), math.ceil(cx + radius) do
      local c = dst:get(x, y)
      if c then
        local d = math.sqrt((x - cx) ^ 2 + (y - cy) ^ 2) / radius
        if d < 1 then
          local band = math.floor((1 - d) * 5 + (((x + y) % 2 == 0) and 0.25 or -0.25)) / 5
          if band > 0 then
            local a = band * strength
            dst:set(x, y, pc.rgba(math.min(255, pc.rgbaR(c) + math.floor(tr * a)), math.min(255, pc.rgbaG(c) + math.floor(tg * a)),
              math.min(255, pc.rgbaB(c) + math.floor(tb * a)), 255))
          end
        end
      end
    end
  end
end

-- a smooth hash noise for wavy boundaries
local function hash(x, y, s)
  local h = math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453
  return h - math.floor(h)
end
local function noise(x, y, cell, s)
  local gx, gy = math.floor(x / cell), math.floor(y / cell)
  local fx, fy = x / cell - gx, y / cell - gy
  fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
  local a, b = hash(gx, gy, s), hash(gx + 1, gy, s)
  local c, d = hash(gx, gy + 1, s), hash(gx + 1, gy + 1, s)
  return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy
end

---------------------------------------------------------------------------------------------------
-- the scene
---------------------------------------------------------------------------------------------------
local SKY_TOP, SKY_LOW = P.hex("5aa8e8"), P.hex("b8e0f8")
local GRASS = { P.hex("a8d45a"), P.hex("76a83a"), P.hex("4a7a2a") }
local CAVE = P.hex("1a1210")

-- opts: w, h, sky (rows of sky), shaftX, tipY, layers (how many bands), boss (true/false), seed
local function scene(o)
  local g = canvas(o.w, o.h)
  local sky = o.sky or 0
  local bands = o.layers or 12
  local bandH = (o.h - sky) / bands

  -- strata
  for y = 0, o.h - 1 do
    for x = 0, o.w - 1 do
      if y < sky then
        g:set(x, y, mixc(SKY_TOP, SKY_LOW, y / math.max(1, sky)))
      else
        local depth = (y - sky) + (noise(x, y, 9, 3) - 0.5) * bandH * 0.7
        local band = math.max(0, math.min(bands - 1, math.floor(depth / bandH)))
        local layer = math.floor(band * 11 / math.max(1, bands - 1) + 0.5)
        local kind = math.floor(hash(x // 16, y // 16, 7) * 5)
        local tex = load(ART .. "Layers/rock_layer_" .. layer .. "_" .. kind .. ".png")
        local c = tex:get(x % 16, y % 16) or P.INK
        -- deeper is darker, and the sides fall into shadow
        local dark = 0.12 + 0.35 * ((y - sky) / (o.h - sky))
        local side = math.abs(x - o.shaftX) / (o.w * 0.75)
        c = mixc(c, P.INK, math.min(0.82, dark + side * 0.6))
        g:set(x, y, c)
      end
    end
  end
  if sky > 0 then
    for x = 0, o.w - 1 do
      local bump = math.floor(noise(x, 0, 6, 5) * 2)
      for k = 0, 3 do g:set(x, sky - 1 + k - bump, GRASS[math.min(3, k + 1)]) end
    end
  end

  -- the shaft: ragged walls, a dark inside, a lit rim
  local half = o.shaftHalf or 9
  for y = math.max(0, sky - 2), o.tipY + 6 do
    local wob = (noise(0, y, 5, 11) - 0.5) * 5
    local lean = (y - sky) * (o.lean or 0)
    local cx = o.shaftX + wob + lean
    local hw = half + (noise(3, y, 3, 13) - 0.5) * 3
    if y > o.tipY - 4 then hw = hw * math.max(0.2, (o.tipY + 6 - y) / 10) end
    for x = math.floor(cx - hw - 1), math.ceil(cx + hw + 1) do
      local d = math.abs(x - cx)
      if d <= hw then
        g:set(x, y, mixc(CAVE, P.hex("3a2418"), 0.25 * (1 - d / hw)))
      elseif d <= hw + 1.2 then
        local c = g:get(x, y)
        if c then g:set(x, y, mixc(c, P.hex("f0c070"), x < cx and 0.35 or 0.12)) end
      end
    end
  end

  -- the swarm filing down
  local tiers = { 3, 5, 6, 7, 8, 9, 10, 11 }
  local n = o.swarm or 10
  for i = 1, n do
    local t = i / (n + 1)
    local y = sky + (o.tipY - 18 - sky) * t
    local x = o.shaftX + (hash(i, 1, 9) - 0.5) * (half * 1.4) + (y - sky) * (o.lean or 0)
    local fam = math.floor(hash(i, 2, 9) * 5)
    local tier = tiers[1 + math.floor(hash(i, 3, 9) * #tiers)]
    local d = frame(ART .. "Drill/drill_" .. fam .. "_" .. tier .. ".png", 8)
    stamp(g, d, math.floor(x - 4), math.floor(y - 6))
  end

  -- the guardian, half in the rock below
  if o.boss then
    local face = frame(ART .. "Layers/boss_face_" .. (o.bossIndex or 9) .. ".png", 64)
    local big = scaled(face, o.bossScale or 2)
    local bx = math.floor(o.shaftX - big.w / 2 + (o.tipY - sky) * (o.lean or 0))
    local by = o.tipY + (o.bossGap or 10)
    stamp(g, big, bx, by)
    glow(g, bx + big.w / 2, by + big.h / 3, big.w * 0.45, P.hex("e8402e"), 0.25)
  end

  -- the hero drill in its flame, and the light it throws; the flame rises from the drill's
  -- middle and towers over it, as it does in the game at the combo cap
  local hk = o.heroScale or 2
  local hx = o.shaftX + (o.tipY - sky) * (o.lean or 0)
  local flame = scaled(load(ART .. "Items/flame_3_1.png"), hk + 1)
  local hero = scaled(frame(ART .. "Drill/drill_0_11.png", 8), hk + 1)
  local heroTop = o.tipY - hero.h + 3
  glow(g, hx, heroTop, 30 * hk, P.hex("68c4ff"), 0.5)
  glow(g, hx, o.tipY, 12 * hk, P.hex("fff6d8"), 0.55)

  -- speed streaks down the shaft above the drill
  for i = 1, (o.streaks or 10) do
    local x = math.floor(hx + (hash(i, 31, 3) - 0.5) * half * 1.6)
    local y0 = sky + hash(i, 32, 3) * (heroTop - sky) * 0.8
    local len = 6 + hash(i, 33, 3) * 14
    for y = y0, math.min(heroTop - 2, y0 + len) do
      local c = g:get(x, y)
      if c then g:set(x, y, mixc(c, P.hex("fff6d8"), 0.55 * (1 - (y - y0) / len))) end
    end
  end

  stamp(g, flame, math.floor(hx - flame.w / 2), math.floor(heroTop + hero.h / 2 - flame.h + 2 * (hk + 1)))
  stamp(g, hero, math.floor(hx - hero.w / 2), math.floor(heroTop))

  -- dust thrown up where the bit bites
  local DUST = { P.hex("c4a080"), P.hex("a07a58"), P.hex("7a5a40") }
  for i = 1, (o.dust or 7) do
    local side = (i % 2 == 0) and 1 or -1
    local cx = hx + side * (4 + hash(i, 41, 5) * 12) * hk
    local cy = o.tipY + 1 - hash(i, 42, 5) * 6 * hk
    local r = (2 + hash(i, 43, 5) * 3) * hk
    for y = math.floor(cy - r), math.ceil(cy + r) do
      for x = math.floor(cx - r), math.ceil(cx + r) do
        local d = math.sqrt((x - cx) ^ 2 + (y - cy) ^ 2) / r
        if d <= 1 and ((x + y) % 2 == 0 or d < 0.6) then
          local c = g:get(x, y)
          if c then g:set(x, y, mixc(c, DUST[1 + math.min(2, math.floor(d * 3))], 0.75)) end
        end
      end
    end
  end

  -- the burst: coins, ores and glints thrown out of the rock
  local coin = load(ART .. "Items/icon_coin.png")
  local glint = load(ART .. "Items/light_glint.png")
  local burst = o.burst or 22
  for i = 1, burst do
    local ang = -math.pi * (0.1 + 0.8 * hash(i, 4, 21)) - (hash(i, 7, 21) < 0.3 and math.pi or 0) * 0
    local dist = (16 + hash(i, 5, 21) * (o.burstReach or 50)) * hk / 2
    local x = hx + math.cos(ang) * dist * 1.6
    local y = o.tipY - 4 + math.sin(ang) * dist
    local pick = hash(i, 6, 21)
    if pick < 0.55 then stamp(g, coin, math.floor(x - 4), math.floor(y - 4))
    elseif pick < 0.85 then stamp(g, load(ART .. "Items/oreicon_" .. math.floor(hash(i, 8, 21) * 24) .. ".png"), math.floor(x - 4), math.floor(y - 4))
    else
      for kk, c in pairs(glint.px) do
        local gx, gy = kk % glint.w, kk // glint.w
        local under = g:get(x - 4 + gx, y - 4 + gy)
        if under then g:set(x - 4 + gx, y - 4 + gy, mixc(under, P.GLINT, 0.85)) end
      end
    end
  end
  return g
end

---------------------------------------------------------------------------------------------------
-- capsules
---------------------------------------------------------------------------------------------------
local logo = load(root .. "art/trailer/logo.png")

-- writes a canvas scaled by k and cropped to exactly w x h (from the centre)
local function save(g, k, w, h, name, transparent)
  local img = Image(w, h, ColorMode.RGB)
  local ox, oy = math.floor((g.w * k - w) / 2), math.floor((g.h * k - h) / 2)
  for y = 0, h - 1 do
    local sy = (y + oy) // k
    for x = 0, w - 1 do
      local c = g:get((x + ox) // k, sy)
      if c then img:drawPixel(x, y, c) elseif not transparent then img:drawPixel(x, y, P.INK) end
    end
  end
  img:saveAs(root .. "art/capsules/" .. name .. ".png")
  return img
end

-- dims the scene behind the logo a little so the letters always read
local function plate(g, x, y, w, h)
  for yy = y, y + h - 1 do for xx = x, x + w - 1 do
    local c = g:get(xx, yy)
    if c then g:set(xx, yy, mixc(c, P.INK, 0.45)) end
  end end
end

local outputs = {}

-- main capsule 1232x706: the shaft on the right, the logo on the left
do
  local g = scene { w = 308, h = 177, sky = 22, shaftX = 222, tipY = 128, swarm = 9, boss = true, bossScale = 1, bossGap = 16, heroScale = 2, burst = 26, burstReach = 46 }
  stamp(g, logo, 10, 52)
  outputs[#outputs + 1] = { save(g, 4, 1232, 706, "main_capsule_1232x706"), "main" }
end

-- header capsule 920x430
do
  local g = scene { w = 230, h = 108, sky = 16, shaftX = 192, tipY = 78, swarm = 6, boss = false, heroScale = 1, burst = 16, burstReach = 34 }
  stamp(g, logo, 4, 30)
  outputs[#outputs + 1] = { save(g, 4, 920, 430, "header_capsule_920x430"), "header" }
end

-- small capsule 462x174: the logo almost alone, it has to read at thumbnail size
do
  local g = scene { w = 231, h = 87, sky = 10, shaftX = 210, tipY = 62, swarm = 3, heroScale = 1, burst = 8, burstReach = 20 }
  plate(g, 18, 12, 196, 64)
  stamp(g, logo, 23, 14)
  outputs[#outputs + 1] = { save(g, 2, 462, 174, "small_capsule_462x174"), "small" }
end

-- vertical capsule 748x896: logo on top, the shaft dropping to the guardian
do
  local g = scene { w = 187, h = 224, sky = 72, shaftX = 94, tipY = 168, swarm = 8, boss = true, bossScale = 1, bossGap = 12, heroScale = 2, burst = 24, burstReach = 44 }
  stamp(g, logo, 2, 6)
  outputs[#outputs + 1] = { save(g, 4, 748, 896, "vertical_capsule_748x896"), "vertical" }
end

-- library capsule 600x900
do
  local g = scene { w = 200, h = 300, sky = 76, shaftX = 100, tipY = 222, swarm = 12, boss = true, bossScale = 1, bossGap = 14, heroScale = 2, burst = 30, burstReach = 50 }
  stamp(g, logo, 8, 8)
  outputs[#outputs + 1] = { save(g, 3, 600, 900, "library_capsule_600x900"), "library" }
end

-- library hero 3840x1240: no logo or text, Steam lays the logo over it
do
  local g = scene { w = 320, h = 104, sky = 14, shaftX = 230, tipY = 76, swarm = 8, boss = true, bossScale = 1, bossGap = 8, heroScale = 1, burst = 22, burstReach = 40, lean = 0 }
  outputs[#outputs + 1] = { save(g, 12, 3840, 1240, "library_hero_3840x1240"), "hero" }
end

-- library logo 1280x720: the logo alone on transparency
do
  local g = canvas(213, 120)
  stamp(g, logo, math.floor((213 - logo.w) / 2), math.floor((120 - logo.h) / 2))
  outputs[#outputs + 1] = { save(g, 6, 1280, 720, "library_logo_1280x720", true), "logo" }
end

if previewDir then
  -- every capsule at a quarter of its size, side by side
  local scaleDown = 4
  local W, H = 0, 0
  for _, o in ipairs(outputs) do
    if o[2] ~= "hero" then W = W + o[1].width // scaleDown + 8 end
    H = math.max(H, o[1].height // scaleDown)
  end
  local hero
  for _, o in ipairs(outputs) do if o[2] == "hero" then hero = o[1] end end
  local heroH = hero.height // (scaleDown * 3)
  local pv = Image(W + 8, H + heroH + 24, ColorMode.RGB)
  pv:clear(P.hex("3a2a20"))
  local x = 8
  for _, o in ipairs(outputs) do
    if o[2] ~= "hero" then
      local img = o[1]
      for y = 0, img.height // scaleDown - 1 do for xx = 0, img.width // scaleDown - 1 do
        local c = img:getPixel(xx * scaleDown, y * scaleDown)
        if pc.rgbaA(c) > 0 then pv:drawPixel(x + xx, 8 + y, c) end
      end end
      x = x + img.width // scaleDown + 8
    end
  end
  for y = 0, heroH - 1 do for xx = 0, hero.width // (scaleDown * 3) - 1 do
    pv:drawPixel(8 + xx, H + 16 + y, hero:getPixel(xx * scaleDown * 3, y * scaleDown * 3))
  end end
  pv:saveAs(previewDir .. "/capsules_preview.png")
end
