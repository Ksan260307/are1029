"""
tools/ と CI の設定の単体テスト（ビルド検査・ワークフロー・モデルの登録もれ・スクリプト）。
  python -m unittest discover -s tools/tests -v
標準ライブラリだけで動く（Blender・Unity は不要）。
"""
import ast
import importlib.util
import io
import json
import os
import re
import shutil
import sys
import tempfile
import unittest
from contextlib import redirect_stdout

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.abspath(os.path.join(HERE, ".."))
ROOT = os.path.abspath(os.path.join(TOOLS, ".."))
PROJECT = os.path.join(ROOT, "NumberRoad")
MODELS_DIR = os.path.join(PROJECT, "Assets", "NumberRoad", "Resources", "Models")
WORKFLOW = os.path.join(ROOT, ".github", "workflows", "deploy-pages.yml")


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(TOOLS, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


check = load("check_build")

GOOD_HTML = (
    '<meta name="viewport" content="width=device-width, viewport-fit=cover">'
    '<meta name="apple-mobile-web-app-capable" content="yes">'
    '<link rel="manifest" href="manifest.webmanifest">'
    '<link rel="icon" type="image/png" href="TemplateData/icon-192.png">'
    '<link rel="apple-touch-icon" href="TemplateData/icon-180.png">'
    '<script>var buildUrl = "Build"; var config = { devicePixelRatio: 2 };'
    ' script.src = buildUrl + "/Game.loader.js";</script>'
)


class CheckBuildTests(unittest.TestCase):
    def make_site(self, loader="Game.loader.js", html=GOOD_HTML, extra_dir=None, manifest=True, icons=True):
        site = tempfile.mkdtemp()
        os.makedirs(os.path.join(site, "Build"))
        for n in (loader, "Game.data.unityweb", "Game.framework.js.unityweb", "Game.wasm.unityweb"):
            with open(os.path.join(site, "Build", n), "wb") as f:
                f.write(b"x" * 10)
        with open(os.path.join(site, "index.html"), "w", encoding="utf-8") as f:
            f.write(html)
        if icons:
            os.makedirs(os.path.join(site, "TemplateData"))
            for size in (180, 192, 512):
                with open(os.path.join(site, "TemplateData", f"icon-{size}.png"), "wb") as f:
                    f.write(b"png")
        if manifest:
            with open(os.path.join(site, "manifest.webmanifest"), "w", encoding="utf-8") as f:
                json.dump({"icons": [{"src": "TemplateData/icon-192.png"}, {"src": "TemplateData/icon-512.png"}]}, f)
        if extra_dir:
            os.makedirs(os.path.join(site, extra_dir))
        return site

    def run_check(self, site, cleanup=True):
        # cleanup: テスト用に作った一時フォルダを消す（本物の docs/ は消さない）
        old = sys.argv
        sys.argv = ["check_build.py", site]
        try:
            with redirect_stdout(io.StringIO()):
                check.main()
            return 0
        except SystemExit as e:
            return e.code
        finally:
            sys.argv = old
            if cleanup:
                shutil.rmtree(site, ignore_errors=True)

    def test_valid_site_passes(self):
        self.assertEqual(self.run_check(self.make_site()), 0)

    def test_missing_loader_fails(self):
        self.assertEqual(self.run_check(self.make_site(loader="Other.loader.js")), 1)

    def test_unexpanded_template_fails(self):
        self.assertEqual(self.run_check(self.make_site(html=GOOD_HTML + " {{{ DATA_FILENAME }}}")), 1)

    def test_default_unity_template_fails(self):
        # Unity の標準テンプレートには、全画面・ホーム画面に追加の設定がない
        html = GOOD_HTML.replace("viewport-fit=cover", "").replace("apple-mobile-web-app-capable", "")
        self.assertEqual(self.run_check(self.make_site(html=html)), 1)

    def test_debug_info_fails(self):
        self.assertEqual(self.run_check(self.make_site(extra_dir="Game_BurstDebugInformation_DoNotShip")), 1)

    def test_missing_manifest_fails(self):
        self.assertEqual(self.run_check(self.make_site(manifest=False)), 1)

    def test_missing_icons_fail(self):
        self.assertEqual(self.run_check(self.make_site(icons=False)), 1)

    def test_missing_index_fails(self):
        self.assertEqual(self.run_check(tempfile.mkdtemp()), 1)

    def test_real_build_passes(self):
        docs = os.path.join(ROOT, "docs")
        if os.path.isdir(os.path.join(docs, "Build")):
            self.assertEqual(self.run_check(docs, cleanup=False), 0)
            self.assertTrue(os.path.isdir(os.path.join(docs, "Build")), "検査で docs/ を消さない")


class WorkflowTests(unittest.TestCase):
    def test_workflow_deploys_pages(self):
        s = read(WORKFLOW)
        for key in ("actions/deploy-pages", "actions/upload-pages-artifact", "game-ci/unity-builder", "game-ci/unity-test-runner",
                    "NumberRoad.EditorTools.BuildScript.BuildWebGL", "tools/check_build.py", "pages: write", "id-token: write",
                    "docs/"):
            self.assertIn(key, s)

    def test_workflow_permissions_are_least_privilege(self):
        # ふだんは読むだけ。書きこみの権限は、それがいるジョブ（公開・テスト結果）にだけ
        s = read(WORKFLOW)
        top = s.split(chr(10) + "jobs:")[0]
        self.assertIn("contents: read", top)
        for w in ("pages: write", "id-token: write", "checks: write", "contents: write"):
            self.assertNotIn(w, top, w)

    def test_build_method_exists(self):
        script = read(os.path.join(PROJECT, "Assets", "NumberRoad", "Editor", "BuildScript.cs"))
        self.assertIn("namespace NumberRoad.EditorTools", script)
        self.assertRegex(script, r"public static void BuildWebGL\(\)")


class ModelRegistryTests(unittest.TestCase):
    """blender/build_models.py が作るモデルと、リポジトリに入っている FBX が一致しているか（Blender なしで調べる）。"""

    @classmethod
    def setUpClass(cls):
        source = read(os.path.join(ROOT, "blender", "build_models.py"))
        cls.tree = ast.parse(source)
        cls.models = None
        for node in cls.tree.body:
            if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "MODELS" for t in node.targets):
                cls.models = [k.value for k in node.value.keys]
        cls.fbx = sorted(f[:-4] for f in os.listdir(MODELS_DIR) if f.endswith(".fbx"))

    def test_script_declares_models(self):
        self.assertIsNotNone(self.models, "build_models.py に MODELS がありません")
        self.assertGreater(len(self.models), 10)

    def test_every_model_is_committed_with_meta(self):
        for name in self.models:
            self.assertIn(name, self.fbx, f"{name}.fbx がありません（blender/build_models.py を実行してコミット）")
            self.assertTrue(os.path.isfile(os.path.join(MODELS_DIR, name + ".fbx.meta")), f"{name}.fbx.meta がありません")

    def test_no_stale_models(self):
        for name in self.fbx:
            self.assertIn(name, self.models, f"{name}.fbx は build_models.py で作られていません")

    def test_models_used_by_the_game_exist(self):
        scripts = os.path.join(PROJECT, "Assets", "NumberRoad", "Scripts")
        used = set()
        for dirpath, _, files in os.walk(scripts):
            for fn in files:
                if fn.endswith(".cs"):
                    used.update(re.findall(r'ModelLibrary\.Spawn\("([a-z_]+)"', read(os.path.join(dirpath, fn))))
                    used.update(re.findall(r'new EnemyArchetype\([^)]*"([a-z_]+)"\)', read(os.path.join(dirpath, fn))))
        self.assertTrue(used)
        for name in used:
            self.assertIn(name, self.models, f"ゲームが使う {name} を build_models.py が作っていません")


