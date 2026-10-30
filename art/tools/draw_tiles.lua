-- Draws terrain tiles, ore overlays and a mock-up scene.
-- Run: Aseprite.exe -b --script-param dir=<art dir> --script draw_tiles.lua
--
-- terrain.png, 32x32 grid, 3 columns:
--   rows 0-3  soil, stone, basalt, abyss (3 seamless variants each)
--   row 4     dug-out back wall, grass-topped soil
-- ores.png, 32x32 grid, transparent overlays placed on any terrain tile:
--   coal, copper, iron, gold, ruby, void crystal
local TS = 32
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<art dir>")

local function hex(s)
  return app.pixelColor.rgba(
    tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), 255)
end

local seed = 20261030
local function rnd()
  seed = (seed * 1103515245 + 12345) % 2147483648
  return seed / 2147483648
end

local function pal(hi, mid, lo, dk, glow)
  return { hi = hex(hi), mid = hex(mid), lo = hex(lo), dk = hex(dk), glow = glow and hex(glow) }
end

local STRATA = {
  { p = pal("c98a55", "a0643a", "7a4a2e", "4f2f22"), cells = 15, specks = 14, soft = true },
  { p = pal("a9a6b5", "7f7e93", "5c5b73", "3b3a52"), cells = 7, specks = 8 },
  { p = pal("5d6079", "40435c", "2c2e45", "1a1b2e"), cells = 5, specks = 6 },
  { p = pal("5a3f7a", "3a2757", "261a40", "140e26", "d45cff"), cells = 6, specks = 5 },
}
local WALL = { p = pal("2a2433", "211c2b", "1a1623", "120f19"), cells = 6, specks = 0, soft = true }

local function wrap(d)
  if d > TS / 2 then return d - TS end
  if d < -TS / 2 then return d + TS end
  return d
end

-- Seamless rock: wrapped Voronoi cells, lit from the top left.
local function rockTile(def)
  local p, pts = def.p, {}
  for i = 1, def.cells do
    pts[i] = { x = rnd() * TS, y = rnd() * TS, c = rnd() < 0.6 and p.mid or p.lo }
  end
  local img = Image(TS, TS, ColorMode.RGB)
  for y = 0, TS - 1 do
    for x = 0, TS - 1 do
      local d1, d2, i1, i2 = 1e9, 1e9, 0, 0
      for i, q in ipairs(pts) do
        local dx, dy = wrap(x - q.x), wrap(y - q.y)
        local d = math.sqrt(dx * dx + dy * dy)
        if d < d1 then d2, i2 = d1, i1; d1, i1 = d, i
        elseif d < d2 then d2, i2 = d, i end
      end
      local q = pts[i1]
      local c = q.c
      local edge = d2 - d1
      local facing = wrap(x - q.x) + wrap(y - q.y)
      if edge < 1.1 then
        c = def.soft and p.lo or p.dk
        if p.glow and (i1 + i2) % 3 == 0 and edge < 0.8 then c = p.glow end
      elseif edge < 2.6 and facing < 0 then
        c = p.hi
      elseif edge < 2.6 and facing > 0 and not def.soft then
        c = p.lo
      end
      img:drawPixel(x, y, c)
    end
  end
  for _ = 1, def.specks do
    local x, y = math.floor(rnd() * TS), math.floor(rnd() * TS)
    img:drawPixel(x, y, rnd() < 0.5 and p.hi or p.dk)
    img:drawPixel((x + 1) % TS, y, p.dk)
  end
  return img
end

local function surfaceTile()
  local img = rockTile(STRATA[1])
  local hi, mid, lo = hex("b6d53c"), hex("6fae3b"), hex("3f7a3a")
  for x = 0, TS - 1 do
    -- periodic in x so grass tiles join up
    local h = 4 + math.floor(2.2 * math.sin(x / TS * 6.283 * 3) + 1.4 * math.sin(x / TS * 6.283 * 7))
    for y = 0, h do
      img:drawPixel(x, y, y == 0 and hi or (y < h - 1 and mid or lo))
    end
  end
  return img
