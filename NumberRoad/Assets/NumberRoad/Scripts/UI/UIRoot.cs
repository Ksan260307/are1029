using System;
using System.Globalization;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>Owns the three canvases (world labels, HUD, screens) and switches between screens.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public LabelLayer Labels { get; private set; }

        TitleView _title;
        HudView _hud;
        PauseView _pause;
        ResultView _result;

        public void Init(GameRoot game, Func<Camera> camera)
        {
            EnsureEventSystem();
            Canvas labels = UIFactory.CreateCanvas("Labels", transform, 0, false);
            Labels = new LabelLayer(UIFactory.Fill(UIFactory.Rect(labels.transform, "Root")), camera);

            Canvas hud = UIFactory.CreateCanvas("HUD", transform, 10, true);
            Canvas screens = UIFactory.CreateCanvas("Screens", transform, 20, true);
            RectTransform full = UIFactory.Fill(UIFactory.Rect(screens.transform, "Full"));
            _hud = new HudView(SafeRoot(hud.transform), game);
            _title = new TitleView(SafeRoot(screens.transform), game);
            _pause = new PauseView(full, game);
            _result = new ResultView(full, game);
            UIFactory.ClickSound = () => game.Context.Audio.Play(Sfx.Click, 0.8f, 0.02f, 0.02f);
            HideAll();
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(transform, false);
        }

        static RectTransform SafeRoot(Transform canvas)
        {
            RectTransform rt = UIFactory.Fill(UIFactory.Rect(canvas, "SafeArea"));
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        void HideAll()
        {
            _title.Root.SetActive(false);
            _hud.Root.SetActive(false);
            _pause.Root.SetActive(false);
            _result.Root.SetActive(false);
        }

        public void ShowTitle(SaveData save)
        {
            HideAll();
            Labels.OnlySquadBadge = true;
            _title.Show(save);
        }

        public void RefreshSave(SaveData save)
        {
            if (_title.Root.activeSelf) _title.Refresh(save);
            if (_pause.Root.activeSelf) _pause.Refresh(save);
        }

        public void ShowHud(LevelSession session, SaveData save)
        {
            HideAll();
            Labels.OnlySquadBadge = false;
            _hud.Show(session, save);
        }

        public void ShowPause(bool show, SaveData save)
        {
            _pause.Root.SetActive(show);
            if (show) _pause.Refresh(save);
        }

        public void ShowResult(bool won, int level, int earned, int total)
        {
            HideAll();
            _result.Show(won, level, earned, total);
        }

        public void LateTick(float unscaledDt)
        {
            Labels.LateTick(Time.deltaTime);
            if (_hud.Root.activeSelf) _hud.Tick(unscaledDt);
            if (_result.Root.activeSelf) _result.Tick(unscaledDt);
        }

        internal static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        internal static RectTransform CoinPill(Transform parent, string name, out Text text)
        {
            Image pill = UIFactory.Image(parent, name, new Color(0f, 0f, 0f, 0.4f), GameAssets.RoundedSprite);
            RectTransform coin = UIFactory.Coin(pill.rectTransform, 60f);
            UIFactory.Place(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(60f, 60f));
            text = UIFactory.Text(pill.rectTransform, "Value", "0", 46, Color.white, TextAnchor.MiddleRight, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(84f, 0f);
            text.rectTransform.offsetMax = new Vector2(-22f, 0f);
            return pill.rectTransform;
        }
    }

    /// <summary>Gentle breathing scale, used on the PLAY button container.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.035f;
        public float Speed = 3.2f;

        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * Speed) * Amount;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    sealed class TitleView
    {
        public readonly GameObject Root;
        readonly Text _level;
        readonly Text _coins;
        readonly Text _best;
        readonly Button _sound;
        readonly Button _music;
        readonly Text _soundText;
        readonly Text _musicText;
        readonly UpgradeCard[] _cards;

        public TitleView(RectTransform parent, GameRoot game)
        {
            RectTransform root = UIFactory.Fill(UIFactory.Rect(parent, "Title"));
            Root = root.gameObject;

            _sound = UIFactory.Button(root, "Sound", "SFX", UIFactory.Blue, 34, () => game.SetSound(!game.Save.sound), out _soundText);
            UIFactory.Place((RectTransform)_sound.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(150f, 100f));
            _music = UIFactory.Button(root, "Music", "BGM", UIFactory.Blue, 34, () => game.SetMusic(!game.Save.music), out _musicText);
            UIFactory.Place((RectTransform)_music.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -24f), new Vector2(150f, 100f));

            RectTransform coins = UIRoot.CoinPill(root, "Coins", out _coins);
            UIFactory.Place(coins, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(310f, 96f));

            Text logo1 = UIFactory.Text(root, "Logo1", "NUMBER", 150, Color.white, TextAnchor.MiddleCenter, UIFactory.BlueDark, 8f);
            UIFactory.Place(logo1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 170f));
            Text logo2 = UIFactory.Text(root, "Logo2", "ROAD", 190, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 9f);
            UIFactory.Place(logo2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(1000f, 210f));
            Text sub = UIFactory.Text(root, "Subtitle", "GATE  DEFENSE", 44, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 3f);
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -505f), new Vector2(1000f, 60f));
            _level = UIFactory.Text(root, "Level", "LEVEL 1", 80, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 4f);
            UIFactory.Place(_level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -585f), new Vector2(1000f, 100f));
            _best = UIFactory.Text(root, "Best", "", 36, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -680f), new Vector2(1000f, 50f));

            RectTransform playHolder = UIFactory.Place(UIFactory.Rect(root, "PlayHolder"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 395f), new Vector2(620f, 180f));
            playHolder.gameObject.AddComponent<Pulse>();
            Button play = UIFactory.Button(playHolder, "Play", "PLAY", UIFactory.Gold, 104, game.Play, out _);
            UIFactory.Fill((RectTransform)play.transform);

            RectTransform row = UIFactory.Place(UIFactory.Rect(root, "Upgrades"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(1040f, 320f));
            var kinds = (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind));
            _cards = new UpgradeCard[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                _cards[i] = new UpgradeCard(row, kinds[i], game, (i - (kinds.Length - 1) * 0.5f) * 258f);
            }
        }

        public void Show(SaveData save)
        {
            Root.SetActive(true);
            Refresh(save);
        }

        public void Refresh(SaveData save)
        {
            _level.text = "LEVEL " + save.level;
            _best.text = save.bestLevel > save.level ? "BEST " + save.bestLevel : string.Empty;
            _coins.text = UIRoot.Number(save.coins);
            foreach (UpgradeCard card in _cards) card.Refresh(save);
            Toggle(_sound, _soundText, "SFX", save.sound);
            Toggle(_music, _musicText, "BGM", save.music);
        }

        internal static void Toggle(Button button, Text text, string name, bool on)
        {
            text.text = name + (on ? "\nON" : "\nOFF");
            UIFactory.SetButtonColor(button, on ? UIFactory.Blue : UIFactory.Gray);
        }
    }

    sealed class UpgradeCard
    {
        readonly UpgradeKind _kind;
        readonly Text _value;
        readonly Text _level;
        readonly Text _cost;
        readonly Button _buy;

        public UpgradeCard(RectTransform parent, UpgradeKind kind, GameRoot game, float x)
        {
            _kind = kind;
            Image card = UIFactory.Image(parent, kind + "Card", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(240f, 320f));
            RectTransform rt = card.rectTransform;

            Text title = UIFactory.Text(rt, "Title", Title(kind), 38, UIFactory.Ink);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(230f, 50f));
            _value = UIFactory.Text(rt, "Value", string.Empty, 62, UIFactory.Blue);
            UIFactory.Place(_value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(230f, 76f));
            _level = UIFactory.Text(rt, "Level", string.Empty, 32, UIFactory.Gray);
            UIFactory.Place(_level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(230f, 44f));

            _buy = UIFactory.Button(rt, "Buy", string.Empty, UIFactory.Green, 42, () => game.Buy(kind), out _cost);
            UIFactory.Place((RectTransform)_buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(208f, 100f));
            RectTransform face = (RectTransform)_buy.targetGraphic.transform;
            RectTransform coin = UIFactory.Coin(face, 42f);
            UIFactory.Place(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(42f, 42f));
            _cost.rectTransform.offsetMin = new Vector2(52f, 0f);
        }

        static string Title(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Squad: return "SQUAD";
                case UpgradeKind.Power: return "POWER";
                case UpgradeKind.Rate: return "RATE";
                default: return "COINS";
            }
        }

        static string ValueText(UpgradeKind kind, int level)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            switch (kind)
            {
                case UpgradeKind.Squad: return Balance.StartSquad(level).ToString(c);
                case UpgradeKind.Power: return "x" + Balance.PowerMultiplier(level).ToString("0.00", c);
                case UpgradeKind.Rate: return "x" + Balance.RateMultiplier(level).ToString("0.00", c);
                default: return "x" + Balance.IncomeMultiplier(level).ToString("0.0", c);
            }
        }

        public void Refresh(SaveData save)
        {
            int level = save.GetUpgrade(_kind);
            int cost = save.UpgradeCost(_kind);
            _value.text = ValueText(_kind, level);
            _level.text = "Lv " + (level + 1);
            _cost.text = UIRoot.Number(cost);
            bool affordable = save.coins >= cost;
            _buy.interactable = affordable;
            UIFactory.SetButtonColor(_buy, affordable ? UIFactory.Green : UIFactory.Gray);
        }
    }

    sealed class HudView
    {
        public readonly GameObject Root;
        readonly Text _level;
        readonly Text _coins;
        readonly Text _weapon;
        readonly Text _bossHp;
        readonly RectTransform _progressFill;
        readonly RectTransform _bossFill;
        readonly GameObject _bossBar;
        readonly CanvasGroup _hint;
        LevelSession _session;
        SaveData _save;
        int _shownCoins = -1;
        string _weaponText;
        float _clock;

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
            UIFactory.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-82f, -50f), new Vector2(390f, 44f));
            Image fill = UIFactory.Image(track.rectTransform, "Fill", UIFactory.Gold, GameAssets.RoundedSprite);
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = Vector2.zero;
            _progressFill.anchorMax = new Vector2(0.05f, 1f);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
            Text boss = UIFactory.Text(track.rectTransform, "BossMark", "BOSS", 26, Color.white, TextAnchor.MiddleRight, UIFactory.Red, 2f);
            UIFactory.Place(boss.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(110f, 44f));

            RectTransform coins = UIRoot.CoinPill(root, "Coins", out _coins);
            UIFactory.Place(coins, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-146f, -26f), new Vector2(240f, 88f));
            Button pause = UIFactory.Button(root, "Pause", "II", UIFactory.Blue, 46, game.Pause, out _);
            UIFactory.Place((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -20f), new Vector2(106f, 100f));

            Image bossBar = UIFactory.Image(root, "BossBar", new Color(0f, 0f, 0f, 0.45f), GameAssets.RoundedSprite);
            _bossBar = bossBar.gameObject;
            UIFactory.Place(bossBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(720f, 70f));
            Image bossFill = UIFactory.Image(bossBar.rectTransform, "Fill", UIFactory.Red, GameAssets.RoundedSprite);
            _bossFill = bossFill.rectTransform;
            _bossFill.anchorMin = Vector2.zero;
            _bossFill.anchorMax = Vector2.one;
            _bossFill.offsetMin = new Vector2(6f, 6f);
            _bossFill.offsetMax = new Vector2(-6f, -6f);
            _bossHp = UIFactory.Text(bossBar.rectTransform, "Hp", "BOSS", 42, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(_bossHp.rectTransform);

            Image hint = UIFactory.Image(root, "Hint", new Color(0f, 0f, 0f, 0.42f), GameAssets.RoundedSprite);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(640f, 100f));
            _hint = hint.gameObject.AddComponent<CanvasGroup>();
            _hint.blocksRaycasts = false;
            Text hintText = UIFactory.Text(hint.rectTransform, "Text", "<   DRAG TO MOVE   >", 46, Color.white);
            UIFactory.Fill(hintText.rectTransform);

            _weapon = UIFactory.Text(root, "Weapon", string.Empty, 34, Color.white, TextAnchor.LowerLeft, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_weapon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 22f), new Vector2(1000f, 50f));
        }

        public void Show(LevelSession session, SaveData save)
        {
            _session = session;
            _save = save;
            _shownCoins = -1;
            _weaponText = null;
            _clock = 0f;
            Root.SetActive(true);
            _level.text = "LV " + session.Def.level;
            Tick(0f);
        }

        public void Tick(float dt)
        {
            if (_session == null) return;
            _clock += dt;
            _progressFill.anchorMax = new Vector2(Mathf.Lerp(0.06f, 1f, _session.Progress), 1f);

            int coins = Mathf.RoundToInt(_session.Coins * _save.IncomeMultiplier);
            if (coins != _shownCoins)
            {
                _shownCoins = coins;
                _coins.text = UIRoot.Number(coins);
            }

            Enemy boss = _session.Boss;
            bool showBoss = boss != null && _session.Phase >= SessionPhase.BossFight && boss.IsAlive;
            if (_bossBar.activeSelf != showBoss) _bossBar.SetActive(showBoss);
            if (showBoss)
            {
                float hp = Mathf.Clamp01(boss.Hp / boss.MaxHp);
                _bossFill.anchorMax = new Vector2(Mathf.Max(0.02f, hp), 1f);
                _bossHp.text = "BOSS  " + UIRoot.Number(Mathf.CeilToInt(Mathf.Max(0f, boss.Hp)));
            }

            float hintTarget = _session.HasMoved || _session.IsOver ? 0f : 0.75f + Mathf.Sin(_clock * 4f) * 0.25f;
            _hint.alpha = Mathf.MoveTowards(_hint.alpha, hintTarget, dt * 3f);

            WeaponStats w = _session.Squad.Weapon;
            CultureInfo c = CultureInfo.InvariantCulture;
            string weapon = "RATE x" + w.fireRateMul.ToString("0.00", c) + "    POWER x" + w.damageMul.ToString("0.00", c) + "    MULTI " + w.multiShot;
            if (weapon != _weaponText)
            {
                _weaponText = weapon;
                _weapon.text = weapon;
            }
        }
    }

    sealed class PauseView
    {
        public readonly GameObject Root;
        readonly Button _sound;
        readonly Button _music;
        readonly Text _soundText;
        readonly Text _musicText;

        public PauseView(RectTransform parent, GameRoot game)
        {
            Image dim = UIFactory.Image(parent, "Pause", UIFactory.Shade, null, true);
            UIFactory.Fill(dim.rectTransform);
            Root = dim.gameObject;

            Image panel = UIFactory.Image(dim.rectTransform, "Panel", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 860f));
            panel.gameObject.AddComponent<PopIn>();
            RectTransform p = panel.rectTransform;

            Text title = UIFactory.Text(p, "Title", "PAUSED", 92, UIFactory.Ink);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 120f));

            AddButton(p, "Resume", "RESUME", UIFactory.Blue, -190f, game.Resume);
            AddButton(p, "Restart", "RESTART", UIFactory.Gold, -330f, game.Restart);
            AddButton(p, "Home", "HOME", UIFactory.Gray, -470f, game.Home);

            _sound = UIFactory.Button(p, "Sound", "SFX", UIFactory.Blue, 36, () => game.SetSound(!game.Save.sound), out _soundText);
            UIFactory.Place((RectTransform)_sound.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-140f, -640f), new Vector2(250f, 120f));
            _music = UIFactory.Button(p, "Music", "BGM", UIFactory.Blue, 36, () => game.SetMusic(!game.Save.music), out _musicText);
            UIFactory.Place((RectTransform)_music.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(140f, -640f), new Vector2(250f, 120f));
        }

        static void AddButton(RectTransform parent, string name, string label, Color color, float y, Action onClick)
        {
            Button b = UIFactory.Button(parent, name, label, color, 58, onClick, out _);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(540f, 120f));
        }

        public void Refresh(SaveData save)
        {
            TitleView.Toggle(_sound, _soundText, "SFX", save.sound);
            TitleView.Toggle(_music, _musicText, "BGM", save.music);
        }
    }

    sealed class ResultView
    {
        public readonly GameObject Root;
        readonly Text _title;
        readonly Text _subtitle;
        readonly Text _coins;
        readonly Text _total;
        readonly Text _continueText;
        readonly Button _continue;
        float _clock;
        int _earned;

        public ResultView(RectTransform parent, GameRoot game)
        {
            Image dim = UIFactory.Image(parent, "Result", UIFactory.Shade, null, true);
            UIFactory.Fill(dim.rectTransform);
            Root = dim.gameObject;

            Image panel = UIFactory.Image(dim.rectTransform, "Panel", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 760f));
            panel.gameObject.AddComponent<PopIn>();
            RectTransform p = panel.rectTransform;

            _title = UIFactory.Text(p, "Title", "VICTORY!", 112, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 6f);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(740f, 140f));
            _subtitle = UIFactory.Text(p, "Subtitle", string.Empty, 48, UIFactory.Ink);
            UIFactory.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(740f, 64f));

            RectTransform coin = UIFactory.Coin(p, 84f);
            UIFactory.Place(coin, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-150f, -330f), new Vector2(84f, 84f));
            _coins = UIFactory.Text(p, "Coins", "+0", 92, UIFactory.Gold, TextAnchor.MiddleLeft, UIFactory.OutlineDark, 4f);
            UIFactory.Place(_coins.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-90f, -330f), new Vector2(400f, 110f));
            _total = UIFactory.Text(p, "Total", string.Empty, 40, UIFactory.Gray);
            UIFactory.Place(_total.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(740f, 56f));

            _continue = UIFactory.Button(p, "Continue", "NEXT", UIFactory.Blue, 64, game.Continue, out _continueText);
            UIFactory.Place((RectTransform)_continue.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(560f, 150f));
        }

        public void Show(bool won, int level, int earned, int total)
        {
            Root.SetActive(true);
            _clock = 0f;
            _earned = earned;
            _title.text = won ? "VICTORY!" : "DEFEAT";
            _title.color = won ? UIFactory.Gold : UIFactory.Red;
            _subtitle.text = won ? "LEVEL " + level + " CLEAR" : "LEVEL " + level;
            _total.text = "TOTAL " + UIRoot.Number(total);
            _continueText.text = won ? "NEXT" : "RETRY";
            UIFactory.SetButtonColor(_continue, won ? UIFactory.Blue : UIFactory.Gold);
            _coins.text = "+0";
        }

        public void Tick(float dt)
        {
            if (_clock >= 1.2f) return;
            _clock = Mathf.Min(1.2f, _clock + dt);
            float t = Mathf.Clamp01((_clock - 0.25f) / 0.8f);
            _coins.text = "+" + UIRoot.Number(Mathf.RoundToInt(_earned * (1f - (1f - t) * (1f - t))));
        }
    }
}
