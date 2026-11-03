-- The trailer logo: 挖了个爽 over DRILL THRILL, in the game's palette. Writes art/trailer/logo.png
-- (stage pixels, transparent; the trailer scales it six times) and art/trailer_logo_preview.png.
-- The letter shapes come from tools/trailer/logo_mask.py, which lays out the game's pixel font;
-- everything that makes them a logo is drawn here: a gold ramp down the Chinese letters and a
-- steel one down the English, a lit top edge and a shaded bottom edge on every stroke, a double
-- outline, a drop shadow, the standard drill diving in beside the name, and a few glints.
-- Run: Aseprite.exe -b --script-param preview=<art dir> --script art/tools/draw_trailer_logo.lua
local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
local root = DEEPDRILL_ROOT or ""
local previewDir = app.params["preview"]
local pc = app.pixelColor

local W, H = 184, 60

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c) if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end end
  function g:get(x, y) if x < 0 or y < 0 or x >= w or y >= h then return nil end return self.px[y * w + x] end
  return g
end

local function load(path)
  local img = Image { fromFile = path }
  local g = canvas(img.width, img.height)
  for y = 0, img.height - 1 do for x = 0, img.width - 1 do
    local c = img:getPixel(x, y)
    if pc.rgbaA(c) > 127 then g:set(x, y, c) end
  end end
  return g
end

-- fills a mask's letters with a vertical ramp, lighting the top edge of every stroke and shading
-- the bottom, so each chunky pixel of the font reads as a bevelled block
local function letters(mask, ramp, glint)
  local g = canvas(mask.w, mask.h)
  for y = 0, mask.h - 1 do
    for x = 0, mask.w - 1 do
      if mask:get(x, y) then
        local light = 1 - y / (mask.h - 1)
        local c = P.shade(ramp, 0.15 + light * 0.75, x, y)
        if not mask:get(x, y - 1) then c = glint
        elseif not mask:get(x, y + 1) then c = ramp[1]
        elseif not mask:get(x - 1, y) then c = ramp[math.min(#ramp, 4)] end
        g:set(x, y, c)
      end
    end
  end
  return g
end

local function stamp(dst, src, ox, oy)
  for k, c in pairs(src.px) do dst:set(ox + k % src.w, oy + k // src.w, c) end
end

-- a solid ring round everything drawn so far
local function ring(g, c)
  local out = {}
  for y = 0, g.h - 1 do for x = 0, g.w - 1 do
    if not g:get(x, y) and (g:get(x - 1, y) or g:get(x + 1, y) or g:get(x, y - 1) or g:get(x, y + 1)) then out[#out + 1] = { x, y } end
  end end
  for _, p in ipairs(out) do g:set(p[1], p[2], c) end
end

local gold = P.ramp("f0a020")
local steel = P.ramp("8ab8e0")
local cn = letters(load(root .. "art/trailer/logo_cn_mask.png"), gold, P.GLINT)
local en = letters(load(root .. "art/trailer/logo_en_mask.png"), steel, P.hex("f4faff"))

local art = canvas(W, H)
local drillGap = 20
local cnX = math.floor((W - cn.w + drillGap) / 2)
local cnY = 6
stamp(art, cn, cnX, cnY)
local enX = math.floor((W - en.w) / 2)
local enY = cnY + cn.h + 7
stamp(art, en, enX, enY)

-- the standard drill, doubled, diving in at the left of the name
local strip = load(root .. "Assets/Art/Drill/drill_0_6.png")
local drill = canvas(16, 24)
for y = 0, 11 do for x = 0, 7 do
  local c = strip:get(x, y)
  if c then for a = 0, 1 do for b = 0, 1 do drill:set(x * 2 + a, y * 2 + b, c) end end end
end end
stamp(art, drill, cnX - drillGap, cnY - 2)

-- outlines: ink, then a warm dark rim, then the drop shadow down and to the right
ring(art, P.INK)
local shadowSource = {}
for k, c in pairs(art.px) do shadowSource[k] = true end
ring(art, P.hex("5a1e2a"))
local logo = canvas(W, H)
for k in pairs(art.px) do
  local x, y = k % W, k // W
  logo:set(x + 2, y + 3, P.hex("1a0e24"))
end
stamp(logo, art, 0, 0)

-- glints where the light catches the gold
local GLINT = { { 0, -1 }, { -1, 0 }, { 0, 0 }, { 1, 0 }, { 0, 1 } }
for _, at in ipairs({ { cnX + 5, cnY + 2 }, { cnX + cn.w - 9, cnY + 1 }, { enX + en.w - 3, enY } }) do
  for _, d in ipairs(GLINT) do logo:set(at[1] + d[1], at[2] + d[2], P.WHITE) end
end

local img = Image(W, H, ColorMode.RGB)
for k, c in pairs(logo.px) do img:drawPixel(k % W, k // W, c) end
img:saveAs(root .. "art/trailer/logo.png")

if previewDir then
  local k = 4
  local pv = Image(W * k, H * k, ColorMode.RGB)
  pv:clear(P.hex("3a2a20"))
  for kk, c in pairs(logo.px) do
    local x, y = kk % W, kk // W
    for a = 0, k - 1 do for b = 0, k - 1 do pv:drawPixel(x * k + a, y * k + b, c) end end
  end
  pv:saveAs(previewDir .. "/trailer_logo_preview.png")
end
