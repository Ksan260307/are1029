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
    /// Home screen, top to bottom: currencies (and CLAIM ALL while rewards wait), logo, the level picker
    /// (&lt; LEVEL N &gt;, tap for the level list), region and stars, the next unlock, two menu columns (left:
    /// collection and records, right: rewards and modes), PLAY, the starting line picker and eight upgrade cards
    /// (affordable ones glow).
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
        readonly Button _prev;
        readonly Button _next;
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
        readonly GameObject _claimAll;
        readonly Text _claimBadge;
        float _clock;

        public TitleView(RectTransform parent, GameRoot game, Panels panels)
        {
            _game = game;
            RectTransform root = UIFactory.Fill(UIFactory.Rect(parent, "Title"));
            Root = root.gameObject;
            var top = new Vector2(0.5f, 1f);

            // --- top bar
            Button settings = UIFactory.Button(root, "Settings", "SETTINGS", UIFactory.Blue, 34, panels.ShowSettings, out _);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(230f, 96f));
            // only there while something is waiting to be collected
            Button claimAll = UIFactory.Button(root, "ClaimAll", "CLAIM ALL", UIFactory.Green, 32, panels.ClaimAll, out _);
            var claimRect = (RectTransform)claimAll.transform;
            UIFactory.Place(claimRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -128f), new Vector2(230f, 88f));
            claimAll.gameObject.AddComponent<Pulse>().Amount = 0.05f;
            _claimBadge = UIFactory.Badge(claimRect, out _);
            _claimAll = claimAll.gameObject;
            RectTransform coins = UIRoot.CoinPill(root, "Coins", out _coins);
            UIFactory.Place(coins, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(320f, 92f));
            RectTransform gems = UIRoot.GemPill(root, "Gems", out _gems);
            UIFactory.Place(gems, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -128f), new Vector2(240f, 84f));

            // --- logo
            Text logo1 = UIFactory.Text(root, "Logo1", "NUMBER", 118, Color.white, TextAnchor.MiddleCenter, UIFactory.BlueDark, 7f);
            UIFactory.Place(logo1.rectTransform, top, top, new Vector2(0f, -112f), new Vector2(1000f, 140f));
            Text logo2 = UIFactory.Text(root, "Logo2", "ROAD", 156, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 8f);
            UIFactory.Place(logo2.rectTransform, top, top, new Vector2(0f, -222f), new Vector2(1000f, 176f));

            // --- level picker: arrows step through the levels reached so far, the number opens the list
            Image levelHit = UIFactory.Image(root, "LevelPick", new Color(0f, 0f, 0f, 0.28f), GameAssets.RoundedSprite, true);
            UIFactory.Place(levelHit.rectTransform, top, top, new Vector2(0f, -410f), new Vector2(460f, 108f));
            var pick = levelHit.gameObject.AddComponent<Button>();
            pick.targetGraphic = levelHit;
            pick.onClick.AddListener(() =>
            {
                UIFactory.ClickSound?.Invoke();
                panels.ShowLevels();
            });
            levelHit.gameObject.AddComponent<PressScale>();
            _level = UIFactory.Text(levelHit.rectTransform, "Level", "LEVEL 1", 74, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 4f);
            UIFactory.Fill(_level.rectTransform);
            _prev = UIFactory.Button(root, "Prev", "<", UIFactory.Blue, 64, () => game.SelectLevel(game.Save.level - 1), out _);
            UIFactory.Place((RectTransform)_prev.transform, top, top, new Vector2(-310f, -410f), new Vector2(120f, 108f));
            _next = UIFactory.Button(root, "Next", ">", UIFactory.Blue, 64, () => game.SelectLevel(game.Save.level + 1), out _);
            UIFactory.Place((RectTransform)_next.transform, top, top, new Vector2(310f, -410f), new Vector2(120f, 108f));

            _biome = UIFactory.Text(root, "Biome", string.Empty, 34, Color.white, TextAnchor.MiddleRight, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_biome.rectTransform, top, new Vector2(1f, 1f), new Vector2(40f, -530f), new Vector2(560f, 50f));
            Image star = UIFactory.Star(root, 44f, true);
            UIFactory.Place(star.rectTransform, top, new Vector2(0.5f, 0.5f), new Vector2(90f, -555f), new Vector2(44f, 44f));
            _stars = UIFactory.Text(root, "Stars", "0", 34, Color.white, TextAnchor.MiddleLeft, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_stars.rectTransform, top, new Vector2(0f, 1f), new Vector2(118f, -530f), new Vector2(260f, 50f));

            _unlock = UIFactory.Text(root, "Unlock", string.Empty, 30, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_unlock.rectTransform, top, top, new Vector2(0f, -584f), new Vector2(900f, 44f));
            Image track = UIFactory.Image(root, "UnlockBar", new Color(0f, 0f, 0f, 0.38f), GameAssets.RoundedSprite);
            UIFactory.Place(track.rectTransform, top, top, new Vector2(0f, -632f), new Vector2(420f, 22f));
            _unlockBar = track.gameObject;
            Image fill = UIFactory.Image(track.rectTransform, "Fill", UIFactory.Green, GameAssets.RoundedSprite);
            _unlockFill = fill.rectTransform;
            _unlockFill.anchorMin = Vector2.zero;
            _unlockFill.anchorMax = new Vector2(0.1f, 1f);
            _unlockFill.offsetMin = Vector2.zero;
            _unlockFill.offsetMax = Vector2.zero;

            // --- left column: collection and records; right column: rewards
            SideButton(root, "Units", "UNITS", 0f, 193f, UIFactory.Blue, panels.ShowBarracks, out _);
            SideButton(root, "Enemies", "ENEMIES", 0f, 89f, UIFactory.Blue, panels.ShowBestiary, out _);
            SideButton(root, "Ranking", "RANKING", 0f, -15f, UIFactory.Blue, panels.ShowRanking, out _);
            SideButton(root, "Stats", "STATS", 0f, -119f, UIFactory.Blue, panels.ShowStats, out _);
            RectTransform daily = SideButton(root, "Daily", "DAILY", 1f, 193f, UIFactory.Green, panels.ShowDaily, out _);
            UIFactory.Badge(daily, out _dailyBadge).text = "!";
            RectTransform lucky = SideButton(root, "Lucky", "LUCKY", 1f, 89f, UIFactory.Gold, panels.ShowLucky, out _);
            UIFactory.Badge(lucky, out _luckyBadge).text = "!";
            RectTransform challenge = SideButton(root, "Challenge", "CHALLENGE", 1f, -15f, UIFactory.Red, panels.ShowChallenge, out _challengeText);
            UIFactory.Badge(challenge, out _challengeBadge).text = "!";
            SideButton(root, "Endless", "ENDLESS", 1f, -119f, new Color32(120, 70, 200, 255), panels.ShowEndless, out _);
            RectTransform awards = SideButton(root, "Awards", "AWARDS", 1f, -223f, UIFactory.Purple, panels.ShowAchievements, out _);
            _awardsBadge = UIFactory.Badge(awards, out _awardsBadgeRoot);

            // --- bottom: play, line picker, upgrades
            var bottom = new Vector2(0.5f, 0f);
            RectTransform playHolder = UIFactory.Place(UIFactory.Rect(root, "PlayHolder"), bottom, bottom, new Vector2(0f, 482f), new Vector2(600f, 166f));
            playHolder.gameObject.AddComponent<Pulse>();
            Button play = UIFactory.Button(playHolder, "Play", "PLAY", UIFactory.Gold, 100, game.Play, out _);
            UIFactory.Fill((RectTransform)play.transform);
            _coupon = UIFactory.Text(root, "Coupon", "x2 COINS NEXT RUN", 30, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(_coupon.rectTransform, bottom, bottom, new Vector2(0f, 654f), new Vector2(520f, 44f));

            Text startWith = UIFactory.Text(root, "StartWith", "START WITH", 28, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(startWith.rectTransform, bottom, bottom, new Vector2(0f, 402f), new Vector2(600f, 40f));
            RectTransform lines = UIFactory.Place(UIFactory.Rect(root, "Lines"), bottom, bottom, new Vector2(0f, 296f), new Vector2(1040f, 104f));
            _lines = new LineButton[Units.Lines];
            for (int i = 0; i < Units.Lines; i++) _lines[i] = new LineButton(lines, (UnitLine)i, game, (i - 2) * 208f);

            RectTransform cards = UIFactory.Scroll(root, "Upgrades", true, 14f, out ScrollRect scroll);
            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 0f);
            scrollRect.pivot = new Vector2(0.5f, 0f);
            scrollRect.offsetMin = new Vector2(16f, 18f);
            scrollRect.offsetMax = new Vector2(-16f, 290f);
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
            bool canPrev = save.level > 1, canNext = save.level < save.bestLevel;
            _prev.interactable = canPrev;
            _next.interactable = canNext;
            UIFactory.SetButtonColor(_prev, canPrev ? UIFactory.Blue : UIFactory.Gray);
            UIFactory.SetButtonColor(_next, canNext ? UIFactory.Blue : UIFactory.Gray);
            _biome.text = Biomes.ForLevel(save.level).Name;
            _stars.text = save.TotalStars + "/" + Mathf.Max(1, save.bestLevel - 1) * 3;
            _coins.text = UIRoot.Number(save.coins);
            _gems.text = UIRoot.Number(save.gems);
            _coupon.gameObject.SetActive(save.doubleCoinsNextRun);

            UnitLine? next = Units.NextUnlock(save.bestLevel);
            _unlockBar.SetActive(next.HasValue);
            if (next.HasValue)
            {
                int at = Units.UnlockLevel(next.Value);
                int from = 1;
                for (int l = 0; l < Units.Lines; l++)
                {
                    int u = Units.UnlockLevel((UnitLine)l);
                    if (u <= save.bestLevel) from = Mathf.Max(from, u);
                }
                _unlock.text = "NEXT UNIT: " + Units.LineName(next.Value) + " AT LEVEL " + at;
                float t = Mathf.Clamp01((save.bestLevel - from) / (float)Mathf.Max(1, at - from));
                _unlockFill.anchorMax = new Vector2(Mathf.Max(0.06f, t), 1f);
            }
            else
            {
                _unlock.text = save.EnemiesDiscovered + "/" + Enemies.All.Length + " ENEMIES FOUND";
            }

            foreach (UpgradeCard card in _cards) card.Refresh(save);
            foreach (LineButton line in _lines) line.Refresh(save);

            int today = Daily.Today;
            int claimable = Rewards.ReadyCount(save, today);
            _claimAll.SetActive(claimable > 0);
            _claimBadge.text = claimable.ToString(CultureInfo.InvariantCulture);
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
            RectTransform slot = UIFactory.Item(list, kind + "Card", new Vector2(236f, 268f));
            _glow = UIFactory.Image(slot, "Glow", UIFactory.Gold, GameAssets.RoundedSprite);
            UIFactory.Fill(_glow.rectTransform);
            Image card = UIFactory.Image(slot, "Card", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Fill(card.rectTransform);
            card.rectTransform.offsetMin = new Vector2(6f, 6f);
            card.rectTransform.offsetMax = new Vector2(-6f, -6f);
            RectTransform rt = card.rectTransform;
            var top = new Vector2(0.5f, 1f);

            Text title = UIFactory.Text(rt, "Title", Title(kind), 32, UIFactory.Ink);
            UIFactory.Place(title.rectTransform, top, top, new Vector2(0f, -10f), new Vector2(220f, 42f));
            _value = UIFactory.Text(rt, "Value", string.Empty, 48, UIFactory.Blue);
            UIFactory.Place(_value.rectTransform, top, top, new Vector2(0f, -50f), new Vector2(220f, 60f));
            _level = UIFactory.Text(rt, "Level", string.Empty, 26, UIFactory.Gray);
            UIFactory.Place(_level.rectTransform, top, top, new Vector2(0f, -108f), new Vector2(220f, 34f));

            _buy = UIFactory.Button(rt, "Buy", string.Empty, UIFactory.Green, 36, () => game.Buy(kind), out _cost);
            UIFactory.Place((RectTransform)_buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(204f, 90f));
            var face = (RectTransform)_buy.targetGraphic.transform;
            RectTransform coin = UIFactory.Coin(face, 36f);
            UIFactory.Place(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(36f, 36f));
            _cost.rectTransform.offsetMin = new Vector2(44f, 0f);
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
            UIFactory.Place(_frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(206f, 110f));
            _button = UIFactory.Button(parent, line.ToString(), Units.LineName(line), Units.LineColor(line), 32, () => game.SetStartLine(line), out _text);
            UIFactory.Place((RectTransform)_button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(192f, 98f));
        }

        public void Refresh(SaveData save)
        {
            bool unlocked = Units.IsUnlocked(_line, save.bestLevel);
            bool selected = save.StartLine == _line;
            _button.interactable = unlocked;
            UIFactory.SetButtonColor(_button, unlocked ? Units.LineColor(_line) : UIFactory.Gray);
            _text.text = unlocked ? Units.LineName(_line) : "LV " + Units.UnlockLevel(_line);
            _frame.color = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
    }
}
