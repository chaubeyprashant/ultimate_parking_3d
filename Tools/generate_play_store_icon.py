#!/usr/bin/env python3
"""512×512 PNG for Google Play listing + Android launcher source (32-bit RGBA)."""
from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    Image = None  # type: ignore

ROOT = Path(__file__).resolve().parents[1]
OUT_APP = ROOT / "Assets/_Project/Art/Branding/AppIcon.png"
OUT_PLAY = ROOT / "Assets/_Project/Art/Branding/PlayStoreIcon.png"
OUT_STORE = ROOT / "StoreAssets/GooglePlay/icon-512.png"
MASTER_STORE = ROOT / "StoreAssets/GooglePlay/icon-master.jpg"
MASTER_APP = ROOT / "Assets/_Project/Art/Branding/AppIconMaster.jpg"
SIZE = 512

BG = (11, 13, 16)
SURFACE = (20, 24, 29)
RAISED = (28, 33, 40)
OUTLINE = (42, 49, 58)
LIME = (182, 255, 46)
WHITE = (242, 245, 247)
GLASS = (55, 62, 72)


def write_png(path: Path, img: "Image.Image") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG", optimize=True)


def render_pil() -> "Image.Image":
    img = Image.new("RGBA", (SIZE, SIZE), BG + (255,))
    px = img.load()

    cx, cy = SIZE / 2, SIZE * 0.54
    for y in range(SIZE):
        ny = y / SIZE
        for x in range(SIZE):
            nx = x / SIZE
            dx, dy = nx - 0.5, ny - 0.52
            r = math.hypot(dx * 0.9, dy * 1.1)
            t = min(1.0, r * 1.35)
            col = tuple(int(SURFACE[i] + (BG[i] - SURFACE[i]) * t * 0.85) for i in range(3))
            fx, fy = (nx - 0.5) / 0.48, (ny - 0.58) / 0.38
            if fx * fx + fy * fy < 1.0:
                w = 1.0 - fx * fx - fy * fy
                col = tuple(int(col[i] + (RAISED[i] - col[i]) * 0.55 * w) for i in range(3))
            px[x, y] = col + (255,)

    draw = ImageDraw.Draw(img)

    # Parking bay floor
    draw.ellipse(
        (SIZE * 0.06, SIZE * 0.22, SIZE * 0.94, SIZE * 0.92),
        fill=RAISED + (255,),
    )
    draw.ellipse(
        (SIZE * 0.10, SIZE * 0.26, SIZE * 0.90, SIZE * 0.88),
        fill=SURFACE + (255,),
    )

    # Glow lines (wide then narrow)
    for w, c in ((14, (70, 110, 20)), (5, LIME)):
        for lx in (0.22, 0.78):
            x0 = lx * SIZE
            draw.line((x0, SIZE * 0.11, x0, SIZE * 0.89), fill=c + (180 if w > 6 else 255,), width=w)
    draw.line((SIZE * 0.22, SIZE * 0.11, SIZE * 0.78, SIZE * 0.11), fill=LIME + (255,), width=4)
    draw.line((SIZE * 0.22, SIZE * 0.89, SIZE * 0.78, SIZE * 0.89), fill=LIME + (255,), width=4)

    scale = SIZE * 0.42
    body = [
        (cx - 0.10 * scale, cy + 0.38 * scale),
        (cx - 0.22 * scale, cy + 0.28 * scale),
        (cx - 0.24 * scale, cy + 0.05 * scale),
        (cx - 0.14 * scale, cy - 0.22 * scale),
        (cx, cy - 0.32 * scale),
        (cx + 0.14 * scale, cy - 0.22 * scale),
        (cx + 0.24 * scale, cy + 0.05 * scale),
        (cx + 0.22 * scale, cy + 0.28 * scale),
        (cx + 0.10 * scale, cy + 0.38 * scale),
    ]
    shadow = [(x, y + 7) for x, y in body]
    draw.polygon(shadow, fill=(0, 0, 0, 110))
    draw.polygon(body, fill=WHITE + (255,), outline=OUTLINE + (255,))

    wind = [
        (cx - 0.12 * scale, cy + 0.12 * scale),
        (cx + 0.12 * scale, cy + 0.12 * scale),
        (cx + 0.08 * scale, cy - 0.08 * scale),
        (cx - 0.08 * scale, cy - 0.08 * scale),
    ]
    draw.polygon(wind, fill=GLASS + (255,))

    draw.rounded_rectangle(
        (cx - 0.17 * scale, cy + 0.24 * scale, cx + 0.17 * scale, cy + 0.30 * scale),
        radius=int(0.03 * scale),
        fill=LIME + (255,),
    )

    # Soft lime vignette at bottom
    grad = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    gd.ellipse((-SIZE * 0.2, SIZE * 0.55, SIZE * 1.2, SIZE * 1.35), fill=LIME + (28,))
    img = Image.alpha_composite(img.convert("RGBA"), grad)
    return img


def main():
    if Image is None:
        raise SystemExit("Pillow required: pip install pillow")
    master_path = None
    for p in (MASTER_STORE, MASTER_APP):
        if p.is_file():
            master_path = p
            break

    if master_path:
        print(f"Using master artwork: {master_path}")
        with Image.open(master_path) as src:
            resample = getattr(getattr(Image, "Resampling", Image), "LANCZOS", Image.BICUBIC)
            img = src.convert("RGBA").resize((SIZE, SIZE), resample)
    else:
        print("Master artwork not found, rendering procedural placeholder icon...")
        img = render_pil()

    for path in (OUT_APP, OUT_PLAY, OUT_STORE):
        write_png(path, img)
        print(f"Wrote {path} ({path.stat().st_size / 1024:.1f} KB)")


if __name__ == "__main__":
    main()
