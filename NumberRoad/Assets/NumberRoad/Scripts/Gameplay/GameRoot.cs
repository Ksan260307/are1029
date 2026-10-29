using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    public enum GameState
    {
        Title,
        Playing,
        Paused,
        Result,
    }

    /// <summary>
    /// Entry point and state machine (title, playing, paused, result). Builds every
    /// system from code, so the scene only needs this one component - and even that
    /// is created automatically if a scene without it is played.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        public SaveData Save { get; private set; }
        public GameState State { get; private set; }
        public LevelSession Session { get; private set; }
        public GameContext Context { get; private set; }
        public UIRoot UI { get; private set; }

        /// <summary>Lets <see cref="AutoPilot"/> steer instead of the player (tests, F1 in the editor).</summary>
        public bool AutoPlay { get; set; }

        readonly InputReader _input = new InputReader();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameRoot>() != null) return;
            new GameObject("Game").AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = true;
            ConfigureOrientation();
            GameAssets.Init();
            Save = SaveSystem.Load();

            Context = new GameContext();
            Context.Camera = CreateChild("Camera").AddComponent<CameraRig>();
            Context.Camera.Init(WorldView.SkyColor);
            Context.World = CreateChild("World").AddComponent<WorldView>();
            Context.World.Init();
            Context.Fx = new FxSystem();
            Context.Audio = gameObject.AddComponent<AudioSystem>();
            Context.Audio.Init(Save.sound, Save.music);
            UI = CreateChild("UI").AddComponent<UIRoot>();
            UI.Init(this, () => Context.Camera != null ? Context.Camera.Camera : null);
            Context.Labels = UI.Labels;

            ShowTitle();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        GameObject CreateChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }

        /// <summary>Phones stay in portrait; tablets (iPad) may rotate freely.</summary>
        static void ConfigureOrientation()
        {
            if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer) return;
            float longSide = Mathf.Max(Screen.width, Screen.height);
            float shortSide = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
            bool tablet = longSide / shortSide < 1.6f;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = tablet;
            Screen.autorotateToLandscapeLeft = tablet;
            Screen.autorotateToLandscapeRight = tablet;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _input.Update(Time.unscaledDeltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F1)) AutoPlay = !AutoPlay;
#endif
            if (Input.GetKeyDown(KeyCode.Escape)) Back();

            switch (State)
            {
                case GameState.Title:
                    Session?.TickIdle(dt);
                    break;
                case GameState.Playing:
                    float steer = AutoPlay ? AutoPilot.SteerTowards(Session, dt) : _input.SteerDelta;
                    Session.Tick(dt, steer);
                    break;
                case GameState.Result:
                    Session?.Tick(dt, 0f);
                    break;
            }
            Context.Fx.Tick(dt);
        }

        void LateUpdate()
        {
            if (Session != null)
            {
                Context.Camera.Tick(Session.Squad.Position, Time.deltaTime);
                Context.World.Tick(Session.Squad.Z);
                Session.Render();
            }
            Context.Fx.Render();
            UI.LateTick(Time.unscaledDeltaTime);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && !Application.isEditor) Pause();
        }

        // --- Flow ------------------------------------------------------------------------

        public void ShowTitle()
        {
            Time.timeScale = 1f;
            DisposeSession();
            Context.Labels.Clear();
            Context.Fx.Clear();

            LevelDefinition def = LevelGenerator.Generate(Save.level);
            Context.World.Build(def);
            Session = new LevelSession(Context, def, Save.StartSquad, Save.StartWeapon);
            Session.Finished += OnSessionFinished;
            Context.Camera.SetBossView(false);
            Context.Camera.SetTitleView(true);
            Context.Camera.Snap(Session.Squad.Position);
            Context.World.Tick(Session.Squad.Z);
            State = GameState.Title;
            UI.ShowTitle(Save);
        }

        public void Play()
        {
            if (State != GameState.Title || Session == null) return;
            _input.Reset();
            Session.Begin();
            Context.Camera.SetTitleView(false);
            State = GameState.Playing;
            UI.ShowHud(Session, Save);
            Context.Audio.Play(Sfx.Start);
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            UI.ShowPause(true, Save);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            _input.Reset();
            UI.ShowPause(false, Save);
        }

        public void Restart()
        {
            ShowTitle();
            Play();
        }

        public void Home() => ShowTitle();

        public void Continue() => ShowTitle();

        void Back()
        {
            switch (State)
            {
                case GameState.Playing: Pause(); break;
                case GameState.Paused: Resume(); break;
                case GameState.Result: Continue(); break;
            }
        }

        void OnSessionFinished(bool won)
        {
            int earned = Mathf.RoundToInt(Session.Coins * Save.IncomeMultiplier);
            int level = Session.Def.level;
            Save.coins += earned;
            if (won) Save.CompleteLevel();
            SaveSystem.Save(Save);
            State = GameState.Result;
            UI.ShowResult(won, level, earned, Save.coins);
        }

        public bool Buy(UpgradeKind kind)
        {
            if (State != GameState.Title || !Save.TryBuy(kind)) return false;
            SaveSystem.Save(Save);
            Context.Audio.Play(Sfx.PowerUp);
            ShowTitle();
            return true;
        }

        public void SetSound(bool on)
        {
            Save.sound = on;
            Context.Audio.SoundOn = on;
            SaveSystem.Save(Save);
            UI.RefreshSave(Save);
        }

        public void SetMusic(bool on)
        {
            Save.music = on;
            Context.Audio.MusicOn = on;
            SaveSystem.Save(Save);
            UI.RefreshSave(Save);
        }

        void DisposeSession()
        {
            if (Session == null) return;
            Session.Finished -= OnSessionFinished;
            Session.Dispose();
            Session = null;
        }
    }
}
