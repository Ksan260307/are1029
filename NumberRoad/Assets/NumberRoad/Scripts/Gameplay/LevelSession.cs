using System;
using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace NumberRoad.Gameplay
{
    public enum SessionPhase
    {
        Ready,
        Running,
        BossFight,
        Won,
        Lost,
    }

    /// <summary>A big centred announcement shown by the HUD (new enemy, class up, boss, warnings...).</summary>
    public struct BannerInfo
    {
        public string Title;
        public string Subtitle;
        public Color Color;
        /// <summary>Resources/Portraits image shown next to the text, or null.</summary>
        public string Portrait;
    }

    /// <summary>What happened during one run (rewards and statistics).</summary>
    public sealed class RunStats
    {
        public int Kills;
        public int BestCombo;
        public int Gates;
        public int Perfect;
        public int ClassUps;
        public int GoldGoblins;
        public int Crates;
        public int Elites;
        public int Gems;
        public float KillCoins;
        public float ComboCoins;
        public float BonusCoins;
        public int VictoryCoins;
        public int BossCoins;
        public readonly int[] KillsByType = new int[Enemies.All.Length];
        // soldiers lost, by cause (balance tuning)
        public int LostContact;
        public int LostMelee;
        public int LostShots;
        public int LostBoss;
        public int LostGates;

        public float Coins => KillCoins + ComboCoins + BonusCoins + VictoryCoins + BossCoins;
    }

    /// <summary>
    /// One play-through of a level: streams the <see cref="LevelDefinition"/> onto the road as the squad marches,
    /// resolves gates, bullets and contacts, runs combos, the VOLLEY skill, the boss fight and the revive, and
    /// reports the result. Owns every object it creates.
    /// </summary>
    public sealed class LevelSession : ISwarmHost, IBossHost
    {
        public const float CountdownTime = 1.3f;
        const float VolleyTime = 0.9f;
        const float VolleyImpact = 0.35f;
        const float VolleyNear = 1.5f;
        const float VolleyFar = 26f;

        sealed class HordeStream
        {
            public SpawnEvent Ev;
            public int[] Order;
            public bool[] Elite;
            public int Next;
            public bool Announced;
        }

        struct VolleyArrow
        {
            public Vector3 Target;
            public float Delay;
        }

        public readonly LevelDefinition Def;
        public readonly Squad Squad;
        public readonly Swarm Swarm;
        public readonly RunStats Stats = new RunStats();
        public readonly Queue<BannerInfo> Banners = new Queue<BannerInfo>();
        public readonly Queue<string> Hints = new Queue<string>();

        public SessionPhase Phase { get; private set; }
        public BossUnit Boss { get; private set; }
        public bool HasMoved { get; private set; }
        public float Elapsed { get; private set; }
        public float Countdown { get; private set; }
        public int Combo { get; private set; }
        public bool Revived { get; private set; }
        /// <summary>Boss HP share left when the squad fell (0 if the fight never started).</summary>
        public float BossHpLeft { get; private set; }
        public event Action<bool> Finished;

        public int Coins => Mathf.FloorToInt(Stats.Coins);
        public float ComboTime01 => Combo > 0 ? Mathf.Clamp01(_comboClock / Balance.ComboWindow) : 0f;
        public float Progress => _bossFight ? 1f : Mathf.Clamp01(Squad.Z / Mathf.Max(1f, Def.length));
        public bool IsOver => Phase == SessionPhase.Won || Phase == SessionPhase.Lost;
        public bool Fighting => Phase == SessionPhase.Running || Phase == SessionPhase.BossFight;
        public int SkillNeeded => Balance.SkillCharge(Def.level, _ctx.Save.skillLevel);
        public float SkillCharge01 => Mathf.Clamp01(_skill / SkillNeeded);
        public bool SkillReady => _skill >= SkillNeeded && Fighting && Countdown <= 0f;
        public bool CanRevive => Phase == SessionPhase.Lost && !Revived;

        readonly GameContext _ctx;
        readonly GameObject _root;
        readonly EnemyShots _shots = new EnemyShots();
        readonly BulletSystem _bullets;
        readonly List<GateRow> _gates = new List<GateRow>();
        readonly List<StripTile> _strips = new List<StripTile>();
        readonly List<Crate> _crates = new List<Crate>();
        readonly List<HordeStream> _streams = new List<HordeStream>();
        readonly List<IBulletTarget> _targets = new List<IBulletTarget>();
        readonly List<Vector4> _shadows = new List<Vector4>();
        readonly List<VolleyArrow> _volley = new List<VolleyArrow>();
        readonly InstancedBatch _batch = new InstancedBatch();
        readonly Action<Vector3, int, bool> _onShotHit;
        readonly int _recordToBeat;
        int _nextEvent;
        bool _bossSpawned;
        bool _bossFight;
        bool _finishedRaised;
        bool _recordShown;
        bool _skillHinted;
        float _endClock;
        float _comboClock;
        float _skill;
        float _volleyClock = -1f;
        float _confetti;
        float _critClock;
        float _stripClock;
        float _stripPitch = 1f;
        float _blockClock;
        float _explosionClock;
        int _chain;

        public LevelSession(GameContext ctx, LevelDefinition def, Army army, WeaponStats weapon)
        {
            _ctx = ctx;
            Def = def;
            _root = new GameObject($"Level {def.level}");
            SaveData save = ctx.Save;
            Squad = new Squad(ctx, army, weapon, save.GuardBonus, save.RecruitBonus, save.StartLine);
            Swarm = new Swarm(this, ctx);
            _bullets = new BulletSystem(ctx);
            _onShotHit = OnShotHit;
            _recordToBeat = save.statMaxArmy;
            Phase = SessionPhase.Ready;
            SpawnUpTo(Balance.SpawnAhead);
            StreamHordes();
        }

        public void Begin()
        {
            if (Phase != SessionPhase.Ready) return;
            Phase = SessionPhase.Running;
            Countdown = CountdownTime;
            Biome biome = Biomes.All[Def.biome % Biomes.All.Length];
            if (Def.daily) Banner("DAILY CHALLENGE", "DOUBLE HORDES - DOUBLE REWARDS", UIFactory.Gold);
            else Banner("LEVEL " + Def.level, biome.Name, Color.white);
            if (Def.castle) Banner("CASTLE SIEGE", "DEFEAT THE BOSS FOR " + Balance.CastleGems + " GEMS", UIFactory.Gold);
            else if (Def.goldRush) Banner("GOLD RUSH", "COINS x" + Balance.GoldRushCoins.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " - CATCH THE GOLD GOBLINS", UIFactory.Gold);

            // things already placed on the road while the title screen was showing
            for (int i = 0; i < _nextEvent; i++)
            {
                SpawnEvent ev = Def.events[i];
                if (ev.kind != SpawnKind.Horde) continue;
                foreach (HordeUnit u in ev.units) Introduce(u.type);
            }
            foreach (GateRow row in _gates) GateHints(row);
        }

        void Banner(string title, string subtitle, Color color, string portrait = null)
        {
            Banners.Enqueue(new BannerInfo { Title = title, Subtitle = subtitle, Color = color, Portrait = portrait });
        }

        void ShowHint(Hint hint, string text)
        {
            if (_ctx.Save.TakeHint(hint)) Hints.Enqueue(text);
        }

        /// <summary>Title-screen idle: everything stands still, soldiers breathe.</summary>
        public void TickIdle(float dt)
        {
            Squad.Tick(dt, false);
            Swarm.Tick(dt, Squad, false);
            Swarm.Compact(Squad.Z - Balance.DespawnBehind);
            foreach (GateRow g in _gates) g.Tick(dt, Squad.X, Squad.Z);
        }

        public void Tick(float dt, float steer)
        {
            if (Phase == SessionPhase.Ready) return;
            Elapsed += dt;
            bool fighting = Fighting;
            // keeps running a little past zero so the HUD can fade "GO!" out
            if (Countdown > -1f)
            {
                bool counting = Countdown > 0f;
                Countdown -= dt;
                if (counting && Countdown <= 0f) _ctx.Audio.Play(Sfx.Start);
            }

            if (fighting)
            {
                if (Mathf.Abs(steer) > 0.0001f) HasMoved = true;
                Squad.Steer(steer);
            }

            bool blocked = Swarm.AnyBlocking(Squad) || (Boss != null && Boss.IsAttacking && Boss.Touching(Squad));
            float advance = Phase == SessionPhase.Running && !blocked && Countdown <= 0f ? Balance.MarchSpeed * dt : 0f;
            if (Phase == SessionPhase.Running && Squad.Z + advance >= Def.length)
            {
                advance = Mathf.Max(0f, Def.length - Squad.Z);
                EnterBossFight();
            }
            Squad.Move(dt, advance);
            SpawnUpTo(Squad.Z + Balance.SpawnAhead);
            StreamHordes();

            if (fighting)
            {
                CheckGates();
                CheckStrips(dt);
                CheckCrates();
            }

            Swarm.Tick(dt, Squad, fighting && Countdown <= 0f);
            Boss?.Tick(dt, Squad, fighting);
            Swarm.Compact(Squad.Z - Balance.DespawnBehind);
            Swarm.BuildGrid(Squad.Z - 4f, Squad.Z + 80f);

            if (fighting && Countdown <= 0f) Squad.Fire(dt, _bullets, Swarm.Chill);
            RebuildTargets();
            _bullets.Tick(dt, Swarm, _targets);
            _shots.Tick(dt, Squad, _ctx, _onShotHit);
            TickVolley(dt);

            foreach (GateRow g in _gates) g.Tick(dt, Squad.X, Squad.Z);
            foreach (StripTile s in _strips) s.Tick(dt);
            foreach (Crate c in _crates) c.Tick(dt);

            Squad.Tick(dt, advance > 0f && fighting);
            Swarm.UpdateLabels(Squad.Z);
            _ctx.Camera.SetCrowdZoom(Mathf.InverseLerp(10f, Balance.MaxVisibleSoldiers, Squad.VisibleCount) * 2.6f);

            TickCombo(dt);
            CheckRecord();
            TickConfetti(dt);
            Cleanup();
            CheckEnd(dt);
        }

        // --- Rendering ------------------------------------------------------------------

        /// <summary>Characters (squad, swarm, boss) go through the shared crowd renderer.</summary>
        public void RenderCrowd(CrowdRenderer crowd)
        {
            Squad.Render(crowd);
            Swarm.Render(crowd, Squad.Z);
            Boss?.Render(crowd);
        }

        /// <summary>Bullets, enemy shots, the volley and blob shadows.</summary>
        public void RenderEffects()
        {
            _bullets.Render();
            _shots.Render();
            RenderVolley();

            _shadows.Clear();
            Squad.CollectShadows(_shadows);
            Swarm.CollectShadows(_shadows, Squad.Z);
            Boss?.CollectShadow(_shadows);
            if (_shadows.Count == 0) return;
            _batch.Begin(GameAssets.GroundQuad, GameAssets.Shadow);
            var shade = new Color(0f, 0f, 0f, 0.32f);
            foreach (Vector4 s in _shadows)
            {
                _batch.Add(Matrix4x4.TRS(new Vector3(s.x, s.y, s.z), Quaternion.identity, new Vector3(s.w, 1f, s.w)), shade);
            }
            _batch.End();
        }

        public void Dispose()
        {
            Swarm.ReleaseAllLabels();
            Boss?.Despawn();
            foreach (GateRow g in _gates) g.Dispose();
            foreach (StripTile s in _strips) s.Dispose();
            foreach (Crate c in _crates)
            {
                if (!c.Broken) c.Dispose();
            }
            _gates.Clear();
            _strips.Clear();
            _crates.Clear();
            _bullets.Clear();
            _shots.Clear();
            Squad.Dispose();
            _ctx.Audio.SetBossMusic(false);
            if (_root != null) Object.Destroy(_root);
        }

        // --- Spawning ----------------------------------------------------------------

        void SpawnUpTo(float z)
        {
            while (_nextEvent < Def.events.Count && Def.events[_nextEvent].z <= z)
            {
                Spawn(Def.events[_nextEvent++]);
            }
            if (!_bossSpawned && Def.length + Balance.BossArenaDistance <= z + 8f) SpawnBoss();
        }

        void Spawn(SpawnEvent ev)
        {
            Transform parent = _root.transform;
            switch (ev.kind)
            {
                case SpawnKind.GateRow:
                    var row = new GateRow(_ctx, parent, ev);
                    _gates.Add(row);
                    GateHints(row);
                    break;
                case SpawnKind.Strip:
                    for (int i = 0; i < ev.count; i++)
                    {
                        _strips.Add(new StripTile(_ctx, parent, ev.x, ev.z + i * LevelGenerator.StripPitch, ev.width, ev.effect));
                    }
                    break;
                case SpawnKind.Horde:
                    _streams.Add(CreateStream(ev));
                    break;
                case SpawnKind.Crate:
                    var crate = new Crate(_ctx, parent, new Vector3(ev.x, 0f, ev.z), ev.hp, ev.effect);
                    crate.Opened = OnCrateOpened;
                    _crates.Add(crate);
                    break;
            }
        }

        void GateHints(GateRow row)
        {
            if (Phase == SessionPhase.Ready) return;
            foreach (GateView g in row.Gates)
            {
                GateSpec s = g.Spec;
                if (s.roulette != null) ShowHint(Core.Hint.Roulette, "ROULETTE GATE: ITS VALUE KEEPS CHANGING - TIME YOUR PASS!");
                if (s.mystery) ShowHint(Core.Hint.Mystery, "MYSTERY GATE: USUALLY A PRIZE... USUALLY.");
                if (s.lockHp > 0f) ShowHint(Core.Hint.Lock, "LOCKED GATE: SHOOT THE LOCK TO OPEN THE PRIZE!");
                if (s.shootable) ShowHint(Core.Hint.Shootable, "SHOOT THE GATE TO RAISE ITS NUMBER!");
                if (s.effect.kind == GateKind.ClassUp) ShowHint(Core.Hint.ClassUp, "CLASS UP: EVERY SOLDIER BECOMES STRONGER!");
                if (s.effect.kind == GateKind.Recruit) ShowHint(Core.Hint.Recruit, "RECRUIT GATE: NEW KINDS OF SOLDIERS JOIN YOU!");
            }
        }

        HordeStream CreateStream(SpawnEvent ev)
        {
            int total = ev.HordeTotal;
            var order = new int[total];
            int k = 0;
            foreach (HordeUnit u in ev.units)
            {
                for (int i = 0; i < u.count && k < total; i++) order[k++] = u.type;
            }
            for (int i = total - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var elite = new bool[total];
            int front = Mathf.Max(1, Mathf.CeilToInt(total * 0.4f));
            for (int e = 0; e < ev.elites && e < total; e++) elite[Random.Range(0, front)] = true;

            var stream = new HordeStream { Ev = ev, Order = order, Elite = elite };
            if (Phase != SessionPhase.Ready)
            {
                foreach (HordeUnit u in ev.units) Introduce(u.type);
            }
            return stream;
        }

        /// <summary>First encounter with an enemy type: NEW ENEMY banner (and it joins the bestiary).</summary>
        void Introduce(int type)
        {
            if (!_ctx.Save.SeeEnemy(type)) return;
            EnemyType t = Enemies.Get(type);
            Banner("NEW ENEMY: " + t.Name, t.Description.ToUpperInvariant(), new Color(1f, 0.55f, 0.45f), t.Model);
            if (t.Has(EnemyTrait.Ranged)) ShowHint(Core.Hint.Ranged, "ARCHERS SHOOT FROM AFAR - KEEP MOVING TO DODGE THE RED MARKS!");
        }

        void StreamHordes()
        {
            float limit = Squad.Z + Balance.SpawnAhead + 30f;
            for (int s = _streams.Count - 1; s >= 0; s--)
            {
                HordeStream h = _streams[s];
                SpawnEvent ev = h.Ev;
                int columns = Mathf.Max(1, ev.columns);
                float spacingX = columns > 1 ? Mathf.Min(ev.spacing, ev.width / (columns - 1)) : 0f;
                while (h.Next < h.Order.Length && Swarm.Total < Balance.MaxAliveEnemies)
                {
                    int i = h.Next;
                    int rowIndex = i / columns;
                    int col = i % columns;
                    float z = ev.z + rowIndex * ev.spacing;
                    if (z > limit) break;
                    float x = ev.x + (col - (columns - 1) * 0.5f) * spacingX + Random.Range(-0.08f, 0.08f);
                    x = Mathf.Clamp(x, -Balance.SoldierEdgeX, Balance.SoldierEdgeX);
                    Swarm.Spawn(h.Order[i], new Vector3(x, 0f, z + Random.Range(-0.1f, 0.1f)), ev.hp, ev.smash, ev.contact, ev.laneLocked, h.Elite[i]);
                    h.Next++;
                }
                if (!h.Announced && Fighting && ev.warning && ev.z - Squad.Z < 48f)
                {
                    h.Announced = true;
                    Banner("HORDE INCOMING!", Fmt.Num(ev.HordeTotal) + " ENEMIES", UIFactory.Red);
                    _ctx.Audio.Play(Sfx.Warning, 0.8f);
                    Haptics.Tap(30);
                }
                if (h.Next >= h.Order.Length && (h.Announced || !ev.warning)) _streams.RemoveAt(s);
            }
        }

        void SpawnBoss()
        {
            _bossSpawned = true;
            float z = Def.length + Balance.BossArenaDistance;
            Boss = new BossUnit(_ctx, this, Enemies.Bosses[Def.bossType % Enemies.Bosses.Length], new Vector3(0f, 0f, z), Def.bossHp, Def.bossSmash);

            // the escort army waits behind the boss and charges when the fight starts
            int total = 0;
            foreach (HordeUnit u in Def.escorts) total += u.count;
            const int columns = 9;
            const float spacing = 0.72f;
            int k = 0;
            foreach (HordeUnit u in Def.escorts)
            {
                for (int i = 0; i < u.count; i++, k++)
                {
                    int row = k / columns, col = k % columns;
                    float x = (col - (columns - 1) * 0.5f) * spacing + Random.Range(-0.08f, 0.08f);
                    Swarm.Spawn(u.type, new Vector3(x, 0f, z + 2.6f + row * spacing), Def.escortHp, Mathf.Max(1, Def.bossSmash / 4), Def.escortContact, false, false);
                }
            }
        }

        void EnterBossFight()
        {
            if (Phase != SessionPhase.Running || _bossFight) return;
            Phase = SessionPhase.BossFight;
            _bossFight = true;
            if (!_bossSpawned) SpawnBoss();
            Boss.Held = false;
            WakeAll();
            BossType t = Boss.Type;
            // a strong squad may shoot the boss down before reaching the arena
            if (Boss.IsAlive) Banner(t.Name, t.Description.ToUpperInvariant(), UIFactory.Red, t.Model);
            if (Def.escorts != null) foreach (HordeUnit u in Def.escorts) Introduce(u.type);
            _ctx.Camera.SetBossView(true);
            _ctx.Camera.Shake(0.35f);
            _ctx.Audio.Play(Sfx.Smash, 0.6f);
            _ctx.Audio.SetBossMusic(true);
            Haptics.Tap(40, true);
        }

        void WakeAll()
        {
            for (int i = 0; i < Swarm.Count; i++)
            {
                ref Agent a = ref Swarm.Agents[i];
                if (a.State == AgentState.Idle && !Enemies.Get(a.Type).Has(EnemyTrait.Ambush)) a.State = AgentState.Walk;
            }
        }

        // --- Gates, strips, crates ------------------------------------------------------------

        void CheckGates()
        {
            foreach (GateRow row in _gates)
            {
                if (row.Done || Squad.PrevZ >= row.Z || Squad.Z < row.Z) continue;
                row.Done = true;
                GateView chosen = row.Pick(Squad.X);
                bool perfect = IsPerfect(row, chosen);
                foreach (GateView g in row.Gates) g.Consume(g == chosen);
                var origin = new Vector3(chosen.CenterX, 0f, chosen.Z);
                Stats.Gates++;

                if (chosen.Locked)
                {
                    _ctx.Labels.Popup("LOCKED!", origin + Vector3.up * 2f, UIFactory.Gray, 1f);
                    _ctx.Audio.Play(Sfx.GateBad, 0.6f);
                    _chain = 0;
                    continue;
                }

                GateEffect effect = chosen.Effect;
                if (chosen.Spec.mystery) _ctx.Labels.Popup("? = " + effect.Label, origin + Vector3.up * 2.8f, GateView.MysteryColor, 1f, 1.2f);
                if (chosen.Spec.jackpot)
                {
                    _ctx.Labels.Popup("JACKPOT!", Squad.Position + new Vector3(0f, 3.2f, 0.5f), UIFactory.Gold, 1.6f, 1.3f);
                    _ctx.Fx.Sparks(origin + Vector3.up * 1.2f, GameContext.GoldColor, 40, 9f, 0.5f);
                    _ctx.Audio.Play(Sfx.Chest);
                    _ctx.Freeze(0.06f);
                    Haptics.Tap(30, true);
                }
                ApplyEffect(effect, origin);

                if (effect.IsBeneficial)
                {
                    _chain++;
                    if (_chain >= 2)
                    {
                        int chainCoins = Balance.ChainCoins(_chain, Def.level);
                        Stats.BonusCoins += chainCoins;
                        _ctx.Labels.Popup("CHAIN x" + _chain + "  +" + chainCoins, Squad.Position + new Vector3(-1.2f, 2.9f, 0.5f), new Color(0.5f, 0.9f, 1f), 0.8f);
                    }
                }
                else
                {
                    _chain = 0;
                }

                if (perfect)
                {
                    int bonus = Balance.PerfectCoins(Def.level);
                    Stats.Perfect++;
                    Stats.BonusCoins += bonus;
                    _ctx.Labels.Popup("PERFECT!  +" + bonus, Squad.Position + new Vector3(1.2f, 3.4f, 0.5f), UIFactory.Gold, 1.1f, 1.1f);
                    _ctx.Audio.PlayPitched(Sfx.Coin, 0.7f, 1.3f);
                }
            }
        }

        /// <summary>True when the chosen gate is (one of) the best of a row whose options really differ.</summary>
        bool IsPerfect(GateRow row, GateView chosen)
        {
            if (row.Gates.Length < 2 || chosen.Locked || row.Shootable) return false;
            var model = new PowerModel(Squad.Army.Clone(), Squad.Weapon);
            float chosenScore = model.ScoreAfter(chosen.Effect);
            float best = float.MinValue, worst = float.MaxValue;
            foreach (GateView g in row.Gates)
            {
                if (g.Locked) continue;
                float s = model.ScoreAfter(g.Effect);
                best = Mathf.Max(best, s);
                worst = Mathf.Min(worst, s);
            }
            return best - worst > best * 0.02f && chosenScore >= best * 0.999f;
        }

        void ApplyEffect(GateEffect effect, Vector3 origin)
        {
            Vector3 popup = Squad.Position + new Vector3(0f, 2.3f, 0.5f);
            switch (effect.kind)
            {
                case GateKind.ClassUp:
                {
                    UnitLine line = Squad.Army.DominantLine;
                    UnitClass before = HighestTier(line);
                    if (Squad.ClassUp())
                    {
                        Stats.ClassUps++;
                        UnitClass after = HighestTier(line);
                        string sub = before != null && after != null && before != after ? before.Name + "  >  " + after.Name : "EVERY SOLDIER PROMOTED";
                        Banner("CLASS UP!", sub, GateView.ClassUpColor, after?.Model);
                        _ctx.Fx.Sparks(Squad.Position + Vector3.up, new Color(1f, 0.85f, 1f), 40, 8f, 0.5f);
                        _ctx.Audio.Play(Sfx.ClassUp);
                        _ctx.Camera.Shake(0.15f);
                        Haptics.Tap(35, true);
                    }
                    else
                    {
                        int bonus = 10 + Def.level * 2;
                        Stats.BonusCoins += bonus;
                        _ctx.Labels.Popup("MAX CLASS!  +" + bonus, popup, UIFactory.Gold, 1f);
                        _ctx.Audio.Play(Sfx.Coin);
                    }
                    return;
                }
                case GateKind.Recruit:
                {
                    int change = Squad.Apply(effect, origin);
                    _ctx.Labels.Popup("+" + Fmt.Num(change) + " " + Units.LineName(effect.line), popup, Units.LineColor(effect.line), 1.2f, 1.1f);
                    _ctx.Audio.Play(Sfx.Recruit);
                    return;
                }
            }

            if (effect.IsWeapon)
            {
                Squad.Apply(effect, origin);
                _ctx.Labels.Popup(effect.PopupText, popup, GateView.WeaponColor, 1f, 1.1f);
                _ctx.Audio.Play(Sfx.PowerUp);
                return;
            }

            int delta = Squad.Apply(effect, origin);
            if (delta < 0) Stats.LostGates -= delta;
            bool good = delta >= 0;
            string text = effect.kind == GateKind.Multiply || effect.kind == GateKind.Divide ? effect.Label + "  " + Fmt.Signed(delta) : Fmt.Signed(delta);
            float magnitude = Mathf.Clamp01(Mathf.Log10(Mathf.Max(1, Mathf.Abs(delta))) / 4f);
            _ctx.Labels.Popup(text, popup, good ? GateView.GoodColor : GateView.BadColor, 1.2f + magnitude * 0.6f, 1.1f);
            _ctx.Audio.Play(good ? Sfx.GateGood : Sfx.GateBad, 0.6f + magnitude * 0.6f);
            if (!good)
            {
                _ctx.Camera.Shake(0.25f);
                Haptics.Tap(25);
            }
        }

        UnitClass HighestTier(UnitLine line)
        {
            UnitClass best = null;
            for (int t = 0; t < Units.Tiers; t++)
            {
                UnitClass u = Units.All[(int)line * Units.Tiers + t];
                if (Squad.Army.CountOf(u) > 0) best = u;
            }
            return best;
        }

        void CheckStrips(float dt)
        {
            _stripClock -= dt;
            if (_stripClock <= 0f) _stripPitch = 1f;
            foreach (StripTile tile in _strips)
            {
                if (tile.Collected || Squad.PrevZ >= tile.Z || Squad.Z < tile.Z) continue;
                if (!tile.Overlaps(Squad)) continue;
                tile.Collect();
                Squad.Gain(tile.Effect.value, new Vector3(tile.X, 0f, tile.Z));
                _ctx.Labels.Popup(tile.Effect.Label, new Vector3(tile.X, 1.3f, tile.Z), GateView.GoodColor, 0.8f);
                // the pickup sound climbs with every tile in a row
                _ctx.Audio.PlayPitched(Sfx.Coin, 0.45f, _stripPitch, 0.05f);
                _stripPitch = Mathf.Min(2.2f, _stripPitch + 0.06f);
                _stripClock = 0.8f;
            }
        }

        void CheckCrates()
        {
            foreach (Crate crate in _crates)
            {
                if (crate.Broken) continue;
                float dz = crate.Position.z - Squad.Z;
                if (dz > Squad.HalfDepth + 0.6f || dz < -Squad.HalfDepth - 0.6f) continue;
                if (Mathf.Abs(crate.Position.x - Squad.X) > Squad.HalfWidth + 0.6f) continue;
                crate.Break(false);
            }
        }

        void OnCrateOpened(Crate crate, bool byBullets)
        {
            if (byBullets)
            {
                Stats.Crates++;
                Stats.BonusCoins += Balance.CrateCoins;
                _ctx.Fx.Sparks(crate.Position + Vector3.up, GameContext.GoldColor, 20, 7f, 0.45f);
                ApplyEffect(crate.Reward, crate.Position);
            }
            else
            {
                // a crate never takes the last soldier
                int penalty = Mathf.Min(Squad.Count - 1, Mathf.Max(1, Mathf.CeilToInt(Squad.Count * 0.15f)));
                if (penalty <= 0) return;
                Stats.LostGates += Squad.Lose(penalty, crate.Position, false);
                _ctx.Labels.Popup("-" + Fmt.Num(penalty), crate.Position + Vector3.up * 1.5f, GateView.BadColor, 1f);
                _ctx.Camera.Shake(0.3f);
            }
        }

        // --- Swarm callbacks ------------------------------------------------------------

        static int RoundRandom(float v)
        {
            int n = Mathf.FloorToInt(v);
            return n + (Random.value < v - n ? 1 : 0);
        }

        public void OnAgentWoke(ref Agent a) { }

        public void OnContact(ref Agent a)
        {
            EnemyType t = Enemies.Get(a.Type);
            // the gold goblin just vanishes with its sack (no reward, no harm)
            if (t.Contact <= 0) return;
            float contact = Mathf.Max(1, t.Contact) * a.Weight * (a.Elite ? 2f : 1f);
            int lost = RoundRandom(contact * (1f - Squad.GuardChance));
            if (lost > 0)
            {
                Stats.LostContact += Squad.Lose(lost, a.Pos);
                if (lost >= 3)
                {
                    _ctx.Labels.Popup("-" + Fmt.Num(lost), a.Pos + Vector3.up * 1.6f, GateView.BadColor, 0.8f);
                    Haptics.Tap(15);
                }
                _ctx.Audio.Play(Sfx.SoldierDie, 0.5f, 0.1f, 0.05f);
            }
            else if (_blockClock <= 0f)
            {
                _blockClock = 0.6f;
                _ctx.Labels.Popup("BLOCK", a.Pos + Vector3.up * 1.4f, new Color(0.75f, 0.85f, 1f), 0.6f);
                _ctx.Fx.Sparks(a.Pos + Vector3.up * 0.6f, Color.white, 4, 4f, 0.25f);
                _ctx.Audio.Play(Sfx.Hit, 0.4f, 0.1f, 0.08f);
            }
        }

        public void OnMeleeStrike(ref Agent a)
        {
            EnemyType t = Enemies.Get(a.Type);
            float smash = Mathf.Max(t.Contact, a.Smash) * (a.Elite ? 1.5f : 1f);
            int lost = Mathf.Max(1, RoundRandom(smash * (1f - Squad.GuardChance * 0.5f)));
            var hit = new Vector3(Squad.X, 0f, Squad.Z + Squad.HalfDepth);
            Stats.LostMelee += Squad.Lose(lost, hit);
            _ctx.Fx.Burst(hit, new Color(0.75f, 0.7f, 0.62f), 10, 5f, 0.2f, 0.8f);
            _ctx.Camera.Shake(0.35f);
            _ctx.Audio.Play(Sfx.Smash, 0.8f);
            _ctx.Labels.Popup("-" + Fmt.Num(lost), Squad.Position + new Vector3(0f, 2f, 0.5f), GateView.BadColor, 1f);
            Haptics.Tap(25);
        }

        public void OnRangedShot(ref Agent a)
        {
            EnemyType t = Enemies.Get(a.Type);
            bool fire = t.Has(EnemyTrait.Flying);
            float flight = fire ? 1.3f : 1.05f;
            var target = new Vector3(Squad.X + Random.Range(-0.6f, 0.6f), 0f, Squad.Z + (Phase == SessionPhase.Running ? Balance.MarchSpeed * flight : 0f));
            int damage = Mathf.Max(1, Mathf.RoundToInt(a.Smash * (fire ? 1f : 0.5f)));
            _shots.Fire(a.Pos + Vector3.up * 1.2f, target, flight, damage, fire ? 1f : 0.45f, fire);
            _ctx.Audio.Play(Sfx.Shoot, 0.25f, 0.2f, 0.15f);
        }

        void OnShotHit(Vector3 point, int damage, bool fire)
        {
            int lost = RoundRandom(damage * (1f - Squad.GuardChance));
            if (lost > 0) Stats.LostShots += Squad.Lose(lost, point);
            _ctx.Fx.Sparks(point + Vector3.up * 0.4f, fire ? new Color(1f, 0.5f, 0.1f) : new Color(0.9f, 0.85f, 0.7f), fire ? 14 : 5, 5f, 0.35f);
            _ctx.Labels.Popup(lost > 0 ? "-" + Fmt.Num(lost) : "BLOCK", point + Vector3.up * 1.5f, lost > 0 ? GateView.BadColor : new Color(0.75f, 0.85f, 1f), 0.75f);
            _ctx.Audio.Play(fire ? Sfx.Explosion : Sfx.Hit, fire ? 0.5f : 0.45f, 0.1f, 0.06f);
            if (fire) _ctx.Camera.Shake(0.15f);
        }

        public void OnCritical(ref Agent a, float damage)
        {
            if (_critClock > 0f) return;
            _critClock = 0.18f;
            _ctx.Labels.Popup(Fmt.Num(Mathf.CeilToInt(damage)), a.Pos + new Vector3(0f, 1.4f, 0f), new Color(1f, 0.6f, 0.15f), 0.65f, 0.6f);
            _ctx.Audio.Play(Sfx.Crit, 0.3f, 0.1f, 0.1f);
        }

        public void OnAgentKilled(ref Agent a, bool byContact)
        {
            _ctx.Save.SeeEnemy(a.Type);
            // trading yourself for a soldier on contact earns the player nothing
            if (byContact) return;
            EnemyType t = Enemies.Get(a.Type);
            Stats.Kills++;
            Stats.KillsByType[a.Type]++;
            Combo++;
            _comboClock = Balance.ComboWindow;
            Stats.BestCombo = Mathf.Max(Stats.BestCombo, Combo);
            if (Fighting) _skill = Mathf.Min(SkillNeeded, _skill + 1f);

            float baseCoins = a.Elite ? Balance.EliteCoins : t.Coins;
            float multiplier = Balance.ComboMultiplier(Combo);
            Stats.KillCoins += baseCoins;
            Stats.ComboCoins += baseCoins * (multiplier - 1f);

            // milestones every 25 kills, then every 50 / 100 once the combo is huge
            if (Combo % (Combo < 100 ? 25 : Combo < 500 ? 50 : 100) == 0)
            {
                _ctx.Labels.Popup("COMBO " + Combo + "!", Squad.Position + new Vector3(0f, 3.6f, 1f), new Color(1f, 0.85f, 0.3f), 0.9f + Mathf.Min(Combo, 200) / 400f);
                _ctx.Audio.PlayPitched(Sfx.Combo, 0.6f, 1f + Mathf.Min(Combo, 250) / 250f);
            }
            _ctx.Audio.PlayPitched(Sfx.EnemyDie, t.IsBig ? 0.9f : 0.4f, 0.9f + Mathf.Min(Combo, 80) * 0.008f, 0.04f);

            if (t.Has(EnemyTrait.Runaway))
            {
                Stats.GoldGoblins++;
                _ctx.Labels.Popup("GOLD! +" + Fmt.Num(Mathf.RoundToInt(baseCoins * multiplier)), a.Pos + Vector3.up * 2.2f, UIFactory.Gold, 1.4f, 1.2f);
                _ctx.Fx.Burst(a.Pos + Vector3.up, GameContext.GoldColor, 30, 7f, 0.18f, 1.2f);
                _ctx.Audio.PlayPitched(Sfx.Coin, 0.9f, 1.4f, 0f);
                _ctx.Freeze(0.05f);
                Haptics.Tap(25);
            }
            if (a.Elite)
            {
                Stats.Elites++;
                if (Random.value < Balance.EliteGemChance)
                {
                    Stats.Gems++;
                    _ctx.Labels.Popup("+1 GEM", a.Pos + Vector3.up * 2.4f, GameContext.GemColor, 1.1f, 1.1f);
                    _ctx.Fx.Sparks(a.Pos + Vector3.up * 1.2f, GameContext.GemColor, 16, 6f, 0.4f);
                    _ctx.Audio.Play(Sfx.Gem);
                }
            }
            if (t.IsBig || a.Elite)
            {
                _ctx.Camera.Shake(0.18f);
                _ctx.Labels.Popup("+" + Fmt.Num(Mathf.RoundToInt(baseCoins * multiplier)), a.Pos + Vector3.up * 2f, GameContext.GoldColor, 0.8f);
            }
        }

        public void OnAgentEscaped(ref Agent a)
        {
            _ctx.Labels.Popup("ESCAPED...", Squad.Position + new Vector3(0f, 2.6f, 4f), UIFactory.Gray, 0.8f);
        }

        public void OnExplosion(Vector3 center, float radius)
        {
            _ctx.Fx.Sparks(center, new Color(1f, 0.55f, 0.15f), _ctx.LowQuality ? 8 : 18, 4f + radius * 2f, 0.5f);
            _ctx.Fx.Burst(center, new Color(0.3f, 0.25f, 0.22f), _ctx.LowQuality ? 3 : 6, 5f, 0.15f, 0.7f);
            if (_explosionClock > 0f) return;
            _explosionClock = 0.12f;
            _ctx.Audio.Play(Sfx.Explosion, 0.6f, 0.15f, 0.08f);
            _ctx.Camera.Shake(0.2f);
            _ctx.Freeze(0.025f);
            Haptics.Tap(15);
        }

        // --- Boss callbacks -----------------------------------------------------------------

        public void OnBossHit(BossUnit boss, float damage, Vector3 point, bool crit)
        {
            _ctx.Audio.Play(Sfx.Hit, 0.35f, 0.15f, 0.07f);
            if (_critClock > 0f || (!crit && damage < boss.MaxHp * 0.015f)) return;
            _critClock = 0.15f;
            _ctx.Labels.Popup(Fmt.Num(Mathf.CeilToInt(damage)), point + new Vector3(Random.Range(-0.6f, 0.6f), 1.5f, 0f), crit ? new Color(1f, 0.6f, 0.15f) : Color.white, crit ? 0.95f : 0.7f, 0.7f);
        }

        public void OnBossStrike(BossUnit boss)
        {
            float smash = boss.Smash * (boss.Raging ? 1.3f : 1f);
            int lost = Mathf.Max(1, RoundRandom(smash * (1f - Squad.GuardChance * 0.5f)));
            var hit = new Vector3(Squad.X, 0f, Squad.Z + Squad.HalfDepth);
            Stats.LostBoss += Squad.Lose(lost, hit);
            _ctx.Fx.Burst(hit, new Color(0.75f, 0.7f, 0.62f), 14, 6f, 0.22f, 0.9f);
            _ctx.Camera.Shake(0.65f);
            _ctx.Audio.Play(Sfx.Smash);
            _ctx.Labels.Popup("-" + Fmt.Num(lost), Squad.Position + new Vector3(0f, 2.2f, 0.5f), GateView.BadColor, 1.2f);
            Haptics.Tap(35, true);
        }

        public void OnBossSkill(BossUnit boss)
        {
            Vector3 p = boss.Position;
            switch (boss.Type.Skill)
            {
                case BossSkill.Summon:
                {
                    int n = boss.Raging ? 9 : 6;
                    for (int i = 0; i < n; i++)
                    {
                        float angle = i / (float)n * Mathf.PI * 2f;
                        var pos = new Vector3(Mathf.Clamp(p.x + Mathf.Cos(angle) * 2.2f, -3.5f, 3.5f), 0f, p.z - 1.5f + Mathf.Sin(angle) * 1.2f);
                        Swarm.Spawn(Enemies.Skeleton, pos, Def.escortHp, Mathf.Max(1, Def.bossSmash / 4), Def.escortContact, false, false, true, 0.6f);
                    }
                    _ctx.Fx.Sparks(p + Vector3.up * 1.5f, new Color(0.6f, 0.3f, 1f), 30, 7f, 0.5f);
                    _ctx.Audio.Play(Sfx.Skill, 0.7f);
                    _ctx.Labels.Popup("RISE!", p + Vector3.up * 4f, new Color(0.75f, 0.5f, 1f), 1.2f);
                    break;
                }
                case BossSkill.Shockwave:
                {
                    float dz = p.z - Squad.Z;
                    for (int i = 0; i < 24; i++)
                    {
                        float angle = i / 24f * Mathf.PI * 2f;
                        _ctx.Fx.Burst(p + new Vector3(Mathf.Cos(angle) * 3f, 0.2f, Mathf.Sin(angle) * 3f), new Color(0.6f, 0.55f, 0.5f), 2, 6f, 0.2f, 0.8f);
                    }
                    _ctx.Camera.Shake(0.8f);
                    _ctx.Audio.Play(Sfx.Explosion);
                    Haptics.Tap(45, true);
                    if (dz < 9f)
                    {
                        int lost = Mathf.Max(2, RoundRandom(boss.Smash * 1.5f * (1f - Squad.GuardChance * 0.5f)));
                        Stats.LostBoss += Squad.Lose(lost, Squad.Position);
                        _ctx.Labels.Popup("SHOCKWAVE -" + Fmt.Num(lost), Squad.Position + new Vector3(0f, 2.5f, 0.5f), GateView.BadColor, 1.2f);
                    }
                    break;
                }
                case BossSkill.FireBreath:
                {
                    int n = boss.Raging ? 5 : 3;
                    Vector3 mouth = p + new Vector3(0f, 3f, -1.5f);
                    for (int i = 0; i < n; i++)
                    {
                        float x = Squad.X + (i - (n - 1) * 0.5f) * 1.6f + Random.Range(-0.4f, 0.4f);
                        _shots.Fire(mouth, new Vector3(Mathf.Clamp(x, -3.5f, 3.5f), 0f, Squad.Z), 1.25f + i * 0.08f, Mathf.Max(2, boss.Smash / 2), 1.1f, true);
                    }
                    _ctx.Audio.Play(Sfx.Skill, 0.6f);
                    break;
                }
            }
        }

        public void OnBossRage(BossUnit boss)
        {
            Banner("RAGE!", boss.Type.Name + " IS FURIOUS", UIFactory.Red);
            _ctx.Audio.Play(Sfx.Warning);
            _ctx.Camera.Shake(0.4f);
        }

        public void OnBossKilled(BossUnit boss)
        {
            Stats.BossCoins = Balance.BossCoins(Def.level);
            _ctx.Freeze(0.14f);
            _ctx.Slow(1.4f);
            _ctx.Camera.Shake(1f);
            _ctx.Audio.Play(Sfx.Explosion);
            _ctx.Labels.Popup("+" + Fmt.Num(Stats.BossCoins), boss.Position + Vector3.up * 3.5f, UIFactory.Gold, 1.5f, 1.3f);
            Haptics.Tap(60, true);
        }

        // --- Skill, combo, effects --------------------------------------------------------

        /// <summary>VOLLEY: a rain of arrows over the road in front of the squad.</summary>
        public bool UseSkill()
        {
            if (!SkillReady || _volleyClock >= 0f) return false;
            _skill = 0f;
            _volleyClock = 0f;
            _volley.Clear();
            int arrows = _ctx.LowQuality ? 70 : 140;
            for (int i = 0; i < arrows; i++)
            {
                _volley.Add(new VolleyArrow
                {
                    Target = new Vector3(Random.Range(-3.8f, 3.8f), 0f, Squad.Z + Random.Range(VolleyNear, VolleyFar)),
                    Delay = Random.Range(0f, 0.3f),
                });
            }
            _ctx.Audio.Play(Sfx.Skill);
            _ctx.Labels.Popup("VOLLEY!", Squad.Position + new Vector3(0f, 3f, 1f), UIFactory.Gold, 1.6f, 1f);
            Haptics.Tap(30);
            return true;
        }

        void TickVolley(float dt)
        {
            if (!_skillHinted && SkillReady)
            {
                _skillHinted = true;
                ShowHint(Core.Hint.Skill, "VOLLEY IS READY - TAP THE BUTTON FOR A RAIN OF ARROWS!");
            }
            if (_volleyClock < 0f) return;
            float before = _volleyClock;
            _volleyClock += dt;
            if (before < VolleyImpact && _volleyClock >= VolleyImpact)
            {
                float damage = Squad.Dps * Balance.SkillPower(_ctx.Save.skillLevel);
                Swarm.BoxDamage(Squad.Z + VolleyNear, Squad.Z + VolleyFar, damage);
                if (Boss != null && Boss.IsAlive && Boss.Position.z - Squad.Z < VolleyFar + 2f) Boss.OnBulletHit(damage * 0.5f, Boss.Position + Vector3.up, false);
                _ctx.Camera.Shake(0.45f);
                _ctx.Audio.Play(Sfx.Explosion, 0.8f);
            }
            if (_volleyClock >= VolleyTime) _volleyClock = -1f;
        }

        void RenderVolley()
        {
            if (_volleyClock < 0f) return;
            _batch.Begin(GameAssets.Bolt, GameAssets.Transparent);
            Color color = Units.LineColor(Squad.Army.DominantLine);
            foreach (VolleyArrow v in _volley)
            {
                float u = Mathf.Clamp01((_volleyClock - v.Delay) / VolleyImpact);
                if (u <= 0f || _volleyClock - v.Delay > VolleyImpact + 0.08f) continue;
                Vector3 p = v.Target + new Vector3(0f, 14f * (1f - u), -3f * (1f - u));
                _batch.Add(Matrix4x4.TRS(p, Quaternion.LookRotation(new Vector3(0f, -1f, 0.2f)), new Vector3(0.18f, 0.18f, 1.4f)), color);
            }
            _batch.End();
        }

        void TickCombo(float dt)
        {
            _critClock -= dt;
            _blockClock -= dt;
            _explosionClock -= dt;
            if (Combo <= 0) return;
            _comboClock -= dt;
            if (_comboClock <= 0f) Combo = 0;
        }

        void CheckRecord()
        {
            if (_recordShown || _recordToBeat < 30 || Squad.Peak <= _recordToBeat) return;
            _recordShown = true;
            Banner("NEW RECORD!", "LARGEST ARMY: " + Fmt.Num(Squad.Peak), UIFactory.Gold);
            _ctx.Audio.Play(Sfx.Banner);
        }

        void TickConfetti(float dt)
        {
            if (_confetti <= 0f) return;
            float before = _confetti;
            _confetti -= dt;
            if (Mathf.Floor(before / 0.15f) == Mathf.Floor(_confetti / 0.15f)) return;
            Color c = Color.HSVToRGB(Random.value, 0.7f, 1f);
            _ctx.Fx.Burst(Squad.Position + new Vector3(Random.Range(-3f, 3f), 4f, Random.Range(-1f, 3f)), c, 10, 5f, 0.1f, 1.6f);
        }

        void RebuildTargets()
        {
            _targets.Clear();
            if (Boss != null && Boss.IsAlive) _targets.Add(Boss);
            foreach (Crate c in _crates)
            {
                if (!c.Broken) _targets.Add(c);
            }
            foreach (GateRow g in _gates)
            {
                if (g.Done) continue;
                foreach (GateView v in g.Gates)
                {
                    if (v.IsTargetable) _targets.Add(v);
                }
            }
        }

        void Cleanup()
        {
            float behind = Squad.Z - Balance.DespawnBehind;
            for (int i = _gates.Count - 1; i >= 0; i--)
            {
                GateRow g = _gates[i];
                if (g.Z < behind || (g.Done && g.Finished))
                {
                    g.Dispose();
                    _gates.RemoveAt(i);
                }
            }
            for (int i = _strips.Count - 1; i >= 0; i--)
            {
                if (_strips[i].Z < behind)
                {
                    _strips[i].Dispose();
                    _strips.RemoveAt(i);
                }
            }
            for (int i = _crates.Count - 1; i >= 0; i--)
            {
                Crate c = _crates[i];
                if (c.Broken) _crates.RemoveAt(i);
                else if (c.Position.z < behind)
                {
                    c.Dispose();
                    _crates.RemoveAt(i);
                }
            }
        }

        void CheckEnd(float dt)
        {
            if (Fighting)
            {
                if (Squad.Count <= 0)
                {
                    Phase = SessionPhase.Lost;
                    _endClock = 1.6f;
                    BossHpLeft = _bossFight && Boss != null ? Mathf.Clamp01(Boss.Hp / Boss.MaxHp) : 0f;
                    _ctx.Audio.Play(Sfx.Lose);
                    _ctx.Audio.SetBossMusic(false);
                    Haptics.Tap(50, true);
                }
                else if (_bossFight && Boss != null && !Boss.IsAlive)
                {
                    Phase = SessionPhase.Won;
                    _endClock = 2.6f;
                    Stats.VictoryCoins = Balance.VictoryCoins(Def.level);
                    if (Def.castle) Stats.Gems += Balance.CastleGems;
                    Swarm.KillAll();
                    _shots.Clear();
                    Squad.Celebrate();
                    _confetti = 2.2f;
                    _ctx.Audio.Play(Sfx.Win);
                    _ctx.Audio.SetBossMusic(false);
                }
                return;
            }

            if (!IsOver || _finishedRaised) return;
            _endClock -= dt;
            if (_endClock > 0f) return;
            _finishedRaised = true;
            _ctx.Camera.SetBossView(false);
            Finished?.Invoke(Phase == SessionPhase.Won);
        }

        /// <summary>Gem revive: half the best army comes back on the spot and a holy blast clears the area.</summary>
        public bool Revive()
        {
            if (!CanRevive) return false;
            Revived = true;
            Phase = _bossFight ? SessionPhase.BossFight : SessionPhase.Running;
            _finishedRaised = false;
            Squad.Revive(Mathf.Max(5, Squad.Peak / 2));
            Swarm.Clear(Squad.Position, 14f);
            _shots.Clear();
            _ctx.Fx.Sparks(Squad.Position + Vector3.up, new Color(1f, 0.95f, 0.6f), 60, 12f, 0.6f);
            _ctx.Camera.Shake(0.5f);
            _ctx.Audio.Play(Sfx.Revive);
            if (_bossFight) _ctx.Audio.SetBossMusic(true);
            Countdown = 0.8f;
            Haptics.Tap(40, true);
            return true;
        }

        // --- Helpers for the auto pilot / tests ---------------------------------------------

        public GateRow NextGateRow()
        {
            GateRow best = null;
            foreach (GateRow g in _gates)
            {
                if (g.Done || g.Z < Squad.Z) continue;
                if (best == null || g.Z < best.Z) best = g;
            }
            return best;
        }

        public StripTile NextStrip()
        {
            StripTile best = null;
            foreach (StripTile s in _strips)
            {
                if (s.Collected || s.Z < Squad.Z) continue;
                if (best == null || s.Z < best.Z) best = s;
            }
            return best;
        }

        public Crate NextCrate()
        {
            Crate best = null;
            foreach (Crate c in _crates)
            {
                if (c.Broken || c.Position.z < Squad.Z) continue;
                if (best == null || c.Position.z < best.Position.z) best = c;
            }
            return best;
        }

        public bool ShotThreat(out float landX) => _shots.Threat(Squad.X, Squad.Z, out landX);
    }
}
