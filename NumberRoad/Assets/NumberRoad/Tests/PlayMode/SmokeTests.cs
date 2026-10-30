using System.Collections;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NumberRoad.Tests
{
    /// <summary>Boots the real game, lets the auto pilot play for a while and checks nothing breaks.</summary>
    public class SmokeTests
    {
        GameRoot _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = GameRoot.Instance != null ? GameRoot.Instance : new GameObject("Game").AddComponent<GameRoot>();
            _root.Save.gameSpeed = 1;
            _root.Save.fastMarch = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null)
            {
                _root.TimeScale = 1f;
                Object.Destroy(_root.gameObject);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Game_BootsToTitle_ThenAutoPilotAdvancesTheSquad()
        {
            Assert.AreEqual(GameState.Title, _root.State);
            Assert.NotNull(_root.Session);

            _root.AutoPlay = true;
            _root.Play();
            Assert.AreEqual(GameState.Playing, _root.State);

            float startZ = _root.Session.Squad.Z;
            _root.TimeScale = 2f;
            float elapsed = 0f;
            while (elapsed < 8f && _root.State == GameState.Playing)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Greater(_root.Session.Squad.Z, startZ + 5f, "the squad should march forward");
            Assert.GreaterOrEqual(_root.Session.Squad.Count, 0);
            Assert.Greater(_root.Session.Stats.Kills, 0, "the squad should have shot something");
        }

        [UnityTest]
        public IEnumerator WaveEnd_StopsTheRoad_UntilACardIsPicked()
        {
            _root.AutoPlay = true;
            _root.Play();
            LevelSession s = _root.Session;
            Assert.Greater(s.Def.waveEnds.Count, 0);
            _root.TimeScale = 4f;
            // the bot plays up to just before the first wave ends, then the "player" takes over
            float clock = 0f;
            while (s.Squad.Z < s.Def.waveEnds[0] - 4f && _root.State == GameState.Playing && clock < 60f)
            {
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            _root.AutoPlay = false;
            while (!s.ChoosingBuff && _root.State == GameState.Playing && clock < 70f)
            {
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(GameState.Playing, _root.State, "the squad should survive the first wave");
            Assert.IsTrue(s.ChoosingBuff, "three cards are dealt at the end of a wave");
            Assert.AreEqual(3, s.Options.Length);

            float z = s.Squad.Z;
            int count = s.Squad.Count;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(z, s.Squad.Z, "the road waits for the pick");
            Assert.AreEqual(count, s.Squad.Count, "nothing hurts the squad meanwhile");

            Buff picked = s.Options[0];
            _root.ChooseBuff(0);
            Assert.IsFalse(s.ChoosingBuff);
            Assert.AreEqual(1, s.Wave);
            Assert.AreEqual(1, s.Mods.CountOf(picked));
            for (int i = 0; i < 20; i++) yield return null;
            Assert.Greater(s.Squad.Z, z, "the march goes on");
        }

        [UnityTest]
        public IEnumerator SpeedButton_DoublesTheGameSpeed()
        {
            _root.Play();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 0.01f);
            _root.ToggleSpeed();
            yield return null;
            Assert.AreEqual(2f, Time.timeScale, 0.01f);
            _root.Pause();
            Assert.AreEqual(0f, Time.timeScale);
            _root.Resume();
            yield return null;
            Assert.AreEqual(2f, Time.timeScale, 0.01f, "still x2 after a pause");
            _root.ToggleSpeed();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 0.01f);
        }

        [UnityTest]
        public IEnumerator FastMarch_HurriesAcrossAnEmptyRoad()
        {
            _root.ToggleFastMarch();
            Assert.IsTrue(_root.Save.fastMarch);
            _root.AutoPlay = true;
            _root.Play();
            LevelSession s = _root.Session;
            Assert.IsTrue(s.FastMarch);
            float clock = 0f;
            bool rushed = false;
            while (clock < 8f && !rushed && _root.State == GameState.Playing)
            {
                clock += Time.unscaledDeltaTime;
                rushed |= s.Rushing;
                yield return null;
            }
            Assert.IsTrue(rushed, "the squad hurries while nothing is near");
            _root.ToggleFastMarch();
            Assert.IsFalse(s.FastMarch);
        }

        /// <summary>An endless run: the bot clears the first stage and the same army marches on into a stronger second one.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Endless_ClearedStage_LeadsIntoAStrongerNextStage()
        {
            _root.AutoPlay = true;
            _root.Save.fastMarch = true;
            _root.PlayEndless();
            Assert.NotNull(_root.Endless);
            LevelSession first = _root.Session;
            Assert.AreEqual(1, first.EndlessStage);
            Assert.AreEqual(1f, first.EnemyPower, 1e-5f);
            _root.TimeScale = 4f;
            float clock = 0f;
            while (_root.Session == first && _root.State == GameState.Playing && clock < 240f)
            {
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_root.State == GameState.Result) Assert.Inconclusive("the bot lost the first stage");
            Assert.AreNotSame(first, _root.Session, "a new stage began");
            LevelSession second = _root.Session;
            Assert.AreEqual(2, second.EndlessStage);
            Assert.AreEqual(1.01f, second.EnemyPower, 1e-4f);
            Assert.AreEqual(first.Def.level + 1, second.Def.level);
            Assert.AreSame(first.Mods, second.Mods, "the buffs carry on");
            Assert.AreEqual(first.Squad.Count, second.Squad.Count, "so does the army");
            Assert.AreEqual(GameState.Playing, _root.State);
            Assert.AreEqual(1, _root.Endless.Bosses);
            _root.Home();
            Assert.IsNull(_root.Endless);
        }

        [UnityTest]
        public IEnumerator PauseAndResume_FreezeTime()
        {
            _root.Play();
            yield return null;
            _root.Pause();
            Assert.AreEqual(GameState.Paused, _root.State);
            Assert.AreEqual(0f, Time.timeScale);
            float z = _root.Session.Squad.Z;
            yield return null;
            yield return null;
            Assert.AreEqual(z, _root.Session.Squad.Z, "nothing moves while paused");
            _root.Resume();
            Assert.AreEqual(GameState.Playing, _root.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        /// <summary>
        /// Plays a fresh save from level 1 like a player would: the auto pilot steers, the coins buy the cheapest
        /// combat upgrade between runs, and a lost level is retried (up to 3 tries). Logs every run (balance check;
        /// not run by default). The real save is restored afterwards.
        /// </summary>
        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator BalanceSweep()
        {
            var report = new System.Text.StringBuilder("[NumberRoad] balance sweep\n");
            string backup = PlayerPrefs.GetString(SaveSystem.Key, string.Empty);
            SaveData save = _root.Save;
            JsonUtility.FromJsonOverwrite(SaveSystem.ToJson(new SaveData()), save);
            _root.AutoPlay = true;
            _root.TimeScale = 4f;
            UpgradeKind[] combat = { UpgradeKind.Squad, UpgradeKind.Power, UpgradeKind.Rate, UpgradeKind.Crit, UpgradeKind.Armor, UpgradeKind.Income };
            int runs = 0;
            while (save.level <= 20 && runs < 45)
            {
                int level = save.level;
                for (int attempt = 1; attempt <= 3 && save.level == level; attempt++)
                {
                    runs++;
                    _root.ShowTitle();
                    _root.Play();
                    float clock = 0f;
                    while (_root.State == GameState.Playing && clock < 150f)
                    {
                        clock += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    LevelSession s = _root.Session;
                    RunResult r = _root.LastResult;
                    RunStats st = s.Stats;
                    string line = $"L{level}#{attempt}: {(r != null && r.Won ? "WIN " : "LOSS")} wave {s.Wave + 1}/{s.Waves} peak {Fmt.Num(s.Squad.Peak)} left {Fmt.Num(s.Squad.Count)} " +
                                      $"(ref {Fmt.Num((long)s.Def.expectedCount)}) kills {st.Kills} coins {r?.TotalCoins} at {s.Progress:0.00} bossHp {s.BossHpLeft:0.00} " +
                                      $"{s.Elapsed:0}s lost contact {st.LostContact} melee {st.LostMelee} shots {st.LostShots} boss {st.LostBoss} gates {st.LostGates} " +
                                      $"upgrades {save.squadLevel}/{save.powerLevel}/{save.rateLevel}/{save.critLevel}/{save.armorLevel}/{save.incomeLevel} " +
                                      $"buffs [{Buffs.Summary(s.Mods)}]";
                    report.AppendLine(line);
                    Debug.Log("[NumberRoad] sweep " + line);
                    _root.Home();
                    // spend like a player: always the cheapest combat upgrade
                    while (true)
                    {
                        UpgradeKind best = combat[0];
                        foreach (UpgradeKind k in combat)
                        {
                            if (save.UpgradeCost(k) < save.UpgradeCost(best)) best = k;
                        }
                        if (!save.TryBuy(best)) break;
                    }
                }
                if (save.level == level) save.CompleteLevel();
            }
            Debug.Log(report.ToString());
            PlayerPrefs.SetString(SaveSystem.Key, backup);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// One endless run with the auto pilot, starting at level 5 with the upgrades that level expects.
        /// Logs every stage (army, buffs, enemy power) and where the run ended (balance check; not run by default).
        /// </summary>
        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator EndlessSweep()
        {
            const int level = 5;
            var report = new System.Text.StringBuilder("[NumberRoad] endless sweep\n");
            string backup = PlayerPrefs.GetString(SaveSystem.Key, string.Empty);
            SaveData save = _root.Save;
            JsonUtility.FromJsonOverwrite(SaveSystem.ToJson(new SaveData { level = level, bestLevel = level }), save);
            int upgrades = Mathf.FloorToInt(LevelGenerator.ReferenceUpgrades(level));
            foreach (UpgradeKind k in (UpgradeKind[])System.Enum.GetValues(typeof(UpgradeKind))) save.SetUpgrade(k, upgrades);
            _root.AutoPlay = true;
            _root.TimeScale = 4f;
            _root.ShowTitle();
            _root.PlayEndless();
            LevelSession current = null;
            float clock = 0f;
            while (_root.State == GameState.Playing && _root.Endless != null && _root.Endless.Stage <= 15 && clock < 1500f)
            {
                if (_root.Session != current)
                {
                    current = _root.Session;
                    string line = $"stage {current.EndlessStage} (L{current.Def.level}, {current.Waves} waves) power x{current.EnemyPower:0.00} " +
                                  $"army {Fmt.Num(current.Squad.Count)} buffs {current.Mods.Taken.Count} at {clock:0}s";
                    report.AppendLine(line);
                    Debug.Log("[NumberRoad] endless " + line);
                }
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            LevelSession s = _root.Session;
            report.AppendLine($"end: stage {s.EndlessStage} bosses {_root.Endless?.Bosses} {_root.State} wave {s.Wave + 1}/{s.Waves} at {s.Progress:0.00} " +
                              $"army {Fmt.Num(s.Squad.Count)} peak {Fmt.Num(s.Squad.Peak)} lost contact {s.Stats.LostContact} melee {s.Stats.LostMelee} " +
                              $"shots {s.Stats.LostShots} boss {s.Stats.LostBoss} gates {s.Stats.LostGates} buffs [{Buffs.Summary(s.Mods)}] {clock:0}s");
            Debug.Log(report.ToString());
            PlayerPrefs.SetString(SaveSystem.Key, backup);
            PlayerPrefs.Save();
        }
    }
}
