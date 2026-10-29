using System.Globalization;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>
    /// In-level HUD: level, progress and army make-up, coins, boss bar, combo counter, the VOLLEY button,
    /// READY... GO!, announcement banners and first-time tips.
    /// </summary>
    sealed class HudView
    {
        const float BannerTime = 1.9f;
        const float HintTime = 4.2f;

        public readonly GameObject Root;
        readonly Text _level;
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
        readonly Image _skillFill;
        readonly Text _skillText;
        readonly Button _skillButton;
        readonly Pulse _skillPulse;
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
        int _shownSkill = -1;
        int _shownBossHp = -1;
        WeaponStats _shownWeapon;

        public HudView(RectTransform parent, GameRoot game)
        {
            RectTransform root = UIFactory.Fill(UIFactory.Rect(parent, "Hud"));
            Root = root.gameObject;

            Image levelPill = UIFactory.Image(root, "LevelPill", UIFactory.Blue, GameAssets.RoundedSprite);
            UIFactory.Place(levelPill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -26f), new Vector2(190f, 88f));
            _level = UIFactory.Text(levelPill.rectTransform, "Text", "LV 1", 46, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_level.rectTransform);

            // sits between the level pill (left) and the coins + pause button (right) on a 1080-wide phone
            Image track = UIFactory.Image(root, "Progress", new Color(0f, 0f, 0f, 0.38f), GameAssets.RoundedSprite);
            UIFactory.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-82f, -42f), new Vector2(390f, 44f));
            Image fill = UIFactory.Image(track.rectTransform, "Fill", UIFactory.Gold, GameAssets.RoundedSprite);
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = Vector2.zero;
            _progressFill.anchorMax = new Vector2(0.05f, 1f);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
            Text bossMark = UIFactory.Text(track.rectTransform, "BossMark", "BOSS", 26, Color.white, TextAnchor.MiddleRight, UIFactory.Red, 2f);
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
            Button pause = UIFactory.Button(root, "Pause", "II", UIFactory.Blue, 46, game.Pause, out _);
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
            _bossHp = UIFactory.Text(bossBar.rectTransform, "Hp", "BOSS", 38, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
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
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 400f), new Vector2(940f, 120f));
            _hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            _hintGroup.blocksRaycasts = false;
            _hintGroup.alpha = 0f;
            _hintText = UIFactory.Text(hint.rectTransform, "Text", string.Empty, 34, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_hintText.rectTransform);
            _hintText.rectTransform.offsetMin = new Vector2(24f, 8f);
            _hintText.rectTransform.offsetMax = new Vector2(-24f, -8f);
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            Image drag = UIFactory.Image(root, "DragHint", new Color(0f, 0f, 0f, 0.42f), GameAssets.RoundedSprite);
            UIFactory.Place(drag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60f, 260f), new Vector2(640f, 100f));
            _dragHint = drag.gameObject.AddComponent<CanvasGroup>();
            _dragHint.blocksRaycasts = false;
            Text dragText = UIFactory.Text(drag.rectTransform, "Text", "<   DRAG TO MOVE   >", 46, Color.white);
            UIFactory.Fill(dragText.rectTransform);

            _weapon = UIFactory.Text(root, "Weapon", string.Empty, 30, Color.white, TextAnchor.LowerLeft, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_weapon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(800f, 46f));

            // VOLLEY skill: fills with kills, tap when ready
            RectTransform skill = UIFactory.Place(UIFactory.Rect(root, "Skill"), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 76f), new Vector2(210f, 210f));
            _skillPulse = skill.gameObject.AddComponent<Pulse>();
            _skillPulse.Amount = 0.07f;
            _skillPulse.Speed = 7f;
            Image skillBack = UIFactory.Image(skill, "Back", new Color(0.03f, 0.06f, 0.16f, 0.78f), GameAssets.CircleSprite, true);
            UIFactory.Fill(skillBack.rectTransform);
            _skillFill = UIFactory.Image(skill, "Fill", UIFactory.Gold, GameAssets.CircleSprite);
            UIFactory.Fill(_skillFill.rectTransform);
            _skillFill.rectTransform.offsetMin = new Vector2(10f, 10f);
            _skillFill.rectTransform.offsetMax = new Vector2(-10f, -10f);
            _skillFill.type = Image.Type.Filled;
            _skillFill.fillMethod = Image.FillMethod.Radial360;
            _skillFill.fillOrigin = (int)Image.Origin360.Top;
            _skillFill.fillClockwise = true;
            _skillText = UIFactory.Text(skill, "Label", "VOLLEY", 40, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 3f);
            UIFactory.Fill(_skillText.rectTransform);
            _skillButton = skill.gameObject.AddComponent<Button>();
            _skillButton.targetGraphic = skillBack;
            _skillButton.onClick.AddListener(() => _session?.UseSkill());
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

        public void Show(LevelSession session, SaveData save)
        {
            _session = session;
            _save = save;
            _shownCoins = -1;
            _shownCombo = -1;
            _shownSkill = -1;
            _shownBossHp = -1;
            _weaponText = null;
            _clock = 0f;
            _bannerClock = -1f;
            _hintClock = -1f;
            _bannerGroup.alpha = 0f;
            _hintGroup.alpha = 0f;
            Root.SetActive(true);
            _level.text = session.Def.daily ? "DAILY" : "LV " + session.Def.level;
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
                    _bossHp.text = boss.Type.Name + "  " + Fmt.Num(bossHp);
                }
            }

            TickCombo(dt);
            TickSkill();
            TickCountdown();
            TickBanner(dt);
            TickHint(dt);

            float dragTarget = s.HasMoved || s.IsOver ? 0f : 0.75f + Mathf.Sin(_clock * 4f) * 0.25f;
            _dragHint.alpha = Mathf.MoveTowards(_dragHint.alpha, dragTarget, dt * 3f);

            WeaponStats w = s.Squad.Weapon;
            if (_weaponText == null || !Same(w, _shownWeapon))
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                _shownWeapon = w;
                _weaponText = "RATE x" + w.fireRateMul.ToString("0.00", c) + "   POWER x" + w.damageMul.ToString("0.00", c) +
                              "   MULTI " + w.multiShot + "   CRIT " + Mathf.RoundToInt(w.critChance * 100f) + "%";
                _weapon.text = _weaponText;
            }
        }

        void TickCombo(float dt)
        {
            int combo = _session.Combo;
            if (combo != _shownCombo)
            {
                if (combo > _shownCombo) _comboPunch = 1f;
                _shownCombo = combo;
                _combo.text = combo >= 2 ? "COMBO x" + combo : string.Empty;
            }
            _comboPunch = Mathf.Max(0f, _comboPunch - dt * 6f);
            float scale = 1f + _comboPunch * 0.18f + Mathf.Min(combo, 200) / 1000f;
            _combo.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _combo.color = Color.Lerp(UIFactory.Gold, new Color(1f, 0.35f, 0.2f), Mathf.Clamp01(combo / 150f));
            _comboGroup.alpha = combo >= 2 ? 1f : Mathf.MoveTowards(_comboGroup.alpha, 0f, dt * 4f);
            _comboFill.anchorMax = new Vector2(_session.ComboTime01, 1f);
        }

        void TickSkill()
        {
            bool ready = _session.SkillReady;
            _skillFill.fillAmount = _session.SkillCharge01;
            _skillFill.color = ready ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.78f, 0.25f, 0.6f);
            int percent = ready ? 101 : Mathf.FloorToInt(_session.SkillCharge01 * 100f);
            if (percent != _shownSkill)
            {
                _shownSkill = percent;
                _skillText.text = ready ? "VOLLEY\nTAP!" : "VOLLEY\n" + percent + "%";
            }
            _skillButton.interactable = ready;
            _skillPulse.enabled = ready;
        }

        void TickCountdown()
        {
            float t = _session.Countdown;
            if (t <= 0f && _countdown.text.Length == 0) return;
            if (t > 0.45f)
            {
                _countdown.text = "READY...";
                _countdown.color = Color.white;
                _countdown.rectTransform.localScale = Vector3.one;
            }
            else if (t > -0.5f)
            {
                _countdown.text = "GO!";
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
                _bannerTitle.text = b.Title;
                _bannerTitle.color = b.Color;
                _bannerSub.text = b.Subtitle ?? string.Empty;
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
                _hintText.text = "TIP: " + _session.Hints.Dequeue();
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
