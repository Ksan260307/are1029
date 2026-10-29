using System.Collections.Generic;
using System.Globalization;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>
    /// Victory / defeat screen: stars, the coin breakdown, gems, the victory chest, how close a defeat was,
    /// and NEXT / RETRY / REVIVE / HOME.
    /// </summary>
    sealed class ResultView
    {
        const int Rows = 9;

        public readonly GameObject Root;
        readonly GameRoot _game;
        readonly Text _title;
        readonly Text _subtitle;
        readonly Image[] _stars = new Image[3];
        readonly Text _detail;
        readonly Text[] _rowLabels = new Text[Rows];
        readonly Text[] _rowValues = new Text[Rows];
        readonly Text _total;
        readonly GameObject _gemsRow;
        readonly RectTransform _gemsRect;
        readonly RectTransform _coinIcon;
        readonly Text _gems;
        readonly Button _chest;
        readonly RectTransform _chestRect;
        readonly Text _chestText;
        readonly Text _awards;
        readonly Button _next;
        readonly Button _home;
        readonly Button _retry;
        readonly Button _revive;
        readonly Text _reviveText;
        RunResult _result;
        float _clock;
        float _chestClock = -1f;
        int _starCount;

        public ResultView(RectTransform parent, GameRoot game)
        {
            _game = game;
            Image dim = UIFactory.Image(parent, "Result", UIFactory.Shade, null, true);
            UIFactory.Fill(dim.rectTransform);
            Root = dim.gameObject;

            Image panel = UIFactory.Image(dim.rectTransform, "Panel", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 1360f));
            panel.gameObject.AddComponent<PopIn>();
            RectTransform p = panel.rectTransform;

            _title = UIFactory.Text(p, "Title", "VICTORY!", 108, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 6f);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(820f, 136f));
            _subtitle = UIFactory.Text(p, "Subtitle", string.Empty, 44, UIFactory.Ink);
            UIFactory.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(820f, 60f));

            for (int i = 0; i < 3; i++)
            {
                _stars[i] = UIFactory.Star(p, 116f, false);
                UIFactory.Place(_stars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 140f, -290f + (i == 1 ? 14f : 0f)), new Vector2(116f, 116f));
            }
            _detail = UIFactory.Text(p, "Detail", string.Empty, 40, UIFactory.Red);
            UIFactory.Place(_detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(800f, 110f));
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;

            for (int i = 0; i < Rows; i++)
            {
                float y = -380f - i * 44f;
                _rowLabels[i] = UIFactory.Text(p, "RowLabel" + i, string.Empty, 32, UIFactory.Gray, TextAnchor.MiddleLeft);
                UIFactory.Place(_rowLabels[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-120f, y), new Vector2(480f, 44f));
                _rowValues[i] = UIFactory.Text(p, "RowValue" + i, string.Empty, 32, UIFactory.Ink, TextAnchor.MiddleRight);
                UIFactory.Place(_rowValues[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(200f, y), new Vector2(300f, 44f));
            }

            RectTransform coin = UIFactory.Coin(p, 76f);
            _coinIcon = coin;
            UIFactory.Place(coin, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-190f, -800f), new Vector2(76f, 76f));
            _total = UIFactory.Text(p, "Total", "+0", 86, UIFactory.Gold, TextAnchor.MiddleLeft, UIFactory.OutlineDark, 4f);
            UIFactory.Place(_total.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-136f, -800f), new Vector2(520f, 104f));

            RectTransform gemsRow = UIFactory.Place(UIFactory.Rect(p, "GemsRow"), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -880f), new Vector2(400f, 60f));
            _gemsRow = gemsRow.gameObject;
            _gemsRect = gemsRow;
            RectTransform gem = UIFactory.Gem(gemsRow, 54f);
            UIFactory.Place(gem, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70f, 0f), new Vector2(54f, 54f));
            _gems = UIFactory.Text(gemsRow, "Value", "+0", 50, UIFactory.Cyan, TextAnchor.MiddleLeft, UIFactory.OutlineDark, 3f);
            UIFactory.Place(_gems.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-34f, 0f), new Vector2(300f, 60f));

            _chest = UIFactory.Button(p, "Chest", "TAP TO OPEN", GameContext.WoodColor, 32, OnChest, out _chestText);
            _chestRect = (RectTransform)_chest.transform;
            UIFactory.Place(_chestRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -990f), new Vector2(360f, 140f));
            Image band = UIFactory.Image(_chest.targetGraphic.transform, "Band", UIFactory.Gold, GameAssets.RoundedSprite);
            UIFactory.Place(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(340f, 14f));

            _awards = UIFactory.Text(p, "Awards", string.Empty, 32, UIFactory.Purple);
            UIFactory.Place(_awards.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1084f), new Vector2(800f, 44f));

            _next = UIFactory.Button(p, "Next", "NEXT", UIFactory.Blue, 64, game.Next, out _);
            UIFactory.Place((RectTransform)_next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-90f, 40f), new Vector2(480f, 140f));
            _home = UIFactory.Button(p, "Home", "HOME", UIFactory.Gray, 46, game.Home, out _);
            UIFactory.Place((RectTransform)_home.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(270f, 40f), new Vector2(210f, 140f));
            _retry = UIFactory.Button(p, "Retry", "RETRY", UIFactory.Gold, 64, game.Retry, out _);
            UIFactory.Place((RectTransform)_retry.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-90f, 40f), new Vector2(480f, 140f));
            _revive = UIFactory.Button(p, "Revive", "REVIVE", UIFactory.Purple, 44, () => game.Revive(), out _reviveText);
            UIFactory.Place((RectTransform)_revive.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(700f, 120f));
        }

        public void Show(RunResult result, SaveData save)
        {
            _result = result;
            Root.SetActive(true);
            _clock = 0f;
            _chestClock = -1f;
            _starCount = 0;
            bool won = result.Won;

            _title.text = won ? "VICTORY!" : "DEFEAT";
            _title.color = won ? UIFactory.Gold : UIFactory.Red;
            string what = result.Daily ? "DAILY CHALLENGE" : "LEVEL " + result.Level;
            _subtitle.text = won ? what + " CLEAR" : what;

            foreach (Image s in _stars)
            {
                s.gameObject.SetActive(won);
                s.color = new Color(0f, 0f, 0f, 0.2f);
                s.rectTransform.localScale = Vector3.one;
            }
            _detail.gameObject.SetActive(!won);
            if (!won)
            {
                if (result.BossHpLeft > 0f)
                {
                    int left = Mathf.Max(1, Mathf.RoundToInt(result.BossHpLeft * 100f));
                    _detail.text = result.BossName + " HAD " + left + "% HP LEFT" + (left <= 25 ? "\nSO CLOSE!" : string.Empty);
                }
                else
                {
                    _detail.text = "REACHED " + Mathf.RoundToInt(result.Progress * 100f) + "% OF THE ROAD";
                }
            }

            var rows = new List<(string, string)>();
            CultureInfo c = CultureInfo.InvariantCulture;
            RunStats st = result.Stats;
            rows.Add(("ENEMIES  (" + Fmt.Num(st.Kills) + ")", "+" + Fmt.Num(Mathf.RoundToInt(st.KillCoins))));
            if (st.ComboCoins >= 1f) rows.Add(("COMBO BONUS  (BEST " + st.BestCombo + ")", "+" + Fmt.Num(Mathf.RoundToInt(st.ComboCoins))));
            if (st.BonusCoins >= 1f) rows.Add(("PERFECT / CHAIN / CRATES", "+" + Fmt.Num(Mathf.RoundToInt(st.BonusCoins))));
            if (st.VictoryCoins > 0) rows.Add(("VICTORY", "+" + Fmt.Num(st.VictoryCoins)));
            if (st.BossCoins > 0) rows.Add(("BOSS", "+" + Fmt.Num(st.BossCoins)));
            if (result.IncomeMultiplier > 1.001f) rows.Add(("COIN UPGRADE", "x" + result.IncomeMultiplier.ToString("0.0", c)));
            if (result.GoldRushMultiplier > 1.001f) rows.Add(("GOLD RUSH", "x" + result.GoldRushMultiplier.ToString("0.0", c)));
            if (result.DailyMultiplier > 1.001f) rows.Add(("DAILY CHALLENGE", "x" + result.DailyMultiplier.ToString("0", c)));
            if (result.CouponMultiplier > 1.001f) rows.Add(("x2 COUPON", "x2"));
            for (int i = 0; i < Rows; i++)
            {
                bool on = i < rows.Count;
                _rowLabels[i].text = on ? rows[i].Item1 : string.Empty;
                _rowValues[i].text = on ? rows[i].Item2 : string.Empty;
            }
            _total.text = "+0";
            _gemsRow.SetActive(result.Gems > 0);
            _gems.text = "+" + result.Gems;

            _chest.gameObject.SetActive(won);
            _chest.interactable = true;
            _chestText.text = "TAP TO OPEN";
            _chestText.fontSize = 32;
            _chestRect.localRotation = Quaternion.identity;

            int ready = Achievements.ReadyCount(save);
            _awards.text = ready > 0 ? ready + (ready == 1 ? " AWARD" : " AWARDS") + " READY TO CLAIM AT HOME!" : string.Empty;
            // the total, gems, chest and notice follow right under however many breakdown rows there are
            float bottom = -380f - Mathf.Max(1, rows.Count) * 44f;
            _coinIcon.anchoredPosition = new Vector2(-190f, bottom - 50f);
            _total.rectTransform.anchoredPosition = new Vector2(-136f, bottom - 50f);
            _gemsRect.anchoredPosition = new Vector2(0f, bottom - 130f);
            float below = result.Gems > 0 ? bottom - 200f : bottom - 130f;
            _chestRect.anchoredPosition = new Vector2(0f, below - 70f);
            _awards.rectTransform.anchoredPosition = new Vector2(0f, won ? below - 180f : below);

            _next.gameObject.SetActive(won);
            _retry.gameObject.SetActive(!won);
            bool revive = !won && result.CanRevive;
            _revive.gameObject.SetActive(revive);
            if (revive)
            {
                bool affordable = save.gems >= Balance.ReviveGems;
                _reviveText.text = "REVIVE HERE  -  " + Balance.ReviveGems + " GEMS" + (affordable ? string.Empty : "  (HAVE " + save.gems + ")");
                _revive.interactable = affordable;
                UIFactory.SetButtonColor(_revive, affordable ? UIFactory.Purple : UIFactory.Gray);
            }
        }

        void OnChest()
        {
            if (_result == null || _result.ChestOpened || _chestClock >= 0f) return;
            _chestClock = 0f;
            _chest.interactable = false;
        }

        public void Tick(float dt)
        {
            if (_result == null) return;
            _clock += dt;
            float t = Mathf.Clamp01((_clock - 0.25f) / 0.9f);
            _total.text = "+" + Fmt.Coins(Mathf.RoundToInt(_result.TotalCoins * (1f - (1f - t) * (1f - t))));

            if (_result.Won)
            {
                // stars pop in one after another
                int shown = Mathf.Clamp(Mathf.FloorToInt((_clock - 0.35f) / 0.3f) + 1, 0, _result.Stars);
                while (_starCount < shown)
                {
                    _stars[_starCount].color = UIFactory.Gold;
                    _stars[_starCount].rectTransform.localScale = Vector3.one * 1.5f;
                    _game.Context.Audio.PlayPitched(Sfx.Coin, 0.8f, 1f + _starCount * 0.2f, 0f);
                    _starCount++;
                }
                foreach (Image s in _stars)
                {
                    Transform tr = s.rectTransform;
                    tr.localScale = Vector3.Lerp(tr.localScale, Vector3.one, 1f - Mathf.Exp(-12f * dt));
                }
            }

            if (_chestClock >= 0f && !_result.ChestOpened)
            {
                _chestClock += dt;
                _chestRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_chestClock * 50f) * 8f * (1f - _chestClock / 0.45f));
                if (_chestClock >= 0.45f)
                {
                    _chestRect.localRotation = Quaternion.identity;
                    _game.OpenChest();
                    _chestText.text = _result.ChestGems > 0 ? "+" + _result.ChestGems + " GEMS!" : "+" + Fmt.Coins(_result.ChestCoins) + " COINS!";
                    _chestText.fontSize = 42;
                }
            }
            else if (_result.Won && !_result.ChestOpened)
            {
                _chestRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_clock * 6f) * 3f);
            }
        }
    }
}
