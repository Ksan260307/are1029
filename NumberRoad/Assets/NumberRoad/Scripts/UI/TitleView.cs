using System;
using System.Globalization;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>
    /// Home screen: logo, level, currencies, the next unlock, the starting line picker, eight upgrade cards
    /// (affordable ones glow) and the buttons for barracks, bestiary, awards, stats and the daily rewards.
    /// </summary>
    sealed class TitleView
    {
        public readonly GameObject Root;
        readonly GameRoot _game;
        readonly Text _level;
        readonly Text _biome;
        readonly Text _stars;
        readonly Text _coins;
        readonly Text _gems;
        readonly Text _unlock;
        readonly Text _coupon;
        readonly RectTransform _unlockFill;
        readonly GameObject _unlockBar;
        readonly UpgradeCard[] _cards;
        readonly LineButton[] _lines;
        readonly Text _awardsBadge;
        readonly GameObject _awardsBadgeRoot;
        readonly GameObject _dailyBadge;
        readonly GameObject _luckyBadge;
        readonly GameObject _challengeBadge;
        readonly Text _challengeText;
        float _clock;

        public TitleView(RectTransform parent, GameRoot game, Panels panels)
        {
            _game = game;
            RectTransform root = UIFactory.Fill(UIFactory.Rect(parent, "Title"));
            Root = root.gameObject;

            Button settings = UIFactory.Button(root, "Settings", "SETTINGS", UIFactory.Blue, 34, panels.ShowSettings, out _);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(230f, 96f));

            RectTransform coins = UIRoot.CoinPill(root, "Coins", out _coins);
            UIFactory.Place(coins, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(320f, 92f));
            RectTransform gems = UIRoot.GemPill(root, "Gems", out _gems);
            UIFactory.Place(gems, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -128f), new Vector2(240f, 84f));

            Text logo1 = UIFactory.Text(root, "Logo1", "NUMBER", 128, Color.white, TextAnchor.MiddleCenter, UIFactory.BlueDark, 8f);
            UIFactory.Place(logo1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1000f, 150f));
            Text logo2 = UIFactory.Text(root, "Logo2", "ROAD", 168, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 9f);
            UIFactory.Place(logo2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1000f, 190f));
            Text sub = UIFactory.Text(root, "Subtitle", "GATE  DEFENSE", 40, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 3f);
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -436f), new Vector2(1000f, 56f));

            _level = UIFactory.Text(root, "Level", "LEVEL 1", 78, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 4f);
            UIFactory.Place(_level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -496f), new Vector2(1000f, 96f));
            _biome = UIFactory.Text(root, "Biome", string.Empty, 34, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_biome.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-40f, -592f), new Vector2(600f, 50f));
            Image star = UIFactory.Star(root, 46f, true);
            UIFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(190f, -617f), new Vector2(46f, 46f));
            _stars = UIFactory.Text(root, "Stars", "0", 34, Color.white, TextAnchor.MiddleLeft, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_stars.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(218f, -617f), new Vector2(200f, 50f));

            _unlock = UIFactory.Text(root, "Unlock", string.Empty, 32, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_unlock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -650f), new Vector2(800f, 46f));
            Image track = UIFactory.Image(root, "UnlockBar", new Color(0f, 0f, 0f, 0.38f), GameAssets.RoundedSprite);
            UIFactory.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -702f), new Vector2(420f, 24f));
            _unlockBar = track.gameObject;
            Image fill = UIFactory.Image(track.rectTransform, "Fill", UIFactory.Green, GameAssets.RoundedSprite);
            _unlockFill = fill.rectTransform;
            _unlockFill.anchorMin = Vector2.zero;
            _unlockFill.anchorMax = new Vector2(0.1f, 1f);
            _unlockFill.offsetMin = Vector2.zero;
            _unlockFill.offsetMax = Vector2.zero;

            // side menus
            SideButton(root, "Units", "UNITS", 0f, 170f, UIFactory.Blue, panels.ShowBarracks, out _);
            SideButton(root, "Enemies", "ENEMIES", 0f, 66f, UIFactory.Blue, panels.ShowBestiary, out _);
            RectTransform awards = SideButton(root, "Awards", "AWARDS", 0f, -38f, UIFactory.Purple, panels.ShowAchievements, out _);
            _awardsBadge = UIFactory.Badge(awards, out _awardsBadgeRoot);
            SideButton(root, "Stats", "STATS", 0f, -142f, UIFactory.Blue, panels.ShowStats, out _);
            RectTransform daily = SideButton(root, "Daily", "DAILY", 1f, 170f, UIFactory.Green, panels.ShowDaily, out _);
            UIFactory.Badge(daily, out _dailyBadge).text = "!";
            RectTransform lucky = SideButton(root, "Lucky", "LUCKY", 1f, 66f, UIFactory.Gold, panels.ShowLucky, out _);
            UIFactory.Badge(lucky, out _luckyBadge).text = "!";
            RectTransform challenge = SideButton(root, "Challenge", "CHALLENGE", 1f, -38f, UIFactory.Red, panels.ShowChallenge, out _challengeText);
            UIFactory.Badge(challenge, out _challengeBadge).text = "!";

            // bottom: play, line picker, upgrades
            RectTransform playHolder = UIFactory.Place(UIFactory.Rect(root, "PlayHolder"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 470f), new Vector2(600f, 170f));
            playHolder.gameObject.AddComponent<Pulse>();
            Button play = UIFactory.Button(playHolder, "Play", "PLAY", UIFactory.Gold, 100, game.Play, out _);
            UIFactory.Fill((RectTransform)play.transform);
            _coupon = UIFactory.Text(root, "Coupon", "x2 COINS NEXT RUN", 30, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_coupon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 646f), new Vector2(520f, 44f));

            RectTransform lines = UIFactory.Place(UIFactory.Rect(root, "Lines"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 344f), new Vector2(1040f, 104f));
            _lines = new LineButton[Units.Lines];
            for (int i = 0; i < Units.Lines; i++) _lines[i] = new LineButton(lines, (UnitLine)i, game, (i - 2) * 208f);

            RectTransform cards = UIFactory.Scroll(root, "Upgrades", true, 14f, out ScrollRect scroll);
            RectTransform scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 0f);
            scrollRect.pivot = new Vector2(0.5f, 0f);
            scrollRect.offsetMin = new Vector2(16f, 20f);
            scrollRect.offsetMax = new Vector2(-16f, 320f);
            var kinds = (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind));
            _cards = new UpgradeCard[kinds.Length];
            for (int i = 0; i < kinds.Length; i++) _cards[i] = new UpgradeCard(cards, kinds[i], game);
        }

        static RectTransform SideButton(RectTransform root, string name, string label, float side, float y, Color color, Action onClick, out Text text)
        {
            Button b = UIFactory.Button(root, name, label, color, label.Length > 7 ? 30 : 34, onClick, out text);
            var rt = (RectTransform)b.transform;
            UIFactory.Place(rt, new Vector2(side, 0.5f), new Vector2(side, 0.5f), new Vector2(side < 0.5f ? 22f : -22f, y), new Vector2(230f, 94f));
            return rt;
        }

        public void Show(SaveData save)
        {
            Root.SetActive(true);
            Refresh(save);
        }

        public void Refresh(SaveData save)
        {
            _level.text = "LEVEL " + save.level;
            _biome.text = Biomes.ForLevel(save.level).Name + (save.bestLevel > save.level ? "   BEST " + save.bestLevel : string.Empty);
            _stars.text = save.TotalStars + "/" + (Mathf.Max(1, save.bestLevel - 1) * 3);
            _coins.text = UIRoot.Number(save.coins);
            _gems.text = UIRoot.Number(save.gems);
            _coupon.gameObject.SetActive(save.doubleCoinsNextRun);

            UnitLine? next = Units.NextUnlock(save.level);
            _unlockBar.SetActive(next.HasValue);
            if (next.HasValue)
            {
                int at = Units.UnlockLevel(next.Value);
                int from = 1;
                for (int l = 0; l < Units.Lines; l++)
                {
                    int u = Units.UnlockLevel((UnitLine)l);
                    if (u <= save.level) from = Mathf.Max(from, u);
                }
                _unlock.text = "NEXT UNIT: " + Units.LineName(next.Value) + " AT LEVEL " + at;
                float t = Mathf.Clamp01((save.level - from) / (float)Mathf.Max(1, at - from));
                _unlockFill.anchorMax = new Vector2(Mathf.Max(0.06f, t), 1f);
            }
            else
            {
                _unlock.text = "ALL UNITS UNLOCKED - " + save.EnemiesDiscovered + "/" + Enemies.All.Length + " ENEMIES FOUND";
            }

            foreach (UpgradeCard card in _cards) card.Refresh(save);
            foreach (LineButton line in _lines) line.Refresh(save);

            int today = Daily.Today;
            int ready = Achievements.ReadyCount(save);
            _awardsBadgeRoot.SetActive(ready > 0);
            _awardsBadge.text = ready.ToString(CultureInfo.InvariantCulture);
            _dailyBadge.SetActive(Daily.LoginRewardReady(save, today));
            _luckyBadge.SetActive(Daily.LuckyReady(save, today));
            bool challenge = Daily.ChallengeReady(save, today);
            _challengeBadge.SetActive(challenge);
            _challengeText.text = challenge ? "CHALLENGE" : "DONE TODAY";
        }

        public void Tick(float dt)
        {
            _clock += dt;
            foreach (UpgradeCard card in _cards) card.Tick(_clock);
        }
    }

    sealed class UpgradeCard
    {
        readonly UpgradeKind _kind;
        readonly Text _value;
        readonly Text _level;
        readonly Text _cost;
        readonly Button _buy;
        readonly Image _glow;
        bool _affordable;

        public UpgradeCard(RectTransform list, UpgradeKind kind, GameRoot game)
        {
            _kind = kind;
            RectTransform slot = UIFactory.Item(list, kind + "Card", new Vector2(236f, 296f));
            _glow = UIFactory.Image(slot, "Glow", UIFactory.Gold, GameAssets.RoundedSprite);
            UIFactory.Fill(_glow.rectTransform);
            Image card = UIFactory.Image(slot, "Card", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Fill(card.rectTransform);
            card.rectTransform.offsetMin = new Vector2(6f, 6f);
            card.rectTransform.offsetMax = new Vector2(-6f, -6f);
            RectTransform rt = card.rectTransform;

            Text title = UIFactory.Text(rt, "Title", Title(kind), 34, UIFactory.Ink);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(220f, 46f));
            _value = UIFactory.Text(rt, "Value", string.Empty, 50, UIFactory.Blue);
            UIFactory.Place(_value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(220f, 64f));
            _level = UIFactory.Text(rt, "Level", string.Empty, 28, UIFactory.Gray);
            UIFactory.Place(_level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(220f, 38f));

            _buy = UIFactory.Button(rt, "Buy", string.Empty, UIFactory.Green, 38, () => game.Buy(kind), out _cost);
            UIFactory.Place((RectTransform)_buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(204f, 96f));
            var face = (RectTransform)_buy.targetGraphic.transform;
            RectTransform coin = UIFactory.Coin(face, 38f);
            UIFactory.Place(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(38f, 38f));
            _cost.rectTransform.offsetMin = new Vector2(46f, 0f);
        }

        static string Title(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Squad: return "SQUAD";
                case UpgradeKind.Power: return "POWER";
                case UpgradeKind.Rate: return "RATE";
                case UpgradeKind.Income: return "COINS";
                case UpgradeKind.Crit: return "CRITICAL";
                case UpgradeKind.Armor: return "ARMOR";
                case UpgradeKind.Skill: return "VOLLEY";
                default: return "RECRUIT";
            }
        }

        static string ValueText(UpgradeKind kind, int level)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            switch (kind)
            {
                case UpgradeKind.Squad: return Fmt.Num(Balance.StartSquad(level));
                case UpgradeKind.Power: return "x" + Balance.PowerMultiplier(level).ToString("0.00", c);
                case UpgradeKind.Rate: return "x" + Balance.RateMultiplier(level).ToString("0.00", c);
                case UpgradeKind.Income: return "x" + Balance.IncomeMultiplier(level).ToString("0.0", c);
                case UpgradeKind.Crit: return Mathf.RoundToInt(Balance.CritChance(level) * 100f) + "%";
                case UpgradeKind.Armor: return "+" + Mathf.RoundToInt(Balance.GuardBonus(level) * 100f) + "%";
                case UpgradeKind.Skill: return Balance.SkillPower(level).ToString("0.0", c) + "s";
                default: return "x" + Balance.RecruitBonus(level).ToString("0.00", c);
            }
        }

        public void Refresh(SaveData save)
        {
            int level = save.GetUpgrade(_kind);
            int cost = save.UpgradeCost(_kind);
            _value.text = ValueText(_kind, level);
            _level.text = "Lv " + (level + 1);
            _cost.text = Fmt.Num(cost);
            _affordable = save.coins >= cost;
            _buy.interactable = _affordable;
            UIFactory.SetButtonColor(_buy, _affordable ? UIFactory.Green : UIFactory.Gray);
        }

        public void Tick(float clock)
        {
            Color c = UIFactory.Gold;
            c.a = _affordable ? 0.55f + 0.45f * Mathf.Sin(clock * 5f) : 0f;
            _glow.color = c;
        }
    }

    /// <summary>Picks the soldier line the squad starts with (locked lines show their unlock level).</summary>
    sealed class LineButton
    {
        readonly UnitLine _line;
        readonly Button _button;
        readonly Text _text;
        readonly Image _frame;

        public LineButton(RectTransform parent, UnitLine line, GameRoot game, float x)
        {
            _line = line;
            _frame = UIFactory.Image(parent, line + "Frame", Color.white, GameAssets.RoundedSprite);
            UIFactory.Place(_frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(204f, 108f));
            _button = UIFactory.Button(parent, line.ToString(), Units.LineName(line), Units.LineColor(line), 32, () => game.SetStartLine(line), out _text);
            UIFactory.Place((RectTransform)_button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(192f, 98f));
        }

        public void Refresh(SaveData save)
        {
            bool unlocked = Units.IsUnlocked(_line, save.level);
            bool selected = save.StartLine == _line;
            _button.interactable = unlocked;
            UIFactory.SetButtonColor(_button, unlocked ? Units.LineColor(_line) : UIFactory.Gray);
            _text.text = unlocked ? Units.LineName(_line) : "LV " + Units.UnlockLevel(_line);
            _frame.color = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
    }
}
