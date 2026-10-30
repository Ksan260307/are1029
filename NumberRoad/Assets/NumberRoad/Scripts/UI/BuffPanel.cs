using NumberRoad.Core;
using NumberRoad.Gameplay;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>Between waves: three power-up cards to pick from, plus one free re-draw per run.</summary>
    sealed class BuffPanel
    {
        const int Cards = 3;
        const float CardHeight = 320f;
        const float CardGap = 26f;
        /// <summary>Seconds before the cards take taps, so a finger that was steering cannot pick one by accident.</summary>
        const float TapDelay = 0.35f;

        public readonly GameObject Root;
        readonly CanvasGroup _group;
        readonly Text _title;
        readonly RectTransform[] _cards = new RectTransform[Cards];
        readonly Image[] _stripes = new Image[Cards];
        readonly Image[] _tags = new Image[Cards];
        readonly Text[] _kinds = new Text[Cards];
        readonly Text[] _rarities = new Text[Cards];
        readonly Text[] _names = new Text[Cards];
        readonly Text[] _texts = new Text[Cards];
        readonly Button _reroll;
        readonly Text _rerollText;
        Buff[] _shown;
        float _clock;

        public BuffPanel(RectTransform parent, GameRoot game)
        {
            Image dim = UIFactory.Image(parent, "BuffChoice", new Color(0.03f, 0.05f, 0.12f, 0.72f), null, true);
            UIFactory.Fill(dim.rectTransform);
            // reaches past the safe area so the notch strip is dimmed too
            dim.rectTransform.offsetMin = new Vector2(-300f, -300f);
            dim.rectTransform.offsetMax = new Vector2(300f, 300f);
            Root = dim.gameObject;

            float height = 190f + Cards * (CardHeight + CardGap) + 140f;
            RectTransform box = UIFactory.Place(UIFactory.Rect(dim.rectTransform, "Box"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, height));
            box.gameObject.AddComponent<PopIn>();
            _group = box.gameObject.AddComponent<CanvasGroup>();

            _title = UIFactory.Text(box, "Title", "WAVE CLEAR!", 84, UIFactory.Gold, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 4f);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1000f, 110f));
            Text sub = UIFactory.Text(box, "Subtitle", "PICK ONE POWER-UP", 42, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1000f, 60f));

            for (int i = 0; i < Cards; i++)
            {
                int index = i;
                Image face = UIFactory.Image(box, "Card" + i, UIFactory.Card, GameAssets.RoundedSprite, true);
                RectTransform card = UIFactory.Place(face.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -190f - i * (CardHeight + CardGap)), new Vector2(980f, CardHeight));
                _cards[i] = card;
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.onClick.AddListener(() =>
                {
                    UIFactory.ClickSound?.Invoke();
                    game.ChooseBuff(index);
                });
                face.gameObject.AddComponent<PressScale>();

                _stripes[i] = UIFactory.Image(card, "Stripe", Color.white, GameAssets.RoundedSprite);
                UIFactory.Place(_stripes[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(26f, CardHeight - 40f));

                _tags[i] = UIFactory.Image(card, "Kind", Color.white, GameAssets.RoundedSprite);
                UIFactory.Place(_tags[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -22f), new Vector2(230f, 58f));
                _kinds[i] = UIFactory.Text(_tags[i].rectTransform, "Text", string.Empty, 32, Color.white, TextAnchor.MiddleCenter, UIFactory.OutlineDark, 2f);
                UIFactory.Fill(_kinds[i].rectTransform);

                _rarities[i] = UIFactory.Text(card, "Rarity", string.Empty, 34, UIFactory.Gray, TextAnchor.MiddleRight);
                UIFactory.Place(_rarities[i].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -22f), new Vector2(600f, 58f));

                _names[i] = UIFactory.Text(card, "Name", string.Empty, 62, UIFactory.Ink, TextAnchor.MiddleLeft);
                UIFactory.Place(_names[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -92f), new Vector2(890f, 84f));

                _texts[i] = UIFactory.Text(card, "Text", string.Empty, 38, new Color(0.24f, 0.28f, 0.38f), TextAnchor.UpperLeft);
                UIFactory.Place(_texts[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -186f), new Vector2(890f, 120f));
                _texts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _texts[i].lineSpacing = 1.05f;
            }

            _reroll = UIFactory.Button(box, "Reroll", "NEW CARDS", UIFactory.Purple, 44, game.RerollBuffs, out _rerollText);
            UIFactory.Place((RectTransform)_reroll.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(480f, 120f));
            Root.SetActive(false);
        }

        public void Tick(LevelSession session, float dt)
        {
            bool choosing = session != null && session.ChoosingBuff;
            if (Root.activeSelf != choosing)
            {
                Root.SetActive(choosing);
                _shown = null;
            }
            if (!choosing) return;
            if (_shown != session.Options) Show(session);

            _clock += dt;
            _group.blocksRaycasts = _clock >= TapDelay;
            // the cards slide in one after another
            for (int i = 0; i < Cards; i++)
            {
                float u = Mathf.Clamp01((_clock - i * 0.07f) / 0.25f);
                float ease = 1f - (1f - u) * (1f - u);
                _cards[i].localScale = new Vector3(Mathf.Lerp(0.85f, 1f, ease), Mathf.Lerp(0.85f, 1f, ease), 1f);
                _cards[i].anchoredPosition = new Vector2(Mathf.Lerp(-260f, 0f, ease), _cards[i].anchoredPosition.y);
            }
        }

        void Show(LevelSession session)
        {
            _shown = session.Options;
            _clock = 0f;
            _title.text = "WAVE " + (session.Wave + 1) + " / " + session.Waves + " CLEAR!";
            for (int i = 0; i < Cards; i++)
            {
                bool has = i < _shown.Length;
                _cards[i].gameObject.SetActive(has);
                if (!has) continue;
                Buff b = _shown[i];
                Color kind = Buffs.KindColor(b.Kind);
                _stripes[i].color = kind;
                _tags[i].color = kind;
                _kinds[i].text = Buffs.KindName(b.Kind);
                int owned = session.Mods.CountOf(b);
                _rarities[i].text = RarityName(b.Rarity) + (owned > 0 ? "  -  OWNED x" + owned : string.Empty);
                _rarities[i].color = RarityColor(b.Rarity);
                _names[i].text = b.Name;
                _texts[i].text = b.Description.ToUpperInvariant();
            }
            _reroll.gameObject.SetActive(session.Rerolls > 0);
            _rerollText.text = "NEW CARDS (" + session.Rerolls + ")";
        }

        static string RarityName(BuffRarity rarity)
        {
            switch (rarity)
            {
                case BuffRarity.Epic: return "EPIC";
                case BuffRarity.Rare: return "RARE";
                default: return "COMMON";
            }
        }

        static Color RarityColor(BuffRarity rarity)
        {
            switch (rarity)
            {
                case BuffRarity.Epic: return UIFactory.Purple;
                case BuffRarity.Rare: return UIFactory.Blue;
                default: return UIFactory.Gray;
            }
        }
    }
}
