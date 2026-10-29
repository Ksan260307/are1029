"""Number Road - ally classes, enemies and bosses (imported by build_models.py).

Most characters come from one parametric `humanoid()` builder (head, hat, weapon,
shield, cape and extras are options); creatures with other shapes (slime, bat,
ghost, wolf rider, beetle, mimic, ram, golem, dragons) have their own builders.
Everything follows the conventions in build_models.py: face -Y, parts named
"<model>_<Part>" with the joint as the object origin, colours as vertex colours.
Crowd enemies are kept low-poly because thousands of them are drawn at once.
"""

import math

KIT = None


def _setup(kit):
    global KIT
    KIT = kit


def C(hex_value):
    return KIT.hexcol(hex_value)


def P(name):
    return KIT.PAL[name]


def sh(color, amount):
    return KIT.shade(color, amount)


# ---------------------------------------------------------------------------
# Humanoid parts
# ---------------------------------------------------------------------------

def _legs(ctx, k, pants, boots, robe=False, spread=0.09, width=0.13, hip=0.42):
    for side, sx in (("LegL", 1), ("LegR", -1)):
        leg = ctx.part(side, k)
        x = spread * sx
        if robe:
            leg.box((x, 0, 0.13), (width, 0.15, 0.22), pants)
            leg.box((x, -0.02, 0.045), (width + 0.015, 0.19, 0.09), boots)
            leg.build(ctx, pivot=(x, 0, 0.24))
        else:
            leg.box((x, 0, hip / 2 + 0.02), (width, 0.15, hip - 0.04), pants)
            leg.box((x, -0.02, 0.055), (width + 0.015, 0.2, 0.11), boots)
            leg.build(ctx, pivot=(x, 0, hip))


def _head(b, head, skin, eyes, hat_color):
    if head == "orc":
        b.sphere((0, -0.02, 0.97), 0.16, skin, seg=8, rings=6, scale=(1.05, 1, 0.95))
        b.box((0, -0.09, 0.89), (0.24, 0.15, 0.09), skin)
        for sx in (1, -1):
            b.cyl((0.07 * sx, -0.16, 0.94), 0.02, 0.07, P("white"), seg=4, radius2=0.0)
            b.box((0.06 * sx, -0.165, 1.01), (0.05, 0.02, 0.035), eyes)
    elif head == "goblin":
        b.sphere((0, -0.01, 0.9), 0.14, skin, seg=8, rings=6)
        for sx in (1, -1):
            b.cyl((0.17 * sx, 0.0, 0.94), 0.045, 0.16, skin, seg=4, radius2=0.0, rot=(0, 1.35 * sx, 0))
            b.box((0.05 * sx, -0.13, 0.93), (0.045, 0.02, 0.035), eyes)
        b.cyl((0, -0.16, 0.89), 0.03, 0.08, sh(skin, -0.15), seg=4, radius2=0.0, rot=(math.pi / 2, 0, 0))
    elif head == "skull":
        b.sphere((0, 0, 0.94), 0.14, P("white"), seg=8, rings=6)
        b.box((0, -0.06, 0.85), (0.15, 0.12, 0.06), P("white"))
        for sx in (1, -1):
            b.box((0.05 * sx, -0.13, 0.95), (0.05, 0.02, 0.05), eyes)
    elif head == "hood":
        b.sphere((0, 0, 0.93), 0.14, skin, seg=8, rings=6)
        b.sphere((0, 0.035, 0.96), 0.175, hat_color, seg=8, rings=6, scale=(1, 1, 1.05))
        for sx in (1, -1):
            b.box((0.05 * sx, -0.13, 0.94), (0.035, 0.02, 0.045), eyes)
    elif head == "helm":
        b.cyl((0, 0, 0.93), 0.16, 0.24, hat_color, seg=8)
        b.sphere((0, 0, 1.05), 0.16, hat_color, seg=8, rings=5, scale=(1, 1, 0.6))
        b.box((0, -0.16, 0.95), (0.18, 0.02, 0.035), P("black"))
        b.box((0, -0.163, 0.9), (0.03, 0.02, 0.1), sh(hat_color, -0.3))
    elif head == "imp":
        b.sphere((0, -0.01, 0.9), 0.15, skin, seg=8, rings=6)
        for sx in (1, -1):
            b.cyl((0.09 * sx, 0, 1.04), 0.035, 0.12, P("black"), seg=4, radius2=0.0, rot=(0, -0.4 * sx, 0))
            b.box((0.05 * sx, -0.14, 0.92), (0.045, 0.02, 0.035), eyes)
        b.box((0, -0.145, 0.85), (0.08, 0.02, 0.02), P("black"))
    else:  # round / zombie
        b.sphere((0, 0, 0.93), 0.15, skin, seg=8, rings=6)
        for sx in (1, -1):
            b.box((0.055 * sx, -0.138, 0.94), (0.035, 0.02, 0.05), eyes)
        if head == "zombie":
            b.box((0, -0.14, 0.86), (0.08, 0.02, 0.04), P("black"))


