"""
blender/build_models.py のテスト（Blender の中で実行する）。
  blender -b --factory-startup --python-exit-code 1 --python blender/tests/test_models.py
失敗すると終了コード 1 を返す。
"""
import os
import shutil
import sys
import tempfile
import traceback
import unittest

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import build_models as kit  # noqa: E402

# every animated character: 20 ally classes, the enemies and the bosses
CHARACTERS = tuple(kit.CHARACTERS)
PART_NAMES = {"Body", "Main", "LegL", "LegR", "ArmL", "ArmR"}


def build(name):
    kit.reset_scene()
    ctx = kit.ModelContext(name, kit.make_material())
    kit.MODELS[name](ctx)
    bpy.context.view_layer.update()
    return ctx.objects


def parts(name):
    # "brute_ArmR" -> "ArmR"（モデル名にも "_" があるので、先頭のモデル名を取りのぞく）
    return {o.name[len(name) + 1:]: o for o in build(name)}


def bounds(objects):
    return kit.model_bounds(objects)


def local_z(ob):
    zs = [v.co.z for v in ob.data.vertices]
    return min(zs), max(zs)


class ModelTests(unittest.TestCase):
    def test_every_model_builds_with_named_mesh_parts(self):
        for name in kit.MODELS:
            with self.subTest(model=name):
                objects = build(name)
                self.assertTrue(objects)
                for ob in objects:
                    self.assertTrue(ob.name.startswith(name + "_"), ob.name)
                    self.assertEqual(ob.type, "MESH")
                    self.assertGreater(len(ob.data.polygons), 0)

    def test_parts_are_vertex_coloured_with_the_shared_material(self):
        # Unity は "NR_VertexColor" を NR_Toon に置きかえ、色は頂点カラー "Col" から読む
        for name in kit.MODELS:
            for ob in build(name):
                with self.subTest(part=ob.name):
                    col = ob.data.color_attributes.get("Col")
                    self.assertIsNotNone(col)
                    self.assertEqual((col.domain, col.data_type), ("CORNER", "BYTE_COLOR"))
                    self.assertEqual([m.name for m in ob.data.materials], [kit.MATERIAL_NAME])

    def test_there_are_20_allies_30_enemy_models_and_6_bosses(self):
        self.assertEqual(len([n for n in CHARACTERS if n.startswith("u_")]), 20)
        self.assertGreaterEqual(len([n for n in CHARACTERS if n.startswith("e_")]) + 2, 30)
        self.assertEqual(len([n for n in CHARACTERS if n.startswith("b_")]) + 1, 6)

    def test_characters_have_the_parts_unity_animates(self):
        expected = {
            "u_crossbow": {"Body", "LegL", "LegR"},
            "grunt": {"Body", "LegL", "LegR"},
            "brute": {"Body", "LegL", "LegR", "ArmL", "ArmR"},
            "boss": {"Body", "LegL", "LegR", "ArmL", "ArmR"},
        }
        for name, want in expected.items():
            self.assertEqual(set(parts(name)), want, name)
        for name in CHARACTERS:
            with self.subTest(model=name):
                names = set(parts(name))
                self.assertTrue(names & {"Body", "Main"}, f"{name} needs a body")
                self.assertTrue(names <= PART_NAMES, f"{name}: {sorted(names - PART_NAMES)}")
                self.assertEqual("LegL" in names, "LegR" in names, f"{name} legs come in pairs")

    def test_legs_pivot_at_the_hip_and_arms_at_the_shoulder(self):
        # 部位の原点が関節。Unity はそこを中心に回して歩かせる・こん棒を振る
        for name in CHARACTERS:
            p = parts(name)
            for side in ("LegL", "LegR"):
                if side not in p:
                    continue
                lo, hi = local_z(p[side])
                self.assertAlmostEqual(hi, 0.0, delta=0.08, msg=f"{name} {side}")
                self.assertLess(lo, -0.12, f"{name} {side}")
            if name in ("brute", "boss"):
                for side in ("ArmL", "ArmR"):
                    self.assertLess(local_z(p[side])[0], -0.6, f"{name} {side}")

    def test_characters_face_minus_y(self):
        # Blender の -Y（正面）が Unity の +Z になる。石弓・こん棒・顔は正面に出ている
        lo, hi = bounds([parts("u_crossbow")["Body"]])
        self.assertLess(lo.y, -0.45, "石弓が正面に出ている")
        self.assertLess(hi.y, 0.25, "背中はマントの厚みだけ")
        lo, hi = bounds([parts("brute")["ArmR"]])
        self.assertLess(lo.y, -1.0, "こん棒が正面に向いている")
        lo, hi = bounds([parts("grunt")["Body"]])
        self.assertLess(lo.y, -0.3, "斧と盾が正面")

    def test_character_heights(self):
        heights = {"u_crossbow": (1.1, 1.35), "grunt": (1.15, 1.45), "brute": (2.3, 2.7), "boss": (2.4, 2.95)}
        for name, (low, high) in heights.items():
            lo, hi = bounds(build(name))
            self.assertGreaterEqual(lo.z, -0.01, f"{name} の足元は地面")
            self.assertTrue(low <= hi.z - lo.z <= high, f"{name} height {hi.z - lo.z:.2f}")
        for name in CHARACTERS:
            lo, hi = bounds(build(name))
            with self.subTest(model=name):
                self.assertGreaterEqual(lo.z, -0.02, f"{name} の足元は地面")
                limit = 4.2 if name.startswith("b_") or name == "boss" else 2.9
                self.assertTrue(0.3 <= hi.z - lo.z <= limit, f"{name} height {hi.z - lo.z:.2f}")
                # 道幅（8 m）からはみ出さない
                self.assertLess(hi.x - lo.x, 5.5, f"{name} width {hi.x - lo.x:.2f}")

    def test_road_pieces_tile_seamlessly(self):
        lo, hi = bounds(build("road_tile"))
        self.assertAlmostEqual(hi.x - lo.x, kit.ROAD_WIDTH, delta=0.01)
        self.assertAlmostEqual(hi.y - lo.y, kit.TILE_LENGTH, delta=0.01)
        self.assertAlmostEqual(hi.z, 0.0, delta=0.02, msg="道の表面は高さ 0")
        lo, hi = bounds(build("wall"))
        self.assertAlmostEqual(hi.y - lo.y, kit.TILE_LENGTH, delta=0.01)
        self.assertLess(lo.z, -1.7, "城壁は水面（-1.6）まで届く")

    def test_gate_beam_is_unit_length(self):
        # Unity がゲートの幅に合わせて X 方向に伸ばす
        lo, hi = bounds(build("gate_beam"))
        self.assertAlmostEqual(hi.x - lo.x, 1.0, delta=0.001)

    def test_export_writes_an_fbx_for_every_model(self):
        out = tempfile.mkdtemp()
        try:
            for name in kit.MODELS:
                path = os.path.join(out, name + ".fbx")
                kit.export_fbx(build(name), path)
                self.assertGreater(os.path.getsize(path), 1000, name)
        finally:
            shutil.rmtree(out, ignore_errors=True)

    def test_colour_helpers(self):
        self.assertEqual(kit.hexcol("#ff0000"), (1.0, 0.0, 0.0))
        for got, want in ((kit.shade((0.5, 0.5, 0.5), 0.2), (0.6, 0.6, 0.6)),
                          (kit.shade((0.9, 0.1, 0.0), 1.0), (1.0, 0.2, 0.0))):
            for g, w in zip(got, want):
                self.assertAlmostEqual(g, w, places=6)


def main():
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(ModelTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    sys.exit(0 if result.wasSuccessful() else 1)


try:
    main()
except SystemExit:
    raise
except Exception:
    traceback.print_exc()
    sys.exit(1)
