"""Cuts the trailer together from the recorded clips.

Inputs (from the project root):
  Recordings/trailer/<clip>/f_00000.png   frames from DeepDrill > Trailer: Record, 1920x1080, 30 fps
  tools/trailer/timeline.json              clip order and length in bars, title cards, sound cues
  art/trailer/logo.png                     the logo in stage pixels, drawn by draw_trailer_logo.lua
  Recordings/trailer/audio.wav             the soundtrack, rendered by tools/trailer/audio
Output: Recordings/trailer/drill_thrill_trailer.mp4 (H.264, 1080p30, AAC)

Cards are set in the game's pixel font at stage resolution and scaled by whole numbers, so they
sit on the same pixel grid as the game. Run: python3 tools/trailer/make_trailer.py
"""
import json
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.getcwd()
REC = os.path.join(ROOT, "Recordings", "trailer")
# cards use the 12-pixel cut of the game's font (tools/trailer/fonts, SIL OFL): at 8 pixels some
# characters blur into others once they are blown up this large
FONT = ImageFont.truetype(os.path.join(ROOT, "tools/trailer/fonts/FusionPixel12.ttf"), 12)
CARD_MAX_W = 200                   # stage pixels a card line may span inside the dig area
W, H, PX = 1920, 1080, 6           # the stage is 320x180, six screen pixels to one
DIG_W = 222 * PX                   # cards centre on the dig area, left of the side panel
GOLD, WHITE, INK, SHADOW = (255, 226, 122), (244, 250, 255), (26, 18, 16), (26, 14, 36)

timeline = json.load(open(os.path.join(ROOT, "tools/trailer/timeline.json"), encoding="utf-8"))
fps = timeline["fps"]
bar = 240.0 / timeline["bpm"]


def text_mask(text):
    """The text in the pixel font at stage resolution, as a 1-bit mask."""
    left, top, right, bottom = FONT.getbbox(text)
    img = Image.new("L", (right + 2, 16), 0)
    d = ImageDraw.Draw(img)
    d.fontmode = "1"
    d.text((0, 0), text, font=FONT, fill=255)
    return img.crop(img.getbbox())


def styled(mask, color, scale):
    """A mask filled with a colour, ringed in ink and dropped onto a shadow, scaled by `scale`
    stage pixels and then by the screen's six."""
    m = mask.resize((mask.width * scale, mask.height * scale), Image.NEAREST)
    pad = 3
    canvas = Image.new("RGBA", (m.width + pad * 2 + 2, m.height + pad * 2 + 3), (0, 0, 0, 0))
    big = Image.new("L", canvas.size, 0)
    big.paste(m, (pad, pad))
    ring = big.filter(ImageFilter.MaxFilter(3))
    shadow = Image.new("L", canvas.size, 0)
    shadow.paste(ring, (2, 3))
    canvas.paste(Image.new("RGBA", canvas.size, SHADOW + (255,)), (0, 0), shadow)
    canvas.paste(Image.new("RGBA", canvas.size, INK + (255,)), (0, 0), ring)
    canvas.paste(Image.new("RGBA", canvas.size, color + (255,)), (0, 0), big)
    return canvas.resize((canvas.width * PX, canvas.height * PX), Image.NEAREST)


