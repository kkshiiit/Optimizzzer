"""Draws the Optimizzzer icon (a gauge with its needle on the right, on a dark rounded square) and writes app.ico.

Run: python3 tools/make_icon.py   (needs Pillow). The result is committed, so builds do not need Python.
"""
import math
import os
from PIL import Image, ImageDraw

BG = (24, 24, 28, 255)
ARC_DIM = (70, 72, 82, 255)
ACCENT = (76, 194, 255, 255)   # AccentFallback (dark theme) from DESIGN.md
WHITE = (242, 242, 244, 255)


def draw(size):
    s = size * 4  # supersample, then shrink for smooth edges
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = s * 0.22
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=BG)
    cx, cy = s / 2, s * 0.58
    R = s * 0.34
    w = max(4, int(s * 0.085))
    box = [cx - R, cy - R, cx + R, cy + R]
    d.arc(box, start=180, end=360, fill=ARC_DIM, width=w)
    d.arc(box, start=180, end=300, fill=ACCENT, width=w)
    ang = math.radians(-60)  # needle at the end of the lit arc: "fast"
    L = R * 0.92
    x2, y2 = cx + L * math.cos(ang), cy + L * math.sin(ang)
    d.line([cx, cy, x2, y2], fill=WHITE, width=max(3, int(s * 0.06)))
    hub = s * 0.07
    d.ellipse([cx - hub, cy - hub, cx + hub, cy + hub], fill=WHITE)
    return img.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    out = os.path.join(os.path.dirname(__file__), "..", "src", "Optimizzzer.App", "Resources", "app.ico")
    sizes = [16, 24, 32, 48, 64, 128, 256]
    big = draw(256)
    big.save(out, format="ICO", sizes=[(n, n) for n in sizes])
    draw(256).save(os.path.join(os.path.dirname(__file__), "..", "docs", "icon.png"))
    print("wrote", os.path.abspath(out))
