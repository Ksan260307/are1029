# Number Road（ナンバーロード）

「トップロード」の広告で見るような **数字ゲートを通って兵と弾を増やし、迫る敵軍とボスを撃退するディフェンスゲーム** を、Unity と Blender で作った個人用プロジェクトです。広告・課金・トラッキングは一切ありません。

- 指を左右にドラッグして兵団を動かす（PC は ←/→ キー・マウスドラッグ）
- `+10` `×2` などのゲートで兵が増え、`-5` `÷2` を通ると減る
- `RATE+`（連射）`POWER+`（攻撃力）`MULTI+`（拡散弾）で武器が強化
- 赤い数字の「撃つゲート」は弾を当てるたびに +1。プラスにしてから通り抜ける
- `+1` の列を通ると 1 人ずつ仲間が増える。反対側のレーンには敵部隊
- 木箱は撃ち壊すと報酬。壊さずぶつかると兵を失う
- 巨人（ブルート）は兵を叩き潰してくる。横に避けることもできる
- 最後は敵城の前でボス戦。倒せばクリア、コインで「初期兵数・攻撃力・連射・コイン獲得」を強化

| タイトル（スマホ縦） | プレイ中（iPad 縦） |
| --- | --- |
| <img src="media/screenshot_title.jpg" width="260"> | <img src="media/screenshot_ipad_crowd.jpg" width="340"> |

Blender で生成しているモデル一覧：

![models](media/models_preview.jpg)

## 遊び方（スマホ・iPad）

**https://ksan260307.github.io/are1029/** を iPhone / iPad / Android の Safari・Chrome で開きます。共有メニューの「ホーム画面に追加」で、全画面のアプリのように起動できます。セーブ（レベル・コイン・強化）はブラウザに残ります。

- 画面の向き：スマホは縦、iPad は縦横どちらでも遊べます（カメラが画面比に合わせて自動調整）
- ノッチやホームバーは Safe Area で避けています
- アプリとして入れたい場合は、Unity のメニュー `Number Road > Build > Android APK / iOS Xcode Project` でローカルにビルドできます（Unity に Android / iOS のモジュールが必要）

## リポジトリ構成

```
.github/workflows/deploy-pages.yml  GitHub Pages への自動デプロイ（テスト・ビルド・公開）
blender/build_models.py             全 3D モデルとアプリアイコンを Python で生成し FBX 出力（Blender 4.2+ / 5.x）
blender/tests/                      Blender スクリプトの単体テスト
docs/                               ビルド済み WebGL（GitHub Pages の公開用）
media/                              README 用の画像
tools/                              ビルド・テストのスクリプトと、その単体テスト
NumberRoad/                         Unity プロジェクト（Unity 6000.5.4f1）
  Assets/NumberRoad/
    Scripts/Core/                   ゲームロジック（数値・ゲート計算・レベル生成・セーブ）
    Scripts/Gameplay/               進行・兵団・敵・ゲート・弾・入力・オートパイロット
    Scripts/Presentation/           カメラ・背景・パーティクル・効果音（全部コード生成）
    Scripts/UI/                     タイトル / HUD / ポーズ / リザルト / ワールドラベル
    Shaders/                        トゥーン・半透明・加算・水面（自前ライティング、キーワード不要）
    Resources/Models/               Blender から出力した FBX
    Editor/                         プロジェクト設定の自動化・ビルドスクリプト
    Tests/                          EditMode（ロジック）/ PlayMode（実際に起動してオートプレイ）
  Assets/WebGLTemplates/NumberRoad/ スマホ向け全画面 WebGL テンプレート（ホーム画面追加対応）
```

アートは Blender のモデル以外すべてコードで生成しています（UI 部品、テクスチャ、効果音・BGM のシンセ）。

## 開発

### Unity で開く・ビルドする

- Unity Hub で `NumberRoad/` フォルダを開き、`Assets/NumberRoad/Scenes/Main.unity` を再生します（エディタでは `F1` でオートパイロット）。
  メニュー **Number Road > Setup Project** でマテリアル・シーン・プレイヤー設定を作り直せます。
