-- Boss skill effects: one looping 4-frame strip per guardian, 32x32 per frame (128x32), into
-- Assets/Art/Layers/boss_skill_<n>.png. Effects that hit the drill are centred on a drill
-- standing in the middle (8x12, feet on row 21); effects on the guardian are centred on it.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_skill_fx.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local INK, WHITE = hex("1a1210"), hex("ffffff")
local S, FRAMES = 32, 4
local CX, CY = 15.5, 15.5

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c)
    x, y = math.floor(x + 0.5), math.floor(y + 0.5)
    if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end
  end
  function g:get(x, y)
    if x < 0 or y < 0 or x >= w or y >= h then return nil end
    return self.px[y * w + x]
  end
  function g:line(x0, y0, x1, y1, c)
    local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0), 1)
    for i = 0, n do self:set(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n, c) end
  end
  function g:dot(x, y, r, c)
    for dy = -r, r do
      for dx = -r, r do
        if dx * dx + dy * dy <= r * r + r * 0.6 then self:set(x + dx, y + dy, c) end
      end
    end
  end
  function g:ring(cx, cy, rx, ry, c, steps)
    steps = steps or 96
    for i = 0, steps - 1 do
      local a = i / steps * 2 * math.pi
      self:set(cx + math.cos(a) * rx, cy + math.sin(a) * ry, c)
    end
  end
  return g
end