def _hat(b, hat, color, accent):
    if hat is None:
        return
    if hat == "cone":
        b.cyl((0, 0, 1.0), 0.162, 0.08, P("steel"), seg=8)
        b.cyl((0, 0, 1.12), 0.165, 0.17, color, seg=8, radius2=0.03)
        b.sphere((0, 0, 1.215), 0.035, accent, seg=6, rings=4)
    elif hat == "wizard":
        b.cyl((0, 0, 1.04), 0.25, 0.03, color, seg=10)
        b.cyl((0, 0, 1.08), 0.15, 0.06, accent, seg=8)
        b.cyl((0, 0.03, 1.3), 0.14, 0.42, color, seg=8, radius2=0.01, rot=(0.15, 0, 0))
    elif hat == "horned":
        b.sphere((0, 0, 1.05), 0.17, color, seg=8, rings=5, scale=(1, 1, 0.62))
        for sx in (1, -1):
            b.cyl((0.16 * sx, 0, 1.13), 0.04, 0.18, P("white"), seg=5, radius2=0.0, rot=(0, 0.8 * sx, 0))
    elif hat == "crown":
        b.cyl((0, 0, 1.08), 0.15, 0.08, P("gold"), seg=8)
        for i in range(5):
            a = i / 5 * math.tau
            b.cyl((math.sin(a) * 0.13, -math.cos(a) * 0.13, 1.16), 0.035, 0.09, P("gold"), seg=4, radius2=0.0)
        b.sphere((0, -0.15, 1.09), 0.03, accent, seg=5, rings=3)
    elif hat == "feather":
        b.cyl((0, 0, 1.05), 0.26, 0.03, color, seg=10)
        b.cyl((0, 0, 1.11), 0.15, 0.12, color, seg=8)
        b.box((0.1, 0.08, 1.2), (0.03, 0.06, 0.28), accent, rot=(0.5, 0.4, 0))
    elif hat == "mitre":
        b.cyl((0, 0, 1.2), 0.14, 0.34, color, seg=4, radius2=0.05, rot=(0, 0, math.pi / 4))
        b.box((0, -0.075, 1.2), (0.03, 0.02, 0.26), accent)
    elif hat == "cap":
        b.cyl((0, 0, 1.05), 0.155, 0.09, color, seg=8)
        b.box((0, -0.15, 1.02), (0.2, 0.12, 0.02), sh(color, -0.2))
    elif hat == "halo":
        for i in range(10):
            a = i / 10 * math.tau
            b.sphere((math.sin(a) * 0.16, math.cos(a) * 0.16, 1.2), 0.028, P("gold"), seg=4, rings=3)
    elif hat == "plume":
        b.sphere((0, 0, 1.04), 0.17, color, seg=8, rings=5, scale=(1, 1, 0.65))
        b.box((0, 0.04, 1.2), (0.05, 0.22, 0.12), accent, rot=(-0.35, 0, 0))
    elif hat == "kabuto":
        b.sphere((0, 0, 1.04), 0.18, color, seg=8, rings=5, scale=(1, 1, 0.65))
        b.box((0, 0, 0.97), (0.42, 0.3, 0.04), color)
        for sx in (1, -1):
            b.box((0.07 * sx, -0.16, 1.15), (0.025, 0.02, 0.22), accent, rot=(0, -0.45 * sx, 0))
    elif hat == "bandana":
        b.cyl((0, 0, 1.0), 0.155, 0.05, color, seg=8)
        b.box((0, 0.16, 0.98), (0.05, 0.08, 0.1), color, rot=(0.6, 0, 0))
    elif hat == "headdress":
        for i in range(5):
            a = (i - 2) * 0.35
            b.box((math.sin(a) * 0.12, 0.05, 1.12 + math.cos(a) * 0.05), (0.04, 0.02, 0.26), accent if i % 2 else color, rot=(0, a, 0))
    elif hat == "mohawk":
        b.box((0, 0.0, 1.08), (0.05, 0.26, 0.12), color)