- ローカルでビルドして `docs/` を更新：`powershell -ExecutionPolicy Bypass -File tools/build_webgl.ps1`
  （`-Assets` で Blender のモデル・アイコンの生成から、`-Setup` で Unity のセットアップからやり直す）
- ビルドした `docs/` をローカルで確かめるとき：`python -m http.server 8000 --directory docs` → http://localhost:8000

### アセットを作り直す

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python blender/build_models.py '--' --preview media/models_preview.jpg --icons NumberRoad/Assets/NumberRoad/Art/Icons
```

- `--out` を省略すると `NumberRoad/Assets/NumberRoad/Resources/Models/` の FBX を上書きします（`--blend out.blend` で .blend も保存）
- 規約：モデルは -Y 向き（Blender の正面）、部位は `<モデル名>_LegL` のような名前、色は頂点カラー
- アイコンは次の Unity セットアップで WebGL テンプレートにも反映されます

### テスト

```powershell
powershell -ExecutionPolicy Bypass -File tools/run_tests.ps1            # すべて
powershell -ExecutionPolicy Bypass -File tools/run_tests.ps1 -Only PlayMode
```

| 種類 | 件数 | 場所 |
| --- | ---: | --- |
| Unity EditMode（単体） | 62 | `NumberRoad/Assets/NumberRoad/Tests/EditMode` |
| Unity PlayMode（総合） | 2 | `NumberRoad/Assets/NumberRoad/Tests/PlayMode` |
| Blender（単体） | 10 | `blender/tests` |
| Python（単体） | 19 | `tools/tests` |

### 公開（CI/CD）

`main` に push すると `.github/workflows/deploy-pages.yml` が動き、GitHub Pages に公開されます。

1. リポジトリ設定 **Settings → Pages → Build and deployment → Source** を **GitHub Actions** にする（初回のみ）
2. そのままでも、リポジトリに入っているビルド済みの `docs/` が公開されます（公開前に `tools/check_build.py` で中身を検査）
3. GitHub Actions 上で Unity からビルドしたい場合は、**Settings → Secrets and variables → Actions** に次を登録します
   - `UNITY_LICENSE` … Unity Hub でライセンスを有効化したときにできる `Unity_lic.ulf` の中身（Windows: `C:\ProgramData\Unity\Unity_lic.ulf`）
   - `UNITY_EMAIL` / `UNITY_PASSWORD` … Unity アカウント（Pro ライセンスの場合は `UNITY_SERIAL` も）

   登録すると、push のたびに [game-ci](https://game.ci/) で Unity のテスト（EditMode / PlayMode）を実行し、WebGL をビルドして公開します。
4. ワークフローの権限は、ふだんは読むだけ（`contents: read`）。公開のジョブだけが `pages: write` / `id-token: write`、Unity のテストのジョブだけが `checks: write` を持ちます。

## 調整したいとき

- 難易度・数値：`Scripts/Core/Balance.cs`（行進速度、連射、ボス、強化コストなど）
- ステージ構成：`Scripts/Core/LevelGenerator.cs`（レベル番号から決定的に生成。敵の HP は「普通に上手く選んだ場合」の兵力から自動計算）
- 見た目：`blender/build_models.py`（モデル）、`Scripts/Presentation/WorldView.cs`（空・霧・光の色）
- フォント：`Assets/NumberRoad/Resources/Fonts/UIFont.ttf` を置くと UI 全体がそのフォントに切り替わります。UI の文言は英語にしてあります（WebGL は端末の日本語フォントを使えないため）。日本語表示にしたい場合は、日本語フォント（例：Noto Sans JP）をここに置いてから文言を書き換えてください

## 技術メモ

- Unity 6000.5.4f1 / Built-in Render Pipeline / 旧 Input Manager（タッチ・マウス・キーボード）
- ビルド設定は `Editor/ProjectSetup.cs` がコードで適用（WebGL: gzip＋解凍フォールバックで GitHub Pages 対応。Android: IL2CPP ARM64、iOS: iPhone+iPad）
- 兵は最大 40 体を描画し、それ以上は数字で表示。弾数は上限付きで、その分 1 発のダメージを上げて総 DPS を正確に保っています（テストで保証）
- 弾・破片・影は GPU インスタンシングで描画、物理エンジンは不使用
