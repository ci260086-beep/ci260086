-- Draws the 12 Earth strata, ore overlays, 20 drill types and rare items into Assets/Art.
-- Run: Aseprite.exe -b --script-param dir=<Assets/Art> --script draw_world_art.lua
local dir = app.params["dir"]
assert(dir, "pass --script-param dir=<Assets/Art>")

local function hex(s, a)
  return app.pixelColor.rgba(
    tonumber(s:sub(1, 2), 16), tonumber(s:sub(3, 4), 16), tonumber(s:sub(5, 6), 16), a or 255)
end

local seed = 7777
local function rnd(n)
  seed = (seed * 1103515245 + 12345) % 2147483648
  return (seed >> 16) % n
end

local function canvas(w, h)
  local g = { w = w, h = h, px = {} }
  function g:set(x, y, c)
    if x >= 0 and y >= 0 and x < w and y < h then self.px[y * w + x] = c end
  end
  function g:get(x, y) return self.px[y * w + x] end
  function g:rect(x0, y0, x1, y1, c)
    for y = y0, y1 do for x = x0, x1 do self:set(x, y, c) end end
  end
  function g:blit(o, ox, oy)
    for k, c in pairs(o.px) do self:set(k % o.w + (ox or 0), k // o.w + (oy or 0), c) end
  end
  return g
end

local function save(g, path)
  local img = Image(g.w, g.h, ColorMode.RGB)
  for k, c in pairs(g.px) do img:drawPixel(k % g.w, k // g.w, c) end
  img:saveAs(dir .. "/" .. path .. ".png")
end

local function outline(g, ink)
  local out = {}
  for y = 0, g.h - 1 do
    for x = 0, g.w - 1 do
      if not g:get(x, y) then
        local near = (x > 0 and g:get(x - 1, y)) or (x < g.w - 1 and g:get(x + 1, y))
          or (y > 0 and g:get(x, y - 1)) or (y < g.h - 1 and g:get(x, y + 1))
        if near then out[#out + 1] = { x, y } end
      end
    end
  end
  for _, p in ipairs(out) do g:set(p[1], p[2], ink) end
end

-- ink, dark, shade, base, light, bright; style picks how the ground is textured
local STRATA = {
  { "1a1210", "4a3122", "6b4630", "8c5f3f", "a87a52", "c49a6c", "speck" },
  { "2a1e14", "4a3520", "94703c", "b8904c", "d4ae62", "e8cc84", "band" },
  { "2a2a26", "5a5a4e", "9a9a84", "bcbca4", "d4d4be", "eaead6", "band" },
  { "1e1c22", "4a4650", "7a7480", "9c94a0", "b8aebc", "d8d0da", "speck" },
  { "0e1016", "20242e", "343a48", "485062", "5e687e", "7c8aa0", "crack" },
  { "14100e", "3a2a22", "5e3a2a", "804a30", "a86438", "d08a48", "crack" },
  { "0e1a10", "1e3a22", "2e5a32", "46804a", "66a85e", "9ad07a", "speck" },
  { "14102a", "2a2458", "423a8a", "5e54b4", "8278d4", "b0a8ee", "band" },
  { "200a14", "4a1226", "7a1a32", "a8283c", "d0483e", "f08a4a", "crack" },
  { "180808", "3a0e0e", "701a10", "a83010", "d85a14", "f89a28", "crack" },
  { "281404", "6a3a08", "b06a10", "e09a20", "f8c840", "fff0a0", "band" },
  { "28243a", "5a5478", "9a96b8", "c8c4dc", "e8e6f4", "ffffff", "speck" },
}

local function ground(p, style, n)
  local g = canvas(16, 16)
  g:rect(0, 0, 15, 15, p.base)
  if style == "band" then
    g:rect(0, 5, 15, 6, p.shade)
    g:rect(0, 13, 15, 13, p.shade)
  elseif style == "crack" then
    for i = 1, 3 do
      local x, y = rnd(16), rnd(16)
      for s = 0, 6 do
        g:set((x + s) % 16, (y + s // 2) % 16, p.shade)
      end
    end
  end
  for _ = 1, n do
    local x, y = rnd(16), rnd(16)
    local c = rnd(3) == 0 and p.light or p.shade
    g:set(x, y, c)
    if rnd(2) == 0 then g:set((x + 1) % 16, y, c) end
  end
  return g
end

for s, v in ipairs(STRATA) do
  local i = s - 1
  local p = { ink = hex(v[1]), dark = hex(v[2]), shade = hex(v[3]), base = hex(v[4]), light = hex(v[5]), bright = hex(v[6]) }
  local style = v[7]
  save(ground(p, style, 12), "Strata/ground_" .. i .. "_a")
  save(ground(p, style, 18), "Strata/ground_" .. i .. "_b")
  save(ground(p, style, 8), "Strata/ground_" .. i .. "_c")

  -- loose block: paler and grainy, so what will fall stands out from the rock
  local loose = canvas(16, 16)
  loose:rect(0, 0, 15, 15, p.light)
  for y = 0, 15 do
    for x = 0, 15 do
      local n = (x * 7 + y * 13 + (x * y) % 5) % 9
      if n == 0 then loose:set(x, y, p.base) elseif n == 4 then loose:set(x, y, p.bright) end
    end
  end
  loose:rect(0, 15, 15, 15, p.base)
  save(loose, "Strata/loose_" .. i)

  local function edge(name, line)
    local g = canvas(16, 16)
    for k = 0, 15 do line(g, k) end
    save(g, "Strata/edge_" .. i .. "_" .. name)
  end
  edge("top", function(g, k) g:set(k, 0, p.ink) g:set(k, 1, p.dark) end)
  edge("bottom", function(g, k) g:set(k, 15, p.ink) g:set(k, 14, p.bright) end)
  edge("left", function(g, k) g:set(0, k, p.ink) g:set(1, k, p.light) end)
  edge("right", function(g, k) g:set(15, k, p.ink) g:set(14, k, p.dark) end)
end

-- overlays laid over any ground tile
local ORES = {
  { "6a6a78", "3a3a44", "1e1e24" }, { "f6b98a", "e08a4a", "b8693a" }, { "e8e8f0", "8a8a96", "5a5a64" },
  { "fff4d8", "e8e8f0", "c0c0cc" }, { "9aa0b0", "6a7084", "464a5c" }, { "ffe27a", "e8b02a", "a87818" },
  { "f0f4ff", "c8d0e4", "98a2bc" }, { "a8d8e8", "6aa8c0", "3a7088" }, { "b0b8a8", "808878", "505848" },
  { "e8fbff", "90e8f8", "40b0d8" }, { "d0f070", "90c040", "508020" }, { "ff90c0", "e84080", "a01850" },
  { "d0b0ff", "9060e0", "5a30a0" }, { "b0ff70", "60d030", "288010" }, { "e0d8b0", "b0a880", "787050" },
  { "f4d0ff", "c05cff", "6a1ad0" },
}
local INK = hex("1a1210")
local LAYOUT = { { 2, 2, 3 }, { 9, 1, 2 }, { 11, 6, 3 }, { 4, 8, 3 }, { 9, 11, 2 }, { 1, 11, 2 } }
for i, c in ipairs(ORES) do
  local g = canvas(16, 16)
  local hi, mid, lo = hex(c[1]), hex(c[2]), hex(c[3])
  for _, n in ipairs(LAYOUT) do
    local x, y, s = n[1], n[2], n[3]
    g:rect(x, y, x + s - 1, y + s - 1, mid)
    g:set(x, y, hi) g:set(x + 1, y, hi) g:set(x, y + 1, hi)
    g:set(x + s - 1, y + s - 1, lo)
  end
  outline(g, INK)
  save(g, "Strata/ore_" .. (i - 1))

  local icon = canvas(8, 8)
  icon:rect(2, 1, 5, 6, mid) icon:rect(1, 2, 6, 5, mid)
  icon:rect(2, 2, 3, 3, hi) icon:rect(4, 4, 5, 5, lo)
  outline(icon, INK)
  save(icon, "Items/oreicon_" .. (i - 1))
end

local rock = canvas(16, 16)
local R1, R2, R3, R4 = hex("3a3a44"), hex("5c5c6a"), hex("8a8a98"), hex("b4b4c2")
for y = 2, 13 do
  for x = 2, 13 do
    local dx, dy = (x - 7.5) / 6, (y - 7.5) / 5.6
    local d = dx * dx + dy * dy
    if d <= 1 then
      local c = R3
      if d > 0.72 then c = (dx + dy < 0) and R4 or R1
      elseif dx + dy < -0.5 then c = R4
      elseif dx + dy > 0.6 then c = R2 end
      rock:set(x, y, c)
    end
  end
end
outline(rock, INK)
save(rock, "Strata/hard_overlay")

local chest = canvas(16, 16)
local W1, W2, W3, GOLD, GOLD_HI = hex("6b4630"), hex("b8693a"), hex("e08a4a"), hex("e8b02a"), hex("ffe27a")
chest:rect(2, 5, 13, 12, W2)
chest:rect(2, 5, 13, 5, W3)
chest:rect(2, 12, 13, 12, W1)
chest:rect(2, 8, 13, 8, INK)
chest:rect(4, 6, 4, 12, W1) chest:rect(11, 6, 11, 12, W1)
chest:rect(7, 7, 8, 10, GOLD) chest:set(7, 7, GOLD_HI)
outline(chest, INK)
save(chest, "Strata/chest_overlay")

-- neutral shells the game tints per guardian
local shell = canvas(16, 16)
shell:rect(0, 0, 15, 15, hex("9a9a9a"))
shell:rect(0, 0, 15, 2, hex("ffffff"))
shell:rect(0, 3, 15, 4, hex("c8c8c8"))
shell:rect(0, 12, 15, 13, hex("5a5a5a"))
shell:rect(0, 14, 15, 15, hex("3a3a3a"))
for x = 3, 15, 8 do shell:rect(x, 3, x, 11, hex("5a5a5a")) end
save(shell, "Strata/boss_shell")
local hide = canvas(16, 16)
hide:rect(0, 0, 15, 15, hex("b0b0b0"))
for y = 0, 15 do
  for x = 0, 15 do
    if (x + y * 3) % 7 == 0 then hide:set(x, y, hex("e0e0e0")) end
    if (x * 3 + y) % 11 == 0 then hide:set(x, y, hex("606060")) end
  end
end
save(hide, "Strata/boss_hide")

-- 20 small drills, 16x16, 4 frames: hull colours then cone colours
local TIERS = {
  { "a87a52", "8c5f3f", "6b4630", "c49a6c", "8c5f3f" }, { "b4b4c2", "8a8a98", "5c5c6a", "c0c0cc", "8a8a96" },
  { "f6b98a", "e08a4a", "b8693a", "f6b98a", "b8693a" }, { "c8ccd8", "8a90a0", "5a6070", "d8dce8", "8a90a0" },
  { "9ab0c8", "6a84a4", "465a78", "c8d4e4", "7a90b0" }, { "f4f8ff", "c4cede", "8894ac", "ffffff", "b0bccc" },
  { "ffe27a", "f2b92e", "c07f1e", "fff0a0", "e8b02a" }, { "e8f0ff", "b8c8e8", "8094c0", "f0f4ff", "a8b8d8" },
  { "a8d8e8", "6aa8c0", "3a7088", "d0f0f8", "6ab8d0" }, { "98a088", "6a7260", "464c40", "b8c0a8", "788070" },
  { "6a98f0", "3a68d8", "2040a0", "a0c0ff", "4070e0" }, { "d0f8ff", "90e0f0", "50b0d0", "ffffff", "a0e8f8" },
  { "ff90a8", "e8305a", "a01440", "ffc0d0", "e84070" }, { "90b0ff", "3050e8", "1830a0", "c0d0ff", "4060f0" },
  { "e8fbff", "a0e8f8", "60c0e8", "ffffff", "c0f4ff" }, { "f0a0ff", "c040f0", "7020b0", "ffd0ff", "d060ff" },
  { "ffb0a0", "ff5030", "c02010", "fff0c0", "ff8040" }, { "b0ffb0", "30e060", "108030", "e0ffe0", "60f080" },
  { "fff0a0", "f89a28", "d85a14", "ffffff", "ffd060" }, { "ffffff", "ffe27a", "f08a4a", "ffffff", "ff6040" },
}
local BODY = { "5e5e6a", "2a2a32" }
local GLASS = { hex("e8fbff"), hex("4fb8e8") }
for t, c in ipairs(TIERS) do
  local hi, mid, lo, coneHi, coneLo = hex(c[1]), hex(c[2]), hex(c[3]), hex(c[4]), hex(c[5])
  local sheet = canvas(64, 16)
  for f = 0, 3 do
    local g = canvas(16, 16)
    -- tracks
    for y = 2, 7 do
      local bar = (y + f) % 2 == 0
      g:set(2, y, bar and hex(BODY[2]) or hex(BODY[1]))
      g:set(13, y, bar and hex(BODY[2]) or hex(BODY[1]))
    end
    g:rect(3, 1, 12, 7, mid)
    g:rect(3, 1, 12, 1, hi)
    g:rect(3, 2, 4, 6, hi)
    g:rect(11, 2, 12, 7, lo)
    g:rect(4, 7, 11, 7, lo)
    g:rect(6, 3, 8, 5, GLASS[2])
    g:set(6, 3, GLASS[1])
    -- spiral cone
    for y = 8, 14 do
      local half = (14 - y) * 0.75 + 0.5
      for x = 0, 15 do
        local n = (x - 7.5) / half
        if math.abs(n) <= 1 then
          local groove = ((x + y * 2 + f * 2) % 4) < 1
          g:set(x, y, groove and coneLo or ((n < 0.2) and coneHi or coneLo))
        end
      end
    end
    outline(g, INK)
    sheet:blit(g, f * 16, 0)
  end
  save(sheet, "Drill/tier_" .. (t - 1))
end

-- rare items: 16x16 crystals and fossils, two per stratum, coloured by where they are found
for i = 0, 23 do
  local v = STRATA[i // 2 + 1]
  local hi, mid, lo = hex(v[6]), hex(v[5]), hex(v[3])
  local g = canvas(16, 16)
  if i % 2 == 0 then
    for y = 1, 14 do
      local half = 6 - math.abs(y - 7.5) * 0.85
      for x = 2, 13 do
        local n = x - 7.5
        if math.abs(n) <= half then g:set(x, y, n < -1 and hi or (n < 2 and mid or lo)) end
      end
    end
  else
    for y = 2, 13 do
      for x = 2, 13 do
        local d = math.sqrt((x - 7.5) ^ 2 + (y - 7.5) ^ 2)
        if d <= 5.8 then
          local ring = math.floor(d * 1.2 + math.atan(y - 7.5, x - 7.5) * 0.9) % 3
          g:set(x, y, ring == 0 and hi or (ring == 1 and mid or lo))
        end
      end
    end
  end
  outline(g, INK)
  save(g, "Items/rare_" .. i)
end

print("done")
