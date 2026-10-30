-- Draws the player drill rig (32x48, 6-frame spin) and saves it as .aseprite.
-- Run: Aseprite.exe -b --script-param out=<path.aseprite> --script draw_drill.lua
local W, H, FRAMES = 32, 48, 6
local CX = 15.5

local function hex(s)
  return app.pixelColor.rgba(
    tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), 255)
end

local C = {
  outline = hex("1a1423"),
  hullHi = hex("f4c15d"), hull = hex("d9822b"), hullLo = hex("9a4a26"), hullDk = hex("5e2c26"),
  steelHi = hex("c0cbdc"), steel = hex("8b9bb4"), steelLo = hex("5a6988"), steelDk = hex("3a4466"),
  glassHi = hex("e8fbff"), glass = hex("4fd6e8"), glassLo = hex("1f7f9e"),
  coneHi = hex("ffe9a8"), cone = hex("e0a93f"), coneLo = hex("a8652a"), coneDk = hex("64381f"),
  hotHi = hex("fff3c4"), hot = hex("ff9a3c"), hotLo = hex("d9472b"),
}

local function ramp(nx, hi, mid, lo)
  if nx < -0.45 then return hi end
  if nx < 0.4 then return mid end
  return lo
end

local function drawFrame(f)
  local g = {}
  local dy = f % 2 -- hull rattles while the bit spins
  local function set(x, y, c)
    if x >= 0 and y >= 0 and x < W and y < H then g[y * W + x] = c end
  end
  local function rect(x0, y0, x1, y1, c)
    for y = y0, y1 do for x = x0, x1 do set(x, y, c) end end
  end

  -- exhaust stacks
  for _, x in ipairs({ 8, 22 }) do
    rect(x, 1 + dy, x + 1, 4 + dy, C.steelLo)
    set(x, 1 + dy, C.steel)
    set(x, 2 + dy, C.steel)
  end

  -- side treads, bars crawl with the frame
  for _, x0 in ipairs({ 3, 26 }) do
    for y = 7, 21 do
      local bar = (y + f) % 3 == 0
      rect(x0, y + dy, x0 + 2, y + dy, bar and C.steelDk or (x0 == 3 and C.steel or C.steelLo))
    end
  end

  -- hull
  for y = 5, 22 do
    local inset = (y == 5 or y == 22) and 2 or ((y == 6 or y == 21) and 1 or 0)
    for x = 6 + inset, 25 - inset do
      -- curve the shade bands so the hull reads as round rather than three flat strips
      local nx = (x - CX) / 10 + ((y - 13) / 9) ^ 2 * 0.3
      local c = ramp(nx, C.hullHi, C.hull, C.hullLo)
      if y >= 20 then c = (nx < 0.4) and C.hullLo or C.hullDk end
      if y == 6 and nx < 0.4 then c = C.hullHi end
      set(x, y + dy, c)
    end
  end

  -- hazard band
  for y = 17, 19 do
    for x = 6, 25 do
      set(x, y + dy, (x + y) % 6 < 3 and C.outline or C.hullHi)
    end
  end

  -- rivets
  for _, p in ipairs({ { 8, 8 }, { 23, 8 }, { 8, 15 }, { 23, 15 } }) do
    set(p[1], p[2] + dy, C.hullDk)
    set(p[1] - 1, p[2] - 1 + dy, C.hullHi)
  end

  -- porthole
  for y = 6, 16 do
    for x = 10, 21 do
      local dx, dyy = x - CX, y - 11
      local d = math.sqrt(dx * dx + dyy * dyy)
      if d <= 5.4 then
        local c
        if d > 4.3 then
          c = (dx + dyy < 0) and C.steelHi or C.steelDk
        else
          c = (dx + dyy < -2) and C.glass or C.glassLo
          if dx + dyy < -4 then c = C.glassHi end
        end
        set(x, y + dy, c)
      end
    end
  end

  -- collar joining hull and bit (does not rattle)
  for y = 23, 26 do
    for x = 7, 24 do
      local nx = (x - CX) / 9
      local c = ramp(nx, C.steelHi, C.steel, C.steelLo)
      if y == 26 then c = C.steelDk end
      set(x, y, c)
    end
  end

  -- spinning cone with helical flute
  local top, tip = 27, 45
  for y = top, tip do
    local t = (y - top) / (tip - top)
    local hw = 8.6 * (1 - t) + 0.5
    for x = 0, W - 1 do
      local nx = (x - CX) / hw
      if math.abs(nx) <= 1 then
        local theta = math.asin(nx) / math.pi * 2
        local phase = (theta * 0.9 + y / 7 - f / FRAMES) % 1
        local groove = phase < 0.34
        local c
        if y >= tip - 4 then
          c = ramp(nx, C.hotHi, C.hot, C.hotLo)
          if groove then c = C.hotLo end
        elseif groove then
          c = ramp(nx, C.coneLo, C.coneLo, C.coneDk)
        else
          c = ramp(nx, C.coneHi, C.cone, C.coneLo)
        end
        set(x, y, c)
      end
    end
  end

  -- one pixel outline around the silhouette
  local out = {}
  for y = 0, H - 1 do
    for x = 0, W - 1 do
      if not g[y * W + x] then
        local near = (x > 0 and g[y * W + x - 1]) or (x < W - 1 and g[y * W + x + 1])
          or (y > 0 and g[(y - 1) * W + x]) or (y < H - 1 and g[(y + 1) * W + x])
        if near then out[y * W + x] = true end
      end
    end
  end
  for k in pairs(out) do g[k] = C.outline end

  local img = Image(W, H, ColorMode.RGB)
  for k, c in pairs(g) do img:drawPixel(k % W, k // W, c) end
  return img
end

local outPath = app.params["out"]
assert(outPath, "pass --script-param out=<path>")

local spr = Sprite(W, H, ColorMode.RGB)
local layer = spr.layers[1]
layer.name = "drill"
for f = 1, FRAMES do
  if f > 1 then spr:newEmptyFrame() end
  spr:newCel(layer, f, drawFrame(f - 1), Point(0, 0))
  spr.frames[f].duration = 0.07
end
spr:saveAs(outPath)
print("saved " .. outPath)
