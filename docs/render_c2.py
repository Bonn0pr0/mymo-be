from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import math

W, H = 1100, 1450
img = Image.new("RGB", (W, H), "#FFFFFF")
draw = ImageDraw.Draw(img)


def font(size, bold=False):
    for p in [
        r"C:\Windows\Fonts\segoeuib.ttf" if bold else r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\arialbd.ttf" if bold else r"C:\Windows\Fonts\arial.ttf",
    ]:
        if Path(p).exists():
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


F12, F13, F14b, F20b = font(12), font(13), font(14, True), font(20, True)
BLUE, GREEN, PURPLE = "#D6EAF8", "#D5F5E3", "#E8DAEF"
YELLOW, PINK, CYAN, ORANGE = "#FCF3CF", "#FADBD8", "#D1F2EB", "#F5CBA7"
BORDER, TEXT, ARROW = "#5D6D7E", "#1C2833", "#2C3E50"


def rounded_rect(xy, fill, outline=BORDER, width=2, radius=14):
    draw.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)


def box(cx, cy, w, h, fill, title, subtitle=None):
    x0, y0, x1, y1 = cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2
    rounded_rect((x0, y0, x1, y1), fill)
    tb = draw.textbbox((0, 0), title, font=F14b)
    tw, th = tb[2] - tb[0], tb[3] - tb[1]
    if subtitle:
        sb = draw.textbbox((0, 0), subtitle, font=F12)
        sw = sb[2] - sb[0]
        draw.text((cx - tw / 2, cy - th - 2), title, fill=TEXT, font=F14b)
        draw.text((cx - sw / 2, cy + 2), subtitle, fill="#566573", font=F12)
    else:
        draw.text((cx - tw / 2, cy - th / 2), title, fill=TEXT, font=F14b)
    return (x0, y0, x1, y1)