def _weapon(b, weapon, color, accent):
    if weapon is None:
        return
    wood = P("wood")
    steel = P("steel")
    if weapon in ("bow", "longbow"):
        h = 0.36 if weapon == "longbow" else 0.27
        b.box((0.13, -0.32, 0.68), (0.035, 0.035, 0.12), color)
        b.box((0.13, -0.29, 0.68 + h * 0.62), (0.03, 0.03, h), color, rot=(-0.35, 0, 0))
        b.box((0.13, -0.29, 0.68 - h * 0.62), (0.03, 0.03, h), color, rot=(0.35, 0, 0))
        b.box((0.13, -0.2, 0.68), (0.008, 0.008, h * 2.1), P("white"))
    elif weapon == "crossbow":
        b.box((0, -0.36, 0.665), (0.05, 0.34, 0.06), wood)
        for sx in (1, -1):
            b.box((0.11 * sx, -0.49, 0.68), (0.23, 0.035, 0.035), color, rot=(0, 0, 0.35 * sx))
        b.box((0, -0.5, 0.705), (0.016, 0.2, 0.016), steel)
    elif weapon in ("staff", "orbstaff", "holystaff", "skullstaff"):
        b.cyl((-0.13, -0.27, 0.62), 0.025, 1.0, color if weapon == "holystaff" else wood, seg=6)
        if weapon == "staff":
            b.sphere((-0.13, -0.27, 1.13), 0.055, accent, seg=6, rings=4)
        elif weapon == "orbstaff":
            b.sphere((-0.13, -0.27, 1.16), 0.085, accent, seg=8, rings=5)
            b.cyl((-0.13, -0.27, 1.16), 0.11, 0.02, P("gold"), seg=8, rot=(math.pi / 2, 0, 0))
        elif weapon == "holystaff":
            b.box((-0.13, -0.27, 1.17), (0.03, 0.03, 0.22), P("gold"))
            b.box((-0.13, -0.27, 1.2), (0.16, 0.03, 0.03), P("gold"))
            b.sphere((-0.13, -0.27, 1.2), 0.04, accent, seg=6, rings=4)
        else:
            b.sphere((-0.13, -0.27, 1.15), 0.08, P("white"), seg=6, rings=4)
            b.box((-0.13, -0.34, 1.15), (0.06, 0.02, 0.04), P("black"))
            b.sphere((-0.13, -0.27, 1.25), 0.05, accent, seg=6, rings=4)
    elif weapon == "wand":
        b.cyl((-0.125, -0.42, 0.66), 0.015, 0.3, wood, seg=5, rot=(math.pi / 2, 0, 0))
        b.sphere((-0.125, -0.58, 0.66), 0.04, accent, seg=6, rings=4)
    elif weapon in ("sword", "greatsword", "katana"):
        length, width = {"sword": (0.45, 0.05), "greatsword": (0.8, 0.09), "katana": (0.7, 0.03)}[weapon]
        b.box((-0.13, -0.3, 0.7), (0.03, 0.03, 0.12), P("brown_dark"))
        b.box((-0.13, -0.32, 0.77), (width + 0.1, 0.04, 0.03), accent)
        b.box((-0.13, -0.36 - length * 0.25, 0.78 + length * 0.45), (width, 0.02, length), color, rot=(0.5, 0, 0))
    elif weapon in ("spear", "pike"):
        length = 1.1 if weapon == "spear" else 1.7
        b.cyl((-0.125, -0.25 - length * 0.4, 0.66), 0.02, length, wood, seg=5, rot=(math.pi / 2, 0, 0))
        b.cyl((-0.125, -0.25 - length * 0.9 - 0.06, 0.66), 0.045, 0.16, color, seg=4, radius2=0.0, rot=(math.pi / 2, 0, 0))
    elif weapon in ("musket", "rifle"):
        b.box((-0.06, -0.45, 0.69), (0.04, 0.7, 0.04), sh(steel, -0.4))
        b.box((-0.06, -0.16, 0.66), (0.06, 0.26, 0.09), wood)
        if weapon == "rifle":
            b.cyl((-0.06, -0.4, 0.74), 0.025, 0.22, P("black"), seg=6, rot=(math.pi / 2, 0, 0))
    elif weapon == "gatling":
        b.box((0, -0.28, 0.62), (0.2, 0.24, 0.17), P("steel_dark"))
        for i in range(6):
            a = i / 6 * math.tau
            b.cyl((math.cos(a) * 0.055, -0.6, 0.62 + math.sin(a) * 0.055), 0.02, 0.5, steel, seg=5, rot=(math.pi / 2, 0, 0))
        b.box((0, -0.86, 0.62), (0.16, 0.04, 0.16), P("steel_dark"))
    elif weapon == "cannon":
        b.cyl((0, -0.55, 0.36), 0.11, 0.6, P("black"), seg=8, radius2=0.08, rot=(math.pi / 2, 0, 0))
        b.cyl((0, -0.86, 0.36), 0.1, 0.04, accent, seg=8, rot=(math.pi / 2, 0, 0))
        for sx in (1, -1):
            b.cyl((0.16 * sx, -0.45, 0.16), 0.15, 0.05, wood, seg=8, rot=(0, math.pi / 2, 0))
    elif weapon == "book":
        for sx in (1, -1):
            b.box((0.07 * sx, -0.32, 0.68), (0.13, 0.015, 0.17), P("white"), rot=(0, 0, 0.35 * sx))
        b.box((0, -0.33, 0.68), (0.27, 0.01, 0.19), color)
    elif weapon in ("axe", "dualaxe"):
        for sx in ((-1,) if weapon == "axe" else (-1, 1)):
            x = 0.27 * sx
            b.box((x, -0.2, 0.67), (0.04, 0.04, 0.6), wood, rot=(0.58, 0, 0))
            b.box((x + 0.08 * sx, -0.36, 0.9), (0.16, 0.03, 0.2), color, rot=(0.58, 0, 0))
    elif weapon == "club":
        b.cyl((-0.15, -0.42, 0.85), 0.04, 0.6, wood, seg=6, radius2=0.09, rot=(0.9, 0, 0))
    elif weapon == "dagger":
        b.box((-0.125, -0.4, 0.66), (0.03, 0.22, 0.015), color)
        b.box((-0.125, -0.28, 0.66), (0.09, 0.03, 0.03), accent)
    elif weapon == "hammer":
        b.cyl((-0.13, -0.3, 0.8), 0.025, 0.7, wood, seg=6, rot=(0.4, 0, 0))
        b.box((-0.13, -0.45, 1.13), (0.26, 0.15, 0.15), color, rot=(0.4, 0, 0))
    elif weapon == "trident":
        b.cyl((-0.15, -0.27, 0.75), 0.025, 1.4, P("black"), seg=6)
        for dx in (-0.06, 0.0, 0.06):
            b.cyl((-0.15 + dx, -0.27, 1.52), 0.025, 0.18, color, seg=4, radius2=0.0)
        b.box((-0.15, -0.27, 1.43), (0.16, 0.03, 0.03), color)
    elif weapon == "drum":
        b.cyl((0, -0.24, 0.56), 0.2, 0.18, color, seg=10, rot=(math.pi / 2, 0, 0))
        for y in (-0.15, -0.33):
            b.cyl((0, y, 0.56), 0.205, 0.025, P("white"), seg=10, rot=(math.pi / 2, 0, 0))
        for sx in (1, -1):
            b.cyl((0.12 * sx, -0.4, 0.72), 0.015, 0.25, wood, seg=4, rot=(0.6, 0, 0))
    elif weapon == "sack":
        b.sphere((0, 0.2, 0.72), 0.2, P("brown"), seg=8, rings=6)
        for dx in (-0.06, 0.03, 0.08):
            b.cyl((dx, 0.16, 0.9), 0.04, 0.02, P("gold"), seg=6)
    elif weapon == "bomb":
        b.sphere((0, -0.33, 0.66), 0.12, P("black"), seg=8, rings=6)
        b.cyl((0, -0.33, 0.8), 0.015, 0.08, P("wood"), seg=4)
        b.sphere((0, -0.33, 0.86), 0.035, accent, seg=5, rings=3)
    elif weapon == "crystal":
        b.sphere((0, -0.36, 0.92), 0.1, accent, seg=4, rings=2, scale=(1, 1, 1.6))


def _shield(b, shield, color, bulk):
    if shield is None:
        return
    x = 0.3 * bulk
    if shield == "round":
        b.cyl((x, -0.115, 0.62), 0.18, 0.03, P("steel_dark"), seg=8, rot=(math.pi / 2, 0, 0))
        b.cyl((x, -0.13, 0.62), 0.15, 0.03, color, seg=8, rot=(math.pi / 2, 0, 0))
        b.sphere((x, -0.15, 0.62), 0.045, P("gold"), seg=5, rings=3)
    elif shield == "kite":
        b.sphere((x, -0.13, 0.6), 0.2, P("steel"), seg=6, rings=4, scale=(0.9, 0.18, 1.4))
        b.sphere((x, -0.145, 0.6), 0.17, color, seg=6, rings=4, scale=(0.9, 0.18, 1.4))
    elif shield == "tower":
        b.box((x - 0.04, -0.2, 0.6), (0.32, 0.05, 0.56), color)
        b.box((x - 0.04, -0.227, 0.62), (0.05, 0.01, 0.36), P("gold"))
        b.box((x - 0.04, -0.227, 0.7), (0.22, 0.01, 0.05), P("gold"))
    elif shield == "big":
        b.box((0, -0.26, 0.58), (0.52, 0.06, 0.72), color)
        b.box((0, -0.295, 0.58), (0.56, 0.02, 0.06), P("steel_dark"))
        b.box((0, -0.295, 0.88), (0.56, 0.02, 0.06), P("steel_dark"))
        b.sphere((0, -0.3, 0.62), 0.07, P("steel"), seg=6, rings=4)


