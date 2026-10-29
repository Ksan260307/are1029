"""Number Road - procedural low-poly asset generator (Blender 4.2+ / 5.x).

Every 3D model the Unity project uses is built here from code, so the art can
be regenerated, tweaked and reviewed like any other source file.

    blender -b --factory-startup -P blender/build_models.py -- [options]

Options
    --out DIR        FBX output folder
                     (default: NumberRoad/Assets/NumberRoad/Resources/Models)
    --icons DIR      also render the app / web icons into DIR
    --preview FILE   also render a contact sheet of all models (.png or .jpg)
    --blend FILE     also save the generated scene as .blend for hand editing
    --only a,b,c     build only the listed models

Conventions (keep these in sync with ModelLibrary.cs on the Unity side)
    * Blender is Z-up and every model faces -Y (Blender "front" view).
      FBX export uses forward=-Z / up=Y with baked space transform, so the
      models arrive in Unity facing +Z, Y-up, 1 unit = 1 metre, no root rotation.
    * A model is one or more mesh objects named "<model>_<Part>". The object
      origin is the pivot Unity animates around (legs swing at the hip, the
      club arm swings at the shoulder).
    * Colours live in the "Col" colour attribute (sRGB). All parts share the
      material "NR_VertexColor", which Unity remaps to its NR_Toon material.
"""

import argparse
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
DEFAULT_OUT = os.path.join(REPO, "NumberRoad", "Assets", "NumberRoad", "Resources", "Models")
MATERIAL_NAME = "NR_VertexColor"