class ScriptTests(unittest.TestCase):
    def test_powershell_scripts_have_bom(self):
        # Windows PowerShell 5.1 は BOM なしの UTF-8 を正しく読めない（日本語がこわれる）
        for name in ("build_webgl.ps1", "run_tests.ps1"):
            with open(os.path.join(TOOLS, name), "rb") as f:
                self.assertEqual(f.read(3), b"\xef\xbb\xbf", name)

    def test_scripts_use_the_project_unity_version(self):
        version = re.search(r"m_EditorVersion: (\S+)", read(os.path.join(PROJECT, "ProjectSettings", "ProjectVersion.txt"))).group(1)
        for name in ("build_webgl.ps1", "run_tests.ps1"):
            with open(os.path.join(TOOLS, name), encoding="utf-8-sig") as f:
                self.assertIn(f"Editor\\{version}\\Editor\\Unity.exe", f.read(), name)

    def test_build_output_is_ignored(self):
        ignore = read(os.path.join(ROOT, ".gitignore"))
        for pattern in ("/WebGLBuild/", "/build-tmp/"):
            self.assertIn(pattern, ignore)
        self.assertNotIn("/docs/", ignore, "docs/ はビルド済みの公開用なのでコミットする")


if __name__ == "__main__":
    unittest.main()