def _extras(b, extras, skin, top, accent, bulk):
    if "quiver" in extras:
        b.cyl((0.1, 0.15, 0.72), 0.05, 0.32, P("brown"), seg=6, rot=(0.3, 0, 0))
        b.box((0.1, 0.2, 0.9), (0.06, 0.04, 0.06), P("white"))
    if "beard" in extras:
        b.cyl((0, -0.12, 0.79), 0.0, 0.2, P("white"), seg=6, radius2=0.1)
    if "wings" in extras:
        for sx in (1, -1):
            b.box((0.25 * sx, 0.2, 0.8), (0.42, 0.02, 0.3), accent, rot=(0, 0.45 * sx, 0.5 * sx))
    if "tail" in extras:
        b.cyl((0, 0.24, 0.42), 0.035, 0.42, skin, seg=5, radius2=0.01, rot=(-1.0, 0, 0))
    if "mask" in extras:
        b.box((0, -0.15, 0.95), (0.22, 0.02, 0.12), accent)
    if "spikes" in extras:
        for sx in (1, -1):
            b.cyl((0.24 * sx * bulk, 0, 0.86), 0.05, 0.16, P("steel"), seg=4, radius2=0.0, rot=(0, 0.55 * sx, 0))
    if "pads" in extras:
        for sx in (1, -1):
            b.box((0.21 * sx * bulk, 0, 0.77), (0.13, 0.27, 0.09), accent)
    if "belly" in extras:
        b.sphere((0, -0.08, 0.57), 0.19 * bulk, skin, seg=8, rings=6, scale=(1, 0.8, 0.9))
    if "scarf" in extras:
        b.cyl((0, 0, 0.8), 0.12, 0.06, accent, seg=8)
        b.box((0.06, 0.12, 0.7), (0.06, 0.03, 0.18), accent)
    if "flames" in extras:
        for i in range(5):
            a = i / 5 * math.tau
            b.cyl((math.sin(a) * 0.08, math.cos(a) * 0.08, 1.06), 0.05, 0.18, accent, seg=4, radius2=0.0)
    if "aura" in extras:
        b.cyl((0, 0, 0.02), 0.32, 0.02, accent, seg=10)


def _arm_geometry(a, sx, top, skin, bulk):
    a.box((0.165 * sx * bulk, -0.135, 0.68), (0.09, 0.285, 0.09), top, rot=(0, 0, -0.29 * sx))
    a.sphere((0.125 * sx * bulk, -0.27, 0.66), 0.05, skin, seg=6, rings=4)


def humanoid(ctx, k=1.0, skin=None, top=None, low=None, pants=None, boots=None, head="round", hat=None,
             hat_color=None, weapon=None, wcolor=None, accent=None, shield=None, scolor=None, cape=None,
             robe=False, bulk=1.0, eyes=None, extras=(), arm_parts=False):
    skin = skin or P("skin")
    top = top or P("blue")
    pants = pants or sh(top, -0.45)
    boots = boots or P("brown_dark")
    accent = accent or P("gold")
    hat_color = hat_color or top
    wcolor = wcolor or P("steel")
    scolor = scolor or top
    eyes = eyes or P("black")

    b = ctx.part("Body", k)
    if robe:
        b.cyl((0, 0, 0.36), 0.22 * bulk, 0.52, top, seg=8, radius2=0.16 * bulk)
        b.box((0, 0, 0.68), (0.34 * bulk, 0.24, 0.22), top)
        b.cyl((0, 0, 0.11), 0.225 * bulk, 0.03, low or sh(top, -0.3), seg=8)
    else:
        b.box((0, 0, 0.62), (0.36 * bulk, 0.24, 0.34), top)
        b.box((0, 0, 0.43), (0.38 * bulk, 0.26, 0.12), low or sh(top, -0.3))
        b.box((0, 0, 0.495), (0.375 * bulk, 0.255, 0.05), P("brown"))
    b.cyl((0, 0, 0.8), 0.07, 0.06, skin, seg=6)
    _head(b, head, skin, eyes, hat_color)
    _hat(b, hat, hat_color, accent)
    if cape:
        b.box((0, 0.14, 0.58), (0.34 * bulk, 0.03, 0.46), cape, rot=(0.12, 0, 0))
        b.box((0, 0.166, 0.36), (0.345 * bulk, 0.034, 0.05), accent, rot=(0.12, 0, 0))
    _shield(b, shield, scolor, bulk)
    _extras(b, extras, skin, top, accent, bulk)

    if arm_parts:
        # bosses and melee enemies swing their weapon arm, so the arms are separate parts
        for side, sx in (("ArmL", 1), ("ArmR", -1)):
            a = ctx.part(side, k)
            _arm_geometry(a, sx, top, skin, bulk)
            if sx < 0:
                _weapon(a, weapon, wcolor, accent)
            a.build(ctx, pivot=(0.19 * sx * bulk, 0, 0.74))
    else:
        for sx in (1, -1):
            _arm_geometry(b, sx, top, skin, bulk)
        _weapon(b, weapon, wcolor, accent)
    b.build(ctx)
    _legs(ctx, k, pants, boots, robe=robe, spread=0.09 * bulk)


# ---------------------------------------------------------------------------
# Allies: 5 lines x 4 tiers
# ---------------------------------------------------------------------------

