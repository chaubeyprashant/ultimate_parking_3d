#!/usr/bin/env python3
"""Build 7 Google Play phone screenshots (1920×1080 landscape) from raw Unity captures."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "StoreAssets/GooglePlay/screenshots/raw"
OUT = ROOT / "StoreAssets/GooglePlay/screenshots"

W, H = 1920, 1080
BG = (11, 13, 16)
LIME = (182, 255, 46)
WHITE = (242, 245, 247)
DIM = (140, 150, 163)


def fit(img: Image.Image, tw: int, th: int) -> Image.Image:
    from PIL import ImageOps

    return ImageOps.fit(img, (tw, th), method=Image.Resampling.LANCZOS)


def load_cover(name: str) -> Image.Image:
    path = RAW / name
    if not path.exists():
        raise FileNotFoundError(path)
    img = Image.open(path).convert("RGBA")
    return fit(img, W, H)


def fonts():
    candidates = [
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
        "/Library/Fonts/Arial Bold.ttf",
    ]
    for c in candidates:
        if Path(c).exists():
            return ImageFont.truetype(c, 72), ImageFont.truetype(c, 38), ImageFont.truetype(c, 28)
    return ImageFont.load_default(), ImageFont.load_default(), ImageFont.load_default()


def gradient_overlay(base: Image.Image, strength: float = 0.55) -> Image.Image:
    g = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(g)
    for y in range(H):
        t = y / H
        a = int(255 * strength * (0.15 + 0.85 * t))
        d.line([(0, y), (W, y)], fill=(BG[0], BG[1], BG[2], a))
    return Image.alpha_composite(base.convert("RGBA"), g)


def draw_hook(img: Image.Image, title: str, subtitle: str, badge: str | None = None) -> Image.Image:
    title_f, sub_f, badge_f = fonts()
    img = gradient_overlay(img, 0.62)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, W, 8), fill=LIME + (255,))
    pad = 72
    y = H - 280
    d.text((pad, y), title.upper(), font=title_f, fill=WHITE)
    d.text((pad, y + 88), subtitle, font=sub_f, fill=DIM)
    if badge:
        bw, bh = 420, 52
        bx, by = pad, y - 70
        d.rounded_rectangle((bx, by, bx + bw, by + bh), radius=26, fill=LIME + (230,))
        d.text((bx + 22, by + 8), badge.upper(), font=badge_f, fill=BG)
    return img


def draw_caption(img: Image.Image, caption: str) -> Image.Image:
    _, sub_f, _ = fonts()
    img = gradient_overlay(img, 0.35)
    d = ImageDraw.Draw(img)
    d.text((72, H - 120), caption, font=sub_f, fill=WHITE)
    return img


SHOTS = [
    ("phone-screenshot-01.png", "01_main_menu.jpg", "hook",
     "Ultimate Car Parking 3D", "Drive. Park. Master every bay.", "Free to play"),
    ("phone-screenshot-02.png", "02_world_tour_parking.jpg", "hook",
     "World Tour Cities", "Real streets from Mumbai to New York.", "3D parking missions"),
    ("phone-screenshot-03.png", "03_campaign_parking.jpg", "hook",
     "30 Campaign Levels", "Earn stars. Unlock cars. Keep improving.", "Touch controls"),
    ("phone-screenshot-04.png", "04_garage.jpg", "caption", "Collect sports cars, sedans, SUVs & more"),
    ("phone-screenshot-05.png", "05_level_select.jpg", "caption", "Progress through structured parking lessons"),
    ("phone-screenshot-06.png", "06_world_tour.jpg", "caption", "Travel country arenas with unique traffic rules"),
    ("phone-screenshot-07.png", "07_world_arena.jpg", "caption", "Tight bays, real props, satisfying precision"),
]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for out_name, raw_name, kind, *text in SHOTS:
        base = load_cover(raw_name)
        if kind == "hook":
            title, subtitle, badge = text
            final = draw_hook(base, title, subtitle, badge)
        else:
            (caption,) = text
            final = draw_caption(base, caption)
        path = OUT / out_name
        final.convert("RGB").save(path, format="PNG", optimize=True)
        print(f"Wrote {path} ({path.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
