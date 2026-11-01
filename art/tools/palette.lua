-- Shared palette and outline rules for every 深渊钻头 art script (art 18). See art/PALETTE.md.
-- Load it from another script with:
--   local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
-- DEEPDRILL_ROOT is set by the in-Aseprite job runner; from the command line the scripts run in
-- the project root, so the relative path works on its own.
local P = {}
local pc = app.pixelColor

---------------------------------------------------------------------------------------------------
-- colour helpers
---------------------------------------------------------------------------------------------------
function P.hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end

local function rgb2hsv(r, g, b)
  r, g, b = r / 255, g / 255, b / 255
  local mx, mn = math.max(r, g, b), math.min(r, g, b)
  local d = mx - mn
  local h = 0
  if d > 0 then
    if mx == r then h = ((g - b) / d) % 6 elseif mx == g then h = (b - r) / d + 2 else h = (r - g) / d + 4 end
    h = h * 60
  end
  return h, mx == 0 and 0 or d / mx, mx
end

local function hsv2rgb(h, s, v)
  h = h % 360
  local c = v * s
  local x = c * (1 - math.abs((h / 60) % 2 - 1))
  local m = v - c
  local r, g, b
  if h < 60 then r, g, b = c, x, 0 elseif h < 120 then r, g, b = x, c, 0 elseif h < 180 then r, g, b = 0, c, x
  elseif h < 240 then r, g, b = 0, x, c elseif h < 300 then r, g, b = x, 0, c else r, g, b = c, 0, x end
  local function q(t) return math.max(0, math.min(255, math.floor((t + m) * 255 + 0.5))) end
  return q(r), q(g), q(b)
end

-- moves hue `h` towards `target` by at most `amount` degrees, the short way round
local function towards(h, target, amount)
  local d = ((target - h + 540) % 360) - 180
  if math.abs(d) <= amount then return target end
  return h + (d > 0 and amount or -amount)
end

P.SHADOW_HUE = 250 -- blue violet: shadows lean this way
P.LIGHT_HUE = 50   -- warm yellow: highlights lean this way

--- One step darker or lighter along the house hue shift. step < 0 is shadow, > 0 is light.
function P.shift(c, step)
  local h, s, v = rgb2hsv(pc.rgbaR(c), pc.rgbaG(c), pc.rgbaB(c))
  local grey = s < 0.08
  if step < 0 then
    for _ = 1, -step do
      if not grey then h = towards(h, P.SHADOW_HUE, 9) end
      s = math.min(1, s + (grey and 0.04 or 0.08))
      v = v * 0.72
    end
    if grey then h = P.SHADOW_HUE end
  else
    for _ = 1, step do
      if not grey then h = towards(h, P.LIGHT_HUE, 12) end
      s = math.max(0, s - 0.1)
      v = math.min(1, v + (1 - v) * 0.45 + 0.04)
    end
  end
  local r, g, b = hsv2rgb(h, s, v)
  return pc.rgba(r, g, b, pc.rgbaA(c))
end

--- A five-step ramp around a base colour: deep shadow, shadow, base, light, highlight.
function P.ramp(base)
  local b = type(base) == "string" and P.hex(base) or base
  return { P.shift(b, -2), P.shift(b, -1), b, P.shift(b, 1), P.shift(b, 2) }
end

--- Picks a shade of a ramp for a light level in [0, 1], with a checker dither on each boundary.
function P.shade(ramp, light, x, y)
  local t = light * (#ramp - 0.01) + (((x + y) % 2 == 0) and 0.18 or -0.18)
  return ramp[math.max(1, math.min(#ramp, math.floor(t) + 1))]
end

---------------------------------------------------------------------------------------------------
-- the house colours
---------------------------------------------------------------------------------------------------
P.INK = P.hex("1a1210")      -- the one outline for the world, drills, items and the interface
P.INK_SPACE = P.hex("0e0c16") -- the outline for things drawn against space (planets, star map)
P.WHITE = P.hex("ffffff")
P.GLINT = P.hex("fff6d8")     -- specular dots: warm white, never pure white on metal or gems

-- named material ramps (deep shadow .. highlight), all built from one base by P.ramp
P.RAMPS = {
  dirt = P.ramp("8c5f3f"),
  stone = P.ramp("8a8a98"),
  wood = P.ramp("6b4630"),
  iron = P.ramp("a8a8b8"),
  gold = P.ramp("e8b02a"),
  crystal = P.ramp("a070e8"),
  ice = P.ramp("5aa0d0"),
  leaf = P.ramp("4a9a2e"),
  lava = P.ramp("e0501e"),
  ui = P.ramp("4a3122"),
}

---------------------------------------------------------------------------------------------------
-- outlines
---------------------------------------------------------------------------------------------------
-- A canvas here is any table with get(x, y) and set(x, y, c) over w by h, as every script has.

--- Full outline in one ink colour. Use for icons of 16 pixels or less, where shape reads first.
function P.outline(g, ink)
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) and (g:get(x - 1, y) or g:get(x + 1, y) or g:get(x, y - 1) or g:get(x, y + 1)) then
        out[#out + 1] = { x, y }
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], ink or P.INK) end
end

--- Selective outline: ink on the shadow side (bottom and right), and on the lit side (top and left)
--- a dark shade of the colour it borders, so lit edges glow instead of looking cut out.
--- Use on sprites larger than 16 pixels: drills, guardians, planets, big pickups.
function P.selout(g, ink)
  ink = ink or P.INK
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) then
        local right, below = g:get(x + 1, y), g:get(x, y + 1)
        local left, above = g:get(x - 1, y), g:get(x, y - 1)
        if right or below or left or above then
          -- the fill this outline pixel sits above or to the left of is on the lit side
          local lit = right or below
          local shadowSide = left or above
          local c = ink
          if lit and not shadowSide and lit ~= ink then c = P.shift(lit, -2) end
          out[#out + 1] = { x, y, c }
        end
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], p[3]) end
end

return P