def ally_models():
    return {
        "u_archer": lambda ctx: humanoid(ctx, top=C("#4f9e45"), low=C("#6e4526"), head="hood", hat_color=C("#3f7f37"),
                                         weapon="bow", wcolor=P("wood"), extras=("quiver",)),
        "u_crossbow": KIT.build_soldier,
        "u_ranger": lambda ctx: humanoid(ctx, top=C("#2f6f4f"), low=C("#4a2d19"), head="hood", hat_color=C("#24553c"),
                                         weapon="bow", wcolor=C("#8a5a2e"), cape=C("#24553c"), accent=C("#c9e07a"), extras=("quiver",)),
        "u_sniper": lambda ctx: humanoid(ctx, top=C("#f4f1ea"), low=C("#c9b27a"), hat="feather", hat_color=C("#f4f1ea"), accent=P("gold"),
                                         weapon="longbow", wcolor=P("gold"), cape=P("gold"), extras=("quiver", "mask")),
        "u_squire": lambda ctx: humanoid(ctx, top=C("#9a6b45"), low=C("#6e4526"), hat="cap", hat_color=C("#7a4b2a"),
                                         weapon="spear", wcolor=P("steel"), shield="round", scolor=C("#c9a27a")),
        "u_knight": lambda ctx: humanoid(ctx, top=P("steel"), low=C("#3a5aa8"), head="helm", hat_color=P("steel"),
                                         weapon="spear", shield="kite", scolor=C("#2f74e6"), extras=("pads",), accent=P("steel_dark")),
        "u_paladin": lambda ctx: humanoid(ctx, top=C("#f4f1ea"), low=P("gold"), head="helm", hat="plume", hat_color=C("#f4f1ea"),
                                          weapon="hammer", wcolor=P("gold"), shield="tower", scolor=C("#f4f1ea"), accent=P("gold"), extras=("pads",)),
        "u_champion": lambda ctx: humanoid(ctx, top=P("gold"), low=C("#b8860b"), head="helm", hat="plume", hat_color=P("gold"),
                                           weapon="greatsword", wcolor=P("steel"), cape=P("red"), accent=P("red"), extras=("spikes", "pads")),
        "u_apprentice": lambda ctx: humanoid(ctx, top=C("#9a6bd6"), robe=True, hat="wizard", hat_color=C("#7d4bd6"), accent=P("gold"),
                                             weapon="wand", wcolor=P("wood")),
        "u_mage": lambda ctx: humanoid(ctx, top=C("#3a5ad6"), robe=True, hat="wizard", hat_color=C("#2f47b0"), accent=C("#8fd6ff"),
                                       weapon="orbstaff", wcolor=P("wood")),
        "u_wizard": lambda ctx: humanoid(ctx, top=C("#5b2fa8"), robe=True, hat="wizard", hat_color=C("#4a2490"), accent=P("gold"),
                                         weapon="orbstaff", extras=("beard",)),
        "u_archmage": lambda ctx: humanoid(ctx, top=C("#f4f1ea"), robe=True, hat="wizard", hat_color=C("#f4f1ea"), accent=C("#ff6bd6"),
                                           weapon="orbstaff", cape=C("#ff6bd6"), extras=("beard", "aura")),
        "u_musketeer": lambda ctx: humanoid(ctx, top=C("#c0392b"), low=C("#2c2c3a"), hat="feather", hat_color=C("#2c2c3a"), accent=P("white"),
                                            weapon="musket"),
        "u_rifleman": lambda ctx: humanoid(ctx, top=C("#4f6b3a"), low=C("#3a4a2a"), hat="cap", hat_color=C("#3a4a2a"), weapon="rifle"),
        "u_gatling": lambda ctx: humanoid(ctx, top=P("steel_dark"), low=C("#3a3f4a"), head="helm", hat_color=C("#5a616b"), weapon="gatling",
                                          bulk=1.15, extras=("pads",), accent=P("steel")),
        "u_cannoneer": lambda ctx: humanoid(ctx, top=C("#7a4b2a"), low=C("#4a2d19"), hat="bandana", hat_color=P("red"), weapon="cannon",
                                            accent=P("gold"), bulk=1.15, extras=("belly",)),
        "u_acolyte": lambda ctx: humanoid(ctx, top=C("#f4f1ea"), robe=True, head="hood", hat_color=C("#e8e2d4"), weapon="holystaff",
                                          wcolor=P("wood"), accent=C("#ffe98a")),
        "u_cleric": lambda ctx: humanoid(ctx, top=C("#f4f1ea"), low=C("#2f74e6"), robe=True, hat="mitre", hat_color=C("#2f74e6"),
                                         weapon="book", wcolor=C("#2f74e6"), accent=P("gold")),
        "u_bishop": lambda ctx: humanoid(ctx, top=C("#f4f1ea"), low=P("gold"), robe=True, hat="mitre", hat_color=C("#f4f1ea"),
                                         weapon="holystaff", wcolor=P("gold"), accent=C("#ff6b6b"), cape=P("gold")),
        "u_saint": lambda ctx: humanoid(ctx, top=C("#fffbe0"), low=P("gold"), robe=True, hat="halo", weapon="holystaff", wcolor=P("gold"),
                                        accent=C("#fff2a8"), extras=("wings", "aura")),
    }


# ---------------------------------------------------------------------------
# Enemies
# ---------------------------------------------------------------------------

def build_slime(ctx, color):
    b = ctx.part("Body")
    b.sphere((0, 0, 0.27), 0.36, color, seg=10, rings=6, scale=(1, 1, 0.75))
    b.sphere((0.12, -0.12, 0.42), 0.07, sh(color, 0.6), seg=6, rings=4)
    for sx in (1, -1):
        b.box((0.1 * sx, -0.33, 0.33), (0.06, 0.02, 0.09), P("black"))
    b.box((0, -0.34, 0.22), (0.14, 0.02, 0.03), sh(color, -0.5))
    b.build(ctx)


def build_bat(ctx):
    purple = C("#5b4a7a")
    b = ctx.part("Body")
    b.sphere((0, 0, 0.3), 0.13, purple, seg=8, rings=5)
    for sx in (1, -1):
        b.cyl((0.07 * sx, 0, 0.44), 0.035, 0.1, purple, seg=4, radius2=0.0)
        b.box((0.045 * sx, -0.12, 0.33), (0.035, 0.02, 0.035), P("red"))
    b.box((0, -0.125, 0.26), (0.06, 0.02, 0.02), P("white"))
    b.build(ctx)
    for side, sx in (("ArmL", 1), ("ArmR", -1)):
        w = ctx.part(side)
        w.box((0.27 * sx, 0, 0.32), (0.34, 0.12, 0.02), sh(purple, -0.2), rot=(0, 0.15 * sx, 0))
        w.box((0.4 * sx, 0.04, 0.3), (0.12, 0.2, 0.02), sh(purple, -0.35), rot=(0, 0.2 * sx, 0))
        w.build(ctx, pivot=(0.1 * sx, 0, 0.32))


