using System;
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

    /// <summary>Everything the result screen shows about a finished run.</summary>
    public sealed class RunResult
    {
        public bool Won;
        public bool Daily;
        public int Level;
        public RunStats Stats;
        public float IncomeMultiplier = 1f;
        public float GoldRushMultiplier = 1f;
        public float DailyMultiplier = 1f;
        public float CouponMultiplier = 1f;
        public int TotalCoins;
        public int Gems;
        public int Stars;
        public float Progress;
        public float BossHpLeft;
        public string BossName;
        public int Survivors;
        public int Peak;
        public bool CanRevive;
        public int ChestCoins;
        public int ChestGems;
        public bool ChestOpened;
        public int Score;
        /// <summary>Place in the score ranking (1 = best), 0 when outside the top 10.</summary>
        public int Rank;
        // endless mode
        public bool Endless;
        public int EndlessStage;
        public int EndlessBosses;
        public int EnemyPowerPercent;
        public bool NewEndlessBest;
        /// <summary>Coins the cleared endless stages already paid (shown, not paid again).</summary>
        public int RunCoins;
        /// <summary>Lost because enemies slipped past and wrecked the war wagon.</summary>
        public bool BaseDestroyed;
    }

    /// <summary>
    /// Entry point and state machine (title, playing, paused, result). Builds every system from code, so the
    /// scene only needs this one component - and even that is created automatically if a scene without it is
    /// played. Also applies hit-stop / slow motion, automatic quality, daily rewards and run rewards.
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
        public RunResult LastResult { get; private set; }
        /// <summary>Coins earned while away, shown once on the title screen.</summary>
        public int OfflineCoins { get; private set; }
        public bool PlayingChallenge { get; private set; }
        /// <summary>The endless run in progress, or null.</summary>
        public EndlessRun Endless { get; private set; }

        /// <summary>Lets <see cref="AutoPilot"/> steer instead of the player (tests, F1 in the editor).</summary>
        public bool AutoPlay { get; set; }

        /// <summary>Holds DASH from code (tests); the player holds the HUD button, Space or up.</summary>
        public bool HoldDash { get; set; }

        /// <summary>Base game speed (tests run faster); hit-stop and slow motion scale it.</summary>
        public float TimeScale { get; set; } = 1f;

        readonly InputReader _input = new InputReader();
        /// <summary>Set by the ?debugLevel=N URL option: a throwaway save that is never written.</summary>
        bool _debugSave;
        float _freeze;
        float _slow;
        float _perfClock;
        float _perfFrames;
        int _slowWindows;
        bool _coupon;

        // what this run has already paid out (a revive continues the run and pays only the difference)
        int _paidCoins;
        int _paidGems;
        int _paidKills;
        int _paidGates;
        int _paidPerfect;
        int _paidClassUps;
        int _paidGold;
        int _paidCrates;
        float _paidSeconds;
        readonly int[] _paidKillsByType = new int[Enemies.All.Length];
        /// <summary>This run's ranking entry (updated, not duplicated, when a revived run ends again).</summary>
        RankEntry _runEntry;

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

            // WebGL: -1 renders on requestAnimationFrame; a fixed rate there falls back to timers and can halve the frame rate
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 60;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = true;
            ConfigureOrientation();
            GameAssets.Init();
            Save = SaveSystem.Load();
            ApplyDebugLevel();
            Loc.Language = (Language)Save.language;

            int today = Daily.Today;
            Daily.RegisterLogin(Save, today);
            OfflineCoins = Daily.OfflineCoins(Save, Daily.NowUnix);
            Save.coins += OfflineCoins;
            Save.lastSeenUnix = Daily.NowUnix;
            Haptics.Enabled = Save.vibration;

            Context = new GameContext { Save = Save };
            Context.Camera = CreateChild("Camera").AddComponent<CameraRig>();
            Context.Camera.Init(WorldView.SkyColor);
            Context.World = CreateChild("World").AddComponent<WorldView>();
            Context.World.Init();
            Context.Fx = new FxSystem();
            Context.Crowd = new CrowdRenderer();
            Context.Weather = new WeatherFx();
            Context.Audio = gameObject.AddComponent<AudioSystem>();
            Context.Audio.Init(Save.sound, Save.music);
            UI = CreateChild("UI").AddComponent<UIRoot>();
            UI.Init(this, () => Context.Camera != null ? Context.Camera.Camera : null);
            Context.Labels = UI.Labels;
            ApplyQuality();
            Persist();

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

        void Persist()
        {
            if (!_debugSave) SaveSystem.Save(Save);
        }

        /// <summary>
        /// Browser testing aid: index.html?debugLevel=25 starts at that level with the upgrades the level
        /// generator expects, using a temporary save so the real progress is untouched.
        /// </summary>
        void ApplyDebugLevel()
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return;
            int at = url.IndexOf("debugLevel=", StringComparison.Ordinal);
            if (at < 0) return;
            int start = at + "debugLevel=".Length, end = start;
            while (end < url.Length && char.IsDigit(url[end])) end++;
            if (!int.TryParse(url.Substring(start, end - start), out int level) || level < 1) return;
            _debugSave = true;
            int upgrades = Mathf.FloorToInt(LevelGenerator.ReferenceUpgrades(level));
            Save = new SaveData { level = level, bestLevel = level, coins = 5000, gems = 50, sound = Save.sound, music = Save.music, language = Save.language };
            foreach (UpgradeKind k in (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind))) Save.SetUpgrade(k, upgrades);
            Save.lastLoginRewardDay = Daily.Today;
            Debug.Log($"[NumberRoad] debug level {level} (temporary save)");
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
            float unscaled = Time.unscaledDeltaTime;
            _input.Update(unscaled);

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
                    if (Session.ChoosingBuff)
                    {
                        // the road waits for a card; the bot picks straight away
                        if (AutoPlay) ChooseBuff(AutoPilot.PickBuff(Session));
                        break;
                    }
                    // DASH: the HUD button, Space or up held down
                    Session.Dash = HoldDash || (!AutoPlay && (UI.DashHeld || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)));
                    float steer = AutoPlay ? AutoPilot.SteerTowards(Session, dt) : _input.SteerDelta;
                    Session.Tick(dt, steer);
                    MeasurePerformance(unscaled);
                    break;
                case GameState.Result:
                    Session?.Tick(dt, 0f);
                    break;
            }
            Context.Fx.Tick(dt);
            ApplyTimeEffects(unscaled);
        }

        void LateUpdate()
        {
            if (Session != null)
            {
                Context.Camera.Tick(Session.Squad.Position, Time.deltaTime);
                Context.World.Tick(Session.Squad.Z);
                CrowdRenderer crowd = Context.Crowd;
                crowd.Eye = Context.Camera.transform.position;
                crowd.LowQuality = Context.LowQuality;
                Context.Camera.SetRush(State == GameState.Playing && Session.Dash && Session.Rushing);
                crowd.Begin();
                Session.RenderCrowd(crowd);
                crowd.End();
                Session.RenderEffects();
                Context.Weather.Tick(Time.deltaTime, Session.Squad.Position);
                Context.Weather.Render();
            }
            Context.Fx.Render();
            UI.LateTick(Time.unscaledDeltaTime);
        }

        /// <summary>Hit-stop (near freeze) and slow motion requested by the session, on top of the base speed.</summary>
        void ApplyTimeEffects(float unscaled)
        {
            _freeze = Mathf.Max(_freeze, Context.FreezeRequest);
            _slow = Mathf.Max(_slow, Context.SlowRequest);
            Context.FreezeRequest = 0f;
            Context.SlowRequest = 0f;
            if (State == GameState.Paused) return;
            _freeze -= unscaled;
            _slow -= unscaled;
            float scale = _freeze > 0f ? 0.03f : _slow > 0f ? 0.3f : 1f;
            // the x2 button speeds up play only (menus, results and the title stay at normal speed)
            float speed = State == GameState.Playing ? Mathf.Clamp(Save.gameSpeed, 1, 2) : 1f;
            Time.timeScale = TimeScale * scale * speed;
        }

        /// <summary>Auto quality: two slow 2-second windows in a row switch to the light renderer.</summary>
        void MeasurePerformance(float unscaled)
        {
            if (Save.quality != (int)Quality.Auto || Context.LowQuality) return;
            _perfClock += unscaled;
            _perfFrames++;
            if (_perfClock < 2f) return;
            float fps = _perfFrames / _perfClock;
            _perfClock = 0f;
            _perfFrames = 0;
            _slowWindows = fps < 38f ? _slowWindows + 1 : 0;
            if (_slowWindows >= 2)
            {
                Context.LowQuality = true;
                ApplyQuality();
            }
        }

        void ApplyQuality()
        {
            if (Save.quality == (int)Quality.Low) Context.LowQuality = true;
            else if (Save.quality == (int)Quality.High) Context.LowQuality = false;
            Context.Weather.Density = Context.LowQuality ? 0.35f : 1f;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            Pause();
            Save.lastSeenUnix = Daily.NowUnix;
            Persist();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && !Application.isEditor) Pause();
        }

        void OnApplicationQuit()
        {
            Save.lastSeenUnix = Daily.NowUnix;
            Persist();
        }

        // --- Flow ------------------------------------------------------------------------

        /// <summary>
        /// Builds the road and the squad. <paramref name="army"/>, <paramref name="weapon"/> and <paramref name="mods"/>
        /// carry an endless run on into its next stage; a normal level starts from the save.
        /// </summary>
        void BuildSession(LevelDefinition def, Army army = null, WeaponStats? weapon = null, RunMods mods = null, float baseHealth = 1f)
        {
            Time.timeScale = TimeScale;
            _freeze = _slow = 0f;
            DisposeSession();
            Context.Labels.Clear();
            Context.Fx.Clear();

            Context.World.Build(def);
            Context.Camera.SetBackground(Context.World.Sky);
            Context.Weather.Set(Context.World.Biome.Weather);
            Session = new LevelSession(Context, def, army ?? new Army(Save.StartLine, Save.StartSquad), weapon ?? Save.StartWeapon, mods,
                Endless != null ? Endless.EnemyPower : 1f, Endless != null ? Endless.Stage : 0, baseHealth);
            Session.FastMarch = Save.fastMarch;
            Session.Finished += OnSessionFinished;
            Context.Camera.SetBossView(false);
            Context.Camera.SetCrowdZoom(0f);
            Context.Camera.SetTitleView(true);
            Context.Camera.Snap(Session.Squad.Position);
            Context.World.Tick(Session.Squad.Z);
            ResetPaid();
        }

        void ResetPaid()
        {
            _paidCoins = _paidGems = _paidKills = _paidGates = _paidPerfect = _paidClassUps = _paidGold = _paidCrates = 0;
            _paidSeconds = 0f;
            Array.Clear(_paidKillsByType, 0, _paidKillsByType.Length);
            _runEntry = null;
        }

        public void ShowTitle()
        {
            PlayingChallenge = false;
            Endless = null;
            BuildSession(LevelGenerator.Generate(Save.level));
            State = GameState.Title;
            UI.ShowTitle(Save);
        }

        public void Play()
        {
            if (State != GameState.Title || Session == null) return;
            _input.Reset();
            _coupon = Save.doubleCoinsNextRun;
            Session.Begin();
            Context.Camera.SetTitleView(false);
            State = GameState.Playing;
            UI.ShowHud(Session, Save);
            Context.Audio.Play(Sfx.Banner);
        }

        /// <summary>Today's challenge: a few levels ahead with double hordes, double coins and gems.</summary>
        public void PlayChallenge()
        {
            if (!Daily.ChallengeReady(Save, Daily.Today)) return;
            BuildSession(LevelGenerator.Generate(Daily.ChallengeLevel(Save), true, Daily.Today));
            PlayingChallenge = true;
            State = GameState.Title;
            Play();
        }

        /// <summary>Takes a buff card between waves.</summary>
        public void ChooseBuff(int index)
        {
            if (State != GameState.Playing || Session == null || !Session.ChoosingBuff) return;
            Session.ChooseBuff(index);
            _input.Reset();
        }

        public void RerollBuffs()
        {
            if (State == GameState.Playing && Session != null) Session.Reroll();
        }

        /// <summary>
        /// Endless mode: stage after stage on one run until the squad falls. The army and its power-ups carry on,
        /// and every boss beaten makes the enemies 1% stronger.
        /// </summary>
        public void PlayEndless()
        {
            if (State != GameState.Title) return;
            Endless = new EndlessRun(Save.bestLevel, UnityEngine.Random.Range(1, 1 << 20), Save.endlessBest);
            BuildSession(LevelGenerator.Generate(Endless.Level, false, Endless.Salt));
            State = GameState.Title;
            Play();
        }

        /// <summary>The boss of an endless stage fell: the same army marches straight on into the next stage.</summary>
        void NextEndlessStage(LevelSession cleared)
        {
            Squad squad = cleared.Squad;
            Army army = squad.Army.Clone();
            WeaponStats weapon = squad.Weapon;
            // the next road is sized for the army that arrives (and the buffs it carries)
            LevelDefinition def = LevelGenerator.Generate(Endless.Level, false, Endless.Salt, army, squad.Effective(1f));
            BuildSession(def, army, weapon, cleared.Mods, cleared.BaseHealth);
            State = GameState.Title;
            Play();
        }

        /// <summary>x2 button: play runs at double speed (kept between runs).</summary>
        public void ToggleSpeed()
        {
            Save.gameSpeed = Save.gameSpeed >= 2 ? 1 : 2;
            Persist();
        }

        /// <summary>FAST MARCH button: the squad hurries along while no enemy is near (kept between runs).</summary>
        public void ToggleFastMarch()
        {
            Save.fastMarch = !Save.fastMarch;
            if (Session != null) Session.FastMarch = Save.fastMarch;
            Persist();
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            UI.ShowPause(true, Save, Session);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = TimeScale;
            _input.Reset();
            UI.ShowPause(false, Save, Session);
        }

        public void Restart()
        {
            if (Endless != null)
            {
                // a new endless run from the first stage
                ShowTitle();
                PlayEndless();
                return;
            }
            if (PlayingChallenge && Daily.ChallengeReady(Save, Daily.Today))
            {
                PlayChallenge();
                return;
            }
            ShowTitle();
            Play();
        }

        public void Home()
        {
            FinalizeResult();
            ShowTitle();
        }

        /// <summary>After a win: straight into the next level (upgrades are bought at home).</summary>
        public void Next()
        {
            FinalizeResult();
            ShowTitle();
            Play();
        }

        public void Retry()
        {
            FinalizeResult();
            Restart();
        }

        /// <summary>Pays the run (once more for a revived run) and shows the result screen.</summary>
        void OnSessionFinished(bool won)
        {
            LevelSession s = Session;
            RunStats st = s.Stats;
            int level = s.Def.level;
            bool daily = s.Def.daily;
            var result = new RunResult
            {
                Won = won,
                Daily = daily,
                Level = level,
                Stats = st,
                IncomeMultiplier = Save.IncomeMultiplier,
                GoldRushMultiplier = s.Def.goldRush ? Balance.GoldRushCoins : 1f,
                DailyMultiplier = daily && won ? 2f : 1f,
                CouponMultiplier = _coupon ? 2f : 1f,
                Progress = s.Progress,
                BossHpLeft = s.BossHpLeft,
                BossName = s.Boss != null ? s.Boss.Type.Name : string.Empty,
                Survivors = s.Squad.Count,
                Peak = s.Squad.Peak,
                BaseDestroyed = s.BaseDestroyed,
            };
            float multiplier = result.IncomeMultiplier * result.GoldRushMultiplier * result.DailyMultiplier * result.CouponMultiplier;
            result.TotalCoins = Mathf.RoundToInt(st.Coins * multiplier);
            result.Gems = st.Gems + (daily && won ? Daily.ChallengeGems : 0);

            // pay only what was not paid before a revive
            Save.coins += result.TotalCoins - _paidCoins;
            Save.statCoinsEarned += result.TotalCoins - _paidCoins;
            Save.gems += result.Gems - _paidGems;
            Save.statKills += st.Kills - _paidKills;
            Save.statGates += st.Gates - _paidGates;
            Save.statPerfect += st.Perfect - _paidPerfect;
            Save.statClassUps += st.ClassUps - _paidClassUps;
            Save.statGoldGoblins += st.GoldGoblins - _paidGold;
            Save.statChests += st.Crates - _paidCrates;
            Save.statPlaySeconds += s.Elapsed - _paidSeconds;
            for (int i = 0; i < st.KillsByType.Length; i++)
            {
                Save.AddKills(i, st.KillsByType[i] - _paidKillsByType[i]);
                _paidKillsByType[i] = st.KillsByType[i];
            }
            _paidCoins = result.TotalCoins;
            _paidGems = result.Gems;
            _paidKills = st.Kills;
            _paidGates = st.Gates;
            _paidPerfect = st.Perfect;
            _paidClassUps = st.ClassUps;
            _paidGold = st.GoldGoblins;
            _paidCrates = st.Crates;
            _paidSeconds = s.Elapsed;
            Save.statMaxArmy = Mathf.Max(Save.statMaxArmy, s.Squad.Peak);
            Save.statBestCombo = Mathf.Max(Save.statBestCombo, st.BestCombo);

            if (Endless != null)
            {
                EndlessStageOver(s, result, won);
                return;
            }

            if (won)
            {
                Save.statWins++;
                Save.statBosses++;
                result.Stars = Balance.Stars(s.Squad.Count, s.Squad.Peak);
                if (daily) Save.lastChallengeDay = Daily.Today;
                else
                {
                    Save.RecordStars(level, result.Stars);
                    Save.CompleteLevel();
                }
                if (_coupon) Save.doubleCoinsNextRun = false;
                bool gems = UnityEngine.Random.value < 0.25f;
                result.ChestGems = gems ? 2 + level / 10 : 0;
                result.ChestCoins = gems ? 0 : Mathf.RoundToInt((40 + level * 12) * Save.IncomeMultiplier);
            }
            else
            {
                Save.statLosses++;
                result.CanRevive = s.CanRevive;
            }
            RecordRanking(result, s);
            Save.lastSeenUnix = Daily.NowUnix;
            Persist();

            LastResult = result;
            State = GameState.Result;
            UI.ShowResult(result, Save);
        }

        /// <summary>Endless: a cleared stage leads straight into the next one; a lost one ends the run.</summary>
        void EndlessStageOver(LevelSession s, RunResult result, bool won)
        {
            EndlessRun run = Endless;
            if (won)
            {
                Save.statBosses++;
                if (_coupon) Save.doubleCoinsNextRun = false;
                run.StageCleared(s.Stats.Kills, result.TotalCoins, s.Squad.Peak, s.Stats.BestCombo);
                Save.endlessBest = Mathf.Max(Save.endlessBest, run.Bosses);
                Save.lastSeenUnix = Daily.NowUnix;
                Persist();
                NextEndlessStage(s);
                return;
            }

            Save.statLosses++;
            result.CanRevive = s.CanRevive;
            result.Endless = true;
            result.EndlessStage = run.Stage;
            result.EndlessBosses = run.Bosses;
            result.EnemyPowerPercent = run.PowerPercent;
            result.NewEndlessBest = run.NewBest;
            result.RunCoins = run.Coins;
            RecordRanking(result, s);
            Save.lastSeenUnix = Daily.NowUnix;
            Persist();

            LastResult = result;
            State = GameState.Result;
            UI.ShowResult(result, Save);
        }

        void RecordRanking(RunResult result, LevelSession s)
        {
            // an endless run counts everything since its first stage
            EndlessRun run = Endless;
            int kills = s.Stats.Kills + (run != null ? run.Kills : 0);
            int army = Mathf.Max(s.Squad.Peak, run != null ? run.Peak : 0);
            int combo = Mathf.Max(s.Stats.BestCombo, run != null ? run.BestCombo : 0);
            result.Score = Ranking.Score(kills, army, combo, result.Level, result.Won, result.Stars, run != null ? run.Bosses : 0);
            RankEntry entry = run != null ? run.Entry ?? (run.Entry = new RankEntry()) : _runEntry ?? (_runEntry = new RankEntry());
            entry.score = result.Score;
            entry.level = result.Level;
            entry.kills = kills;
            entry.army = army;
            entry.combo = combo;
            entry.won = result.Won;
            entry.daily = result.Daily;
            entry.endless = run != null;
            entry.stage = run != null ? run.Stage : 0;
            entry.unix = Daily.NowUnix;
            Ranking.Record(Save, entry);
            result.Rank = Ranking.PlaceOf(Save, entry, RankMetric.Score);
        }

        /// <summary>Gem revive from the defeat screen: the same run continues.</summary>
        public bool Revive()
        {
            if (State != GameState.Result || Session == null || !Session.CanRevive || Save.gems < Balance.ReviveGems) return false;
            Save.gems -= Balance.ReviveGems;
            Save.statLosses--;
            Persist();
            Session.Revive();
            State = GameState.Playing;
            _input.Reset();
            UI.ShowHud(Session, Save);
            return true;
        }

        /// <summary>The victory chest. Leaving the result screen opens it automatically.</summary>
        public void OpenChest()
        {
            RunResult r = LastResult;
            if (r == null || !r.Won || r.ChestOpened) return;
            r.ChestOpened = true;
            Save.coins += r.ChestCoins;
            Save.gems += r.ChestGems;
            Save.statCoinsEarned += r.ChestCoins;
            Persist();
            Context.Audio.Play(r.ChestGems > 0 ? Sfx.Gem : Sfx.Chest);
            Haptics.Tap(20);
        }

        void FinalizeResult()
        {
            if (State == GameState.Result) OpenChest();
        }

        void Back()
        {
            switch (State)
            {
                case GameState.Title: UI.CloseTopPanel(); break;
                case GameState.Playing: Pause(); break;
                case GameState.Paused: Resume(); break;
                case GameState.Result: Home(); break;
            }
        }

        // --- Title-screen actions ------------------------------------------------------------

        public bool Buy(UpgradeKind kind)
        {
            if (State != GameState.Title || !Save.TryBuy(kind)) return false;
            Persist();
            Context.Audio.Play(Sfx.PowerUp);
            ShowTitle();
            return true;
        }

        public void SetStartLine(UnitLine line)
        {
            if (State != GameState.Title || !Units.IsUnlocked(line, Save.bestLevel)) return;
            Save.startLine = (int)line;
            Persist();
            ShowTitle();
        }

        public bool ClaimAchievement(Achievement a)
        {
            if (!Achievements.Claim(Save, a)) return false;
            Persist();
            Context.Audio.Play(Sfx.Gem);
            UI.RefreshSave(Save);
            return true;
        }

        /// <summary>Collects every reward that is ready (awards, the daily bonus, the lucky draw) in one go.</summary>
        public RewardSummary ClaimAllRewards()
        {
            RewardSummary summary = Rewards.ClaimAll(Save, Daily.Today, CheapestUpgrade);
            if (!summary.Any) return summary;
            Persist();
            Context.Audio.Play(Sfx.Chest);
            Context.Audio.Play(summary.Gems > 0 ? Sfx.Gem : Sfx.Coin, 0.8f);
            UI.RefreshSave(Save);
            return summary;
        }

        public Prize ClaimLogin()
        {
            int today = Daily.Today;
            Prize prize = Daily.LoginPrize(Daily.StreakDay(Save), Save.bestLevel);
            if (!Daily.LoginRewardReady(Save, today)) return prize;
            Save.lastLoginRewardDay = today;
            Daily.Grant(Save, prize, CheapestUpgrade());
            Persist();
            Context.Audio.Play(prize.Kind == PrizeKind.Gems ? Sfx.Gem : Sfx.Coin);
            UI.RefreshSave(Save);
            return prize;
        }

        /// <summary>Today's lucky draw (fixed per day). Returns the prize index on the wheel.</summary>
        public int ClaimLucky()
        {
            int today = Daily.Today;
            Prize[] prizes = Daily.LuckyPrizes(Save.bestLevel);
            int index = Daily.LuckyIndex(today, prizes.Length);
            if (!Daily.LuckyReady(Save, today)) return index;
            Save.lastLuckyDay = today;
            Daily.Grant(Save, prizes[index], CheapestUpgrade());
            Persist();
            UI.RefreshSave(Save);
            return index;
        }

        UpgradeKind CheapestUpgrade()
        {
            UpgradeKind best = UpgradeKind.Squad;
            foreach (UpgradeKind k in (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind)))
            {
                if (Save.UpgradeCost(k) < Save.UpgradeCost(best)) best = k;
            }
            return best;
        }

        public void ClearOfflineCoins() => OfflineCoins = 0;

        /// <summary>Level select (title screen): any level up to the furthest one reached.</summary>
        public void SelectLevel(int level)
        {
            if (State != GameState.Title) return;
            int before = Save.level;
            Save.SelectLevel(level);
            if (Save.level == before) return;
            Persist();
            ShowTitle();
        }

        /// <summary>Settings > reset: everything starts over except the sound and display settings.</summary>
        public void ResetProgress()
        {
            if (State != GameState.Title) return;
            Save.ResetProgress();
            Context.LowQuality = false;
            _slowWindows = 0;
            ApplyQuality();
            Persist();
            ShowTitle();
            Context.Audio.Play(Sfx.Banner);
        }

        // --- Settings ---------------------------------------------------------------------------

        /// <summary>Switches the UI language. Every screen is built again so no label stays in the old language.</summary>
        public void SetLanguage(Language language)
        {
            if (State != GameState.Title || Loc.Language == language) return;
            Save.language = (int)language;
            Loc.Language = language;
            Persist();
            DisposeSession();
            DestroyImmediate(UI.gameObject);
            UI = CreateChild("UI").AddComponent<UIRoot>();
            UI.Init(this, () => Context.Camera != null ? Context.Camera.Camera : null);
            Context.Labels = UI.Labels;
            ShowTitle();
            UI.OpenSettings();
        }

        public void SetSound(bool on)
        {
            Save.sound = on;
            Context.Audio.SoundOn = on;
            Persist();
            UI.RefreshSave(Save);
        }

        public void SetMusic(bool on)
        {
            Save.music = on;
            Context.Audio.MusicOn = on;
            Persist();
            UI.RefreshSave(Save);
        }

        public void SetVibration(bool on)
        {
            Save.vibration = on;
            Haptics.Enabled = on;
            if (on) Haptics.Tap(30, true);
            Persist();
            UI.RefreshSave(Save);
        }

        public void CycleQuality()
        {
            Save.quality = (Save.quality + 1) % 3;
            Context.LowQuality = false;
            _slowWindows = 0;
            ApplyQuality();
            Persist();
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