end

local function ring(g, dk)
  local out = {}
  for y = 0, TS - 1 do
    for x = 0, TS - 1 do
      if not g[y * TS + x] then
        local near = (x > 0 and g[y * TS + x - 1]) or (x < TS - 1 and g[y * TS + x + 1])
          or (y > 0 and g[(y - 1) * TS + x]) or (y < TS - 1 and g[(y + 1) * TS + x])
        if near then out[y * TS + x] = true end
      end
    end
  end
  for k in pairs(out) do g[k] = dk end
end

local function oreTile(def)
  local p, g, placed = def.p, {}, {}
  local tries = 0
  while #placed < def.count and tries < 200 do
    tries = tries + 1
    local r = def.rmin + rnd() * (def.rmax - def.rmin)
    local cx, cy = 7 + rnd() * 18, 7 + rnd() * 18
    local ok = true
    for _, q in ipairs(placed) do
      local dx, dy = cx - q.x, cy - q.y
      if math.sqrt(dx * dx + dy * dy) < r + q.r + 2.5 then ok = false end
    end
    if ok then placed[#placed + 1] = { x = cx, y = cy, r = r } end
  end
  for _, q in ipairs(placed) do
    local ph = rnd() * 6.283
    for y = 2, TS - 3 do
      for x = 2, TS - 3 do
        local dx, dy = x - q.x, y - q.y
        local inside, nx
        if def.gem then
          -- upright shard with a light left face and a dark right face
          inside = math.abs(dx) / (q.r * 0.8) + math.abs(dy) / (q.r * 1.5) <= 1
          nx = dx < 0 and -1 or 1
          if dy < -q.r * 0.5 then nx = nx * 0.2 end
        else
          local a = math.atan(dy, dx)
          local rr = q.r * (1 + 0.18 * math.sin(a * 3 + ph))
          inside = math.sqrt(dx * dx + dy * dy) <= rr
          nx = (dx + dy) / q.r
        end
        if inside then
          local c = p.mid
          if nx < -0.5 then c = p.hi elseif nx > 0.4 then c = p.lo end
          g[y * TS + x] = c
        end
      end
    end
    g[math.floor(q.y - q.r * 0.4) * TS + math.floor(q.x - q.r * 0.3)] = def.shine
  end
  ring(g, p.dk)
  local img = Image(TS, TS, ColorMode.RGB)
  for k, c in pairs(g) do img:drawPixel(k % TS, k // TS, c) end
  return img
end

local WHITE = hex("ffffff")
local ORES = {
  { p = pal("6b6b7a", "3a3a46", "24242e", "101015"), count = 7, rmin = 2, rmax = 3.6, shine = hex("9a9aa8") },
  { p = pal("ffb98a", "e07b45", "a84a2a", "5a2418"), count = 6, rmin = 2, rmax = 3.4, shine = WHITE },
  { p = pal("e6eaf0", "b0b8c8", "7884a0", "3a4060"), count = 6, rmin = 2, rmax = 3.4, shine = WHITE },
  { p = pal("fff2a0", "ffc933", "d18a1c", "6e3f10"), count = 5, rmin = 2, rmax = 3.2, shine = WHITE },
  { p = pal("ffb0c0", "f0345a", "a31440", "4f0a28"), count = 3, rmin = 4, rmax = 6, gem = true, shine = WHITE },
  { p = pal("f4d0ff", "c05cff", "7a2bd0", "2c0f5e"), count = 3, rmin = 5, rmax = 7, gem = true, shine = WHITE },
}

local function saveSheet(name, sheet)
  local spr = Sprite(sheet.width, sheet.height, ColorMode.RGB)
  spr:newCel(spr.layers[1], 1, sheet, Point(0, 0))
  spr:saveAs(dir .. "/sprites/" .. name .. ".aseprite")
  spr:saveCopyAs(dir .. "/sprites/" .. name .. ".png")
end

-- terrain sheet
local terrain = Image(TS * 3, TS * 5, ColorMode.RGB)
local tiles = {}
for row, def in ipairs(STRATA) do
  tiles[row] = {}
  for col = 1, 3 do
    tiles[row][col] = rockTile(def)
    terrain:drawImage(tiles[row][col], Point((col - 1) * TS, (row - 1) * TS))
  end
end
local wallTile, grassTile = rockTile(WALL), surfaceTile()
terrain:drawImage(wallTile, Point(0, 4 * TS))
terrain:drawImage(grassTile, Point(TS, 4 * TS))
saveSheet("terrain", terrain)

-- ore sheet
local oreImgs = {}
local oreSheet = Image(TS * #ORES, TS, ColorMode.RGB)
for i, def in ipairs(ORES) do
  oreImgs[i] = oreTile(def)
  oreSheet:drawImage(oreImgs[i], Point((i - 1) * TS, 0))
end
saveSheet("ores", oreSheet)

-- mock-up: a shaft cut through every stratum
local COLS, ROWS, SHAFT = 12, 13, 6
local scene = Image(COLS * TS, ROWS * TS, ColorMode.RGB)
local sky = { hex("2b2350"), hex("4a2f6b"), hex("8a4378"), hex("d9705e"), hex("f4b267") }
for y = 0, 2 * TS - 1 do
  local band = y / (2 * TS) * (#sky - 1)
  for x = 0, COLS * TS - 1 do
    local b = math.floor(band + ((x + y) % 2 == 0 and 0.25 or 0))
    scene:drawPixel(x, y, sky[math.min(b, #sky - 2) + 1])
  end
end
for x = 0, COLS * TS - 1 do
  local h = 2 * TS - 18 - math.floor(9 * math.sin(x / 41) + 6 * math.sin(x / 17))
  for y = h, 2 * TS - 1 do scene:drawPixel(x, y, hex("3a2a55")) end
end

local bandFor = { 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4 }
for row = 2, ROWS - 1 do
  local band = bandFor[row - 1]
  for col = 0, COLS - 1 do
    local t = (row == 2) and grassTile or tiles[band][(col * 7 + row * 5) % 3 + 1]
    if col == SHAFT and row <= 9 then t = wallTile end
    scene:drawImage(t, Point(col * TS, row * TS))
  end
end

local veins = {
  { 3, 2, 1 }, { 3, 9, 1 }, { 4, 3, 2 }, { 4, 4, 2 }, { 5, 8, 3 }, { 6, 2, 3 }, { 6, 10, 2 },
  { 7, 4, 4 }, { 8, 8, 4 }, { 8, 9, 4 }, { 9, 2, 5 }, { 10, 8, 5 }, { 11, 4, 6 }, { 11, 5, 6 }, { 12, 9, 6 },
}
for _, v in ipairs(veins) do
  scene:drawImage(oreImgs[v[3]], Point(v[2] * TS, v[1] * TS))
end

-- darken the shaft edges so it reads as a hole in the rock
local edge = hex("0d0a14")
for y = 3 * TS, 10 * TS - 1 do
  scene:drawPixel(SHAFT * TS, y, edge)
  scene:drawPixel(SHAFT * TS + 1, y, edge)
  scene:drawPixel((SHAFT + 1) * TS - 1, y, edge)
  scene:drawPixel((SHAFT + 1) * TS - 2, y, edge)
end

local drillSheet = Image { fromFile = dir .. "/sprites/drill_sheet.png" }
local drill = Image(drillSheet, Rectangle(0, 0, 32, 48))
scene:drawImage(drill, Point(SHAFT * TS, 10 * TS - 46))

scene:saveAs(dir .. "/preview/scene_mock.png")
print("done")
