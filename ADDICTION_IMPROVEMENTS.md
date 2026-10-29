# 中毒性を上げる 100 の改善

「もう 1 回だけ」を何度も押したくなるように、物量・選択・収集・成長・演出の 5 つの面から 100 個の改善を入れました。
番号はカテゴリごとに並べています。どれもゲームに実装済みで、主な実装場所を右に書いています。

## A. 物量と爽快感

| # | 改善 | 主な実装 |
| ---: | --- | --- |
| 1 | 敵の数がレベルごとに指数関数的に増える（1 波 ≒ 18 × 1.19^(Lv−1) 体、Lv26 で上限の 1,400 体／波、同時に最大 3,000 体）。兵が多いほど 1 体の体当たりで倒される兵も増える | `Core/Balance.cs` `HordeSize` `ContactWeight` |
| 2 | 大軍は GPU インスタンシングで描画し、数千体でも軽い（1 種類・1 部位あたり 1023 体ずつまとめて描く） | `Presentation/CrowdRenderer.cs` |
| 3 | 味方の表示人数を 40 → 80 人に（多いほど密集して見える） | `Gameplay/Squad.cs` |
| 4 | 兵士数の上限を 999,999 に拡大し、1.2K / 3.4M と表示 | `Core/Fmt.cs` |
| 5 | 連続撃破コンボ「COMBO ×N」（1.5 秒撃破がないと途切れる） | `Gameplay/LevelSession.cs` |
| 6 | コンボが続くほど撃破コインが増える（最大 ×3） | `Core/Balance.cs` `ComboMultiplier` |
| 7 | コンボ中は撃破音のピッチが少しずつ上がる | `Presentation/AudioSystem.cs` |
| 8 | ヒットストップ（ボス撃破や大爆発で一瞬止まる） | `Gameplay/GameRoot.cs` |
| 9 | ボス撃破のスローモーション | `Gameplay/GameRoot.cs` |
| 10 | 会心の一撃（基本 5%、2 倍ダメージ） | `Gameplay/BulletSystem.cs` |
| 11 | 会心とボスへの大ダメージは数字が飛び出す | `Gameplay/LevelSession.cs` |
| 12 | 敵の種類ごとに色の違う破片が飛ぶ | `Core/Enemies.cs` `Debris` |
| 13 | 爆弾インプを倒すと周りを巻きこんで誘爆する | `Gameplay/Swarm.cs` |
| 14 | 大軍が近づくと「HORDE INCOMING」警告 | `Gameplay/LevelSession.cs` |
| 15 | 兵が増えるとカメラが少し引いて全体が見える | `Presentation/CameraRig.cs` |

## B. ゲート

| # | 改善 | 主な実装 |
| ---: | --- | --- |
| 16 | ルーレットゲート：＋・−・×・÷ と数値が 0.45 秒ごとにランダムに切り替わる（通った瞬間の値になる） | `Gameplay/Gates.cs` |
| 17 | 1 列のゲート枚数がレベルで 2 → 3 → 4 枚に増える | `Core/LevelGenerator.cs` |
| 18 | ゲートの出現回数がレベルとともに増える | `Core/LevelGenerator.cs` |
| 19 | 兵種募集ゲート「+8 MAGE」など | `Core/GateEffect.cs` |
| 20 | クラスアップゲート「CLASS UP」（全員が 1 段階昇格） | `Core/Army.cs` |
| 21 | 黄金のジャックポットゲート（まれに ×5、きらめく） | `Gameplay/Gates.cs` |
| 22 | ミステリーゲート「?」（通るまで中身は秘密、7 割は当たり） | `Gameplay/Gates.cs` |
| 23 | 左右に動くゲート列（Lv 8〜） | `Gameplay/Gates.cs` |
| 24 | 今通ろうとしているゲートが光って分かる | `Gameplay/Gates.cs` |
| 25 | カギ付きゲート：錠を撃ち壊すと大当たりが開く | `Gameplay/Gates.cs` |
| 26 | いちばん良いゲートを選ぶと「PERFECT!」とボーナス | `Gameplay/LevelSession.cs` |
| 27 | 良いゲートを続けて通ると「CHAIN ×N」ボーナス | `Gameplay/LevelSession.cs` |
| 28 | 増えた兵がゲートから走りこむ（数が多いほど音も大きく） | `Gameplay/Squad.cs` |
| 29 | 大きな数値ほどゲートの文字が大きい | `Gameplay/Gates.cs` |
| 30 | 悪いゲートは赤く脈打って警告 | `Gameplay/Gates.cs` |