def build_ghost(ctx, color, extras=False):
    b = ctx.part("Body")
    b.cyl((0, 0, 0.42), 0.27, 0.6, color, seg=8, radius2=0.14)
    b.sphere((0, 0, 0.8), 0.19, color, seg=8, rings=6)
    for sx in (1, -1):
        b.box((0.06 * sx, -0.17, 0.82), (0.05, 0.02, 0.08), P("black"))
        b.sphere((0.24 * sx, -0.05, 0.55), 0.07, color, seg=6, rings=4)
    b.box((0, -0.18, 0.72), (0.06, 0.02, 0.06), P("black"))
    if extras:
        for i in range(6):
            a = i / 6 * math.tau
            b.cyl((math.sin(a) * 0.3, math.cos(a) * 0.3, 0.6), 0.05, 0.22, C("#e8fbff"), seg=4, radius2=0.0, rot=(0.4 * math.cos(a), -0.4 * math.sin(a), 0))
        for i in range(4):
            a = i / 4 * math.tau
            b.cyl((math.sin(a) * 0.1, math.cos(a) * 0.1, 1.02), 0.03, 0.14, C("#e8fbff"), seg=4, radius2=0.0)
    b.build(ctx)


def build_wolf(ctx):
    fur = C("#7a7a86")
    b = ctx.part("Body")
    b.box((0, 0, 0.47), (0.3, 0.72, 0.3), fur)
    b.box((0, -0.46, 0.58), (0.22, 0.26, 0.22), fur)
    b.box((0, -0.62, 0.53), (0.12, 0.14, 0.1), sh(fur, -0.2))
    for sx in (1, -1):
        b.cyl((0.07 * sx, -0.42, 0.74), 0.04, 0.1, fur, seg=4, radius2=0.0)
        b.box((0.06 * sx, -0.59, 0.62), (0.035, 0.02, 0.03), P("yellow"))
    b.cyl((0, 0.45, 0.55), 0.05, 0.36, fur, seg=5, radius2=0.02, rot=(-1.1, 0, 0))
    # goblin rider
    b.box((0, 0.02, 0.78), (0.26, 0.2, 0.28), C("#6e4526"))
    b.sphere((0, 0.0, 1.03), 0.12, C("#7fbf3f"), seg=8, rings=5)
    for sx in (1, -1):
        b.cyl((0.14 * sx, 0.0, 1.06), 0.035, 0.12, C("#7fbf3f"), seg=4, radius2=0.0, rot=(0, 1.35 * sx, 0))
        b.box((0.04 * sx, -0.11, 1.05), (0.035, 0.02, 0.03), P("yellow"))
    b.cyl((-0.15, -0.45, 0.85), 0.02, 1.0, P("wood"), seg=5, rot=(math.pi / 2 - 0.2, 0, 0))
    b.cyl((-0.15, -0.97, 0.94), 0.04, 0.14, P("steel"), seg=4, radius2=0.0, rot=(math.pi / 2 - 0.2, 0, 0))
    b.build(ctx)
    for side, y in (("LegL", -0.25), ("LegR", 0.25)):
        leg = ctx.part(side)
        for sx in (1, -1):
            leg.box((0.1 * sx, y, 0.18), (0.08, 0.09, 0.36), sh(fur, -0.15))
        leg.build(ctx, pivot=(0, y, 0.36))


def build_beetle(ctx):
    shell = C("#2f7f5f")
    b = ctx.part("Body")
    b.sphere((0, 0.04, 0.27), 0.36, shell, seg=10, rings=6, scale=(1, 1.2, 0.6))
    b.box((0, 0.04, 0.48), (0.02, 0.7, 0.05), sh(shell, -0.4))
    b.sphere((0, -0.38, 0.22), 0.14, C("#1f2a24"), seg=8, rings=5)
    b.cyl((0, -0.52, 0.32), 0.04, 0.24, C("#cfd6df"), seg=4, radius2=0.0, rot=(math.pi / 2 - 0.6, 0, 0))
    for sx in (1, -1):
        b.box((0.06 * sx, -0.48, 0.24), (0.035, 0.02, 0.035), P("red"))
        for i, y in enumerate((-0.2, 0.05, 0.3)):
            b.box((0.36 * sx, y, 0.1), (0.3, 0.04, 0.04), C("#1f2a24"), rot=(0, 0.6 * sx, 0.2 * (i - 1)))
    b.build(ctx)


def build_mimic(ctx):
    wood = P("wood")
    dark = P("wood_dark")
    b = ctx.part("Body")
    b.box((0, 0, 0.35), (0.9, 0.9, 0.6), wood)
    b.box((0, 0.1, 0.82), (0.9, 0.9, 0.22), wood, rot=(-0.45, 0, 0))
    for sx in (-0.45, 0.45):
        b.box((sx, 0, 0.35), (0.1, 0.92, 0.62), dark)
    for i in range(6):
        x = -0.35 + i * 0.14
        b.cyl((x, -0.44, 0.69), 0.04, 0.12, P("white"), seg=4, radius2=0.0, rot=(math.pi, 0, 0))
        b.cyl((x + 0.07, -0.5, 0.78), 0.04, 0.12, P("white"), seg=4, radius2=0.0)
    b.box((0, -0.5, 0.64), (0.22, 0.3, 0.04), P("red"), rot=(0.4, 0, 0))
    for sx in (1, -1):
        b.sphere((0.22 * sx, -0.42, 0.96), 0.06, P("yellow"), seg=6, rings=4)
    b.build(ctx)


def build_ram(ctx):
    wood = C("#7a5230")
    b = ctx.part("Body")
    b.cyl((0, 0, 0.55), 0.22, 1.5, wood, seg=8, rot=(math.pi / 2, 0, 0))
    b.sphere((0, -0.8, 0.55), 0.26, P("steel_dark"), seg=8, rings=6, scale=(1, 0.8, 1))
    for sx in (1, -1):
        b.cyl((0.18 * sx, -0.92, 0.7), 0.06, 0.25, P("steel"), seg=5, radius2=0.0, rot=(0.6, 0, 0.6 * sx))
        b.box((0.33 * sx, 0, 0.95), (0.5, 1.4, 0.06), C("#8a1c18"), rot=(0, 0.6 * sx, 0))
        for y in (-0.5, 0.5):
            b.cyl((0.32 * sx, y, 0.2), 0.2, 0.08, P("black"), seg=8, rot=(0, math.pi / 2, 0))
    b.box((0, 0, 1.2), (0.12, 1.42, 0.08), P("wood_dark"))
    b.build(ctx)


