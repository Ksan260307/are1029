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
                    string line = $"L{level}#{attempt}: {(r != null && r.Won ? "WIN " : "LOSS")} peak {Fmt.Num(s.Squad.Peak)} left {Fmt.Num(s.Squad.Count)} " +
                                      $"(ref {Fmt.Num((long)s.Def.expectedCount)}) kills {st.Kills} coins {r?.TotalCoins} at {s.Progress:0.00} bossHp {s.BossHpLeft:0.00} " +
                                      $"{s.Elapsed:0}s lost contact {st.LostContact} melee {st.LostMelee} shots {st.LostShots} boss {st.LostBoss} gates {st.LostGates} " +
                                      $"upgrades {save.squadLevel}/{save.powerLevel}/{save.rateLevel}/{save.critLevel}/{save.armorLevel}/{save.incomeLevel}";
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
    }
}