def fit(mask, want):
    """The largest whole scale up to `want` that keeps the line inside the dig area."""
    return max(1, min(want, CARD_MAX_W // max(1, mask.width)))


def card(lines, big):
    """Chinese over English, centred on each other."""
    cn_mask, en_mask = text_mask(lines[0]), text_mask(lines[1])
    cn = styled(cn_mask, GOLD, fit(cn_mask, big))
    en = styled(en_mask, WHITE, fit(en_mask, max(1, big - 1)))
    out = Image.new("RGBA", (max(cn.width, en.width), cn.height + en.height - 2 * PX), (0, 0, 0, 0))
    out.alpha_composite(cn, ((out.width - cn.width) // 2, 0))
    out.alpha_composite(en, ((out.width - en.width) // 2, cn.height - 2 * PX))
    return out


def clip_frames(name):
    folder = os.path.join(REC, name)
    return [os.path.join(folder, f) for f in sorted(os.listdir(folder)) if f.endswith(".png")]


def main():
    audio = os.path.join(REC, "audio.wav")
    out = os.path.join(REC, "drill_thrill_trailer.mp4")
    ffmpeg = subprocess.Popen(
        ["ffmpeg", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(fps),
         "-i", "-", "-i", audio, "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-pix_fmt", "yuv420p",
         "-af", "loudnorm=I=-13:TP=-1:LRA=9", "-ar", "48000", "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", out],
        stdin=subprocess.PIPE)

    beat_frames = bar / 4 * fps
    elapsed_bars = 0.0
    last = None
    for clip in timeline["clips"]:
        start = round(elapsed_bars * bar * fps)
        elapsed_bars += clip["bars"]
        end = round(elapsed_bars * bar * fps)
        count = end - start
        name = clip["clip"]

        if name == "logo":
            write_logo(ffmpeg, last, count + int(1.5 * fps))
            continue

        # a clip plays its own frames; a montage strings beats of other clips together
        pieces = clip.get("montage") or [[name, clip.get("from", 0), clip["bars"]]]
        sources, cuts = [], set()
        for piece_clip, piece_from, piece_bars in pieces:
            frames = clip_frames(piece_clip)
            first = int(piece_from * fps)
            n = round(piece_bars * bar * fps)
            cuts.add(len(sources))
            sources += [frames[min(len(frames) - 1, first + i)] for i in range(n)]
        cards = [card(clip["card"], 3), card(clip["card"], 4)] if clip.get("card") else None
        card_frames = int(beat_frames * 3)
        climax = clip.get("section") == "climax"
        for i in range(count):
            img = Image.open(sources[min(len(sources) - 1, i)]).convert("RGB")
            if img.size != (W, H):
                img = img.resize((W, H), Image.NEAREST)
            since_cut = min((i - c for c in cuts if c <= i), default=i)
            if since_cut < 3:  # punch in on every cut, settling over three frames
                zoom = 1 + 0.06 * (3 - since_cut) / 3
                zw, zh = int(W * zoom), int(H * zoom)
                img = img.resize((zw, zh), Image.NEAREST).crop(((zw - W) // 2, (zh - H) // 2, (zw - W) // 2 + W, (zh - H) // 2 + H))
            if since_cut < 2:  # and flash
                img = ImageEnhance.Brightness(img).enhance(1.5 - 0.25 * since_cut)
            beat_phase = (i % beat_frames) / beat_frames
            if climax and beat_phase < 0.12:  # the climax shakes on every beat
                img = img.transform(img.size, Image.AFFINE, (1, 0, PX * 2, 0, 1, -PX), resample=Image.NEAREST)
            if cards and i < card_frames:
                c = cards[1] if i < 2 else cards[0]  # pops in big, then settles
                shake = (PX * (2 if i % 2 else -2), 0) if i < 6 else (0, 0)
                x = (DIG_W - c.width) // 2 + shake[0]
                y = int(H * 0.36) - c.height // 2 + shake[1]
                frame = img.convert("RGBA")
                frame.alpha_composite(c, (max(0, x), max(0, y)))
                img = frame.convert("RGB")
            ffmpeg.stdin.write(img.tobytes())
            last = img
        print(f"{name}: {count} frames", flush=True)

    ffmpeg.stdin.close()
    ffmpeg.wait()
    print("wrote", out)


def write_logo(ffmpeg, background, count):
    """The last shot, darkened and blurred, with the logo landing on it and the call to wishlist."""
    base = ImageEnhance.Brightness(background.filter(ImageFilter.GaussianBlur(14))).enhance(0.35)
    logo = Image.open(os.path.join(ROOT, "art/trailer/logo.png")).convert("RGBA")
    logo_big = logo.resize((logo.width * PX, logo.height * PX), Image.NEAREST)
    logo_pop = logo.resize((int(logo.width * PX * 1.15), int(logo.height * PX * 1.15)), Image.NEAREST)
    cta = card(["加入 Steam 愿望单", "WISHLIST ON STEAM"], 2)
    for i in range(count):
        frame = base.convert("RGBA")
        if i < 2:
            frame = ImageEnhance.Brightness(frame.convert("RGB")).enhance(2.2 - 0.5 * i).convert("RGBA")
        lg = logo_pop if i < 3 else logo_big
        frame.alpha_composite(lg, ((W - lg.width) // 2, int(H * 0.36) - lg.height // 2))
        if i >= int(bar * fps * 0.5):  # the call to action comes in half a bar later
            frame.alpha_composite(cta, ((W - cta.width) // 2, int(H * 0.78) - cta.height // 2))
        ffmpeg.stdin.write(frame.convert("RGB").tobytes())
    print(f"logo: {count} frames", flush=True)


if __name__ == "__main__":
    sys.exit(main())