def build_golem(ctx, k=1.0, king=False):
    stone = C("#9a9a9a") if not king else C("#7f7f8f")
    moss = C("#6f8f4a")
    b = ctx.part("Body", k)
    b.box((0, 0, 1.25), (0.95, 0.62, 0.8), stone)
    b.box((0.25, -0.31, 1.45), (0.3, 0.02, 0.2), moss)
    b.sphere((0, -0.33, 1.25), 0.12, C("#5fe0ff") if not king else C("#ff5fd6"), seg=6, rings=4)
    b.box((0, -0.02, 1.88), (0.42, 0.38, 0.36), stone)
    for sx in (1, -1):
        b.box((0.1 * sx, -0.2, 1.9), (0.08, 0.02, 0.05), P("yellow"))
        b.box((0.55 * sx, 0, 1.6), (0.3, 0.4, 0.3), sh(stone, -0.1))
    b.box((0, 0, 0.82), (0.8, 0.5, 0.2), sh(stone, -0.15))
    if king:
        b.cyl((0, -0.02, 2.12), 0.2, 0.1, P("gold"), seg=8)
        for i in range(5):
            a = i / 5 * math.tau
            b.cyl((math.sin(a) * 0.17, -0.02 - math.cos(a) * 0.17, 2.24), 0.05, 0.14, P("gold"), seg=4, radius2=0.0)
        for i in range(3):
            b.ico((0.35 - i * 0.35, 0.3, 1.75), 0.12, C("#ff5fd6"), subdiv=1)
    b.build(ctx)
    for side, sx in (("ArmL", 1), ("ArmR", -1)):
        a = ctx.part(side, k)
        a.box((0.7 * sx, -0.05, 1.25), (0.28, 0.3, 0.6), stone)
        a.box((0.72 * sx, -0.12, 0.8), (0.36, 0.38, 0.34), sh(stone, -0.1))
        a.build(ctx, pivot=(0.62 * sx, 0, 1.6))
    for side, sx in (("LegL", 1), ("LegR", -1)):
        leg = ctx.part(side, k)
        leg.box((0.24 * sx, 0, 0.4), (0.32, 0.36, 0.8), sh(stone, -0.05))
        leg.build(ctx, pivot=(0.24 * sx, 0, 0.8))


def build_dragon(ctx, k=1.0, scales=None, belly=None, boss=False):
    scales = scales or C("#d23a30")
    belly = belly or C("#ffcf6b")
    b = ctx.part("Body", k)
    b.sphere((0, 0.05, 0.62), 0.32, scales, seg=10, rings=6, scale=(1, 1.4, 0.95))
    b.sphere((0, -0.12, 0.55), 0.24, belly, seg=8, rings=5, scale=(0.9, 1.2, 0.9))
    b.cyl((0, -0.38, 0.85), 0.12, 0.42, scales, seg=6, radius2=0.09, rot=(-0.7, 0, 0))
    b.box((0, -0.58, 1.05), (0.24, 0.32, 0.2), scales)
    b.box((0, -0.76, 1.0), (0.16, 0.16, 0.12), sh(scales, -0.1))
    for sx in (1, -1):
        b.box((0.07 * sx, -0.66, 1.12), (0.04, 0.02, 0.04), P("yellow"))
        b.cyl((0.08 * sx, -0.5, 1.2), 0.035, 0.18, P("white"), seg=4, radius2=0.0, rot=(0.5, 0.3 * sx, 0))
    b.cyl((0, 0.62, 0.5), 0.1, 0.7, scales, seg=6, radius2=0.02, rot=(-1.25, 0, 0))
    if boss:
        for i in range(5):
            b.cyl((0, 0.1 - i * 0.15 + 0.3, 0.98 - abs(i - 2) * 0.03), 0.05, 0.16, sh(scales, -0.3), seg=4, radius2=0.0)
    b.build(ctx)
    for side, sx in (("ArmL", 1), ("ArmR", -1)):
        w = ctx.part(side, k)
        w.box((0.45 * sx, 0.1, 0.95), (0.62, 0.05, 0.08), sh(scales, -0.25), rot=(0, -0.35 * sx, 0))
        w.box((0.5 * sx, 0.22, 0.82), (0.6, 0.3, 0.02), sh(scales, 0.15), rot=(0.2, -0.35 * sx, 0))
        w.box((0.42 * sx, 0.3, 0.78), (0.4, 0.22, 0.02), sh(scales, 0.3), rot=(0.3, -0.25 * sx, 0))
        w.build(ctx, pivot=(0.2 * sx, 0.05, 0.9))
    for side, sx in (("LegL", 1), ("LegR", -1)):
        leg = ctx.part(side, k)
        leg.box((0.2 * sx, 0.0, 0.2), (0.12, 0.16, 0.4), scales)
        leg.box((0.2 * sx, -0.06, 0.04), (0.14, 0.24, 0.08), P("white"))
        leg.build(ctx, pivot=(0.2 * sx, 0, 0.4))


