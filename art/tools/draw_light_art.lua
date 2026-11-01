-- Light sprites for art 20, into Assets/Art/Items:
--   light_soft   48x48 white pool of light: stepped rings with a checker dither between them, so
--                light reads as pixel art rather than a smooth airbrush. Drawn additively in game.
--   light_glint  9x9 four-pointed sparkle for gems catching the light
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script art/tools/draw_light_art.lua
local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]
local pc = app.pixelColor

local function save(w, h, at, name)
  local img = Image(w, h, ColorMode.RGB)
  for y = 0, h - 1 do for x = 0, w - 1 do
    local a = at(x, y)
    if a > 0 then img:drawPixel(x, y, pc.rgba(255, 255, 255, a)) end
  end end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
  return img
end

-- five bands from the centre out; each boundary dithers half a band into the next
local BANDS = { 255, 190, 130, 80, 40 }
local soft = save(48, 48, function(x, y)
  local d = math.sqrt((x - 23.5) ^ 2 + (y - 23.5) ^ 2) / 24
  if d >= 1 then return 0 end
  local t = d * #BANDS + (((x + y) % 2 == 0) and 0.25 or -0.25)
  local band = math.floor(t) + 1
  if band > #BANDS then return 0 end
  return BANDS[math.max(1, band)]
end, "light_soft")

local GLINT = {
  "....#....",
  "....#....",
  "...+#+...",
  "..+###+..",
  "####@####",
  "..+###+..",
  "...+#+...",
  "....#....",
  "....#....",
}
local glint = save(9, 9, function(x, y)
  local c = GLINT[y + 1]:sub(x + 1, x + 1)
  return c == "@" and 255 or c == "#" and 200 or c == "+" and 90 or 0
end, "light_glint")

if previewDir then
  local k = 4
  local img = Image((48 + 4 + 9 + 4) * k, 52 * k, ColorMode.RGB)
  img:clear(P.hex("120e18"))
  local function put(src, ox, oy)
    for y = 0, src.height - 1 do for x = 0, src.width - 1 do
      local a = pc.rgbaA(src:getPixel(x, y))
      if a > 0 then
        local v = math.floor(0x12 + (255 - 0x12) * a / 255)
        for i = 0, k - 1 do for j = 0, k - 1 do img:drawPixel((ox + x) * k + i, (oy + y) * k + j, pc.rgba(v, math.floor(v * 0.9), math.floor(v * 0.7))) end end
      end
    end end
  end
  put(soft, 2, 2)
  put(glint, 54, 20)
  img:saveAs(previewDir .. "/light_preview.png")
end
