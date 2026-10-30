using System.Globalization;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>
    /// In-level HUD: level and wave, progress (with the wave marks) and army make-up, coins, boss bar, combo
    /// counter, the war wagon's health, the x2 / FAST MARCH switches and the DASH button, READY... GO!,
    /// announcement banners, first-time tips and the buff cards dealt between waves.
    /// </summary>
    sealed class HudView
    {
        const float BannerTime = 1.9f;
        const float HintTime = 4.2f;
        const int MaxWaveMarks = 6;

        public readonly GameObject Root;
        readonly Text _level;
        readonly Text _wave;
        readonly RectTransform[] _waveMarks = new RectTransform[MaxWaveMarks];
        readonly BuffPanel _buffs;
        readonly Button _speed;
        readonly Text _speedText;
        readonly Button _march;
        readonly Text _marchText;
        readonly Text _coins;
        readonly Text _weapon;
        readonly Text _bossHp;
        readonly RectTransform _progressFill;
        readonly RectTransform[] _lineBars = new RectTransform[Units.Lines];
        readonly RectTransform _bossFill;
        readonly GameObject _bossBar;
        readonly CanvasGroup _dragHint;
        readonly Text _combo;
        readonly RectTransform _comboFill;
        readonly CanvasGroup _comboGroup;
        readonly Text _countdown;
        readonly RectTransform _bannerRoot;
        readonly CanvasGroup _bannerGroup;
        readonly Text _bannerTitle;
        readonly Text _bannerSub;
        readonly RawImage _bannerPortrait;
        readonly CanvasGroup _hintGroup;
        readonly Text _hintText;
        readonly HoldButton _dash;
        readonly RectTransform _dashRect;
        readonly Image _dashFill;
        readonly Image _basePill;
        readonly RectTransform _baseFill;
        readonly Image _baseFillImage;
        readonly Text _baseText;
        LevelSession _session;
        SaveData _save;
        int _shownCoins = -1;
        int _shownCombo = -1;
        string _weaponText;
        float _clock;
        float _bannerClock = -1f;
        float _hintClock = -1f;
        float _comboPunch;
        float _barClock;
        int _shownBase = -1;
        float _baseFlash;
        int _shownBossHp = -1;
        int _shownWave = -1;
        int _shownSpeed = -1;
        int _shownMarch = -1;
        WeaponStats _shownWeapon;

        public HudView(RectTransform parent, GameRoot game)
        {
            RectTransform root = UIFactory.Fill(UIFactory.Rect(parent, "Hud"));
            Root = root.gameObject;

            Image levelPill = UIFactory.Image(root, "LevelPill", UIFactory.Blue, GameAssets.RoundedSprite);
            UIFactory.Place(levelPill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -26f), new Vector2(190f, 88f));
            _level = UIFactory.Text(levelPill.rectTransform, "Text", Loc.T("LV 1"), 46, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_level.rectTransform);
            _level.rectTransform.offsetMin = new Vector2(8f, 0f);
            _level.rectTransform.offsetMax = new Vector2(-8f, 0f);
            FitText(_level, 26);
            Image wavePill = UIFactory.Image(root, "WavePill", new Color(0f, 0f, 0f, 0.42f), GameAssets.RoundedSprite);
            UIFactory.Place(wavePill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -122f), new Vector2(190f, 52f));
            _wave = UIFactory.Text(wavePill.rectTransform, "Text", Loc.T("WAVE 1/3"), 30, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_wave.rectTransform);

            // x2 game speed and FAST MARCH (both kept between runs)
            _speed = UIFactory.Button(root, "Speed", Loc.T("x1"), UIFactory.Blue, 46, game.ToggleSpeed, out _speedText);
            UIFactory.Place((RectTransform)_speed.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -186f), new Vector2(112f, 86f));
            _march = UIFactory.Button(root, "March", Loc.T("FAST\nMARCH"), UIFactory.Blue, 26, game.ToggleFastMarch, out _marchText);
            UIFactory.Place((RectTransform)_march.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -186f), new Vector2(150f, 86f));

            // sits between the level pill (left) and the coins + pause button (right) on a 1080-wide phone
            Image track = UIFactory.Image(root, "Progress", new Color(0f, 0f, 0f, 0.38f), GameAssets.RoundedSprite);
            UIFactory.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-82f, -42f), new Vector2(390f, 44f));
            Image fill = UIFactory.Image(track.rectTransform, "Fill", UIFactory.Gold, GameAssets.RoundedSprite);
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = Vector2.zero;
            _progressFill.anchorMax = new Vector2(0.05f, 1f);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
            // where each wave ends (the road stops there for a buff card)
            for (int i = 0; i < MaxWaveMarks; i++)
            {
                Image mark = UIFactory.Image(track.rectTransform, "Wave" + i, new Color(1f, 1f, 1f, 0.85f));
                _waveMarks[i] = mark.rectTransform;
                _waveMarks[i].anchorMin = new Vector2(0f, 0.15f);
                _waveMarks[i].anchorMax = new Vector2(0f, 0.85f);
                _waveMarks[i].sizeDelta = new Vector2(5f, 0f);
                mark.gameObject.SetActive(false);
            }
            Text bossMark = UIFactory.Text(track.rectTransform, "BossMark", Loc.T("BOSS"), 26, Color.white, TextAnchor.MiddleRight, UIFactory.Red, 2f);
            UIFactory.Place(bossMark.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(110f, 44f));

            // army make-up: one coloured segment per soldier line
            Image mix = UIFactory.Image(root, "Mix", new Color(0f, 0f, 0f, 0.38f), GameAssets.RoundedSprite);
            UIFactory.Place(mix.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-82f, -96f), new Vector2(390f, 18f));
            for (int l = 0; l < Units.Lines; l++)
            {
                Image seg = UIFactory.Image(mix.rectTransform, "Line" + l, Units.LineColor((UnitLine)l));
                _lineBars[l] = seg.rectTransform;
                _lineBars[l].offsetMin = Vector2.zero;
                _lineBars[l].offsetMax = Vector2.zero;
            }

            RectTransform coins = UIRoot.CoinPill(root, "Coins", out _coins);
            UIFactory.Place(coins, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-146f, -26f), new Vector2(240f, 88f));
            Button pause = UIFactory.Button(root, "Pause", Loc.T("II"), UIFactory.Blue, 46, game.Pause, out _);
            UIFactory.Place((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -20f), new Vector2(106f, 100f));

            Image bossBar = UIFactory.Image(root, "BossBar", new Color(0f, 0f, 0f, 0.45f), GameAssets.RoundedSprite);
            _bossBar = bossBar.gameObject;
            UIFactory.Place(bossBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(720f, 70f));
            Image bossFill = UIFactory.Image(bossBar.rectTransform, "Fill", UIFactory.Red, GameAssets.RoundedSprite);
            _bossFill = bossFill.rectTransform;
            _bossFill.anchorMin = Vector2.zero;
            _bossFill.anchorMax = Vector2.one;
            _bossFill.offsetMin = new Vector2(6f, 6f);
            _bossFill.offsetMax = new Vector2(-6f, -6f);
            _bossHp = UIFactory.Text(bossBar.rectTransform, "Hp", Loc.T("BOSS"), 38, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_bossHp.rectTransform);

            RectTransform combo = UIFactory.Place(UIFactory.Rect(root, "Combo"), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -226f), new Vector2(340f, 100f));
            _comboGroup = combo.gameObject.AddComponent<CanvasGroup>();
            _comboGroup.blocksRaycasts = false;
            _combo = UIFactory.Text(combo, "Text", string.Empty, 58, UIFactory.Gold, TextAnchor.MiddleRight, UIFactory.OutlineDark, 4f);
            UIFactory.Fill(_combo.rectTransform);
            // grows towards the centre when it pulses, never past the screen edge
            _combo.rectTransform.pivot = new Vector2(1f, 0.5f);
            _combo.rectTransform.anchoredPosition = Vector2.zero;
            Image comboTrack = UIFactory.Image(combo, "Timer", new Color(0f, 0f, 0f, 0.35f), GameAssets.RoundedSprite);
            UIFactory.Place(comboTrack.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 4f), new Vector2(240f, 12f));
            Image comboFill = UIFactory.Image(comboTrack.rectTransform, "Fill", UIFactory.Gold, GameAssets.RoundedSprite);
            _comboFill = comboFill.rectTransform;
            _comboFill.anchorMin = Vector2.zero;
            _comboFill.anchorMax = Vector2.one;
            _comboFill.offsetMin = Vector2.zero;
            _comboFill.offsetMax = Vector2.zero;

            _countdown = UIFactory.Text(root, "Countdown", string.Empty, 170, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 8f);
            UIFactory.Place(_countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1000f, 220f));

            Image banner = UIFactory.Image(root, "Banner", new Color(0.03f, 0.06f, 0.16f, 0.72f), GameAssets.RoundedSprite);
            _bannerRoot = banner.rectTransform;
            UIFactory.Place(_bannerRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(960f, 200f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.alpha = 0f;
            _bannerPortrait = UIFactory.Rect(_bannerRoot, "Portrait").gameObject.AddComponent<RawImage>();
            _bannerPortrait.raycastTarget = false;
            UIFactory.Place(_bannerPortrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(170f, 170f));
            _bannerTitle = UIFactory.Text(_bannerRoot, "Title", string.Empty, 66, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 4f);
            UIFactory.Place(_bannerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(940f, 92f));
            _bannerSub = UIFactory.Text(_bannerRoot, "Subtitle", string.Empty, 32, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(940f, 70f));
            _bannerSub.horizontalOverflow = HorizontalWrapMode.Wrap;
            // long enemy names shrink to fit instead of running off the banner
            FitText(_bannerTitle, 34);
            FitText(_bannerSub, 22);

            Image hint = UIFactory.Image(root, "Tip", new Color(0.03f, 0.06f, 0.16f, 0.75f), GameAssets.RoundedSprite);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-100f, 150f), new Vector2(760f, 120f));
            _hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            _hintGroup.blocksRaycasts = false;
            _hintGroup.alpha = 0f;
            _hintText = UIFactory.Text(hint.rectTransform, "Text", string.Empty, 34, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_hintText.rectTransform);
            _hintText.rectTransform.offsetMin = new Vector2(24f, 8f);
            _hintText.rectTransform.offsetMax = new Vector2(-24f, -8f);
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            Image drag = UIFactory.Image(root, "DragHint", new Color(0f, 0f, 0f, 0.42f), GameAssets.RoundedSprite);
            UIFactory.Place(drag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-50f, 290f), new Vector2(640f, 100f));
            _dragHint = drag.gameObject.AddComponent<CanvasGroup>();
            _dragHint.blocksRaycasts = false;
            Text dragText = UIFactory.Text(drag.rectTransform, "Text", Loc.T("<   DRAG TO MOVE   >"), 46, Color.white);
            UIFactory.Fill(dragText.rectTransform);

            _weapon = UIFactory.Text(root, "Weapon", string.Empty, 30, Color.white, TextAnchor.LowerLeft, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_weapon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(800f, 46f));

            // the war wagon's health: enemies that slip past the squad wear it down
            _basePill = UIFactory.Image(root, "Base", new Color(0f, 0f, 0f, 0.42f), GameAssets.RoundedSprite);
            UIFactory.Place(_basePill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 78f), new Vector2(430f, 58f));
            Text baseLabel = UIFactory.Text(_basePill.rectTransform, "Label", Loc.T("BASE"), 28, Color.white, TextAnchor.MiddleLeft, UIFactory.OutlineDark, 2f);
            UIFactory.Place(baseLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(128f, 50f));
            FitText(baseLabel, 18);
            Image baseTrack = UIFactory.Image(_basePill.rectTransform, "Track", new Color(0f, 0f, 0f, 0.45f), GameAssets.RoundedSprite);
            UIFactory.Place(baseTrack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(148f, 0f), new Vector2(270f, 32f));
            _baseFillImage = UIFactory.Image(baseTrack.rectTransform, "Fill", UIFactory.Green, GameAssets.RoundedSprite);
            _baseFill = _baseFillImage.rectTransform;
            _baseFill.anchorMin = Vector2.zero;
            _baseFill.anchorMax = Vector2.one;
            _baseFill.offsetMin = Vector2.zero;
            _baseFill.offsetMax = Vector2.zero;
            _baseText = UIFactory.Text(baseTrack.rectTransform, "Value", "100%", 24, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_baseText.rectTransform);

            // DASH: hold to charge ahead (where the right thumb rests); Space or up on a keyboard
            _dashRect = UIFactory.Place(UIFactory.Rect(root, "Dash"), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 76f), new Vector2(210f, 210f));
            Image dashBack = UIFactory.Image(_dashRect, "Back", new Color(0.03f, 0.06f, 0.16f, 0.78f), GameAssets.CircleSprite, true);
            UIFactory.Fill(dashBack.rectTransform);
            _dashFill = UIFactory.Image(_dashRect, "Fill", UIFactory.Gold, GameAssets.CircleSprite);
            UIFactory.Fill(_dashFill.rectTransform);
            _dashFill.rectTransform.offsetMin = new Vector2(12f, 12f);
            _dashFill.rectTransform.offsetMax = new Vector2(-12f, -12f);
            Text dashText = UIFactory.Text(_dashRect, "Label", Loc.T(">>\nDASH"), 44, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 3f);
            UIFactory.Fill(dashText.rectTransform);
            dashText.rectTransform.offsetMin = new Vector2(20f, 20f);
            dashText.rectTransform.offsetMax = new Vector2(-20f, -20f);
            FitText(dashText, 24);
            _dash = _dashRect.gameObject.AddComponent<HoldButton>();

            // drawn last: covers the rest of the HUD while the cards are up
            _buffs = new BuffPanel(root, game);
        }

        static bool Same(WeaponStats a, WeaponStats b) =>
            a.fireRateMul == b.fireRateMul && a.damageMul == b.damageMul && a.multiShot == b.multiShot && a.critChance == b.critChance;

        static void FitText(Text text, int min)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = text.fontSize;
        }

        /// <summary>True while a finger (or the mouse) holds the DASH button.</summary>
        public bool DashHeld => _dash.Held;

        public void Show(LevelSession session, SaveData save)
        {
            _session = session;
            _save = save;
            _shownCoins = -1;
            _shownCombo = -1;
            _shownBase = -1;
            _baseFlash = 0f;
            _shownBossHp = -1;
            _shownWave = -1;
            _shownSpeed = -1;
            _shownMarch = -1;
            _weaponText = null;
            _clock = 0f;
            _bannerClock = -1f;
            _hintClock = -1f;
            _bannerGroup.alpha = 0f;
            _hintGroup.alpha = 0f;
            Root.SetActive(true);
            _level.text = session.EndlessStage > 0 ? Loc.F("STAGE {0}", session.EndlessStage) : session.Def.daily ? Loc.T("DAILY") : Loc.F("LV {0}", session.Def.level);
            LevelDefinition def = session.Def;
            for (int i = 0; i < MaxWaveMarks; i++)
            {
                bool on = i < def.waveEnds.Count;
                _waveMarks[i].gameObject.SetActive(on);
                if (!on) continue;
                // the fill runs from 6% to 100% of the track (see Tick)
                float x = Mathf.Lerp(0.06f, 1f, Mathf.Clamp01(def.waveEnds[i] / Mathf.Max(1f, def.length)));
                _waveMarks[i].anchorMin = new Vector2(x, 0.15f);
                _waveMarks[i].anchorMax = new Vector2(x, 0.85f);
                _waveMarks[i].anchoredPosition = Vector2.zero;
            }
            Tick(0f);
        }

        public void Tick(float dt)
        {
            if (_session == null) return;
            _clock += dt;
            LevelSession s = _session;
            _progressFill.anchorMax = new Vector2(Mathf.Lerp(0.06f, 1f, s.Progress), 1f);

            int coins = Mathf.RoundToInt(s.Coins * _save.IncomeMultiplier);
            if (coins != _shownCoins)
            {
                _shownCoins = coins;
                _coins.text = UIRoot.Number(coins);
            }

            _barClock -= dt;
            if (_barClock <= 0f)
            {
                _barClock = 0.25f;
                float x = 0f;
                for (int l = 0; l < Units.Lines; l++)
                {
                    float share = s.Squad.LineShare((UnitLine)l);
                    _lineBars[l].anchorMin = new Vector2(x, 0f);
                    x += share;
                    _lineBars[l].anchorMax = new Vector2(x, 1f);
                }
            }

            BossUnit boss = s.Boss;
            bool showBoss = boss != null && s.Phase >= SessionPhase.BossFight && boss.IsAlive;
            if (_bossBar.activeSelf != showBoss) _bossBar.SetActive(showBoss);
            if (showBoss)
            {
                float hp = Mathf.Clamp01(boss.Hp / boss.MaxHp);
                _bossFill.anchorMax = new Vector2(Mathf.Max(0.02f, hp), 1f);
                int bossHp = Mathf.CeilToInt(Mathf.Max(0f, boss.Hp));
                if (bossHp != _shownBossHp)
                {
                    _shownBossHp = bossHp;
                    _bossHp.text = Loc.T(boss.Type.Name) + "  " + Fmt.Num(bossHp);
                }
            }

            int wave = s.Phase >= SessionPhase.BossFight ? 99 : s.Wave;
            if (wave != _shownWave)
            {
                _shownWave = wave;
                _wave.text = wave == 99 ? Loc.T("BOSS") : Loc.F("WAVE {0}/{1}", wave + 1, s.Waves);
                _wave.color = wave == 99 || wave == s.Waves - 1 ? new Color(1f, 0.55f, 0.45f) : Color.white;
            }
            _buffs.Tick(s, dt);
            TickSpeedButtons();

            TickCombo(dt);
            TickDash(dt);
            TickBase(dt);
            TickCountdown();
            TickBanner(dt);
            TickHint(dt);

            float dragTarget = s.HasMoved || s.IsOver ? 0f : 0.75f + Mathf.Sin(_clock * 4f) * 0.25f;
            _dragHint.alpha = Mathf.MoveTowards(_dragHint.alpha, dragTarget, dt * 3f);

            WeaponStats w = s.Squad.Effective(1f);
            if (_weaponText == null || !Same(w, _shownWeapon))
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                _shownWeapon = w;
                _weaponText = Loc.F("RATE x{0}   POWER x{1}   MULTI {2}   CRIT {3}%", w.fireRateMul.ToString("0.00", c), w.damageMul.ToString("0.00", c),
                    w.multiShot, Mathf.RoundToInt(w.critChance * 100f));
                _weapon.text = _weaponText;
            }
        }

        void TickSpeedButtons()
        {
            int speed = _save.gameSpeed;
            if (speed != _shownSpeed)
            {
                _shownSpeed = speed;
                _speedText.text = "x" + speed;
                UIFactory.SetButtonColor(_speed, speed > 1 ? UIFactory.Gold : UIFactory.Blue);
            }
            int march = _session.FastMarch ? 1 : 0;
            if (march != _shownMarch)
            {
                _shownMarch = march;
                UIFactory.SetButtonColor(_march, march > 0 ? UIFactory.Gold : UIFactory.Blue);
            }
            // the label flickers while the squad is actually hurrying
            _marchText.color = _session.Rushing ? Color.Lerp(Color.white, new Color(1f, 0.95f, 0.6f), 0.5f + 0.5f * Mathf.Sin(_clock * 14f)) : Color.white;
        }

        void TickCombo(float dt)
        {
            int combo = _session.Combo;
            if (combo != _shownCombo)
            {
                if (combo > _shownCombo) _comboPunch = 1f;
                _shownCombo = combo;
                _combo.text = combo >= 2 ? Loc.F("COMBO x{0}", combo) : string.Empty;
            }
            _comboPunch = Mathf.Max(0f, _comboPunch - dt * 6f);
            float scale = 1f + _comboPunch * 0.18f + Mathf.Min(combo, 200) / 1000f;
            _combo.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _combo.color = Color.Lerp(UIFactory.Gold, new Color(1f, 0.35f, 0.2f), Mathf.Clamp01(combo / 150f));
            _comboGroup.alpha = combo >= 2 ? 1f : Mathf.MoveTowards(_comboGroup.alpha, 0f, dt * 4f);
            _comboFill.anchorMax = new Vector2(_session.ComboTime01, 1f);
        }

        void TickDash(float dt)
        {
            bool held = _session.Dash;
            _dashFill.color = held ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.78f, 0.25f, 0.45f);
            float scale = held ? 0.9f : 1f;
            _dashRect.localScale = Vector3.Lerp(_dashRect.localScale, new Vector3(scale, scale, 1f), 1f - Mathf.Exp(-20f * dt));
        }

        void TickBase(float dt)
        {
            float health = _session.BaseHealth;
            int percent = Mathf.CeilToInt(health * 100f);
            if (percent != _shownBase)
            {
                if (_shownBase >= 0 && percent < _shownBase) _baseFlash = 1f;
                _shownBase = percent;
                _baseText.text = percent + "%";
                _baseFill.anchorMax = new Vector2(Mathf.Max(0.03f, health), 1f);
                _baseFillImage.color = health > 0.6f ? UIFactory.Green : health > 0.3f ? UIFactory.Gold : UIFactory.Red;
            }
            _baseFlash = Mathf.Max(0f, _baseFlash - dt * 3f);
            _basePill.color = Color.Lerp(new Color(0f, 0f, 0f, 0.42f), new Color(0.85f, 0.1f, 0.08f, 0.75f), _baseFlash);
        }

        void TickCountdown()
        {
            float t = _session.Countdown;
            if (t <= 0f && _countdown.text.Length == 0) return;
            if (t > 0.45f)
            {
                _countdown.text = Loc.T("READY...");
                _countdown.color = Color.white;
                _countdown.rectTransform.localScale = Vector3.one;
            }
            else if (t > -0.5f)
            {
                _countdown.text = Loc.T("GO!");
                _countdown.color = UIFactory.Gold;
                float k = Mathf.Clamp01((0.45f - t) / 0.95f);
                float s = 1f + k * 0.5f;
                _countdown.rectTransform.localScale = new Vector3(s, s, 1f);
                Color c = _countdown.color;
                c.a = 1f - k * k;
                _countdown.color = c;
            }
            else
            {
                _countdown.text = string.Empty;
            }
        }

        void TickBanner(float dt)
        {
            if (_bannerClock < 0f)
            {
                if (_session.Banners.Count == 0) return;
                BannerInfo b = _session.Banners.Dequeue();
                _bannerTitle.text = Loc.T(b.Title);
                _bannerTitle.color = b.Color;
                _bannerSub.text = Loc.T(b.Subtitle ?? string.Empty);
                Texture2D portrait = Portraits.Get(b.Portrait);
                _bannerPortrait.texture = portrait;
                _bannerPortrait.gameObject.SetActive(portrait != null);
                float inset = portrait != null ? 90f : 0f;
                _bannerTitle.rectTransform.anchoredPosition = new Vector2(inset, -14f);
                _bannerSub.rectTransform.anchoredPosition = new Vector2(inset, 22f);
                _bannerTitle.rectTransform.sizeDelta = new Vector2(940f - inset * 2f, 92f);
                _bannerSub.rectTransform.sizeDelta = new Vector2(940f - inset * 2f, 70f);
                _bannerClock = 0f;
                GameRoot.Instance?.Context.Audio.Play(Sfx.Banner, 0.7f);
            }
            _bannerClock += dt;
            float t = _bannerClock;
            float pop = t < 0.18f ? Mathf.Lerp(0.6f, 1.06f, t / 0.18f) : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((t - 0.18f) / 0.12f));
            _bannerRoot.localScale = new Vector3(pop, pop, 1f);
            // queued banners hurry along
            float duration = _session.Banners.Count > 0 ? BannerTime * 0.7f : BannerTime;
            _bannerGroup.alpha = t < duration - 0.3f ? 1f : Mathf.Clamp01((duration - t) / 0.3f);
            if (t >= duration)
            {
                _bannerClock = -1f;
                _bannerGroup.alpha = 0f;
            }
        }

        void TickHint(float dt)
        {
            if (_hintClock < 0f)
            {
                if (_session.Hints.Count == 0) return;
                _hintText.text = Loc.F("TIP: {0}", Loc.T(_session.Hints.Dequeue()));
                _hintClock = 0f;
            }
            _hintClock += dt;
            float t = _hintClock;
            _hintGroup.alpha = t < 0.25f ? t / 0.25f : t < HintTime - 0.4f ? 1f : Mathf.Clamp01((HintTime - t) / 0.4f);
            if (t >= HintTime)
            {
                _hintClock = -1f;
                _hintGroup.alpha = 0f;
            }
        }
    }
}