def enemy_models():
    orc = C("#7fb046")
    gob = C("#7fbf3f")
    return {
        "e_goblin": lambda ctx: humanoid(ctx, skin=gob, top=C("#8a6a45"), low=C("#5a4530"), head="goblin", eyes=P("yellow"),
                                         weapon="dagger", wcolor=P("steel"), accent=P("brown_dark")),
        "e_runner": lambda ctx: humanoid(ctx, skin=orc, top=C("#ffb02e"), low=C("#8a5a1e"), head="orc", eyes=P("yellow"),
                                         hat="bandana", hat_color=C("#c0392b"), weapon="dualaxe", wcolor=P("steel")),
        "e_skeleton": lambda ctx: humanoid(ctx, skin=C("#efe9dc"), top=C("#d8d1c2"), low=C("#a89f8f"), pants=C("#efe9dc"),
                                           boots=C("#cfc7b6"), head="skull", eyes=P("black"), weapon="sword", wcolor=C("#a8743a")),
        "e_zombie": lambda ctx: humanoid(ctx, skin=C("#8fae6a"), top=C("#5a6f8a"), low=C("#4a3f33"), head="zombie", eyes=P("black")),
        "e_shield": lambda ctx: humanoid(ctx, skin=orc, top=C("#8e1c18"), head="orc", hat="horned", hat_color=C("#5a1410"),
                                         eyes=P("yellow"), shield="big", scolor=C("#9a7a52"), weapon="axe"),
        "e_slime": lambda ctx: build_slime(ctx, C("#52d6a0")),
        "e_bat": build_bat,
        "e_imp": lambda ctx: humanoid(ctx, skin=C("#e2382f"), top=C("#8e1c18"), head="imp", eyes=P("yellow"), weapon="bomb",
                                      accent=C("#ffd34d"), extras=("tail",)),
        "e_knight": lambda ctx: humanoid(ctx, top=C("#3a3150"), low=C("#26203a"), head="helm", hat_color=C("#2a2338"),
                                         weapon="sword", wcolor=C("#a8a0c8"), shield="kite", scolor=C("#5a2a8a"), accent=C("#7d4bd6"),
                                         extras=("pads", "spikes")),
        "e_shaman": lambda ctx: humanoid(ctx, skin=orc, top=C("#5a7f9a"), robe=True, head="orc", eyes=P("yellow"), hat="headdress",
                                         hat_color=C("#e2382f"), accent=C("#ffd34d"), weapon="skullstaff", extras=("mask",)),
        "e_archer": lambda ctx: humanoid(ctx, skin=gob, top=C("#6f8f3a"), low=C("#4a5a2a"), head="goblin", eyes=P("yellow"),
                                         weapon="bow", wcolor=P("wood"), extras=("quiver",)),
        "e_ghost": lambda ctx: build_ghost(ctx, C("#dfe8ff")),
        "e_wolf": build_wolf,
        "e_berserker": lambda ctx: humanoid(ctx, skin=orc, top=orc, low=C("#6e4526"), head="orc", eyes=P("red"), hat="mohawk",
                                            hat_color=C("#ff5a2e"), weapon="dualaxe", wcolor=P("steel"), bulk=1.25, extras=("spikes",)),
        "e_drummer": lambda ctx: humanoid(ctx, skin=orc, top=C("#8e5a2a"), head="orc", eyes=P("yellow"), hat="horned",
                                          hat_color=C("#5a3a1a"), weapon="drum", wcolor=C("#c0392b"), bulk=1.15),
        "e_troll": lambda ctx: KIT.build_ogre(ctx, C("#7a9aa8"), C("#56707a"), C("#6e4526"), C("#4a2d19")),
        "e_spearman": lambda ctx: humanoid(ctx, skin=orc, top=C("#6b4f2a"), low=C("#4a3520"), head="orc", eyes=P("yellow"),
                                           hat="horned", hat_color=C("#5a5a5a"), weapon="pike", wcolor=P("steel"), extras=("pads",),
                                           accent=P("steel_dark")),
        "e_necro": lambda ctx: humanoid(ctx, skin=C("#d8d1c2"), top=C("#2a1f3a"), robe=True, head="skull", eyes=C("#b06bff"),
                                        hat_color=C("#2a1f3a"), weapon="skullstaff", accent=C("#b06bff"), cape=C("#4a2a6a")),
        "e_beetle": build_beetle,
        "e_mimic": build_mimic,
        "e_blinker": lambda ctx: humanoid(ctx, skin=gob, top=C("#7d4bd6"), robe=True, head="goblin", eyes=P("yellow"), hat="wizard",
                                          hat_color=C("#5a2fa8"), accent=C("#ff6bd6"), weapon="crystal"),
        "e_golem": lambda ctx: build_golem(ctx),
        "e_icewraith": lambda ctx: build_ghost(ctx, C("#9fe6ff"), extras=True),
        "e_goldgoblin": lambda ctx: humanoid(ctx, skin=gob, top=P("gold"), low=C("#b8860b"), head="goblin", eyes=P("black"),
                                             hat="crown", accent=P("red"), weapon="sack"),
        "e_captain": lambda ctx: humanoid(ctx, skin=orc, top=C("#8e1c18"), low=C("#4a1410"), head="orc", eyes=P("yellow"),
                                          hat="horned", hat_color=C("#2a2a2a"), weapon="greatsword", wcolor=P("steel"),
                                          cape=C("#b02a2a"), bulk=1.25, extras=("pads", "spikes"), accent=P("gold"), arm_parts=True),
        "e_ram": build_ram,
        "e_fireimp": lambda ctx: humanoid(ctx, skin=C("#ff8a2e"), top=C("#c0392b"), head="imp", eyes=P("yellow"), weapon="bomb",
                                          accent=C("#ff4a2e"), extras=("tail", "flames")),
        "e_whelp": lambda ctx: build_dragon(ctx),
    }


def boss_models():
    return {
        "b_lich": lambda ctx: humanoid(ctx, skin=C("#d8d1c2"), top=C("#3a2458"), robe=True, head="skull", eyes=C("#7dffb0"), hat="crown",
                                       accent=C("#7dffb0"), weapon="skullstaff", cape=C("#1f1430"), bulk=1.15, arm_parts=True),
        "b_golemking": lambda ctx: build_golem(ctx, king=True),
        "b_dragon": lambda ctx: build_dragon(ctx, k=1.3, scales=C("#8e1c18"), belly=C("#ff9f4a"), boss=True),
        "b_demon": lambda ctx: humanoid(ctx, skin=C("#c0392b"), top=C("#1f1f26"), low=C("#3a1414"), head="imp", eyes=P("yellow"),
                                        weapon="trident", wcolor=P("gold"), cape=C("#14141a"), accent=C("#ff4a2e"), bulk=1.3,
                                        extras=("wings", "spikes", "tail"), arm_parts=True),
        "b_shogun": lambda ctx: humanoid(ctx, top=C("#8e1c18"), low=C("#1f1f26"), head="round", hat="kabuto", hat_color=C("#1f1f26"),
                                         accent=P("gold"), weapon="katana", wcolor=P("steel"), bulk=1.3, extras=("pads", "mask"),
                                         eyes=P("red"), arm_parts=True),
    }


def models(kit):
    """All character builders keyed by model name."""
    _setup(kit)
    result = {}
    result.update(ally_models())
    result.update(enemy_models())
    result.update(boss_models())
    return result
