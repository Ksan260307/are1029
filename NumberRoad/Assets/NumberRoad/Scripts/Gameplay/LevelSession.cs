using System;
using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
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

    /// <summary>
    /// One play-through of a level: spawns the <see cref="LevelDefinition"/> events
    /// as the squad marches, resolves gates, bullets and contacts, runs the boss
    /// fight and reports the result. Owns every object it creates.
    /// </summary>
    public sealed class LevelSession
    {
        public readonly LevelDefinition Def;
        public readonly Squad Squad;
        public SessionPhase Phase { get; private set; }
        public Enemy Boss { get; private set; }
        public int Coins { get; private set; }
        public bool HasMoved { get; private set; }
        public float Elapsed { get; private set; }
        public event Action<bool> Finished;

        public float Progress => Phase >= SessionPhase.BossFight ? 1f : Mathf.Clamp01(Squad.Z / Mathf.Max(1f, Def.length));
        public bool IsOver => Phase == SessionPhase.Won || Phase == SessionPhase.Lost;

        readonly GameContext _ctx;
        readonly GameObject _root;
        readonly List<Enemy> _enemies = new List<Enemy>();
        readonly List<GatePair> _gates = new List<GatePair>();
        readonly List<StripTile> _strips = new List<StripTile>();
        readonly List<Crate> _crates = new List<Crate>();
        readonly List<IBulletTarget> _targets = new List<IBulletTarget>();
        readonly List<Vector4> _shadows = new List<Vector4>();
        readonly BulletSystem _bullets = new BulletSystem();
        readonly InstancedBatch _shadowBatch = new InstancedBatch();
        int _nextEvent;
        bool _bossSpawned;
        bool _finishedRaised;
        float _endClock;

        public LevelSession(GameContext ctx, LevelDefinition def, int startCount, WeaponStats weapon)
        {
            _ctx = ctx;
            Def = def;
            _root = new GameObject($"Level {def.level}");
            Squad = new Squad(ctx, _root.transform, startCount, weapon);
            Phase = SessionPhase.Ready;
            SpawnUpTo(Balance.SpawnAhead);
        }

        public void Begin()
        {
            if (Phase == SessionPhase.Ready) Phase = SessionPhase.Running;
        }

        /// <summary>Title-screen idle: everything stands still, soldiers breathe.</summary>
        public void TickIdle(float dt)
        {
            Squad.UpdateVisuals(dt, false);
            foreach (Enemy e in _enemies) e.Tick(0f, Squad, false);
        }

        public void Tick(float dt, float steer)
        {
            if (Phase == SessionPhase.Ready) return;
            Elapsed += dt;
            bool fighting = Phase == SessionPhase.Running || Phase == SessionPhase.BossFight;

            if (fighting)
            {
                if (Mathf.Abs(steer) > 0.0001f) HasMoved = true;
                Squad.Steer(steer);
            }

            bool blocked = false;
            foreach (Enemy e in _enemies)
            {
                if (e.IsAttacking && Touching(e)) blocked = true;
            }

            float advance = Phase == SessionPhase.Running && !blocked ? Balance.MarchSpeed * dt : 0f;
            if (Phase == SessionPhase.Running && Squad.Z + advance >= Def.length)
            {
                advance = Mathf.Max(0f, Def.length - Squad.Z);
                EnterBossFight();
            }
            Squad.Move(dt, advance);
            SpawnUpTo(Squad.Z + Balance.SpawnAhead);

            if (fighting)
            {
                CheckGates();
                CheckStrips();
                CheckCrates();
                Squad.Fire(dt, _bullets);
            }

            TickEnemies(dt, fighting);
            RebuildTargets();
            _bullets.Tick(dt, _targets);

            foreach (GatePair g in _gates) g.Tick(dt);
            foreach (StripTile s in _strips) s.Tick(dt);
            foreach (Crate c in _crates) c.Tick(dt);

            Squad.UpdateVisuals(dt, advance > 0f && fighting);
            Cleanup();
            CheckEnd(dt);
        }

        /// <summary>Instanced drawing (bullets and blob shadows). Call once per frame.</summary>
        public void Render()
        {
            _bullets.Render();
            _shadows.Clear();
            Squad.CollectShadows(_shadows);
            foreach (Enemy e in _enemies) e.CollectShadow(_shadows);
            if (_shadows.Count == 0) return;
            _shadowBatch.Begin(GameAssets.GroundQuad, GameAssets.Shadow);
            Color shade = new Color(0f, 0f, 0f, 0.32f);
            foreach (Vector4 s in _shadows)
            {
                _shadowBatch.Add(Matrix4x4.TRS(new Vector3(s.x, s.y, s.z), Quaternion.identity, new Vector3(s.w, 1f, s.w)), shade);
            }
            _shadowBatch.End();
        }

        public void Dispose()
        {
            foreach (Enemy e in _enemies) e.Despawn();
            foreach (GatePair g in _gates) g.Dispose();
            foreach (StripTile s in _strips) s.Dispose();
            foreach (Crate c in _crates)
            {
                if (!c.Broken) c.Dispose();
            }
            _enemies.Clear();
            _gates.Clear();
            _strips.Clear();
            _crates.Clear();
            _bullets.Clear();
            Squad.Dispose();
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
                case SpawnKind.GatePair:
                    _gates.Add(new GatePair(_ctx, parent, ev, false));
                    break;
                case SpawnKind.ShootGatePair:
                    _gates.Add(new GatePair(_ctx, parent, ev, true));
                    break;
                case SpawnKind.Strip:
                    for (int i = 0; i < ev.count; i++)
                    {
                        _strips.Add(new StripTile(_ctx, parent, ev.x, ev.z + i * LevelGenerator.StripPitch, ev.width, ev.effect));
                    }
                    break;
                case SpawnKind.Army:
                    SpawnArmy(ev.enemy, ev.x, ev.z, ev.width, ev.rows, ev.columns, ev.hp, ev.laneLocked);
                    break;
                case SpawnKind.Crate:
                    var crate = new Crate(_ctx, parent, new Vector3(ev.x, 0f, ev.z), ev.hp, ev.effect);
                    crate.Opened = OnCrateOpened;
                    _crates.Add(crate);
                    break;
                case SpawnKind.Brute:
                    for (int i = 0; i < ev.count; i++)
                    {
                        float x = ev.count == 1 ? ev.x : (i == 0 ? -1.7f : 1.7f);
                        AddEnemy(EnemyKind.Brute, new Vector3(x, 0f, ev.z + i * 1.5f), ev.hp, ev.smash, false);
                    }
                    SpawnArmy(EnemyKind.Grunt, 0f, ev.z - 3f, 3.2f, 1, 4, Mathf.Max(1f, ev.hp * 0.04f), false);
                    break;
            }
        }

        void SpawnArmy(EnemyKind kind, float x, float z, float width, int rows, int columns, float hp, bool laneLocked)
        {
            float spacingX = columns > 1 ? Mathf.Min(0.8f, width / (columns - 1)) : 0f;
            const float spacingZ = 0.8f;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float px = x + (c - (columns - 1) * 0.5f) * spacingX + Random.Range(-0.08f, 0.08f);
                    float pz = z + r * spacingZ + Random.Range(-0.1f, 0.1f);
                    AddEnemy(kind, new Vector3(px, 0f, pz), hp, 1, laneLocked);
                }
            }
        }

        Enemy AddEnemy(EnemyKind kind, Vector3 position, float hp, int smash, bool laneLocked)
        {
            var enemy = new Enemy(_ctx, _root.transform, kind, position, hp, smash, laneLocked);
            enemy.Killed = OnEnemyKilled;
            _enemies.Add(enemy);
            return enemy;
        }

        void SpawnBoss()
        {
            _bossSpawned = true;
            float z = Def.length + Balance.BossArenaDistance;
            Boss = AddEnemy(EnemyKind.Boss, new Vector3(0f, 0f, z), Def.bossHp, Def.bossSmash, false);
            Boss.Held = true;
            int rows = Mathf.Max(1, Mathf.CeilToInt(Def.escortCount / 6f));
            int columns = Mathf.CeilToInt(Def.escortCount / (float)rows);
            SpawnArmy(EnemyKind.Grunt, 0f, z - 5.5f, 5.6f, rows, columns, Def.escortHp, false);
        }

        void EnterBossFight()
        {
            if (Phase != SessionPhase.Running) return;
            Phase = SessionPhase.BossFight;
            if (!_bossSpawned) SpawnBoss();
            Boss.Wake();
            _ctx.Camera.SetBossView(true);
            _ctx.Camera.Shake(0.35f);
            _ctx.Audio.Play(Sfx.Smash, 0.6f);
        }

        // --- Resolution ------------------------------------------------------------------

        void CheckGates()
        {
            foreach (GatePair pair in _gates)
            {
                if (pair.Done || Squad.PrevZ >= pair.Z || Squad.Z < pair.Z) continue;
                pair.Done = true;
                GateView chosen = pair.Pick(Squad.X);
                GateView other = chosen == pair.Left ? pair.Right : pair.Left;
                chosen.Consume(true);
                other.Consume(false);
                ApplyEffect(chosen.Effect, new Vector3(chosen.CenterX, 0f, chosen.Z));
            }
        }

        void CheckStrips()
        {
            foreach (StripTile tile in _strips)
            {
                if (tile.Collected || Squad.PrevZ >= tile.Z || Squad.Z < tile.Z) continue;
                if (!tile.Overlaps(Squad)) continue;
                tile.Collect();
                Squad.Add(tile.Effect.value, new Vector3(tile.X, 0f, tile.Z));
                _ctx.Labels.Popup(tile.Effect.Label, new Vector3(tile.X, 1.3f, tile.Z), GateView.GoodColor, 0.8f);
                _ctx.Audio.Play(Sfx.Coin, 0.45f, 0.08f, 0.05f);
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

        void ApplyEffect(GateEffect effect, Vector3 origin)
        {
            Squad.Apply(effect, origin);
            Color color = !effect.AffectsCount ? GateView.WeaponColor : effect.IsBeneficial ? GateView.GoodColor : GateView.BadColor;
            _ctx.Labels.Popup(effect.PopupText, Squad.Position + new Vector3(0f, 2.3f, 0.5f), color, effect.AffectsCount ? 1.3f : 1f, 1.1f);
            if (!effect.AffectsCount) _ctx.Audio.Play(Sfx.PowerUp);
            else _ctx.Audio.Play(effect.IsBeneficial ? Sfx.GateGood : Sfx.GateBad);
            if (!effect.IsBeneficial) _ctx.Camera.Shake(0.25f);
        }

        void OnCrateOpened(Crate crate, bool byBullets)
        {
            if (byBullets)
            {
                Coins += Balance.CrateCoins;
                ApplyEffect(crate.Reward, crate.Position);
            }
            else
            {
                int penalty = Mathf.Max(1, Mathf.CeilToInt(Squad.Count * 0.15f));
                Squad.Remove(penalty, crate.Position);
                _ctx.Labels.Popup("-" + penalty, crate.Position + Vector3.up * 1.5f, GateView.BadColor, 1f);
                _ctx.Camera.Shake(0.3f);
            }
        }

        void OnEnemyKilled(Enemy enemy)
        {
            int coins = enemy.Kind == EnemyKind.Boss ? Balance.BossCoins(Def.level) : enemy.Arch.Coins;
            Coins += coins;
            if (enemy.Arch.Melee)
            {
                _ctx.Camera.Shake(enemy.Kind == EnemyKind.Boss ? 0.8f : 0.4f);
                _ctx.Labels.Popup("+" + coins, enemy.Position + Vector3.up * 2.5f, GameContext.GoldColor, 0.9f);
                _ctx.Audio.Play(Sfx.Coin, 0.7f);
            }
        }

        bool Touching(Enemy e)
        {
            float dz = e.Position.z - Squad.Z;
            float reachZ = Squad.HalfDepth + e.Arch.Radius;
            if (dz > reachZ || dz < -reachZ - 0.5f) return false;
            return Mathf.Abs(e.Position.x - Squad.X) < Squad.HalfWidth + e.Arch.Radius;
        }

        void TickEnemies(float dt, bool fighting)
        {
            foreach (Enemy e in _enemies)
            {
                if (!e.IsAlive) continue;
                e.Tick(dt, Squad, fighting);
                if (!fighting || !e.IsAlive || Squad.Count == 0) continue;

                bool touching = Touching(e);
                if (e.Arch.Melee)
                {
                    if (e.TickMelee(dt, touching))
                    {
                        Vector3 hit = new Vector3(Squad.X, 0f, Squad.Z + Squad.HalfDepth);
                        Squad.Remove(e.Smash, hit);
                        _ctx.Fx.Burst(hit, new Color(0.75f, 0.7f, 0.62f), 10, 5f, 0.2f, 0.8f);
                        _ctx.Camera.Shake(e.Kind == EnemyKind.Boss ? 0.65f : 0.4f);
                        _ctx.Audio.Play(Sfx.Smash);
                        _ctx.Labels.Popup("-" + e.Smash, Squad.Position + new Vector3(0f, 2f, 0.5f), GateView.BadColor, 1f);
                    }
                }
                else if (touching)
                {
                    Squad.Remove(1, e.Position);
                    e.Die(true);
                }
            }
        }

        void RebuildTargets()
        {
            _targets.Clear();
            foreach (Enemy e in _enemies)
            {
                if (e.IsAlive && e.Position.z > Squad.Z - 1f) _targets.Add(e);
            }
            foreach (Crate c in _crates)
            {
                if (!c.Broken) _targets.Add(c);
            }
            foreach (GatePair g in _gates)
            {
                if (g.Done) continue;
                if (g.Left.Shootable) _targets.Add(g.Left);
                if (g.Right.Shootable) _targets.Add(g.Right);
            }
        }

        void Cleanup()
        {
            float behind = Squad.Z - Balance.DespawnBehind;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _enemies[i];
                if (e.IsAlive && e.Position.z < behind) e.Despawn();
                if (!e.IsAlive) _enemies.RemoveAt(i);
            }
            for (int i = _gates.Count - 1; i >= 0; i--)
            {
                GatePair g = _gates[i];
                if (g.Z < behind || (g.Left.Finished && g.Right.Finished))
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
                if (c.Broken)
                {
                    _crates.RemoveAt(i);
                }
                else if (c.Position.z < behind)
                {
                    c.Dispose();
                    _crates.RemoveAt(i);
                }
            }
        }

        void CheckEnd(float dt)
        {
            if (Phase == SessionPhase.Running || Phase == SessionPhase.BossFight)
            {
                if (Squad.Count <= 0)
                {
                    Phase = SessionPhase.Lost;
                    _endClock = 1.6f;
                    _ctx.Audio.Play(Sfx.Lose);
                }
                else if (Phase == SessionPhase.BossFight && Boss != null && !Boss.IsAlive)
                {
                    Phase = SessionPhase.Won;
                    _endClock = 2.2f;
                    Coins += Balance.VictoryCoins(Def.level);
                    foreach (Enemy e in _enemies)
                    {
                        if (e.IsAlive) e.Die(false);
                    }
                    Squad.Celebrate();
                    _ctx.Audio.Play(Sfx.Win);
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

        // --- Helpers for the auto pilot / tests ---------------------------------------------

        public GatePair NextGate()
        {
            GatePair best = null;
            foreach (GatePair g in _gates)
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

        public IReadOnlyList<Enemy> Enemies => _enemies;
    }
}
