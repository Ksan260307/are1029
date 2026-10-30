using System;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>Owns the canvases (world labels, HUD, screens, panels) and switches between screens.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public LabelLayer Labels { get; private set; }

        /// <summary>The HUD's DASH button is being held.</summary>
        public bool DashHeld => _hud != null && _hud.Root.activeSelf && _hud.DashHeld;

        GameRoot _game;
        TitleView _title;
        HudView _hud;
        PauseView _pause;
        ResultView _result;
        Panels _panels;
        bool _dailyOffered;

        public void Init(GameRoot game, Func<Camera> camera)
        {
            _game = game;
            EnsureEventSystem();
            Canvas labels = UIFactory.CreateCanvas("Labels", transform, 0, false);
            Labels = new LabelLayer(UIFactory.Fill(UIFactory.Rect(labels.transform, "Root")), camera);

            Canvas hud = UIFactory.CreateCanvas("HUD", transform, 10, true);
            Canvas screens = UIFactory.CreateCanvas("Screens", transform, 20, true);
            Canvas modal = UIFactory.CreateCanvas("Panels", transform, 30, true);
            RectTransform full = UIFactory.Fill(UIFactory.Rect(screens.transform, "Full"));
            _panels = new Panels(UIFactory.Fill(UIFactory.Rect(modal.transform, "Root")), game);
            _hud = new HudView(SafeRoot(hud.transform), game);
            _title = new TitleView(SafeRoot(screens.transform), game, _panels);
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
            _panels.CloseAll();
        }

        public void ShowTitle(SaveData save)
        {
            HideAll();
            Labels.OnlySquadBadge = true;
            _title.Show(save);
            if (_game.OfflineCoins > 0)
            {
                _panels.ShowOffline(_game.OfflineCoins);
                _game.ClearOfflineCoins();
            }
            else if (!_dailyOffered && Daily.LoginRewardReady(save, Daily.Today))
            {
                // offered once per launch; afterwards the DAILY button carries a badge
                _dailyOffered = true;
                _panels.ShowDaily();
            }
        }

        public void RefreshSave(SaveData save)
        {
            if (_title.Root.activeSelf) _title.Refresh(save);
            if (_pause.Root.activeSelf) _pause.Refresh(save, null);
            _panels.Refresh();
        }

        public void ShowHud(LevelSession session, SaveData save)
        {
            HideAll();
            Labels.OnlySquadBadge = false;
            _hud.Show(session, save);
        }

        public void ShowPause(bool show, SaveData save, LevelSession session)
        {
            _pause.Root.SetActive(show);
            if (show) _pause.Refresh(save, session);
        }

        public void ShowResult(RunResult result, SaveData save)
        {
            HideAll();
            _result.Show(result, save);
        }

        /// <summary>Back button on the title screen: closes the open panel, if any.</summary>
        public void CloseTopPanel() => _panels.CloseTop();

        /// <summary>Opens the settings panel (after the language changed and the screens were rebuilt).</summary>
        public void OpenSettings() => _panels.ShowSettings();

        public void LateTick(float unscaledDt)
        {
            Labels.LateTick(Time.deltaTime);
            if (_hud.Root.activeSelf) _hud.Tick(unscaledDt);
            if (_result.Root.activeSelf) _result.Tick(unscaledDt);
            if (_title.Root.activeSelf) _title.Tick(unscaledDt);
            _panels.Tick(unscaledDt);
        }

        internal static string Number(long value) => Fmt.Coins(value);

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

        internal static RectTransform GemPill(Transform parent, string name, out Text text)
        {
            Image pill = UIFactory.Image(parent, name, new Color(0f, 0f, 0f, 0.4f), GameAssets.RoundedSprite);
            RectTransform gem = UIFactory.Gem(pill.rectTransform, 58f);
            UIFactory.Place(gem, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(58f, 58f));
            text = UIFactory.Text(pill.rectTransform, "Value", "0", 42, Color.white, TextAnchor.MiddleRight, UIFactory.OutlineDark, 2f);
            UIFactory.Fill(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(80f, 0f);
            text.rectTransform.offsetMax = new Vector2(-20f, 0f);
            return pill.rectTransform;
        }

        internal static void Toggle(Button button, Text text, string name, bool on)
        {
            text.text = Loc.F(on ? "{0}\nON" : "{0}\nOFF", Loc.T(name));
            UIFactory.SetButtonColor(button, on ? UIFactory.Blue : UIFactory.Gray);
        }
    }

    /// <summary>Gentle breathing scale, used on the PLAY button and ready buttons.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.035f;
        public float Speed = 3.2f;

        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * Speed) * Amount;
            transform.localScale = new Vector3(s, s, 1f);
        }

        void OnDisable() => transform.localScale = Vector3.one;
    }

    /// <summary>Pause menu with this run's numbers so far.</summary>
    sealed class PauseView
    {
        public readonly GameObject Root;
        readonly Button _sound;
        readonly Button _music;
        readonly Text _soundText;
        readonly Text _musicText;
        readonly Text _stats;
        readonly Text _buffs;

        public PauseView(RectTransform parent, GameRoot game)
        {
            Image dim = UIFactory.Image(parent, "Pause", UIFactory.Shade, null, true);
            UIFactory.Fill(dim.rectTransform);
            Root = dim.gameObject;

            Image panel = UIFactory.Image(dim.rectTransform, "Panel", UIFactory.Card, GameAssets.RoundedSprite);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 1340f));
            panel.gameObject.AddComponent<PopIn>();
            RectTransform p = panel.rectTransform;

            Text title = UIFactory.Text(p, "Title", Loc.T("PAUSED"), 92, UIFactory.Ink);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(700f, 120f));

            _stats = UIFactory.Text(p, "Stats", string.Empty, 38, UIFactory.Ink, TextAnchor.UpperCenter);
            UIFactory.Place(_stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(740f, 210f));
            _stats.lineSpacing = 1.15f;

            // the power-ups picked so far this run
            _buffs = UIFactory.Text(p, "Buffs", string.Empty, 30, new Color(0.3f, 0.34f, 0.45f), TextAnchor.UpperCenter);
            UIFactory.Place(_buffs.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(740f, 200f));
            _buffs.horizontalOverflow = HorizontalWrapMode.Wrap;
            _buffs.verticalOverflow = VerticalWrapMode.Truncate;
            _buffs.resizeTextForBestFit = true;
            _buffs.resizeTextMinSize = 20;
            _buffs.resizeTextMaxSize = 30;

            AddButton(p, "Resume", "RESUME", UIFactory.Blue, -610f, game.Resume);
            AddButton(p, "Restart", "RESTART", UIFactory.Gold, -750f, game.Restart);
            AddButton(p, "Home", "HOME", UIFactory.Gray, -890f, game.Home);

            _sound = UIFactory.Button(p, "Sound", Loc.T("SFX"), UIFactory.Blue, 36, () => game.SetSound(!game.Save.sound), out _soundText);
            UIFactory.Place((RectTransform)_sound.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-140f, -1060f), new Vector2(250f, 120f));
            _music = UIFactory.Button(p, "Music", Loc.T("BGM"), UIFactory.Blue, 36, () => game.SetMusic(!game.Save.music), out _musicText);
            UIFactory.Place((RectTransform)_music.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(140f, -1060f), new Vector2(250f, 120f));
        }

        static void AddButton(RectTransform parent, string name, string label, Color color, float y, Action onClick)
        {
            Button b = UIFactory.Button(parent, name, label, color, 58, onClick, out _);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(540f, 120f));
        }

        public void Refresh(SaveData save, LevelSession session)
        {
            UIRoot.Toggle(_sound, _soundText, "SFX", save.sound);
            UIRoot.Toggle(_music, _musicText, "BGM", save.music);
            if (session == null) return;
            RunStats s = session.Stats;
            string where = session.EndlessStage > 0
                ? Loc.F("ENDLESS STAGE {0}   ENEMIES +{1}%", session.EndlessStage, Mathf.RoundToInt((session.EnemyPower - 1f) * 100f))
                : Loc.F("LEVEL {0}", session.Def.level);
            _stats.text =
                where + Loc.F("   WAVE {0}/{1}   {2}%", Mathf.Min(session.Wave + 1, session.Waves), session.Waves, Mathf.RoundToInt(session.Progress * 100f)) + "\n" +
                Loc.F("DEFEATED  {0}     BASE  {1}%", Fmt.Num(s.Kills), Mathf.CeilToInt(session.BaseHealth * 100f)) + "\n" +
                Loc.F("BEST ARMY  {0}     BEST COMBO  {1}", Fmt.Num(session.Squad.Peak), s.BestCombo) + "\n" +
                Loc.F("COINS  {0}     TIME  {1}", Fmt.Num(session.Coins), Fmt.Duration(session.Elapsed));
            string taken = Buffs.Summary(session.Mods);
            _buffs.text = taken.Length > 0 ? Loc.T("POWER-UPS") + "\n" + taken : Loc.T("POWER-UPS\nPICK ONE AFTER EVERY WAVE");
        }
    }
}