## C. 敵の種類

| # | 改善 | 主な実装 |
| ---: | --- | --- |
| 31 | 新しい敵 27 種・ボス 5 種を追加（敵 31 種＋ボス 6 種）。レベルが上がるたびに新しい敵が加わる | `Core/Enemies.cs` `blender/build_models.py` |
| 32 | 初めて出会う敵は「NEW ENEMY」で名前と特徴を紹介 | `Gameplay/LevelSession.cs` |
| 33 | 盾オーク：盾が壊れるまで受けるダメージが大きく減る | `Gameplay/Swarm.cs` |
| 34 | スライム：倒すと小さなスライム 2 匹に分裂 | `Gameplay/Swarm.cs` |
| 35 | コウモリ：空を飛んでジグザグに迫る | `Gameplay/Swarm.cs` |
| 36 | シャーマン：周りの敵を回復する（先に倒したい） | `Gameplay/Swarm.cs` |
| 37 | ゴブリン弓兵：止まって矢を撃ってくる（動けばよけられる） | `Gameplay/EnemyShots.cs` |
| 38 | ゴースト：ときどき透けて無敵になる | `Gameplay/Swarm.cs` |
| 39 | 狼ライダー：近づくと突進してくる | `Gameplay/Swarm.cs` |
| 40 | トロール：体力がどんどん再生する | `Gameplay/Swarm.cs` |
| 41 | ネクロマンサー：骸骨を召喚し続ける | `Gameplay/Swarm.cs` |
| 42 | ブリンク魔導士：前へ瞬間移動する | `Gameplay/Swarm.cs` |
| 43 | アイスレイス：近くにいると味方の連射が遅くなる | `Gameplay/Swarm.cs` |
| 44 | ゴールドゴブリン：撃たれると逃げる、倒すと大量のコイン | `Gameplay/Swarm.cs` |
| 45 | ミミック：木箱のふりをして待ち、近づくと襲ってくる | `Gameplay/Swarm.cs` |
| 46 | 攻城槌：とても硬く、ぶつかると大損害 | `Core/Enemies.cs` |
| 47 | エリート敵：金色で強く、倒すと宝石を落とす | `Gameplay/Swarm.cs` |
| 48 | ボス 6 種のローテーション（オーガ王・リッチ王・ゴーレム王・ドラゴン・魔王・将軍）、それぞれ固有の攻撃 | `Gameplay/BossUnit.cs` |
| 49 | ボスの怒りモード（HP 30% で赤く速くなる） | `Gameplay/BossUnit.cs` |
| 50 | ボス登場バナー（名前と特徴） | `Gameplay/LevelSession.cs` |

## D. 味方の兵種

| # | 改善 | 主な実装 |
| ---: | --- | --- |
| 51 | 5 系統 × 4 段階 = 20 兵種（弓・盾・魔法・銃・聖職） | `Core/Units.cs` |
| 52 | 弓系の最上位スナイパーは敵を貫通する | `Core/Units.cs` |
| 53 | 魔法系は範囲攻撃（大軍に強い） | `Gameplay/BulletSystem.cs` |
| 54 | 盾系は敵の体当たりを防ぐ（ガード率） | `Core/Army.cs` |
| 55 | 銃系は高速連射、最上位の大砲は爆発する | `Core/Units.cs` |
| 56 | 聖職系は倒れた兵を少しずつ復活させる | `Gameplay/Squad.cs` |
| 57 | クラスアップで見た目・弾・性能が変わる | `Core/Army.cs` |
| 58 | クラスアップの演出（光とバナー「KNIGHT → PALADIN」） | `Gameplay/LevelSession.cs` |
| 59 | 兵の構成バー（系統ごとの色で割合を表示） | `UI/HudView.cs` |
| 60 | 系統はレベルで解放され、タイトルに次の解放を予告 | `Core/Units.cs` |
| 61 | 出撃時の兵種（系統）を選べる | `UI/TitleView.cs` |
| 62 | 兵舎（兵種図鑑）で 20 兵種の解放状況と性能を見られる | `UI/Panels.cs` |
| 63 | 兵種ごとに弾の形と色が違う（矢・槍・魔弾・銃弾・砲弾・聖光） | `Gameplay/BulletSystem.cs` |
| 64 | 必殺技ボタン「VOLLEY」：撃破でたまり、前方に矢の雨を降らせる | `Gameplay/LevelSession.cs` |
| 65 | 必殺技を強くするアップグレード | `Core/Balance.cs` |

