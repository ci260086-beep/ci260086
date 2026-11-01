-- Space art for the galaxy warp: the eight planets of phase3.json (48x48, planet_0 to planet_7,
-- in the same order as "planets") and the 320x180 background of the star map (star_map),
-- into Assets/Art/Items. Planets are lit from the upper left with a few hard bands of shade.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> [--script-param preview=<dir>] --script draw_space_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")
local previewDir = app.params["preview"]

local pc = app.pixelColor
local function hex(s, a)
  return pc.rgba(tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end
local INK, WHITE = hex("0e0c16"), hex("ffffff")

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
  return g
end

-- smooth value noise in [0, 1], wrapping every `period` cells so a planet's surface meets itself
local function hash(x, y, s)
  local n = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xffffffff
  n = ((n ~ (n >> 13)) * 1274126177) & 0xffffffff
  return (n & 0xffff) / 0xffff
end
local function noise(x, y, s, period)
  local x0, y0 = math.floor(x), math.floor(y)
  local fx, fy = x - x0, y - y0
  fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
  local function h(a, b) return hash(a % period, b, s) end
  local top = h(x0, y0) + (h(x0 + 1, y0) - h(x0, y0)) * fx
  local bot = h(x0, y0 + 1) + (h(x0 + 1, y0 + 1) - h(x0, y0 + 1)) * fx
  return top + (bot - top) * fy
end
local function fbm(x, y, s, period)
  return noise(x, y, s, period) * 0.6 + noise(x * 2, y * 2, s + 1, period * 2) * 0.3 + noise(x * 4, y * 4, s + 2, period * 4) * 0.1
end

local R, C = 21, 23.5 -- planet radius and centre inside the 48x48 frame

-- walks every pixel of the disc, handing the surface function its longitude/latitude, the
-- light level (0 dark to 1 lit) and the pixel; the function returns a colour
local function sphere(g, surface)
  for y = 0, 47 do
    for x = 0, 47 do
      local nx, ny = (x - C) / R, (y - C) / R
      local d = nx * nx + ny * ny
      if d <= 1 then
        local nz = math.sqrt(1 - d)
        local light = math.max(0, -0.55 * nx - 0.6 * ny + 0.58 * nz)
        local lon, lat = math.atan(nx, nz), math.asin(ny)
        g:set(x, y, surface(lon, lat, light, x, y, d))
      end
    end
  end
end

-- picks one of the shades by light level, dithering at each boundary
local function shade(ramp, light, x, y)
  local t = light * (#ramp - 0.01) + (((x + y) % 2 == 0) and 0.18 or -0.18)
  return ramp[math.max(1, math.min(#ramp, math.floor(t) + 1))]
end

local function ramp(list)
  local out = {}
  for i, s in ipairs(list) do out[i] = hex(s) end
  return out
end

local PLANETS = {
  -- 地球: blue oceans, green land, white cloud and a thin blue atmosphere
  function(g)
    local ocean = ramp({ "0e1e4a", "1e3a8a", "2e5ac8", "4a8ae8", "8ac0ff" })
    local land = ramp({ "1a3a12", "2e6a22", "4a9a2e", "76c03a", "b8e86a" })
    local cloud = ramp({ "6a7a98", "b8c8e0", "ffffff" })
    sphere(g, function(lon, lat, light, x, y)
      local u, v = (lon + math.pi) / (2 * math.pi) * 12, (lat + 1.6) * 3.5
      if fbm(u * 1.4 + 3, v * 1.4, 21, 17) > 0.62 then return shade(cloud, light, x, y) end
      if fbm(u, v, 7, 12) > 0.52 then return shade(land, light, x, y) end
      return shade(ocean, light, x, y)
    end)
  end,
  -- 月壤星: pale grey dust pitted with craters
  function(g)
    local dust = ramp({ "2a2a34", "4a4a58", "7a7a88", "a8a8b4", "d8d8e0" })
    local craters = { { -0.35, -0.3, 0.22 }, { 0.3, 0.1, 0.16 }, { -0.1, 0.45, 0.13 }, { 0.45, -0.45, 0.1 }, { -0.6, 0.2, 0.09 } }
    sphere(g, function(lon, lat, light, x, y)
      local nx, ny = (x - C) / R, (y - C) / R
      for _, c in ipairs(craters) do
        local d = math.sqrt((nx - c[1]) ^ 2 + (ny - c[2]) ^ 2)
        if d < c[3] then
          local rimSide = (nx - c[1]) + (ny - c[2])
          return shade(dust, math.max(0, light + (rimSide < 0 and -0.35 or 0.1)), x, y)
        elseif d < c[3] + 0.04 then
          return shade(dust, math.min(1, light + 0.25), x, y)
        end
      end
      local grit = (fbm(lon * 4, lat * 4, 31, 26) - 0.5) * 0.3
      return shade(dust, math.max(0, math.min(1, light + grit)), x, y)
    end)
  end,
  -- 熔岩星: black crust split by glowing rivers of lava, brightest where it is dark
  function(g)
    local crust = ramp({ "120a0e", "24141a", "3e2228", "5a3436" })
    local lava = ramp({ "a82a10", "e0501e", "ff8a2a", "ffd060" })
    sphere(g, function(lon, lat, light, x, y)
      local u, v = (lon + math.pi) / (2 * math.pi) * 10, (lat + 1.6) * 3
      local n = fbm(u, v, 41, 10)
      if math.abs(n - 0.5) < 0.045 then return shade(lava, 1 - light * 0.5, x, y) end
      if math.abs(n - 0.5) < 0.07 then return lava[1] end
      return shade(crust, light, x, y)
    end)
  end,
  -- 富矿星: brown rock shot through with gold and gem veins that glint
  function(g)
    local rock = ramp({ "2a1a12", "4a3122", "6b4630", "8c5f3f", "b8865a" })
    local gold, gems = hex("ffd24a"), { hex("e84a6a"), hex("4ae0c0"), hex("7a8aff") }
    sphere(g, function(lon, lat, light, x, y)
      local u, v = (lon + math.pi) / (2 * math.pi) * 14, (lat + 1.6) * 4
      local n = fbm(u, v, 51, 14)
      if math.abs(n - 0.55) < 0.03 then return light > 0.35 and gold or hex("a8701a") end
      if hash(x, y, 9) < 0.025 and light > 0.2 then return gems[(x + y) % 3 + 1] end
      return shade(rock, light, x, y)
    end)
  end,
  -- 碎晶星: violet crystal facets, with shards standing proud of the edge and a ring of debris
  function(g)
    local glassR = ramp({ "1e1040", "3a2070", "6a3ab0", "a070e8", "d8c0ff" })
    sphere(g, function(lon, lat, light, x, y)
      local facet = math.floor((lon + math.pi) * 3) + math.floor((lat + 2) * 3) * 7
      local tilt = (hash(facet, 0, 61) - 0.5) * 0.5
      return shade(glassR, math.max(0, math.min(1, light + tilt)), x, y)
    end)
    for _, s in ipairs({ { 8, 10, -1, -1 }, { 40, 12, 1, -1 }, { 6, 34, -1, 1 }, { 41, 37, 1, 1 } }) do
      for i = 0, 4 do
        g:set(s[1] + s[3] * i * 0.6, s[2] + s[4] * i, i < 2 and hex("d8c0ff") or hex("a070e8"))
        g:set(s[1] + s[3] * i * 0.6 + 1, s[2] + s[4] * i, hex("6a3ab0"))
      end
    end
    for i = 0, 127 do
      local a = i / 128 * 2 * math.pi
      for w = 0, 1 do
        local x, y = C + math.cos(a) * (23 - w), C + math.sin(a) * (7 - w) + 2
        local behind = math.sin(a) < 0 and ((x - C) / R) ^ 2 + ((y - C) / R) ^ 2 <= 1
        if not behind then g:set(x, y, (i % 7 == 0) and WHITE or (w == 0 and hex("e8d8ff") or hex("a070e8"))) end
      end
    end
  end,
  -- 雷暴星: violet-black storm bands wrapped round the globe, lit by forks of lightning
  function(g)
    local cloud = ramp({ "120c1e", "261840", "44306e", "6e4ca0", "b496e0" })
    local bolt, glow = hex("fff6a8"), hex("c8a8ff")
    sphere(g, function(lon, lat, light, x, y)
      local u, v = (lon + math.pi) / (2 * math.pi) * 10, (lat + 1.6) * 4
      -- bands sheared by latitude so the storm looks like it is turning
      local band = fbm(u + lat * 2.5, v * 1.6, 91, 10)
      local swirl = math.sin(v * 2.4 + band * 6) * 0.22
      return shade(cloud, math.max(0, math.min(1, light * 0.8 + swirl + (band - 0.5) * 0.5)), x, y)
    end)
    -- two forked bolts on the lit side, each a jagged walk with a glow halo
    for _, b in ipairs({ { 15, 9, 1, 14, 91 }, { 27, 20, -1, 12, 92 } }) do
      local x, y = b[1], b[2]
      for i = 0, b[4] do
        local nx = x + (hash(i, 0, b[5]) < 0.5 and -1 or 1) * (i % 2) + b[3] * (i % 3 == 0 and 1 or 0)
        for _, d in ipairs({ { -1, 0 }, { 2, 0 }, { 0, -1 }, { 1, -1 } }) do
          local hx, hy = math.floor(nx + d[1]), math.floor(y + d[2])
          if g:get(hx, hy) and g:get(hx, hy) ~= bolt then g:set(hx, hy, glow) end
        end
        g:set(nx, y, bolt)
        g:set(nx + 1, y, bolt) -- two pixels wide so it reads at 48x48
        if i == 6 then -- a short fork
          for j = 1, 3 do g:set(nx + j * b[3] * -1, y + j, bolt) end
        end
        x, y = nx, y + 1
      end
    end
  end,
  -- 永冻星: an ice-blue shell cracked into plates, frost bright at the poles
  function(g)
    local ice = ramp({ "1a3a5a", "2e6a96", "5aa0d0", "9ad4f0", "e0f6ff" })
    local crack = hex("0e2a46")
    sphere(g, function(lon, lat, light, x, y)
      local u, v = (lon + math.pi) / (2 * math.pi) * 9, (lat + 1.6) * 3
      local n = fbm(u, v, 101, 9)
      local m = fbm(u * 1.7 + 5, v * 1.7, 103, 15)
      if math.abs(n - 0.5) < 0.02 or (math.abs(m - 0.52) < 0.012 and light > 0.3) then
        return light > 0.55 and hex("2e6a96") or crack
      end
      local polar = math.abs(lat) > 1.05 and 0.3 or 0
      return shade(ice, math.max(0, math.min(1, light + polar + (n - 0.5) * 0.2)), x, y)
    end)
    -- glints of frost on the lit limb
    for _, p in ipairs({ { 12, 13 }, { 17, 9 }, { 10, 20 } }) do
      g:set(p[1], p[2], WHITE); g:set(p[1] - 1, p[2], hex("c8eeff")); g:set(p[1] + 1, p[2], hex("c8eeff"))
      g:set(p[1], p[2] - 1, hex("c8eeff")); g:set(p[1], p[2] + 1, hex("c8eeff"))
    end
  end,
  -- 潮汐星: a deep blue ocean world with bright bands of waves rolling round it
  function(g)
    local sea = ramp({ "081838", "0e2e66", "1a4e9a", "2e78c8", "6ab4f0" })
    local foam = ramp({ "4a8ac0", "a8dcff", "ffffff" })
    sphere(g, function(lon, lat, light, x, y)
      local wave = math.sin(lat * 9 + math.sin(lon * 3) * 1.4)
      local ripple = fbm((lon + math.pi) * 2, (lat + 1.6) * 3, 111, 13)
      if wave > 0.82 then return shade(foam, light, x, y) end
      local deep = (wave + 1) / 2 * 0.25 + (ripple - 0.5) * 0.2
      return shade(sea, math.max(0, math.min(1, light * 0.9 + deep)), x, y)
    end)
    -- a small moon pulling the tide, upper right
    for y = 3, 9 do for x = 37, 43 do
      local d = (x - 40) ^ 2 + (y - 6) ^ 2
      if d <= 9 then g:set(x, y, d <= 2 and hex("e8e8f0") or ((x + y) % 2 == 0 and hex("b4b4c2") or hex("8a8a98"))) end
    end end
  end,
}

local function outline(g)
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) and (g:get(x - 1, y) or g:get(x + 1, y) or g:get(x, y - 1) or g:get(x, y + 1)) then
        out[#out + 1] = { x, y }
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], INK) end
end

local function save(g, name)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/Items/" .. name .. ".png")
end

local planets = {}
for i, draw in ipairs(PLANETS) do
  local g = canvas(48, 48)
  draw(g)
  outline(g)
  save(g, "planet_" .. (i - 1))
  planets[i] = g
end

---------------------------------------------------------------------------------------------------
-- the star map: deep violet space, a soft diagonal band of stars, two nebula clouds and a
-- scatter of stars in three brightnesses; quiet enough for the constellation nodes on top
---------------------------------------------------------------------------------------------------
local map = canvas(320, 180)
local SPACE = ramp({ "0a0a1a", "10102a", "18163a", "22204e" })
local NEBULA = { { 70, 50, 70, 40, hex("2e1a46"), hex("4a2468") }, { 250, 130, 80, 45, hex("142e4a"), hex("1c4064") } }
for y = 0, 179 do
  for x = 0, 319 do
    local band = math.abs((y - 90) - (x - 160) * -0.35) / 60
    local t = math.max(0, 1 - band) * 0.7 + fbm(x / 40, y / 40, 71, 8) * 0.3
    local c = SPACE[math.max(1, math.min(4, math.floor(t * 4 + (((x + y) % 2 == 0) and 0.25 or -0.25)) + 1))]
    for _, n in ipairs(NEBULA) do
      local d = ((x - n[1]) / n[3]) ^ 2 + ((y - n[2]) / n[4]) ^ 2
      local cloud = fbm(x / 18, y / 18, 81, 20)
      local edge = 0.4 + d * 0.35
      if d < 1 and cloud > edge then
        local depth = cloud - edge
        if depth > 0.22 and (x + y) % 2 == 0 then c = n[6]
        elseif depth > 0.08 and (x + y) % 2 == 0 then c = n[5]
        elseif x % 2 == 0 and y % 2 == 0 then c = n[5] end
      end
    end
    map:set(x, y, c)
  end
end
for i = 0, 520 do
  local x, y = math.floor(hash(i, 1, 91) * 320), math.floor(hash(i, 2, 91) * 180)
  local band = math.abs((y - 90) - (x - 160) * -0.35) < 40
  local roll = hash(i, 3, 91)
  if band or roll < 0.55 then
    if roll < 0.04 then
      map:set(x, y, WHITE)
      map:set(x - 1, y, hex("8ab0ff")); map:set(x + 1, y, hex("8ab0ff"))
      map:set(x, y - 1, hex("8ab0ff")); map:set(x, y + 1, hex("8ab0ff"))
    elseif roll < 0.25 then
      map:set(x, y, hex("c8d0f0"))
    else
      map:set(x, y, hex("5a6a9a"))
    end
  end
end
save(map, "star_map")

-- preview: the star map at 3x with the planets laid over it in two rows
if previewDir then
  local k = 3
  local img = Image(320 * k, 180 * k, ColorMode.RGB)
  for y = 0, 179 do for x = 0, 319 do
    local c = map:get(x, y)
    for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel(x * k + a, y * k + b, c) end end
  end end
  for i, g in ipairs(planets) do
    local ox, oy = 14 + ((i - 1) % 5) * 60, (i <= 5) and 30 or 100
    for y = 0, 47 do for x = 0, 47 do
      local c = g:get(x, y)
      if c and pc.rgbaA(c) > 0 then
        for a = 0, k - 1 do for b = 0, k - 1 do img:drawPixel((ox + x) * k + a, (oy + y) * k + b, c) end end
      end
    end end
  end
  img:saveAs(previewDir .. "/space_preview.png")
end
