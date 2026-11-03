"""Lays out the trailer logo's letters as masks for art/tools/draw_trailer_logo.lua.

The Chinese name is the game's pixel font scaled three times, the English name twice, so the
logo is built from the same letters as the game. Writes art/trailer/logo_cn_mask.png and
art/trailer/logo_en_mask.png: white where a letter is, transparent elsewhere.
Run from the project root: python3 tools/trailer/logo_mask.py
"""
from PIL import Image, ImageDraw, ImageFont

FONT = "Assets/Resources/Fonts/FusionPixel8.ttf"


def mask(text, scale, spacing):
    font = ImageFont.truetype(FONT, 8)
    # draw each character on its own so the gaps between them can be widened
    glyphs = []
    for ch in text:
        left, top, right, bottom = font.getbbox(ch)
        w = max(right, 3) if ch != " " else 4
        img = Image.new("L", (w, 12), 0)
        d = ImageDraw.Draw(img)
        d.fontmode = "1"
        d.text((0, 0), ch, font=font, fill=255)
        glyphs.append(img)
    width = sum(g.width for g in glyphs) + spacing * (len(glyphs) - 1)
    line = Image.new("L", (width, 12), 0)
    x = 0
    for g in glyphs:
        line.paste(g, (x, 0))
        x += g.width + spacing
    line = line.crop(line.getbbox())
    line = line.resize((line.width * scale, line.height * scale), Image.NEAREST)
    out = Image.new("RGBA", line.size, (0, 0, 0, 0))
    out.putalpha(line.point(lambda v: 255 if v > 127 else 0))
    white = Image.new("RGBA", line.size, (255, 255, 255, 255))
    out = Image.composite(white, out, out)
    return out


mask("挖了个爽", 3, 1).save("art/trailer/logo_cn_mask.png")
mask("DRILL THRILL", 2, 1).save("art/trailer/logo_en_mask.png")
