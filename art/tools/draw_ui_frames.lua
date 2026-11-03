-- Nine-slice frames for the interface (art 21), into Assets/Art/Items. Drawn in greys so the game
-- can tint them with the colour each button or card already uses; the outline stays dark whatever
-- the tint. ArtImportRules gives every ui9_ sprite a 4-pixel border.
--   ui9_button  raised: light top and left edge, shaded bottom and right, rounded corners
--   ui9_inset   sunk: shaded top and left edge, for rows and cards that sit in a panel
--   ui9_panel   flat with a thin inner rim, for side panels and dialogs
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script art/tools/draw_ui_frames.lua
local P = dofile((DEEPDRILL_ROOT or "") .. "art/tools/palette.lua")
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]
local pc = app.pixelColor

local function grey(v) return pc.rgba(v, v, v, 255) end
local INK = P.INK
local S = 12

local function frame(name, edge)
  local img = Image(S, S, ColorMode.RGB)
  for y = 0, S - 1 do for x = 0, S - 1 do
    local corner = (x == 0 or x == S - 1) and (y == 0 or y == S - 1)
    if not corner then
      local outer = x == 0 or y == 0 or x == S - 1 or y == S - 1
      img:drawPixel(x, y, outer and INK or edge(x, y))
    end
  end end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
  return img
end

local made = {
  frame("ui9_button", function(x, y)
    if y == 1 or x == 1 then return grey(255) end          -- lit edge
    if y == S - 2 or x == S - 2 then return grey(150) end   -- shaded edge
    return grey(222)
  end),
  frame("ui9_inset", function(x, y)
    if y == 1 or x == 1 then return grey(170) end
    return grey(235)
  end),
  frame("ui9_panel", function(x, y)
    if y == 1 or x == 1 or y == S - 2 or x == S - 2 then return grey(200) end
    return grey(235)
  end),
}

if previewDir then
  -- each frame stretched to a 40x16 button, tinted brown, green and dark like the game does
  local tints = { { 0x6b, 0x46, 0x30 }, { 0x4a, 0x9a, 0x2e }, { 0x2e, 0x24, 0x22 } }
  local k, W, H = 4, 40, 16
  local out = Image((#made * (W + 4) + 4) * k, (#tints * (H + 4) + 4) * k, ColorMode.RGB)
  out:clear(P.hex("120e18"))
  for i, src in ipairs(made) do
    for j, t in ipairs(tints) do
      for y = 0, H - 1 do for x = 0, W - 1 do
        local sx = x < 4 and x or (x >= W - 4 and S - (W - x) or 4 + (x - 4) % 4)
        local sy = y < 4 and y or (y >= H - 4 and S - (H - y) or 4 + (y - 4) % 4)
        local c = src:getPixel(sx, sy)
        if pc.rgbaA(c) > 0 then
          local r, g, b = pc.rgbaR(c), pc.rgbaG(c), pc.rgbaB(c)
          if c ~= INK then r, g, b = r * t[1] // 255 * 2, g * t[2] // 255 * 2, b * t[3] // 255 * 2 end
          local col = pc.rgba(math.min(255, r), math.min(255, g), math.min(255, b))
          local ox, oy = 4 + (i - 1) * (W + 4) + x, 4 + (j - 1) * (H + 4) + y
          for a = 0, k - 1 do for bb = 0, k - 1 do out:drawPixel(ox * k + a, oy * k + bb, col) end end
        end
      end end
    end
  end
  out:saveAs(previewDir .. "/ui_frames_preview.png")
end