def center(b):
    return ((b[0] + b[2]) // 2, (b[1] + b[3]) // 2)


def edge_point(b, tx, ty):
    cx, cy = center(b)
    dx, dy = tx - cx, ty - cy
    if dx == 0 and dy == 0:
        return cx, cy
    x0, y0, x1, y1 = b
    hits = []
    if dx:
        for x in (x0, x1):
            t = (x - cx) / dx
            if t > 0:
                y = cy + t * dy
                if y0 - 1 <= y <= y1 + 1:
                    hits.append((t, x, int(y)))
    if dy:
        for y in (y0, y1):
            t = (y - cy) / dy
            if t > 0:
                x = cx + t * dx
                if x0 - 1 <= x <= x1 + 1:
                    hits.append((t, int(x), y))
    hits.sort()
    return hits[0][1], hits[0][2] if hits else (cx, cy)


def arrow(b1, b2, label, dashed=False):
    c1, c2 = center(b1), center(b2)
    x1, y1 = edge_point(b1, *c2)
    x2, y2 = edge_point(b2, *c1)
    if dashed:
        length = math.hypot(x2 - x1, y2 - y1) or 1
        ux, uy = (x2 - x1) / length, (y2 - y1) / length
        pos, on = 0.0, True
        while pos < length - 10:
            seg = 8 if on else 6
            nx = min(pos + seg, length - 10)
            if on:
                draw.line((x1 + ux * pos, y1 + uy * pos, x1 + ux * nx, y1 + uy * nx), fill=ARROW, width=2)
            pos, on = nx, not on
    else:
        draw.line((x1, y1, x2, y2), fill=ARROW, width=2)
    ang = math.atan2(y2 - y1, x2 - x1)
    s = 9
    draw.polygon(
        [
            (x2, y2),
            (x2 - s * math.cos(ang - 0.4), y2 - s * math.sin(ang - 0.4)),
            (x2 - s * math.cos(ang + 0.4), y2 - s * math.sin(ang + 0.4)),
        ],
        fill=ARROW,
    )
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    length = math.hypot(x2 - x1, y2 - y1) or 1
    px, py = -(y2 - y1) / length * 14, (x2 - x1) / length * 14
    lx, ly = mx + px, my + py - 6
    bb = draw.textbbox((0, 0), label, font=F12)
    lw = bb[2] - bb[0]
    for ox, oy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        draw.text((lx - lw / 2 + ox, ly + oy), label, fill="white", font=F12)
    draw.text((lx - lw / 2, ly), label, fill=ARROW, font=F12)


title = "MyMo C2 Container Architecture"
tb = draw.textbbox((0, 0), title, font=F20b)
draw.text(((W - (tb[2] - tb[0])) / 2, 20), title, fill=TEXT, font=F20b)

user = box(W // 2, 85, 90, 48, GREEN, "User")
web = box(380, 180, 210, 64, GREEN, "Web Application", "[Expo Web]")
mobile = box(720, 180, 240, 64, GREEN, "Mobile App", "[Expo / React Native]")
gw = box(W // 2, 300, 210, 58, ORANGE, "YARP Gateway", "[API Gateway]")

# peach system boundary
rounded_rect((40, 360, W - 40, 1220), "#FDF2E9", outline="#E67E22", width=3, radius=28)
draw.text((60, 375), "MyMo Backend", fill="#A04000", font=F13)

# APIs - Map on the RIGHT so Mapbox wire is unambiguous
y_api = 520
identity = box(160, y_api, 150, 74, PURPLE, "Identity API", "[Auth / Users]")
social = box(340, y_api, 150, 74, PURPLE, "Social API", "[Posts / Friends]")
chat = box(520, y_api, 150, 74, PURPLE, "Chat API", "[Messages]")
business = box(700, y_api, 150, 74, PURPLE, "Business API", "[Biz / Billing]")
mapapi = box(900, y_api, 150, 74, PURPLE, "Map API", "[Places / Geo]")

rabbit = box(340, 760, 190, 66, CYAN, "RabbitMQ", "[Commands]")
kafka = box(700, 760, 190, 66, CYAN, "Kafka", "[Events]")
sqlite = box(W // 2, 980, 210, 84, BLUE, "SQLite", "[EF Core]")

# Externals: short, non-crossing wires
# Cloudinary LEFT of Identity (same row)
cloudinary = box(160, 400, 170, 66, PINK, "Cloudinary", "CDN anh/video")
# AI LEFT-ISH above Social
ai = box(340, 400, 160, 66, ORANGE, "AI Service", "Phan tich")
# Mapbox RIGHT of Map API (outside-ish, same row) - clearest
mapbox = box(900, 400, 160, 66, YELLOW, "Mapbox", "Ban do")

# Client flow
arrow(user, web, "HTTPS")
arrow(user, mobile, "HTTPS")
arrow(web, gw, "HTTPS")
arrow(mobile, gw, "HTTPS")

# Gateway routes
arrow(gw, identity, "HTTPS")
arrow(gw, social, "HTTPS")
arrow(gw, chat, "HTTPS")
arrow(gw, business, "HTTPS")
arrow(gw, mapapi, "HTTPS")
arrow(gw, chat, "SignalR", dashed=True)
arrow(gw, mapapi, "SignalR", dashed=True)

# SQL
for api in (identity, social, chat, business, mapapi):
    arrow(api, sqlite, "SQL")

# Messaging
arrow(business, rabbit, "Produce", dashed=True)
arrow(social, rabbit, "Produce", dashed=True)
arrow(social, kafka, "PostCreated", dashed=True)
arrow(rabbit, identity, "Consume", dashed=True)
arrow(rabbit, mapapi, "Consume", dashed=True)
arrow(kafka, mapapi, "Consume", dashed=True)

# CORRECT externals - short vertical wires, no crossing
arrow(identity, cloudinary, "HTTPS", dashed=True)
arrow(social, ai, "HTTPS", dashed=True)
arrow(mapapi, mapbox, "HTTPS", dashed=True)

rounded_rect((60, 1260, 1040, 1410), "#F8F9F9", radius=12)
draw.text((80, 1275), "External dung (khong nham):", fill=TEXT, font=F14b)
draw.text((80, 1310), "1) Map API  <-->  Mapbox (ban do)", fill=TEXT, font=F13)
draw.text((80, 1340), "2) Identity API  <-->  Cloudinary (anh/video)", fill=TEXT, font=F13)
draw.text((80, 1370), "3) Social API  <-->  AI Service (phan tich)", fill=TEXT, font=F13)
draw.text((560, 1310), "Net dut = async / external", fill="#566573", font=F12)
draw.text((560, 1340), "Net lien = HTTPS route / SQL", fill="#566573", font=F12)

out = Path(r"e:\mymo-be\docs\mymo-c2-architecture.jpg")
img.save(out, quality=95)
img.save(Path(r"C:\Users\gmt\.cursor\projects\e-mymo-be\assets\mymo-c2-architecture.jpg"), quality=95)
print("saved", out)