def hexcol(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


PAL = {
    "skin": hexcol("#f6c9a0"),
    "blue": hexcol("#2f74e6"),
    "blue_dark": hexcol("#1d3f86"),
    "blue_light": hexcol("#7fb4ff"),
    "steel": hexcol("#cfd6df"),
    "steel_dark": hexcol("#6b7380"),
    "white": hexcol("#f4f1ea"),
    "black": hexcol("#1b1b22"),
    "gold": hexcol("#ffc53a"),
    "wood": hexcol("#b07a45"),
    "wood_dark": hexcol("#6e4526"),
    "brown": hexcol("#7a4b2a"),
    "brown_dark": hexcol("#4a2d19"),
    "red": hexcol("#e2382f"),
    "red_dark": hexcol("#8e1c18"),
    "orc": hexcol("#7fb046"),
    "orc_dark": hexcol("#4f7a27"),
    "yellow": hexcol("#ffd84a"),
    "ogre": hexcol("#e9a37e"),
    "ogre_dark": hexcol("#b8714f"),
    "king": hexcol("#9db36b"),
    "king_dark": hexcol("#647a3a"),
    "stone": hexcol("#d8cfbf"),
    "stone_mid": hexcol("#c4b9a6"),
    "stone_dark": hexcol("#9e9483"),
    "grout": hexcol("#8f8676"),
    "enemy_stone": hexcol("#8a7f76"),
    "enemy_stone_dark": hexcol("#645a53"),
    "door": hexcol("#2b1d14"),
    "rock": hexcol("#8e8a84"),
    "roof_blue": hexcol("#2f6fe0"),
    "roof_red": hexcol("#d23a30"),
}


def shade(color, amount):
    """Brighten (amount > 0) or darken (amount < 0) an sRGB colour."""
    return tuple(min(1.0, max(0.0, c * (1.0 + amount))) for c in color)


# ---------------------------------------------------------------------------
# Mesh building
# ---------------------------------------------------------------------------

class Part:
    """Accumulates coloured primitives into one bmesh, then becomes an object."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.layer = self.bm.loops.layers.color.new("Col")

    @staticmethod
    def _matrix(loc, rot, scale):
        return Matrix.LocRotScale(Vector(loc), Euler(rot), Vector(scale))

    def _paint(self, verts, color):
        rgba = (color[0], color[1], color[2], 1.0)
        faces = set()
        for v in verts:
            faces.update(v.link_faces)
        for face in faces:
            for loop in face.loops:
                loop[self.layer] = rgba
        return verts

    def box(self, loc, size, color, rot=(0, 0, 0)):
        verts = bmesh.ops.create_cube(self.bm, size=1.0, matrix=self._matrix(loc, rot, size))["verts"]
        return self._paint(verts, color)

    def cyl(self, loc, radius, depth, color, seg=8, radius2=None, rot=(0, 0, 0), scale=(1, 1, 1)):
        verts = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False, segments=seg,
            radius1=radius, radius2=radius if radius2 is None else radius2,
            depth=depth, matrix=self._matrix(loc, rot, scale))["verts"]
        return self._paint(verts, color)

    def sphere(self, loc, radius, color, seg=8, rings=6, scale=(1, 1, 1), rot=(0, 0, 0)):
        verts = bmesh.ops.create_uvsphere(
            self.bm, u_segments=seg, v_segments=rings, radius=radius,
            matrix=self._matrix(loc, rot, scale))["verts"]
        return self._paint(verts, color)

    def ico(self, loc, radius, color, subdiv=1, scale=(1, 1, 1), rot=(0, 0, 0), jitter=0.0, rng=None):
        verts = bmesh.ops.create_icosphere(
            self.bm, subdivisions=subdiv, radius=radius,
            matrix=self._matrix(loc, rot, scale))["verts"]
        if jitter and rng:
            for v in verts:
                v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * jitter
        return self._paint(verts, color)

    def build(self, ctx, pivot=(0, 0, 0)):
        pivot = Vector(pivot)
        bmesh.ops.translate(self.bm, vec=-pivot, verts=self.bm.verts)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = False
        mesh.color_attributes.active_color = mesh.color_attributes["Col"]
        mesh.materials.append(ctx.material)
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = pivot
        ctx.collection.objects.link(obj)
        ctx.objects.append(obj)
        return obj


class ModelContext:
    def __init__(self, name, material):
        self.name = name
        self.material = material
        self.collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(self.collection)
        self.objects = []

    def part(self, suffix):
        return Part(f"{self.name}_{suffix}")


def mirror_x(loc):
    return (-loc[0], loc[1], loc[2])


# ---------------------------------------------------------------------------
# Characters
# ---------------------------------------------------------------------------

def build_soldier(ctx):
    """Blue crossbow knight. Seen mostly from behind, so the back gets a cape."""
    body = ctx.part("Body")
    body.box((0, 0, 0.62), (0.36, 0.24, 0.34), PAL["blue"])
    body.box((0, 0, 0.43), (0.38, 0.26, 0.12), PAL["blue_dark"])
    body.box((0, 0, 0.495), (0.375, 0.255, 0.05), PAL["brown"])
    body.box((0, -0.13, 0.495), (0.06, 0.02, 0.05), PAL["gold"])
    body.box((0, -0.122, 0.64), (0.12, 0.012, 0.12), PAL["white"])
    body.box((0, -0.129, 0.64), (0.06, 0.012, 0.06), PAL["gold"], rot=(0, math.pi / 4, 0))
    for sx in (1, -1):
        body.box((0.21 * sx, 0, 0.76), (0.12, 0.26, 0.08), PAL["steel"])
        # arms reach forward to the crossbow
        body.box((0.165 * sx, -0.135, 0.68), (0.09, 0.285, 0.09), PAL["blue"], rot=(0, 0, -0.29 * sx))
        body.sphere((0.125 * sx, -0.27, 0.66), 0.05, PAL["skin"], seg=6, rings=4)
    # cape on the back (what the camera sees most)
    body.box((0, 0.14, 0.6), (0.34, 0.03, 0.42), PAL["blue_dark"], rot=(0.12, 0, 0))
    body.box((0, 0.166, 0.39), (0.345, 0.034, 0.05), PAL["white"], rot=(0.12, 0, 0))
    body.box((0, 0.152, 0.7), (0.11, 0.02, 0.11), PAL["gold"], rot=(0.12, math.pi / 4, 0))
    # head + helmet
    body.cyl((0, 0, 0.8), 0.07, 0.06, PAL["skin"], seg=8)
    body.sphere((0, 0, 0.93), 0.15, PAL["skin"], seg=10, rings=7)
    for sx in (1, -1):
        body.box((0.055 * sx, -0.138, 0.94), (0.035, 0.02, 0.05), PAL["black"])
    body.cyl((0, 0, 1.0), 0.162, 0.08, PAL["steel"], seg=10)
    body.cyl((0, 0, 1.12), 0.165, 0.17, PAL["blue"], seg=10, radius2=0.03)
    body.sphere((0, 0, 1.215), 0.035, PAL["gold"], seg=6, rings=4)
    body.box((0, -0.158, 0.965), (0.03, 0.02, 0.09), PAL["steel"])
    # crossbow
    body.box((0, -0.36, 0.665), (0.05, 0.34, 0.06), PAL["wood"])
    for sx in (1, -1):
        body.box((0.11 * sx, -0.49, 0.68), (0.23, 0.035, 0.035), PAL["wood_dark"], rot=(0, 0, 0.35 * sx))
    body.box((0, -0.5, 0.705), (0.016, 0.2, 0.016), PAL["steel"])
    body.build(ctx)

    for side, sx in (("LegL", 1), ("LegR", -1)):
        leg = ctx.part(side)
        x = 0.09 * sx
        leg.box((x, 0, 0.22), (0.13, 0.15, 0.4), PAL["blue_dark"])
        leg.box((x, -0.02, 0.055), (0.145, 0.2, 0.11), PAL["brown_dark"])
        leg.build(ctx, pivot=(x, 0, 0.42))


def build_grunt(ctx):
    """Red-armoured orc foot soldier with an axe and a round shield."""
    body = ctx.part("Body")
    body.box((0, 0, 0.66), (0.42, 0.28, 0.38), PAL["red"])
    body.box((0, -0.142, 0.7), (0.26, 0.012, 0.2), PAL["red_dark"])
    body.box((0, -0.149, 0.72), (0.08, 0.012, 0.08), PAL["white"], rot=(0, math.pi / 4, 0))
    body.box((0, -0.02, 0.52), (0.4, 0.27, 0.12), PAL["brown_dark"])
    body.box((0, -0.16, 0.52), (0.07, 0.02, 0.06), PAL["steel"])
    for sx in (1, -1):
        body.sphere((0.24 * sx, 0, 0.8), 0.1, PAL["steel_dark"], seg=8, rings=5, scale=(1, 1, 0.7))
        body.cyl((0.27 * sx, 0, 0.88), 0.05, 0.14, PAL["white"], seg=6, radius2=0.0, rot=(0, 0.55 * sx, 0))
        body.box((0.27 * sx, -0.03, 0.62), (0.1, 0.11, 0.3), PAL["orc"])
        body.sphere((0.27 * sx, -0.06, 0.46), 0.06, PAL["orc"], seg=6, rings=4)
    # head
    body.sphere((0, -0.02, 1.0), 0.17, PAL["orc"], seg=10, rings=7, scale=(1.05, 1, 0.95))
    body.box((0, -0.09, 0.92), (0.26, 0.16, 0.1), PAL["orc"])
    for sx in (1, -1):
        body.cyl((0.075 * sx, -0.17, 0.97), 0.022, 0.07, PAL["white"], seg=5, radius2=0.0)
        body.box((0.06 * sx, -0.175, 1.04), (0.05, 0.02, 0.035), PAL["yellow"])
        body.box((0.06 * sx, -0.184, 1.04), (0.018, 0.012, 0.03), PAL["black"])
        body.box((0.065 * sx, -0.18, 1.08), (0.09, 0.025, 0.028), PAL["orc_dark"], rot=(0, -0.35 * sx, 0))
        body.cyl((0.17 * sx, 0, 1.16), 0.045, 0.2, PAL["white"], seg=6, radius2=0.0, rot=(0, 0.8 * sx, 0))
    body.sphere((0, 0, 1.08), 0.18, PAL["red_dark"], seg=10, rings=6, scale=(1, 1, 0.62))
    # axe in the right hand (x < 0)
    body.box((-0.27, -0.1975, 0.6675), (0.04, 0.04, 0.6), PAL["wood"], rot=(0.58, 0, 0))
    body.box((-0.35, -0.36, 0.9), (0.16, 0.03, 0.2), PAL["steel"], rot=(0.58, 0, 0))
    # shield on the left arm
    body.cyl((0.31, -0.115, 0.62), 0.18, 0.03, PAL["steel_dark"], seg=10, rot=(math.pi / 2, 0, 0))
    body.cyl((0.31, -0.13, 0.62), 0.15, 0.03, PAL["red"], seg=10, rot=(math.pi / 2, 0, 0))
    body.sphere((0.31, -0.15, 0.62), 0.05, PAL["gold"], seg=6, rings=4)
    body.build(ctx)

    for side, sx in (("LegL", 1), ("LegR", -1)):
        leg = ctx.part(side)
        x = 0.1 * sx
        leg.box((x, 0, 0.24), (0.14, 0.16, 0.42), PAL["brown_dark"])
        leg.box((x, -0.03, 0.06), (0.16, 0.22, 0.12), PAL["black"])
        leg.build(ctx, pivot=(x, 0, 0.45))


def build_ogre(ctx, skin, skin_dark, cloth, cloth_dark, king=False):
    """Big melee enemy. `king` adds crown, armour, cape and a spiked club."""
    body = ctx.part("Body")
    body.sphere((0, -0.12, 1.25), 0.55, skin, seg=12, rings=8, scale=(1.0, 0.85, 0.95))
    body.box((0, 0.08, 1.55), (1.05, 0.6, 0.7), cloth)
    for sx in (1, -1):
        body.box((0.4 * sx, -0.2, 1.55), (0.28, 0.25, 0.7), cloth)
    body.box((0, -0.02, 0.9), (1.0, 0.76, 0.16), PAL["brown_dark"])
    body.box((0, -0.4, 0.9), (0.2, 0.04, 0.14), PAL["gold"])
    body.box((0, -0.32, 0.72), (0.36, 0.05, 0.3), cloth_dark)
    # head
    body.box((0, -0.05, 2.08), (0.56, 0.5, 0.5), skin)
    body.box((0, -0.12, 1.88), (0.5, 0.4, 0.2), skin)
    body.box((0, 0.05, 2.36), (0.6, 0.56, 0.12), PAL["brown_dark"])
    body.box((0, 0.25, 2.15), (0.58, 0.1, 0.4), PAL["brown_dark"])
    for sx in (1, -1):
        body.box((0.13 * sx, -0.31, 2.21), (0.19, 0.05, 0.06), PAL["brown_dark"], rot=(0, -0.3 * sx, 0))
        body.box((0.12 * sx, -0.302, 2.12), (0.11, 0.02, 0.07), PAL["white"])
        body.box((0.11 * sx, -0.314, 2.115), (0.045, 0.012, 0.05), PAL["black"])
        body.sphere((0.3 * sx, 0, 2.08), 0.075, skin, seg=6, rings=4)
        body.sphere((0.56 * sx, 0, 1.78), 0.2, skin, seg=8, rings=6)
    body.box((0, -0.335, 2.03), (0.11, 0.08, 0.1), skin_dark)
    body.box((0, -0.322, 1.9), (0.24, 0.02, 0.05), PAL["red_dark"])
    body.box((0.06, -0.328, 1.925), (0.04, 0.01, 0.03), PAL["white"])
    body.box((-0.06, -0.328, 1.925), (0.04, 0.01, 0.03), PAL["white"])
    if king:
        body.cyl((0, -0.02, 2.5), 0.3, 0.14, PAL["gold"], seg=10)
        for k in range(5):
            a = k / 5 * math.tau
            body.cyl((math.sin(a) * 0.27, -0.02 - math.cos(a) * 0.27, 2.64), 0.06, 0.16, PAL["gold"], seg=4, radius2=0.0)
        body.sphere((0, -0.31, 2.52), 0.05, PAL["red"], seg=6, rings=4)
        for sx in (1, -1):
            body.sphere((0.58 * sx, 0, 1.86), 0.26, PAL["steel_dark"], seg=8, rings=6, scale=(1, 1, 0.75))
            for k in range(3):
                body.cyl((0.58 * sx + 0.12 * (k - 1) * sx, 0, 2.06), 0.06, 0.22, PAL["steel"], seg=5, radius2=0.0,
                         rot=(0, 0.35 * sx, 0))
        body.box((0, 0.42, 1.25), (0.95, 0.06, 1.35), PAL["red"], rot=(0.12, 0, 0))
        body.box((0, 0.5, 0.6), (0.97, 0.07, 0.12), PAL["gold"], rot=(0.12, 0, 0))
    body.build(ctx)

    for side, sx in (("ArmL", 1), ("ArmR", -1)):
        arm = ctx.part(side)
        x = 0.58 * sx
        arm.cyl((0.62 * sx, 0, 1.5), 0.13, 0.5, skin, seg=8)
        arm.cyl((0.66 * sx, -0.05, 1.12), 0.14, 0.42, skin, seg=8)
        arm.cyl((0.66 * sx, -0.05, 0.96), 0.15, 0.08, PAL["brown_dark"], seg=8)
        arm.sphere((0.66 * sx, -0.07, 0.85), 0.14, skin, seg=8, rings=6)
        if sx < 0:
            # club in the right hand pointing forward/up: swung by rotating the arm
            club_color = PAL["steel_dark"] if king else PAL["wood"]
            arm.cyl((-0.66, -0.546, 1.125), 0.06, 1.2, PAL["wood_dark"] if king else PAL["wood"], seg=8,
                    radius2=0.18, rot=(1.047, 0, 0))
            if king:
                tip = Vector((-0.66, -0.07, 0.85)) + Vector((0, -0.866, 0.5)) * 1.05
                arm.sphere(tuple(tip), 0.22, club_color, seg=8, rings=6)
                for k in range(6):
                    a = k / 6 * math.tau
                    # perpendicular to the club axis (0, -0.866, 0.5)
                    d = Vector((math.cos(a), math.sin(a) * 0.5, math.sin(a) * 0.866))
                    arm.cyl(tuple(tip + d.normalized() * 0.22), 0.05, 0.16, PAL["steel"], seg=4, radius2=0.0,
                            rot=Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler())
        arm.build(ctx, pivot=(x, 0, 1.78))

    for side, sx in (("LegL", 1), ("LegR", -1)):
        leg = ctx.part(side)
        x = 0.28 * sx
        leg.box((x, 0, 0.5), (0.3, 0.32, 0.7), cloth_dark)
        leg.box((x, -0.08, 0.09), (0.32, 0.42, 0.18), PAL["brown_dark"])
        leg.build(ctx, pivot=(x, 0, 0.85))


def build_brute(ctx):
    build_ogre(ctx, PAL["ogre"], PAL["ogre_dark"], PAL["red"], PAL["red_dark"])


def build_boss(ctx):
    build_ogre(ctx, PAL["king"], PAL["king_dark"], PAL["red_dark"], PAL["brown_dark"], king=True)


# ---------------------------------------------------------------------------
# Props
# ---------------------------------------------------------------------------

def build_gate_pillar(ctx):
    p = ctx.part("Main")
    p.box((0, 0, 0.12), (0.5, 0.5, 0.24), PAL["stone_dark"])
    p.box((0, 0, 1.35), (0.34, 0.34, 2.3), PAL["white"])
    p.box((0, 0, 0.36), (0.4, 0.4, 0.1), PAL["gold"])
    p.box((0, 0, 2.45), (0.42, 0.42, 0.1), PAL["gold"])
    p.cyl((0, 0, 2.65), 0.3, 0.3, PAL["gold"], seg=4, radius2=0.0, rot=(0, 0, math.pi / 4))
    p.build(ctx)


def build_gate_beam(ctx):
    """Unit-length beam along X; Unity stretches it to the gate width."""
    b = ctx.part("Main")
    b.box((0, 0, 0), (1.0, 0.26, 0.26), PAL["white"])
    for y in (-0.135, 0.135):
        b.box((0, y, 0), (1.0, 0.012, 0.08), PAL["gold"])
    b.build(ctx)


def build_crate(ctx):
    c = ctx.part("Main")
    c.box((0, 0, 0.55), (1.0, 1.0, 1.0), PAL["wood"])
    t = 0.14
    for sx in (-0.5, 0.5):
        for sy in (-0.5, 0.5):
            c.box((sx, sy, 0.55), (t, t, 1.1), PAL["wood_dark"])
    for z in (0.05, 1.05):
        for s in (-0.5, 0.5):
            c.box((0, s, z), (1.1, t, t), PAL["wood_dark"])
            c.box((s, 0, z), (t, 1.1, t), PAL["wood_dark"])
    for y in (-0.505, 0.505):
        c.box((0, y, 0.55), (1.2, 0.06, 0.12), PAL["wood_dark"], rot=(0, math.pi / 4, 0))
    for x in (-0.505, 0.505):
        c.box((x, 0, 0.55), (0.06, 1.2, 0.12), PAL["wood_dark"], rot=(math.pi / 4, 0, 0))
    for sx in (-0.52, 0.52):
        for sy in (-0.52, 0.52):
            for z in (0.06, 1.04):
                c.box((sx, sy, z), (0.17, 0.17, 0.17), PAL["steel_dark"])
    c.build(ctx)


def build_banner(ctx):
    b = ctx.part("Main")
    b.cyl((0, 0, 1.2), 0.04, 2.4, PAL["wood_dark"], seg=6)
    b.sphere((0, 0, 2.43), 0.07, PAL["gold"], seg=6, rings=4)
    b.box((0, 0, 2.25), (0.72, 0.045, 0.045), PAL["wood_dark"])
    b.box((0, -0.03, 1.74), (0.62, 0.03, 0.96), PAL["blue"])
    b.box((0, -0.03, 1.24), (0.62, 0.032, 0.06), PAL["gold"])
    b.box((0, -0.05, 1.82), (0.26, 0.02, 0.26), PAL["white"], rot=(0, math.pi / 4, 0))
    b.build(ctx)


def build_rock(ctx):
    rng = random.Random(7)
    r = ctx.part("Main")
    r.ico((0, 0, 0), 1.0, PAL["rock"], subdiv=1, scale=(1.25, 1.0, 0.62), jitter=0.16, rng=rng)
    r.ico((0.75, -0.35, -0.15), 0.5, shade(PAL["rock"], -0.12), subdiv=1, jitter=0.1, rng=rng)
    r.build(ctx)


# ---------------------------------------------------------------------------
# Environment
# ---------------------------------------------------------------------------

ROAD_WIDTH = 8.0
TILE_LENGTH = 10.0


def build_road_tile(ctx):
    """8 m x 10 m paved walkway. Top surface at z = 0, centred on the origin."""
    rng = random.Random(42)
    t = ctx.part("Main")
    t.box((0, 0, -0.39), (ROAD_WIDTH, TILE_LENGTH, 0.7), PAL["grout"])
    stones = [PAL["stone"], PAL["stone_mid"], hexcol("#e2dacb"), hexcol("#cdc2b0")]
    rows = 16
    pitch_y = TILE_LENGTH / rows
    pitch_x = 1.0
    gap = 0.05
    half_w = ROAD_WIDTH / 2
    for row in range(rows):
        y = -TILE_LENGTH / 2 + pitch_y * (row + 0.5)
        offset = 0.5 * pitch_x if row % 2 else 0.0
        x = -half_w - offset
        while x < half_w - 1e-6:
            x0 = max(x, -half_w)
            x1 = min(x + pitch_x, half_w)
            if x1 - x0 > gap * 2:
                cx = (x0 + x1) / 2
                top = rng.uniform(-0.012, 0.01)
                col = shade(rng.choice(stones), rng.uniform(-0.05, 0.04))
                t.box((cx, y, top - 0.05), (x1 - x0 - gap, pitch_y - gap, 0.1), col)
            x += pitch_x
    t.build(ctx)


def build_wall(ctx):
    """10 m parapet running along Y; Unity places one on each side of the road."""
    rng = random.Random(11)
    w = ctx.part("Main")
    w.box((0, 0, -0.6), (0.44, TILE_LENGTH, 2.4), PAL["grout"])
    courses = ((-1.8, -1.0), (-1.0, -0.2), (-0.2, 0.6))
    for ci, (z0, z1) in enumerate(courses):
        length = 2.5
        y = -TILE_LENGTH / 2 - (length / 2 if ci % 2 else 0)
        while y < TILE_LENGTH / 2 - 1e-6:
            y0 = max(y, -TILE_LENGTH / 2)
            y1 = min(y + length, TILE_LENGTH / 2)
            if y1 - y0 > 0.1:
                col = shade(PAL["stone"], rng.uniform(-0.1, 0.03))
                w.box((0, (y0 + y1) / 2, (z0 + z1) / 2), (0.5, y1 - y0 - 0.04, z1 - z0 - 0.04), col)
            y += length
    w.box((0, 0, 0.63), (0.6, TILE_LENGTH, 0.08), PAL["stone_dark"])
    count = 8
    pitch = TILE_LENGTH / count
    for i in range(count):
        y = -TILE_LENGTH / 2 + pitch * (i + 0.5)
        w.box((0, y, 0.88), (0.5, pitch * 0.5, 0.42), shade(PAL["stone"], rng.uniform(-0.08, 0.03)))
    w.build(ctx)


def build_tower(ctx, roof):
    t = ctx.part("Main")
    t.cyl((0, 0, 0.9), 1.3, 5.4, PAL["stone"], seg=12)
    for z in (-0.6, 1.2, 3.0):
        t.cyl((0, 0, z), 1.33, 0.12, PAL["stone_dark"], seg=12)
    t.cyl((0, 0, 3.7), 1.48, 0.28, PAL["stone_dark"], seg=12)
    t.cyl((0, 0, 5.15), 1.62, 2.6, roof, seg=12, radius2=0.0)
    for k in range(2):
        a = k * math.pi
        t.box((math.sin(a) * 1.27, -math.cos(a) * 1.27, 2.2), (0.32, 0.12, 0.56), PAL["black"], rot=(0, 0, a))
    t.cyl((0, 0, 6.9), 0.035, 1.0, PAL["wood_dark"], seg=6)
    t.box((0.3, 0, 7.15), (0.56, 0.03, 0.34), roof)
    t.build(ctx)


def build_tower_blue(ctx):
    build_tower(ctx, PAL["roof_blue"])


def build_tower_red(ctx):
    build_tower(ctx, PAL["roof_red"])


def build_castle_gate(ctx):
    """Enemy fortress closing the end of a level. Faces -Y (towards the player)."""
    rng = random.Random(5)
    c = ctx.part("Main")
    stone = PAL["enemy_stone"]
    c.box((0, 0, 1.4), (9.6, 2.5, 6.4), stone)
    for z in (0.2, 1.6, 3.0):
        c.box((0, -1.26, z), (9.6, 0.04, 0.08), PAL["enemy_stone_dark"])
    c.box((0, -1.27, 1.1), (3.0, 0.05, 3.2), PAL["door"])
    c.cyl((0, -1.27, 2.7), 1.5, 0.05, PAL["door"], seg=16, rot=(math.pi / 2, 0, 0))
    for i in range(7):
        c.box((-1.2 + i * 0.4, -1.31, 1.25), (0.08, 0.04, 3.3), PAL["steel_dark"])
    for z in (0.5, 1.5, 2.5):
        c.box((0, -1.31, z), (2.9, 0.04, 0.08), PAL["steel_dark"])
    for i in range(9):
        x = -4.4 + i * 1.1
        c.box((x, 0, 4.85), (0.62, 2.5, 0.5), shade(stone, rng.uniform(-0.08, 0.05)))
    for sx in (-1, 1):
        c.cyl((5.5 * sx, 0, 2.0), 1.6, 7.6, stone, seg=12)
        c.cyl((5.5 * sx, 0, 5.9), 1.75, 0.3, PAL["enemy_stone_dark"], seg=12)
        c.cyl((5.5 * sx, 0, 7.35), 1.95, 2.9, PAL["roof_red"], seg=12, radius2=0.0)
        c.box((5.5 * sx, -1.57, 3.4), (0.36, 0.1, 0.7), PAL["black"])
        c.box((2.6 * sx, -1.28, 3.0), (0.9, 0.04, 1.8), PAL["red"])
        c.box((2.6 * sx, -1.3, 3.25), (0.36, 0.02, 0.36), PAL["gold"], rot=(0, math.pi / 4, 0))
        c.box((2.6 * sx, -1.29, 2.08), (0.9, 0.05, 0.08), PAL["gold"])
    c.build(ctx)


MODELS = {
    "soldier": build_soldier,
    "grunt": build_grunt,
    "brute": build_brute,
    "boss": build_boss,
    "gate_pillar": build_gate_pillar,
    "gate_beam": build_gate_beam,
    "crate": build_crate,
    "banner": build_banner,
    "rock": build_rock,
    "road_tile": build_road_tile,
    "wall": build_wall,
    "tower_blue": build_tower_blue,
    "tower_red": build_tower_red,
    "castle_gate": build_castle_gate,
}


# ---------------------------------------------------------------------------
# Scene / export / rendering
# ---------------------------------------------------------------------------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_material():
    mat = bpy.data.materials.new(MATERIAL_NAME)
    if mat.node_tree is None:  # Blender < 5 creates materials without nodes
        mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    attr = nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    mat.node_tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.65
    return mat


def export_fbx(objects, path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        colors_type="SRGB",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        use_metadata=False,
    )


def model_bounds(objects):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in objects:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


def setup_render(scene, engine, width, height, sky, path):
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    if path.lower().endswith((".jpg", ".jpeg")):
        scene.render.image_settings.file_format = "JPEG"
        scene.render.image_settings.quality = 88
    else:
        scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = sky
    if engine == "BLENDER_EEVEE":
        scene.render.engine = "BLENDER_EEVEE"
        if scene.world.node_tree is None:  # Blender < 5
            scene.world.use_nodes = True
        bg = scene.world.node_tree.nodes.get("Background") if scene.world.node_tree else None
        if bg is not None:
            bg.inputs["Color"].default_value = (*sky, 1.0)
            bg.inputs["Strength"].default_value = 1.0
        scene.view_settings.view_transform = "Standard"
    else:
        scene.render.engine = "BLENDER_WORKBENCH"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "VERTEX"
        shading.show_object_outline = True
        shading.show_shadows = True
        scene.view_settings.view_transform = "Standard"


def add_sun(scene, rot=(0.9, 0.2, -0.6), energy=3.0):
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", type="SUN"))
    sun.data.energy = energy
    sun.rotation_euler = rot
    scene.collection.objects.link(sun)
    return sun


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render(scene, path, engines=("BLENDER_EEVEE", "BLENDER_WORKBENCH"), **kw):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    for engine in engines:
        try:
            setup_render(scene, engine, path=path, **kw)
            scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            print(f"[build_models] rendered {path} with {engine}")
            return True
        except Exception as exc:  # noqa: BLE001 - try the next engine
            print(f"[build_models] {engine} failed: {exc}")
    return False


def render_preview(built, path):
    scene = bpy.context.scene
    names = list(built.keys())
    cols = 5
    cell = 3.6
    for index, name in enumerate(names):
        objects = built[name]
        lo, hi = model_bounds(objects)
        size = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
        s = 2.2 / size
        cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
        col, row = index % cols, index // cols
        offset = Vector(((col - (cols - 1) / 2) * cell, row * cell * 0.9, -lo.z * s))
        for obj in objects:
            local = obj.location - Vector((cx, cy, 0))
            obj.location = local * s + offset
            obj.scale = obj.scale * s
    rows = (len(names) + cols - 1) // cols
    cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = cols * cell + 1.0
    scene.collection.objects.link(cam)
    scene.camera = cam
    center = Vector((0, (rows - 1) * cell * 0.45, 0.9))
    cam.location = center + Vector((-6.0, -14.0, 9.0))
    look_at(cam, center)
    add_sun(scene)
    render(scene, path, width=1600, height=int(1600 * 0.62), sky=hexcol("#9fd4ff"))


def text_object(name, body, size, color, extrude, offset, location, material):
    curve = bpy.data.curves.new(name, type="FONT")
    curve.body = body
    curve.size = size
    curve.extrude = extrude
    curve.offset = offset
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
    bpy.data.objects.remove(obj)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for item in attr.data:
        item.color = (*color, 1.0)
    mesh.color_attributes.active_color = attr
    mesh.materials.append(material)
    mesh_obj = bpy.data.objects.new(name, mesh)
    mesh_obj.location = location
    mesh_obj.rotation_euler = (math.pi / 2, 0, 0)
    bpy.context.scene.collection.objects.link(mesh_obj)
    return mesh_obj


def render_icons(out_dir):
    """Small diorama: three knights in front of a blue '+1' gate."""
    reset_scene()
    material = make_material()
    scene = bpy.context.scene
    built = {}
    for name in ("soldier", "gate_pillar", "gate_beam", "road_tile", "grunt"):
        ctx = ModelContext(name, material)
        MODELS[name](ctx)
        built[name] = ctx.objects

    def place(objects, loc, rot_z=0.0, scale=1.0):
        for obj in objects:
            obj.location = Matrix.Rotation(rot_z, 4, "Z") @ (obj.location * scale) + Vector(loc)
            obj.rotation_euler.z += rot_z
            obj.scale = Vector((scale, scale, scale))

    place(built["road_tile"], (0, 2.0, 0))
    place(built["soldier"], (0, -1.6, 0), scale=1.25)
    for loc in ((-1.05, -0.75, 0), (1.05, -0.75, 0), (-0.55, 0.2, 0), (0.55, 0.2, 0)):
        ctx = ModelContext("soldier", material)
        build_soldier(ctx)
        place(ctx.objects, loc, scale=1.05)
    for loc in ((-0.7, 5.2, 0), (0.6, 5.6, 0), (1.6, 5.0, 0), (-1.7, 5.4, 0)):
        ctx = ModelContext("grunt", material)
        build_grunt(ctx)
        place(ctx.objects, loc, scale=1.0)
    place(built["grunt"], (0.0, 4.7, 0))
    for sx in (-1, 1):
        ctx = ModelContext("gate_pillar", material)
        build_gate_pillar(ctx)
        place(ctx.objects, (2.2 * sx, 1.6, 0))
    for obj in built["gate_pillar"]:
        obj.hide_render = True
    for obj in built["gate_beam"]:
        obj.location = Vector((0, 1.6, 2.35))
        obj.scale = Vector((4.4, 1, 1))
    panel_ctx = ModelContext("icon_panel", material)
    panel = panel_ctx.part("Main")
    panel.box((0, 1.6, 1.25), (4.1, 0.05, 2.1), PAL["blue"])
    panel.build(panel_ctx)
    text_object("icon_text_back", "×2", 1.7, PAL["blue_dark"], 0.02, 0.09, (0, 1.56, 1.2), material)
    text_object("icon_text", "×2", 1.7, PAL["white"], 0.05, 0.02, (0, 1.48, 1.2), material)

    cam = bpy.data.objects.new("IconCam", bpy.data.cameras.new("IconCam"))
    cam.data.lens = 40
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = Vector((0, -7.6, 4.6))
    look_at(cam, (0, 0.8, 1.05))
    add_sun(scene, rot=(0.75, 0.15, -0.5), energy=2.6)
    os.makedirs(out_dir, exist_ok=True)
    master = os.path.join(out_dir, "icon_1024.png")
    if not render(scene, master, width=1024, height=1024, sky=hexcol("#7cc6ff")):
        return
    for size in (512, 192, 180):
        img = bpy.data.images.load(master)
        img.scale(size, size)
        img.filepath_raw = os.path.join(out_dir, f"icon_{size}.png")
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
        print(f"[build_models] wrote icon_{size}.png")


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--icons", default=None)
    parser.add_argument("--preview", default=None)
    parser.add_argument("--blend", default=None)
    parser.add_argument("--only", default=None)
    return parser.parse_args(argv)


def main():
    args = parse_args()
    names = list(MODELS) if not args.only else [n.strip() for n in args.only.split(",") if n.strip()]
    unknown = [n for n in names if n not in MODELS]
    if unknown:
        raise SystemExit(f"unknown model(s): {', '.join(unknown)}")

    reset_scene()
    material = make_material()
    os.makedirs(args.out, exist_ok=True)
    built = {}
    for name in names:
        ctx = ModelContext(name, material)
        MODELS[name](ctx)
        built[name] = ctx.objects
        path = os.path.join(args.out, f"{name}.fbx")
        export_fbx(ctx.objects, path)
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in ctx.objects)
        print(f"[build_models] {name:12s} parts={len(ctx.objects)} tris={tris} -> {path}")

    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
    if args.preview:
        render_preview(built, os.path.abspath(args.preview))
    if args.icons:
        render_icons(os.path.abspath(args.icons))
    print("[build_models] done")


if __name__ == "__main__":
    main()
