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
    /// Modal panels opened from the title screen: settings, barracks (20 classes), bestiary, awards, statistics,
    /// the daily login bonus, the lucky draw, the daily challenge, the "welcome back" coins and the CLAIM ALL
    /// summary. Each panel is built when opened and destroyed when closed.
    /// </summary>
    sealed class Panels
    {
        readonly RectTransform _root;
        readonly GameRoot _game;
        GameObject _current;
        Action _rebuild;
        Action<float> _tick;
        ScrollRect _scroll;

        public Panels(RectTransform root, GameRoot game)
        {
            _root = root;
            _game = game;
        }

        SaveData Save => _game.Save;

        public bool IsOpen => _current != null;

        public void CloseAll()
        {
            if (_current != null) UnityEngine.Object.Destroy(_current);
            _current = null;
            _rebuild = null;
            _tick = null;
            _scroll = null;
        }

        public void CloseTop() => CloseAll();

        /// <summary>Rebuilds the open panel after the save changed (keeps the scroll position).</summary>
        public void Refresh()
        {
            if (_rebuild == null) return;
            float position = _scroll != null ? _scroll.verticalNormalizedPosition : 1f;
            Action rebuild = _rebuild;
            rebuild();
            if (_scroll == null) return;
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = position;
        }

        public void Tick(float dt) => _tick?.Invoke(dt);

        RectTransform Open(string title, Vector2 size, Action rebuild)
        {
            CloseAll();
            Image dim = UIFactory.Image(_root, "Panel_" + title, UIFactory.Shade, null, true);
            UIFactory.Fill(dim.rectTransform);
            _current = dim.gameObject;
            _rebuild = rebuild;

            Image card = UIFactory.Image(dim.rectTransform, "Card", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            card.gameObject.AddComponent<PopIn>();
            RectTransform c = card.rectTransform;

            Text head = UIFactory.Text(c, "Title", title, 72, UIFactory.Ink);
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(size.x - 260f, 100f));
            // long titles shrink instead of running under the close button
            head.horizontalOverflow = HorizontalWrapMode.Wrap;
            head.verticalOverflow = VerticalWrapMode.Truncate;
            head.resizeTextForBestFit = true;
            head.resizeTextMinSize = 40;
            head.resizeTextMaxSize = 72;
            Button close = UIFactory.Button(c, "Close", "X", UIFactory.Red, 50, CloseAll, out _);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(104f, 104f));

            RectTransform body = UIFactory.Rect(c, "Body");
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(20f, 20f);
            body.offsetMax = new Vector2(-20f, -140f);
            return body;
        }

        RectTransform OpenList(string title, Action rebuild, float spacing = 12f)
        {
            RectTransform body = Open(title, new Vector2(900f, 1480f), rebuild);
            RectTransform list = UIFactory.Scroll(body, "List", false, spacing, out _scroll);
            UIFactory.Fill((RectTransform)_scroll.transform);
            return list;
        }

        /// <summary>A text box. <paramref name="pivot"/> defaults to the anchor (a top-anchored label hangs from its position).</summary>
        static Text Label(RectTransform parent, string text, int size, Color color, TextAnchor anchor, Vector2 anchorPoint, Vector2 position, Vector2 box,
            Vector2? pivot = null)
        {
            Text t = UIFactory.Text(parent, "Text", text, size, color, anchor);
            UIFactory.Place(t.rectTransform, anchorPoint, pivot ?? anchorPoint, position, box);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        static RawImage Portrait(RectTransform parent, string model, bool known, Vector2 anchor, Vector2 position, float size, Vector2? pivot = null)
        {
            var image = UIFactory.Rect(parent, "Portrait").gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = Portraits.Get(model);
            image.color = known ? Color.white : new Color(0.05f, 0.05f, 0.1f, 0.85f);
            UIFactory.Place(image.rectTransform, anchor, pivot ?? anchor, position, new Vector2(size, size));
            if (image.texture == null) image.color = new Color(0f, 0f, 0f, 0.1f);
            return image;
        }

        Button Big(RectTransform parent, string label, Color color, Action onClick, Vector2 anchor, Vector2 position, Vector2 size, out Text text)
        {
            Button b = UIFactory.Button(parent, label, label, color, 50, onClick, out text);
            UIFactory.Place((RectTransform)b.transform, anchor, anchor, position, size);
            return b;
        }

        // --- Settings ------------------------------------------------------------------

        public void ShowSettings()
        {
            RectTransform body = Open("SETTINGS", new Vector2(820f, 1180f), ShowSettings);
            var top = new Vector2(0.5f, 1f);
            Toggle(body, "SOUND EFFECTS", Save.sound, () => _game.SetSound(!Save.sound), -20f);
            Toggle(body, "MUSIC", Save.music, () => _game.SetMusic(!Save.music), -170f);
            Toggle(body, "VIBRATION", Save.vibration, () => _game.SetVibration(!Save.vibration), -320f);
            string quality;
            switch ((Quality)Save.quality)
            {
                case Quality.High: quality = "HIGH"; break;
                case Quality.Low: quality = "LOW"; break;
                default: quality = "AUTO"; break;
            }
            Big(body, "QUALITY: " + quality, UIFactory.Blue, _game.CycleQuality, top, new Vector2(0f, -470f), new Vector2(620f, 130f), out _);
            Label(body, "LOW quality draws far enemies simply and uses fewer particles. AUTO switches to it when the game runs slowly.",
                28, UIFactory.Gray, TextAnchor.UpperCenter, top, new Vector2(0f, -606f), new Vector2(720f, 90f));
            Label(body, "HOW TO PLAY\nDrag to steer. Pass through the best gate, shoot crates and gold goblins, build a huge army and defeat the boss!",
                28, UIFactory.Ink, TextAnchor.UpperCenter, top, new Vector2(0f, -706f), new Vector2(740f, 140f));
            Big(body, "RESET PROGRESS", UIFactory.Red, ShowResetConfirm, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(520f, 110f), out Text reset);
            reset.fontSize = 40;
        }

        void ShowResetConfirm()
        {
            RectTransform body = Open("RESET?", new Vector2(820f, 760f), null);
            var top = new Vector2(0.5f, 1f);
            Label(body, "ALL PROGRESS WILL BE DELETED:", 38, UIFactory.Red, TextAnchor.MiddleCenter, top, new Vector2(0f, -20f), new Vector2(760f, 56f));
            Label(body, "levels, coins, gems, upgrades, awards, records and the ranking.\nSound and display settings are kept. This cannot be undone.",
                30, UIFactory.Ink, TextAnchor.UpperCenter, top, new Vector2(0f, -100f), new Vector2(740f, 160f));
            Big(body, "CANCEL", UIFactory.Gray, ShowSettings, new Vector2(0.5f, 0f), new Vector2(-180f, 40f), new Vector2(320f, 130f), out _);
            Big(body, "RESET", UIFactory.Red, () =>
            {
                CloseAll();
                _game.ResetProgress();
            }, new Vector2(0.5f, 0f), new Vector2(180f, 40f), new Vector2(320f, 130f), out _);
        }

        void Toggle(RectTransform body, string name, bool on, Action onClick, float y)
        {
            Button b = Big(body, name + (on ? ": ON" : ": OFF"), on ? UIFactory.Green : UIFactory.Gray, onClick,
                new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(620f, 130f), out _);
            b.name = name;
        }

        // --- Level select ----------------------------------------------------------------

        public void ShowLevels()
        {
            RectTransform list = OpenList("LEVELS", ShowLevels, 14f);
            const int perRow = 5;
            int shown = Save.bestLevel + 5;
            for (int first = 1; first <= shown; first += perRow)
            {
                RectTransform row = UIFactory.Item(list, "Row" + first, new Vector2(840f, 156f));
                for (int k = 0; k < perRow && first + k <= shown; k++)
                {
                    int level = first + k;
                    bool open = level <= Save.bestLevel;
                    bool current = level == Save.level;
                    Color color = !open ? UIFactory.Gray : current ? UIFactory.Gold : Biomes.ForLevel(level).Sky;
                    Button cell = UIFactory.Button(row, "Level" + level, level.ToString(CultureInfo.InvariantCulture), color, 46, () =>
                    {
                        CloseAll();
                        _game.SelectLevel(level);
                    }, out Text text);
                    UIFactory.Place((RectTransform)cell.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f + k * 167f, 0f), new Vector2(156f, 150f));
                    cell.interactable = open;
                    text.rectTransform.offsetMin = new Vector2(0f, 34f);
                    if (open)
                    {
                        int stars = Save.StarsFor(level);
                        for (int st = 0; st < 3; st++)
                        {
                            Image star = UIFactory.Star(cell.targetGraphic.transform, 34f, st < stars);
                            UIFactory.Place(star.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((st - 1) * 38f, 14f), new Vector2(34f, 34f));
                        }
                    }
                    string tag = level % 10 == 0 ? "CASTLE" : level % 5 == 0 ? "GOLD" : null;
                    if (tag != null)
                    {
                        Text t = UIFactory.Text(cell.targetGraphic.transform, "Tag", tag, 20, Color.white, TextAnchor.UpperCenter, UIFactory.OutlineDark, 2f);
                        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(150f, 26f));
                    }
                }
            }
            // start with the current level in view
            Canvas.ForceUpdateCanvases();
            int rows = (shown + perRow - 1) / perRow;
            int currentRow = (Save.level - 1) / perRow;
            _scroll.verticalNormalizedPosition = rows <= 1 ? 1f : 1f - Mathf.Clamp01(currentRow / (float)(rows - 1));
        }

        // --- Ranking -----------------------------------------------------------------------

        RankMetric _rankTab;

        public void ShowRanking() => ShowRanking(_rankTab);

        void ShowRanking(RankMetric tab)
        {
            _rankTab = tab;
            RectTransform body = Open("RANKING", new Vector2(900f, 1480f), () => ShowRanking(tab));
            var top = new Vector2(0.5f, 1f);
            (RankMetric metric, string label)[] tabs = { (RankMetric.Score, "SCORE"), (RankMetric.Army, "ARMY"), (RankMetric.Combo, "COMBO") };
            for (int i = 0; i < tabs.Length; i++)
            {
                RankMetric m = tabs[i].metric;
                Big(body, tabs[i].label, m == tab ? UIFactory.Blue : UIFactory.Gray, () => ShowRanking(m), top,
                    new Vector2((i - 1) * 270f, -10f), new Vector2(250f, 100f), out Text t);
                t.fontSize = 38;
            }
            Label(body, "YOUR BEST RUNS ON THIS DEVICE", 26, UIFactory.Gray, TextAnchor.MiddleCenter, top, new Vector2(0f, -122f), new Vector2(800f, 36f));

            System.Collections.Generic.List<RankEntry> entries = Ranking.Top(Save, tab);
            if (entries.Count == 0)
            {
                Label(body, "NO RUNS YET - GO PLAY!", 40, UIFactory.Ink, TextAnchor.MiddleCenter, top, new Vector2(0f, -400f), new Vector2(800f, 60f));
                return;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                RankEntry e = entries[i];
                float y = -170f - i * 112f;
                Image back = UIFactory.Image(body, "Row" + i, i == 0 ? new Color(1f, 0.85f, 0.3f, 0.35f) : new Color(0f, 0f, 0f, i % 2 == 0 ? 0.06f : 0.03f), GameAssets.RoundedSprite);
                UIFactory.Place(back.rectTransform, top, top, new Vector2(0f, y), new Vector2(840f, 104f));
                RectTransform r = back.rectTransform;
                Color medal = i == 0 ? UIFactory.GoldDark : i == 1 ? UIFactory.Gray : i == 2 ? new Color32(176, 110, 60, 255) : UIFactory.Ink;
                Label(r, "#" + (i + 1), 46, medal, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(110f, 80f), new Vector2(0f, 0.5f));
                Label(r, Fmt.Num(Ranking.Value(e, tab)), 46, UIFactory.Ink, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(140f, 10f), new Vector2(330f, 60f), new Vector2(0f, 0.5f));
                string where = e.endless ? "ENDLESS STAGE " + e.stage : (e.daily ? "DAILY " : "LEVEL ") + e.level + (e.won ? "  CLEAR" : "");
                Label(r, where, 24, e.won ? UIFactory.Green : UIFactory.Gray, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(142f, -30f), new Vector2(330f, 34f), new Vector2(0f, 0.5f));
                string detail = tab == RankMetric.Score
                    ? "ARMY " + Fmt.Num(e.army) + "   COMBO " + Fmt.Num(e.combo)
                    : "SCORE " + Fmt.Num(e.score);
                Label(r, detail, 24, UIFactory.Gray, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-20f, 14f), new Vector2(360f, 34f), new Vector2(1f, 0.5f));
                string date = System.DateTimeOffset.FromUnixTimeSeconds(e.unix).ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
                Label(r, date, 22, UIFactory.Gray, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-20f, -22f), new Vector2(360f, 30f), new Vector2(1f, 0.5f));
            }
        }

        // --- Barracks ------------------------------------------------------------------

        public void ShowBarracks()
        {
            RectTransform list = OpenList("UNITS", ShowBarracks, 16f);
            var topLeft = new Vector2(0f, 1f);
            var hang = new Vector2(0.5f, 1f);
            for (int l = 0; l < Units.Lines; l++)
            {
                var line = (UnitLine)l;
                bool open = Units.IsUnlocked(line, Save.bestLevel);
                RectTransform row = UIFactory.Item(list, "Line" + l, new Vector2(840f, 410f));
                Image back = UIFactory.Image(row, "Back", Color.Lerp(Units.LineColor(line), Color.white, 0.75f), GameAssets.RoundedSprite);
                UIFactory.Fill(back.rectTransform);
                Label(row, Units.LineName(line) + " LINE" + (open ? string.Empty : "  -  UNLOCKS AT LEVEL " + Units.UnlockLevel(line)),
                    32, UIFactory.Ink, TextAnchor.MiddleCenter, hang, new Vector2(0f, -12f), new Vector2(800f, 44f));
                for (int t = 0; t < Units.Tiers; t++)
                {
                    UnitClass u = Units.All[l * Units.Tiers + t];
                    bool known = open && Save.HasUnit(u.Id);
                    // four 200-wide columns; x is each column's centre
                    float x = 112f + t * 205f;
                    Portrait(row, u.Model, known, topLeft, new Vector2(x, -58f), 150f, hang);
                    Label(row, known ? u.Name : "???", 28, UIFactory.Ink, TextAnchor.MiddleCenter, topLeft, new Vector2(x, -210f), new Vector2(200f, 36f), hang);
                    if (known)
                    {
                        // how it attacks, in the colour of its element
                        string how = Units.StyleName(u.Style) + (u.Element != Element.Physical ? "  " + Units.ElementName(u.Element) : string.Empty);
                        Label(row, how, 21, u.Element == Element.Physical ? UIFactory.Ink : Color.Lerp(Units.ElementColor(u.Element), UIFactory.Ink, 0.45f), TextAnchor.MiddleCenter, topLeft,
                            new Vector2(x, -248f), new Vector2(200f, 30f), hang);
                    }
                    string stats = known
                        ? "DMG " + u.Damage.ToString("0.#", CultureInfo.InvariantCulture) + "  RATE " + u.Rate.ToString("0.#", CultureInfo.InvariantCulture) + "\n" + u.Description
                        : t > 0 ? "CLASS UP TO MEET" : open ? "PICK IT AT THE START" : "UNLOCKS AT LEVEL " + Units.UnlockLevel(line);
                    Label(row, stats, 20, UIFactory.Gray, TextAnchor.UpperCenter, topLeft, new Vector2(x, known ? -282f : -250f), new Vector2(194f, 120f), hang);
                    if (t < Units.Tiers - 1)
                    {
                        Label(row, ">", 40, UIFactory.Gray, TextAnchor.MiddleCenter, topLeft, new Vector2(x + 102f, -108f), new Vector2(30f, 50f), hang);
                    }
                }
            }
        }

        // --- Bestiary ------------------------------------------------------------------

        void BestiaryRow(RectTransform list, string key, string model, bool known, string name, string description, string count, Color tint,
            string weakness = null)
        {
            RectTransform row = UIFactory.Item(list, key, new Vector2(840f, weakness != null ? 178f : 150f));
            Image back = UIFactory.Image(row, "Back", tint, GameAssets.RoundedSprite);
            UIFactory.Fill(back.rectTransform);
            var topLeft = new Vector2(0f, 1f);
            Portrait(row, model, known, new Vector2(0f, 0.5f), new Vector2(14f, 0f), 130f);
            Label(row, name, 34, UIFactory.Ink, TextAnchor.MiddleLeft, topLeft, new Vector2(160f, -14f), new Vector2(420f, 44f));
            Label(row, description, 25, UIFactory.Gray, TextAnchor.UpperLeft, topLeft, new Vector2(160f, -62f), new Vector2(660f, 80f));
            if (count != null) Label(row, count, 25, UIFactory.Blue, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(250f, 44f));
            if (!string.IsNullOrEmpty(weakness))
            {
                Label(row, weakness, 23, new Color(0.75f, 0.32f, 0.1f), TextAnchor.MiddleLeft, Vector2.zero, new Vector2(160f, 10f), new Vector2(660f, 32f));
            }
        }

        static string Weakness(ElementMask weak, ElementMask resist)
        {
            string text = string.Empty;
            if (weak != ElementMask.None) text += "WEAK: " + Combat.Names(weak);
            if (resist != ElementMask.None) text += (text.Length > 0 ? "     " : string.Empty) + "RESISTS: " + Combat.Names(resist);
            return text;
        }

        public void ShowBestiary()
        {
            RectTransform list = OpenList("ENEMIES  " + Save.EnemiesDiscovered + "/" + Enemies.All.Length, ShowBestiary);
            foreach (EnemyType t in Enemies.All)
            {
                bool known = Save.HasSeenEnemy(t.Id);
                BestiaryRow(list, t.Key, t.Model, known, known ? t.Name : "???",
                    known ? t.Description : "Not met yet. Appears from level " + t.Unlock + ".",
                    known ? "DEFEATED " + Fmt.Num(Save.KillsOf(t.Id)) : null, new Color(0f, 0f, 0f, 0.06f), known ? Weakness(t.Weak, t.Resist) : null);
            }
            RectTransform header = UIFactory.Item(list, "BossHeader", new Vector2(840f, 70f));
            Label(header, "BOSSES", 40, UIFactory.Red, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 60f));
            foreach (BossType b in Enemies.Bosses)
            {
                bool met = Save.bestLevel > b.Id + 1 || Save.statBosses > b.Id;
                BestiaryRow(list, b.Key, b.Model, met, met ? b.Name : "???",
                    met ? b.Description : "Guards level " + (b.Id + 1) + " and every " + Enemies.Bosses.Length + "th level after it.",
                    null, new Color(1f, 0.2f, 0.1f, 0.08f), met ? Weakness(b.Weak, b.Resist) : null);
            }
        }

        // --- Achievements ---------------------------------------------------------------

        public void ShowAchievements()
        {
            RectTransform list = OpenList("AWARDS", ShowAchievements);
            var topLeft = new Vector2(0f, 1f);
            var bottomLeft = new Vector2(0f, 0f);
            int readyNow = Rewards.ReadyCount(Save, Daily.Today);
            if (readyNow > 0)
            {
                RectTransform head = UIFactory.Item(list, "ClaimAll", new Vector2(840f, 130f));
                Button all = UIFactory.Button(head, "ClaimAll", "CLAIM ALL (" + readyNow + ")", UIFactory.Green, 46, ClaimAll, out _);
                UIFactory.Place((RectTransform)all.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 116f));
                all.gameObject.AddComponent<Pulse>();
            }
            foreach (Achievement a in Achievements.All)
            {
                long progress = Achievements.Progress(Save, a.Metric);
                bool claimed = Achievements.IsClaimed(Save, a);
                bool ready = Achievements.IsReady(Save, a);
                RectTransform row = UIFactory.Item(list, a.Id, new Vector2(840f, 150f));
                Image back = UIFactory.Image(row, "Back", ready ? new Color(1f, 0.85f, 0.3f, 0.3f) : new Color(0f, 0f, 0f, 0.06f), GameAssets.RoundedSprite);
                UIFactory.Fill(back.rectTransform);
                Label(row, a.Title, 34, UIFactory.Ink, TextAnchor.MiddleLeft, topLeft, new Vector2(24f, -12f), new Vector2(600f, 44f));
                Label(row, Achievements.Describe(a), 26, UIFactory.Gray, TextAnchor.MiddleLeft, topLeft, new Vector2(24f, -56f), new Vector2(600f, 36f));

                Image track = UIFactory.Image(row, "Track", new Color(0f, 0f, 0f, 0.15f), GameAssets.RoundedSprite);
                UIFactory.Place(track.rectTransform, bottomLeft, bottomLeft, new Vector2(24f, 22f), new Vector2(400f, 22f));
                Image fill = UIFactory.Image(track.rectTransform, "Fill", claimed ? UIFactory.Gray : UIFactory.Green, GameAssets.RoundedSprite);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress / (float)a.Target), 1f);
                fill.rectTransform.offsetMin = Vector2.zero;
                fill.rectTransform.offsetMax = Vector2.zero;
                Label(row, Fmt.Num(Math.Min(progress, a.Target)) + "/" + Fmt.Num(a.Target), 24, UIFactory.Gray, TextAnchor.MiddleLeft,
                    bottomLeft, new Vector2(436f, 18f), new Vector2(200f, 30f));

                string label = claimed ? "DONE" : a.Gems + " GEMS\n+" + Fmt.Num(a.Coins);
                Button claim = UIFactory.Button(row, "Claim", label, ready ? UIFactory.Green : UIFactory.Gray, 26, () => _game.ClaimAchievement(a), out _);
                UIFactory.Place((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(170f, 116f));
                claim.interactable = ready;
                if (ready) claim.gameObject.AddComponent<Pulse>();
            }
        }

        /// <summary>CLAIM ALL: collects every reward that is ready and shows what came in.</summary>
        public void ClaimAll()
        {
            RewardSummary s = _game.ClaimAllRewards();
            RectTransform body = Open("REWARDS", new Vector2(860f, 860f), null);
            var top = new Vector2(0.5f, 1f);
            if (!s.Any)
            {
                Label(body, "NOTHING TO CLAIM RIGHT NOW.\nPLAY A LEVEL AND COME BACK!", 38, UIFactory.Ink, TextAnchor.MiddleCenter, top, new Vector2(0f, -120f),
                    new Vector2(780f, 200f));
                Big(body, "OK", UIFactory.Blue, CloseAll, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 140f), out _);
                return;
            }

            // the totals, big
            float y = -70f;
            if (s.Coins > 0)
            {
                RectTransform coin = UIFactory.Coin(body, 84f);
                UIFactory.Place(coin, top, new Vector2(0.5f, 0.5f), new Vector2(-170f, y), new Vector2(84f, 84f));
                Label(body, "+" + Fmt.Coins(s.Coins), 66, UIFactory.GoldDark, TextAnchor.MiddleLeft, top, new Vector2(110f, y), new Vector2(460f, 90f),
                    new Vector2(0.5f, 0.5f));
                y -= 110f;
            }
            if (s.Gems > 0)
            {
                RectTransform gem = UIFactory.Gem(body, 80f);
                UIFactory.Place(gem, top, new Vector2(0.5f, 0.5f), new Vector2(-170f, y), new Vector2(80f, 80f));
                Label(body, "+" + s.Gems, 66, UIFactory.Purple, TextAnchor.MiddleLeft, top, new Vector2(110f, y), new Vector2(460f, 90f), new Vector2(0.5f, 0.5f));
                y -= 110f;
            }
            // where it came from
            Label(body, string.Join("\n", s.Lines), 34, UIFactory.Ink, TextAnchor.UpperCenter, top, new Vector2(0f, y - 30f), new Vector2(780f, 220f));
            Big(body, "GREAT!", UIFactory.Green, CloseAll, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(460f, 140f), out _);
        }

        // --- Stats -----------------------------------------------------------------------

        public void ShowStats()
        {
            RectTransform body = Open("STATS", new Vector2(820f, 1240f), ShowStats);
            SaveData s = Save;
            (string, string)[] lines =
            {
                ("LEVEL", s.level.ToString(CultureInfo.InvariantCulture)),
                ("LEVELS CLEARED", Fmt.Num(s.statWins)),
                ("DEFEATS", Fmt.Num(s.statLosses)),
                ("BOSSES DEFEATED", Fmt.Num(s.statBosses)),
                ("ENEMIES DEFEATED", Fmt.Num(s.statKills)),
                ("LARGEST ARMY", Fmt.Num(s.statMaxArmy)),
                ("BEST COMBO", Fmt.Num(s.statBestCombo)),
                ("GATES PASSED", Fmt.Num(s.statGates)),
                ("PERFECT GATES", Fmt.Num(s.statPerfect)),
                ("CLASS UPS", Fmt.Num(s.statClassUps)),
                ("GOLD GOBLINS CAUGHT", Fmt.Num(s.statGoldGoblins)),
                ("CRATES OPENED", Fmt.Num(s.statChests)),
                ("COINS EARNED", Fmt.Num(s.statCoinsEarned)),
                ("STARS", s.TotalStars.ToString(CultureInfo.InvariantCulture)),
                ("ENEMIES DISCOVERED", s.EnemiesDiscovered + "/" + Enemies.All.Length),
                ("LOGIN STREAK", s.loginStreak + " DAYS"),
                ("PLAY TIME", Fmt.Duration(s.statPlaySeconds)),
            };
            for (int i = 0; i < lines.Length; i++)
            {
                float y = -14f - i * 62f;
                Label(body, lines[i].Item1, 34, UIFactory.Gray, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(480f, 56f));
                Label(body, lines[i].Item2, 36, UIFactory.Ink, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-40f, y), new Vector2(300f, 56f));
            }
        }

        // --- Daily login ---------------------------------------------------------------

        public void ShowDaily()
        {
            RectTransform body = Open("DAILY BONUS", new Vector2(900f, 900f), ShowDaily);
            int today = Daily.Today;
            int day = Daily.StreakDay(Save);
            bool ready = Daily.LoginRewardReady(Save, today);
            var top = new Vector2(0.5f, 1f);
            Label(body, "LOGIN STREAK: " + Mathf.Max(1, Save.loginStreak) + (Save.loginStreak == 1 ? " DAY" : " DAYS"), 40, UIFactory.Ink,
                TextAnchor.MiddleCenter, top, new Vector2(0f, -20f), new Vector2(820f, 60f));
            for (int d = 1; d <= 7; d++)
            {
                Prize prize = Daily.LoginPrize(d, Save.bestLevel);
                bool past = d < day || (d == day && !ready);
                bool current = d == day && ready;
                Color color = current ? UIFactory.Gold : past ? new Color(0.75f, 0.9f, 0.75f) : new Color(0.9f, 0.92f, 0.96f);
                Image cell = UIFactory.Image(body, "Day" + d, color, GameAssets.RoundedSprite);
                UIFactory.Place(cell.rectTransform, top, top, new Vector2((d - 4) * 120f, -190f), new Vector2(110f, 190f));
                if (current) cell.gameObject.AddComponent<Pulse>();
                Label(cell.rectTransform, "DAY " + d, 24, UIFactory.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(110f, 30f));
                string amount = prize.Kind == PrizeKind.Gems ? prize.Amount + "\nGEMS" : Fmt.Num(prize.Amount) + "\nCOINS";
                Label(cell.rectTransform, past ? "OK" : amount, 24, prize.Kind == PrizeKind.Gems ? UIFactory.Purple : UIFactory.GoldDark, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(108f, 80f));
            }
            Prize todayPrize = Daily.LoginPrize(day, Save.bestLevel);
            Label(body, ready ? "TODAY: " + todayPrize.Label : "COME BACK TOMORROW FOR DAY " + (day % 7 + 1) + "!", 38, UIFactory.Ink,
                TextAnchor.MiddleCenter, top, new Vector2(0f, -360f), new Vector2(820f, 60f));
            if (ready)
            {
                Big(body, "CLAIM", UIFactory.Green, () => _game.ClaimLogin(), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(520f, 150f), out _).gameObject.AddComponent<Pulse>();
            }
            else
            {
                Big(body, "OK", UIFactory.Blue, CloseAll, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 140f), out _);
            }
        }

        // --- Lucky draw --------------------------------------------------------------------

        public void ShowLucky()
        {
            RectTransform body = Open("LUCKY DRAW", new Vector2(900f, 1020f), null);
            int today = Daily.Today;
            Prize[] prizes = Daily.LuckyPrizes(Save.bestLevel);
            int result = Daily.LuckyIndex(today, prizes.Length);
            bool ready = Daily.LuckyReady(Save, today);
            var top = new Vector2(0.5f, 1f);
            var cells = new Image[prizes.Length];
            // tiles go round the edge of a 4 x 2 grid so the highlight circles
            int[] order = { 0, 1, 2, 3, 7, 6, 5, 4 };
            for (int i = 0; i < prizes.Length; i++)
            {
                int slot = Array.IndexOf(order, i);
                int col = slot % 4, row = slot / 4;
                Image cell = UIFactory.Image(body, "Prize" + i, new Color(0.9f, 0.92f, 0.96f), GameAssets.RoundedSprite);
                UIFactory.Place(cell.rectTransform, top, top, new Vector2((col - 1.5f) * 200f, -40f - row * 220f), new Vector2(186f, 200f));
                Prize p = prizes[i];
                string text;
                switch (p.Kind)
                {
                    case PrizeKind.Gems: text = p.Amount + "\nGEMS"; break;
                    case PrizeKind.FreeUpgrade: text = "FREE\nUPGRADE"; break;
                    case PrizeKind.DoubleCoins: text = "x2 COINS\nNEXT RUN"; break;
                    default: text = Fmt.Num(p.Amount) + "\nCOINS"; break;
                }
                Label(cell.rectTransform, text, 32, p.Kind == PrizeKind.Gems ? UIFactory.Purple : UIFactory.Ink, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 160f));
                cells[i] = cell;
            }
            Text status = Label(body, ready ? "ONE FREE DRAW EVERY DAY" : "TODAY'S PRIZE: " + prizes[result].Label + "\nNEXT DRAW TOMORROW",
                36, UIFactory.Ink, TextAnchor.MiddleCenter, top, new Vector2(0f, -520f), new Vector2(820f, 110f));
            if (!ready) cells[result].color = UIFactory.Gold;

            Button spin = null;
            float clock = -1f;
            int lastStep = -1;
            const float duration = 2.6f;
            int steps = 3 * prizes.Length + Array.IndexOf(order, result);
            if (ready)
            {
                spin = Big(body, "SPIN!", UIFactory.Gold, () =>
                {
                    if (clock >= 0f) return;
                    clock = 0f;
                    spin.interactable = false;
                }, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(520f, 150f), out _);
                spin.gameObject.AddComponent<Pulse>();
            }
            else
            {
                Big(body, "OK", UIFactory.Blue, CloseAll, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 140f), out _);
            }

            _tick = dt =>
            {
                if (clock < 0f || clock > duration + 1f) return;
                clock += dt;
                // ease out: the highlight slows down and stops on today's prize
                float u = Mathf.Clamp01(clock / duration);
                int step = Mathf.Min(steps, Mathf.FloorToInt((1f - (1f - u) * (1f - u) * (1f - u)) * steps));
                if (step != lastStep)
                {
                    lastStep = step;
                    for (int i = 0; i < cells.Length; i++) cells[i].color = new Color(0.9f, 0.92f, 0.96f);
                    cells[order[step % order.Length]].color = UIFactory.Gold;
                    _game.Context.Audio.Play(Sfx.Click, 0.5f, 0.02f, 0f);
                }
                if (clock >= duration)
                {
                    clock = duration + 2f;
                    _game.ClaimLucky();
                    status.text = "YOU WON: " + prizes[result].Label + "!";
                    _game.Context.Audio.Play(Sfx.Chest);
                    Haptics.Tap(25);
                    _game.UI.RefreshSave(Save);
                }
            };
        }

        // --- Daily challenge ----------------------------------------------------------------

        public void ShowChallenge()
        {
            RectTransform body = Open("DAILY CHALLENGE", new Vector2(900f, 860f), null);
            bool ready = Daily.ChallengeReady(Save, Daily.Today);
            var top = new Vector2(0.5f, 1f);
            Label(body, "LEVEL " + Daily.ChallengeLevel(Save) + " WITH DOUBLE HORDES", 42, UIFactory.Red, TextAnchor.MiddleCenter, top, new Vector2(0f, -30f), new Vector2(820f, 60f));
            Label(body, "A new road every day, a few levels ahead of you.\nWin for DOUBLE COINS and " + Daily.ChallengeGems + " GEMS.\nYou keep your main level either way.",
                34, UIFactory.Ink, TextAnchor.UpperCenter, top, new Vector2(0f, -120f), new Vector2(800f, 220f));
            if (ready)
            {
                Big(body, "FIGHT!", UIFactory.Red, () =>
                {
                    CloseAll();
                    _game.PlayChallenge();
                }, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(520f, 150f), out _).gameObject.AddComponent<Pulse>();
            }
            else
            {
                Label(body, "DONE FOR TODAY - COME BACK TOMORROW!", 36, UIFactory.Green, TextAnchor.MiddleCenter, top, new Vector2(0f, -380f), new Vector2(800f, 60f));
                Big(body, "OK", UIFactory.Blue, CloseAll, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 140f), out _);
            }
        }

        // --- Endless ------------------------------------------------------------------------

        public void ShowEndless()
        {
            RectTransform body = Open("ENDLESS", new Vector2(900f, 960f), null);
            var top = new Vector2(0.5f, 1f);
            Label(body, "HOW FAR CAN YOUR ARMY GO?", 44, UIFactory.Purple, TextAnchor.MiddleCenter, top, new Vector2(0f, -30f), new Vector2(820f, 60f));
            Label(body, "One run, stage after stage, starting at level " + Mathf.Max(1, Save.bestLevel) + ".\n" +
                        "Your army, power gates and power-ups carry on to the next stage.\n" +
                        "Every boss you beat makes the enemies 1% stronger.",
                34, UIFactory.Ink, TextAnchor.UpperCenter, top, new Vector2(0f, -120f), new Vector2(800f, 260f));
            string best = Save.endlessBest > 0 ? "BEST: " + Save.endlessBest + (Save.endlessBest == 1 ? " BOSS" : " BOSSES") : "NO RECORD YET";
            Label(body, best, 44, UIFactory.GoldDark, TextAnchor.MiddleCenter, top, new Vector2(0f, -430f), new Vector2(800f, 64f));
            Big(body, "START", new Color32(120, 70, 200, 255), () =>
            {
                CloseAll();
                _game.PlayEndless();
            }, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(520f, 150f), out _).gameObject.AddComponent<Pulse>();
        }

        // --- Welcome back -------------------------------------------------------------------

        public void ShowOffline(int coins)
        {
            RectTransform body = Open("WELCOME BACK!", new Vector2(860f, 700f), null);
            var top = new Vector2(0.5f, 1f);
            Label(body, "YOUR ARMY COLLECTED WHILE YOU WERE AWAY", 34, UIFactory.Ink, TextAnchor.MiddleCenter, top, new Vector2(0f, -30f), new Vector2(800f, 60f));
            RectTransform coin = UIFactory.Coin(body, 90f);
            UIFactory.Place(coin, top, new Vector2(0.5f, 0.5f), new Vector2(-170f, -180f), new Vector2(90f, 90f));
            Label(body, "+" + Fmt.Coins(coins), 90, UIFactory.Gold, TextAnchor.MiddleLeft, top, new Vector2(110f, -180f), new Vector2(460f, 120f));
            Big(body, "OK", UIFactory.Blue, () =>
            {
                CloseAll();
                if (Daily.LoginRewardReady(Save, Daily.Today)) ShowDaily();
            }, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 140f), out _);
        }
    }
}