## E. 成長と報酬

| # | 改善 | 主な実装 |
| ---: | --- | --- |
| 66 | 強化を 4 → 8 種に（会心・アーマー・必殺技・募集を追加） | `Core/SaveData.cs` |
| 67 | 宝石（2 つ目の通貨） | `Core/SaveData.cs` |
| 68 | 勝利の宝箱（タップで開ける） | `UI/ResultView.cs` |
| 69 | 星評価（生き残った割合で 1〜3 つ星、最高記録を保存） | `Core/Balance.cs` `Stars` |
| 70 | 実績 30 個（報酬つき） | `Core/Achievements.cs` |
| 71 | 毎日ログインボーナス（7 日周期、続けるほど豪華） | `Core/Daily.cs` |
| 72 | デイリーチャレンジ（日替わりの大軍ステージ、報酬 2 倍） | `Core/Daily.cs` |
| 73 | 放置報酬（離れていた時間ぶんのコイン、最大 8 時間） | `Core/Daily.cs` |
| 74 | 5 レベルごとの「GOLD RUSH」（ゴールドゴブリン大量・コイン 1.5 倍） | `Core/LevelGenerator.cs` |
| 75 | 10 レベルごとの「CASTLE SIEGE」（ボス撃破で宝石） | `Core/LevelGenerator.cs` |
| 76 | 敗北時にボスの残り HP を表示（あと少しだったと分かる） | `UI/ResultView.cs` |
| 77 | 宝石で復活（1 回だけ、兵 50% でその場から再開） | `Gameplay/LevelSession.cs` |
| 78 | 敗北後すぐにリトライ | `UI/ResultView.cs` |
| 79 | 勝利後は NEXT ですぐ次のレベルへ（強化はホームで） | `UI/ResultView.cs` |
| 80 | 最大兵数の自己ベスト更新で「NEW RECORD!」 | `Gameplay/LevelSession.cs` |
| 81 | 統計画面（総撃破数・最大兵数・最高コンボ・プレイ時間など） | `UI/Panels.cs` |
| 82 | 敵図鑑（撃破数、未発見は ???） | `UI/Panels.cs` |
| 83 | 次の解放までの進み具合を表示 | `UI/TitleView.cs` |
| 84 | 強化できるカードが光る | `UI/TitleView.cs` |
| 85 | 1 日 1 回の無料くじ（コイン・宝石・無料強化・コイン 2 倍券） | `Core/Daily.cs` |

## F. 見た目・音・快適さ

| # | 改善 | 主な実装 |
| ---: | --- | --- |
| 86 | 5 レベルごとに地域が変わる（湾岸の城・砂漠・雪の砦・火山・夜の沼・天空の城） | `Core/Biomes.cs` |
| 87 | 地域ごとの天気（雪・火の粉・ホタル・花びら・きらめき） | `Presentation/WeatherFx.cs` |
| 88 | 地域ごとに空・霧・水・道・壁の色が変わる | `Presentation/WorldView.cs` |
| 89 | ボス戦は BGM が速く緊迫した曲に切り替わる | `Presentation/AudioSystem.cs` |
| 90 | 勝利のファンファーレと紙吹雪 | `Gameplay/LevelSession.cs` |
| 91 | コインの音は続けて取るほど高くなる | `Presentation/AudioSystem.cs` |
| 92 | 振動（対応する端末・ブラウザ） | `Presentation/Haptics.cs` |
| 93 | 設定画面（効果音・BGM・振動・画質） | `UI/Panels.cs` |
| 94 | 画質設定（低：遠くの敵を簡略表示し、破片を減らす） | `Presentation/CrowdRenderer.cs` |
| 95 | 自動画質調整（重いと感じたら自動で軽くする） | `Gameplay/GameRoot.cs` |
| 96 | 新しい仕組みは初回だけヒントを表示（ルーレット・カギ・遠距離の敵など） | `Gameplay/LevelSession.cs` |
| 97 | 大きな数字は UI 全体で K/M 表記 | `Core/Fmt.cs` |
| 98 | 一時停止中に今回の戦績を表示 | `UI/UIRoot.cs` |
| 99 | ステージ開始の「READY… GO!」演出 | `UI/HudView.cs` |
| 100 | 結果画面で獲得コインの内訳（撃破・コンボ・勝利・ボーナス）を表示 | `UI/ResultView.cs` |