-- dark rim on the outside of an effect so it reads on any ground
local function rim(g, ink)
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) then
        local n = g:get(x - 1, y) or g:get(x + 1, y) or g:get(x, y - 1) or g:get(x, y + 1)
        if n and pc.rgbaA(n) == 255 then out[#out + 1] = { x, y } end
      end
    end
  end
  for _, p in ipairs(out) do g.px[p[2] * g.w + p[1]] = ink end
end

local function zigzag(g, pts, c)
  for i = 1, #pts - 1 do g:line(pts[i][1], pts[i][2], pts[i + 1][1], pts[i + 1][2], c) end
end

local TAU = 2 * math.pi

-- f runs 0..3; every effect returns to frame 0 after frame 3
local EFFECTS = {
  -- 0 巨型木根 缠绕根须: roots wound round the drill, squeezing and slackening
  function(g, f)
    local root, hi, leaf = hex("8f5a30"), hex("c8935a"), hex("76a83a")
    local squeeze = ({ 0, 1, 2, 1 })[f + 1]
    for band = 0, 2 do
      local y0 = 11 + band * 4
      local half = 6 - squeeze + (band == 1 and 1 or 0)
      for x = -half, half do
        local y = y0 + math.floor(x * 0.35 + 0.5)
        g:set(CX + x, y, root); g:set(CX + x, y + 1, (x + f) % 3 == 0 and hi or root)
      end
    end
    g:line(4, 30, 9, 22, root); g:line(5, 30, 10, 22, root)
    g:line(27, 30, 22, 22, root); g:line(26, 30, 21, 22, root)
    g:set(CX - 7 + squeeze, 9 + f % 2, leaf); g:set(CX + 7 - squeeze, 20 - f % 2, leaf)
  end,

  -- 1 岩甲虫 缩壳: two golden arcs close over the guardian and flash where they meet
  function(g, f)
    local gold, light = hex("e8c860"), hex("fff4c0")
    local open = ({ 1.3, 0.8, 0.3, 0.8 })[f + 1]
    for side = -1, 1, 2 do
      for i = 0, 40 do
        local a = math.pi / 2 + side * (open + i / 40 * (math.pi - open - 0.1))
        local x, y = CX + math.cos(a) * 13, CY - math.sin(a) * 11 + 4
        g:set(x, y, gold); g:set(x, y + 1, i % 4 == 0 and light or gold)
      end
    end
    if f == 2 then
      g:dot(CX, CY - 7, 2, WHITE)
      for _, d in ipairs({ { -5, 0 }, { 5, 0 }, { 0, -4 } }) do g:set(CX + d[1], CY - 7 + d[2], light) end
    end
  end,

  -- 2 水晶巨像 晶棱折射: five crystal shards circling the drill; a fifth of a turn every four frames
  function(g, f)
    local mid, hi, lo = hex("5ac8e0"), hex("c8fbff"), hex("2a8ab8")
    for i = 0, 4 do
      local a = i * TAU / 5 + f * TAU / 20
      local x, y = CX + math.cos(a) * 12, CY + math.sin(a) * 9
      g:set(x, y - 2, hi); g:set(x - 1, y - 1, hi); g:set(x, y - 1, mid); g:set(x + 1, y - 1, mid)
      g:set(x - 1, y, mid); g:set(x, y, mid); g:set(x + 1, y, lo); g:set(x, y + 1, lo)
      if i == f % 5 then g:set(x + 2, y - 2, WHITE) end
    end
  end,

  -- 3 盲眼巨鱼 掀起水流: a current sweeps sideways through the drill, throwing spray
  function(g, f)
    local water, light, foam = hex("2a6ab0"), hex("5aa8e8"), hex("c8f0ff")
    for row = 0, 2 do
      local y0 = 10 + row * 5
      for x = 0, S - 1 do
        local y = y0 + math.floor(1.5 * math.sin((x + f * 2) * TAU / 8) + 0.5)
        g:set(x, y, row == 1 and light or water)
        if (x + f * 2) % 8 == 2 then g:set(x, y - 1, foam) end
      end
    end
    for i = 0, 3 do g:set(26 + (i + f) % 4, 6 + ((i * 3 + f) % 4), foam) end
  end,

  -- 4 石像守卫 石化凝视: a coat of grey stone creeps up the drill from its feet, cracks running ahead
  function(g, f)
    local stone, light, dark = hex("8a8478"), hex("c8c0b0"), hex("4a463e")
    local top = 21 - ({ 4, 7, 10, 7 })[f + 1]
    for y = top, 21 do
      for x = 11, 20 do
        if (x + y) % 2 == 0 or y > top + 2 then g:set(x, y, (x + y * 3) % 7 == 0 and light or stone) end
      end
    end
    local cracks = { { 13, 21, 12, top - 2 }, { 18, 21, 19, top - 3 } }
    for _, c in ipairs(cracks) do
      g:line(c[1], c[2], c[3], c[4], dark); g:line(c[1] + 1, c[2], c[3] + 1, c[4], dark)
      g:set(c[3], c[4], light)
    end
    g:set(15, top - 4, WHITE); g:set(16, top - 4, light)
  end,

  -- 5 花岗岩魔像 岩壳加固: flakes of rock fly in from the edges and settle on the guardian
  function(g, f)
    local chip, hi, trail = hex("a8989a"), hex("e8dcd8"), hex("ff8a2a")
    for i = 0, 5 do
      local a = i * TAU / 6 + 0.3
      local r = 14 - ((f + i) % 4) * 3
      local x, y = CX + math.cos(a) * r, CY + math.sin(a) * r * 0.8
      g:set(x, y, hi); g:set(x + 1, y, chip); g:set(x, y + 1, chip); g:set(x + 1, y + 1, chip)
      g:set(x + math.cos(a) * 2, y + math.sin(a) * 2, trail)
    end
    g:ring(CX, CY, 4, 3, hex("e8dcd8", 160), 24)
  end,

  -- 6 莫霍守门人 高压冲击: red pressure chevrons slam down onto the drill
  function(g, f)
    local red, hot = hex("e83a2a"), hex("ff9a7a")
    for k = 0, 2 do
      local y = (k * 7 + f * 2) % 21 + 1
      for d = 0, 8 do
        local c = d < 2 and hot or red
        g:set(CX - d, y + d * 0.5, c); g:set(CX + d, y + d * 0.5, c)
        g:set(CX - d, y + d * 0.5 + 1, red); g:set(CX + d, y + d * 0.5 + 1, red)
      end
    end
    g:line(6, 23, 25, 23, hot)
  end,

  -- 7 熔岩巨蛇 喷吐岩浆: globs of magma burst over the drill and splash down
  function(g, f)
    local magma, hot, crust = hex("f07a20"), hex("ffe27a"), hex("8a2a10")
    for i = 0, 3 do
      local t = (f + i) % 4
      local side = (i % 2 == 0) and -1 or 1
      local x = CX + side * (3 + t * 3)
      local y = 8 + t * t * 1.2 - (3 - t) * 0.5
      g:dot(x, y, t == 3 and 1 or 2, magma)
      g:set(x - 1, y - 1, hot)
      if t == 3 then g:set(x - 2, y + 2, crust); g:set(x + 2, y + 2, crust) end
    end
    g:dot(CX, 6, 1, hot)
  end,

  -- 8 相变之灵 相变: a shimmering half-seen veil; brightness pulses so the guardian seems to fade
  function(g, f)
    local alpha = ({ 200, 130, 70, 130 })[f + 1]
    local veil, spark = hex("5ad8c8", alpha), hex("e8ffff")
    for y = 2, 29 do
      for x = 4, 27 do
        local nx, ny = (x - CX) / 12, (y - CY) / 14
        local d = nx * nx + ny * ny
        if d <= 1 and d > 0.75 and (x + y + f) % 2 == 0 then g:set(x, y, veil) end
      end
    end
    for i = 0, 4 do
      local a = i * TAU / 5 + f * TAU / 20
      g:set(CX + math.cos(a) * 9, CY + math.sin(a) * 11, spark)
    end
  end,

  -- 9 地幔巨鲸 鲸波: a red wave rolls through the drill from the left
  function(g, f)
    local red, crest, foam = hex("c83a20"), hex("ff7a4a"), hex("ffc8a0")
    local cx = (f * 8 + 4) % 32
    for x = 0, S - 1 do
      local dx = ((x - cx + 16) % 32) - 16
      local h = math.max(0, 9 - dx * dx * 0.12)
      local top = math.floor(22 - h + 0.5)
      for y = top, 23 do g:set(x, y, y == top and crest or red) end
      if h > 6 and dx > 0 then g:set(x, top - 1, foam) end
    end
  end,

  -- 10 磁暴旋涡 磁暴: forked lightning crackles round the drill, a new shape every frame
  function(g, f)
    local arc, core = hex("8ae8ff"), WHITE
    local seed = f * 37 + 11
    local function r(n) seed = (seed * 1103515245 + 12345) % 2147483648; return (seed >> 16) % n end
    for b = 0, 2 do
      local x, y = 3 + r(6) + b * 9, 3
      local pts = { { x, y } }
      while y < 28 do
        y = y + 3 + r(3); x = x + r(5) - 2
        pts[#pts + 1] = { x, y }
      end
      zigzag(g, pts, b == f % 3 and core or arc)
    end
    g:set(CX - 6, CY, core); g:set(CX + 6, CY - 3, core)
  end,

  -- 11 地核心脏 心跳复苏: a heartbeat ring spreads out from the core and fades
  function(g, f)
    local colours = { hex("fff0b0"), hex("ffc860"), hex("e8803a"), hex("a83a20", 160) }
    local r = 3 + f * 4
    g:ring(CX, CY, r, r * 0.85, colours[f + 1])
    g:ring(CX, CY, r + 1, r * 0.85 + 1, colours[f + 1])
    if f == 0 then g:dot(CX, CY, 2, WHITE) end
    if f >= 1 then g:ring(CX, CY, r - 4, (r - 4) * 0.85, hex("ffc860", 140), 48) end
    g:set(CX - 1, CY - 1, hex("ff6a6a")); g:set(CX + 1, CY - 1, hex("ff6a6a")); g:set(CX, CY, hex("ff6a6a"))
  end,
}

local function save(g, path)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(path)
end

local strips = {}
for n = 0, 11 do
  local strip = canvas(S * FRAMES, S)
  for f = 0, FRAMES - 1 do
    local g = canvas(S, S)
    EFFECTS[n + 1](g, f)
    rim(g, INK)
    for k, c in pairs(g.px) do strip.px[(k // S) * strip.w + f * S + k % S] = c end
  end
  save(strip, dir .. "/Layers/boss_skill_" .. n .. ".png")
  strips[n] = strip
end

-- preview: one row per guardian, every frame 4x over dark ground with a standing drill behind
if previewDir then
  local k = 4
  local drill = Image { fromFile = dir .. "/Drill/drill_0_4.png" }
  local img = Image((S * FRAMES + FRAMES + 1) * k, (S + 1) * 12 * k + k, ColorMode.RGB)
  img:clear(hex("2a2630"))
  for n = 0, 11 do
    for f = 0, FRAMES - 1 do
      local ox, oy = 1 + f * (S + 1), 1 + n * (S + 1)
      for y = 0, S - 1 do
        for x = 0, S - 1 do
          local c = hex(((x * 7 + y * 13) % 9 == 0) and "2c2026" or "1e161c")
          local dx, dy = x - 12, y - 10
          if dx >= 0 and dx < 8 and dy >= 0 and dy < 12 then
            local d = drill:getPixel(dx, dy)
            if pc.rgbaA(d) > 0 then c = d end
          end
          local e = strips[n]:get(f * S + x, y)
          if e then
            local a = pc.rgbaA(e) / 255
            local function m(p, q) return math.floor(p + (q - p) * a + 0.5) end
            c = pc.rgba(m(pc.rgbaR(c), pc.rgbaR(e)), m(pc.rgbaG(c), pc.rgbaG(e)), m(pc.rgbaB(c), pc.rgbaB(e)))
          end
          for a2 = 0, k - 1 do for b = 0, k - 1 do img:drawPixel((ox + x) * k + a2, (oy + y) * k + b, c) end end
        end
      end
    end
  end
  img:saveAs(previewDir .. "/boss_skill_preview.png")
end
