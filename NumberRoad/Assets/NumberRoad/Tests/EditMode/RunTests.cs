using System.Collections.Generic;
using NumberRoad.Core;
using NUnit.Framework;
using UnityEngine;

namespace NumberRoad.Tests
{
    public class CombatTests
    {
        static EnemyType Enemy(string key)
        {
            foreach (EnemyType t in Enemies.All)
            {
                if (t.Key == key) return t;
            }
            Assert.Fail("no enemy " + key);
            return null;
        }

        [Test]
        public void Weakness_AndResistance_ScaleDamage()
        {
            Assert.AreEqual(Combat.WeakMultiplier, Combat.ElementMultiplier(ElementMask.Fire, ElementMask.None, Element.Fire), 1e-5f);
            Assert.AreEqual(Combat.ResistMultiplier, Combat.ElementMultiplier(ElementMask.None, ElementMask.Fire, Element.Fire), 1e-5f);
            Assert.AreEqual(1f, Combat.ElementMultiplier(ElementMask.Fire, ElementMask.Frost, Element.Physical), 1e-5f);
        }

        [Test]
        public void CarryOver_KeepsTheRest_UntilTooLittleIsLeft()
        {
            Assert.AreEqual(6f, Combat.CarryOver(10f, 4f, 10f), 1e-5f);
            Assert.AreEqual(0f, Combat.CarryOver(10f, 9f, 10f), "a tenth of the shot is below the minimum");
            Assert.AreEqual(0f, Combat.CarryOver(3f, 5f, 10f), "no kill, nothing carries over");
        }

        [Test]
        public void KillsPerHit_CountsEveryEnemyAStrongShotGoesThrough()
        {
            Assert.AreEqual(0.5f, Combat.KillsPerHit(1f, 2f), 1e-5f);
            Assert.AreEqual(1f, Combat.KillsPerHit(1f, 1f), 1e-5f);
            Assert.Less(Combat.KillsPerHit(1f, Balance.SwarmToughness), 1f, "a basic horde enemy takes more than an average hit");
            Assert.AreEqual(1f, Combat.KillsPerHit(1f, 1f), 1e-5f);
            Assert.GreaterOrEqual(Combat.KillsPerHit(1f, 0.4f), 2f, "a strong hit goes on through weaker ones");
            Assert.Greater(Combat.KillsPerHit(10f, 0.4f), 20f);
        }

        [Test]
        public void ShotsToKill_CountsShieldsRevivesSplitsAndFlyers()
        {
            EnemyType grunt = Enemy("grunt"), shield = Enemy("shield"), skeleton = Enemy("skeleton"), slime = Enemy("slime"), bat = Enemy("bat");
            Assert.AreEqual(1f, Combat.ShotsToKill(grunt, 1f, 1f, 1f, 1f, 1f), 1e-5f, "a plain enemy takes one even shot");
            Assert.AreEqual(0.5f, Combat.ShotsToKill(grunt, 1f, 1f, 2f, 1f, 1f), 1e-5f, "a piercing shot reaches two");

            float bare = 1f / Combat.KillsPerHit(1f, shield.Hp);
            Assert.AreEqual(bare + 3f, Combat.ShotsToKill(shield, 1f, 1f, 1f, 1f, 0f), 1e-4f, "bolts first break a shield of twice its HP");
            Assert.AreEqual(bare / 2f + 3f, Combat.ShotsToKill(shield, 1f, 1f, 2f, 1f, 0f), 1e-4f, "and do not pierce it");
            Assert.AreEqual(bare, Combat.ShotsToKill(shield, 1f, 1f, 1f, 0f, 0f), 1e-4f, "lobs and smites go over it");

            Assert.Greater(Combat.ShotsToKill(skeleton, 1f, 1f, 1f, 1f, 0f), 1f / Combat.KillsPerHit(1f, skeleton.Hp), "skeletons get back up");
            Assert.Greater(Combat.ShotsToKill(slime, 1f, 1f, 1f, 1f, 0f), 1f / Combat.KillsPerHit(1f, slime.Hp / Combat.ResistMultiplier), "slimes split");

            float seeking = Combat.ShotsToKill(bat, 1f, 1f, 1f, 1f, 0f);
            Assert.AreEqual(seeking / (1f - Combat.FlyerMiss), Combat.ShotsToKill(bat, 1f, 1f, 1f, 1f, 1f), 1e-4f, "straight bolts often miss bats");
        }

        [Test]
        public void Units_ThatGoOverShields_AreNotFrontal()
        {
            foreach (UnitClass u in Units.All)
            {
                bool over = u.Style == AttackStyle.Lob || u.Style == AttackStyle.Smite || u.Style == AttackStyle.Chain || u.Style == AttackStyle.Sweep;
                Assert.AreEqual(!over, u.Frontal, u.Name);
                if (u.MissesFlyers) Assert.IsTrue(u.Frontal, u.Name);
            }
            var bows = new Army(UnitLine.Bow, 10);
            Assert.AreEqual(1f, bows.DamageShare(u => u.Frontal), 1e-5f);
            Assert.AreEqual(0f, bows.DamageShare(u => u.MissesFlyers), 1e-5f, "archers seek");
        }

        [Test]
        public void Enemies_HaveWeaknesses_NeverBothWeakAndResistant()
        {
            int withWeakness = 0;
            foreach (EnemyType t in Enemies.All)
            {
                Assert.AreEqual(ElementMask.None, t.Weak & t.Resist, t.Key);
                if (t.Weak != ElementMask.None) withWeakness++;
            }
            Assert.Greater(withWeakness, Enemies.All.Length / 2, "most enemies have something they fear");
            foreach (BossType b in Enemies.Bosses)
            {
                Assert.AreEqual(ElementMask.None, b.Weak & b.Resist, b.Key);
                Assert.AreNotEqual(ElementMask.None, b.Weak, b.Key + " can be beaten faster with the right army");
            }
        }

        [Test]
        public void Armour_ShrugsOffArrows_ButLightningMeltsIt()
        {
            EnemyType knight = Enemy("knight");
            Assert.Less(knight.Multiplier(Element.Physical), 1f);
            Assert.Greater(knight.Multiplier(Element.Lightning), 1f);
            EnemyType skeleton = Enemy("skeleton");
            Assert.IsTrue(skeleton.Has(EnemyTrait.Undead));
            Assert.Greater(skeleton.Multiplier(Element.Holy), 1f);
            EnemyType fireImp = Enemy("fireimp");
            Assert.Less(fireImp.Multiplier(Element.Fire), 1f);
            Assert.Greater(fireImp.Multiplier(Element.Frost), 1f);
        }

        [Test]
        public void ElementFactor_FollowsTheArmysDamage()
        {
            var bows = new Army(UnitLine.Bow, 10);
            Assert.AreEqual(1f, bows.ElementFactor(e => 1f), 1e-5f);
            Assert.AreEqual(0.45f, bows.ElementFactor(e => e == Element.Physical ? 0.45f : 1.75f), 1e-5f);
            var mages = new Army(UnitLine.Magic, 10);
            Assert.AreEqual(1.75f, mages.ElementFactor(e => e == Element.Fire ? 1.75f : 1f), 1e-5f, "apprentices throw fire");
            Assert.AreEqual(1f, new Army().ElementFactor(e => 2f), 1e-5f, "an empty army changes nothing");
        }

        [Test]
        public void UnitClasses_FightInDifferentWays()
        {
            var styles = new HashSet<AttackStyle>();
            var elements = new HashSet<Element>();
            foreach (UnitClass u in Units.All)
            {
                styles.Add(u.Style);
                elements.Add(u.Element);
                Assert.Greater(u.CrowdFactor, 0.99f, u.Name);
            }
            Assert.AreEqual(8, styles.Count, "every attack style is used");
            Assert.AreEqual(5, elements.Count, "every element is used");
            // the four lines each have a style nobody else has
            Assert.AreEqual(AttackStyle.Seek, Units.First(UnitLine.Bow).Style);
            Assert.AreEqual(AttackStyle.Sweep, Units.All[(int)UnitLine.Guard * Units.Tiers + 1].Style);
            Assert.AreEqual(AttackStyle.Lob, Units.First(UnitLine.Magic).Style);
            Assert.AreEqual(AttackStyle.Smite, Units.First(UnitLine.Holy).Style);
        }
    }

    public class BuffTests
    {
        sealed class FakeTarget : IBuffTarget
        {
            public int Soldiers { get; set; } = 100;
            public int Fallen { get; set; } = 20;
            public bool CanClassUp { get; set; } = true;
            public int Added;
            public int Revived;
            public float Lost;
            public int ClassUps;
            public float Repaired;
            public float BaseHealth { get; set; } = 0.5f;

            public bool LineUnlocked(UnitLine line) => true;

            public void AddSoldiers(int n, UnitLine? line)
            {
                Added += n;
                Soldiers += n;
            }

            public void Revive(int n) => Revived += n;
            public void LoseShare(float share) => Lost += share;

            public bool ClassUp()
            {
                ClassUps++;
                return true;
            }

            public void RepairBase(float share) => Repaired += share;

            public bool Touched => Added > 0 || Revived > 0 || Lost > 0f || ClassUps > 0 || Repaired > 0f;
        }

        static string Snapshot(RunMods m)
        {
            var sb = new System.Text.StringBuilder();
            foreach (System.Reflection.FieldInfo f in typeof(RunMods).GetFields())
            {
                if (f.Name == nameof(RunMods.Taken)) continue;
                sb.Append(f.Name).Append('=').Append(f.GetValue(m)).Append(';');
            }
            return sb.ToString();
        }

        [Test]
        public void FiftyBuffs_InSixKinds_WithUniqueIds()
        {
            Assert.GreaterOrEqual(Buffs.All.Count, 50);
            var ids = new HashSet<string>();
            var kinds = new HashSet<BuffKind>();
            foreach (Buff b in Buffs.All)
            {
                Assert.IsTrue(ids.Add(b.Id), b.Id);
                kinds.Add(b.Kind);
                Assert.IsNotEmpty(b.Name);
                Assert.IsNotEmpty(b.Description);
                Assert.AreSame(b, Buffs.Get(b.Id));
            }
            Assert.AreEqual(6, kinds.Count);
            Assert.IsNull(Buffs.Get("nope"));
        }

        [Test]
        public void EveryBuff_DoesSomething()
        {
            foreach (Buff b in Buffs.All)
            {
                var mods = new RunMods();
                var target = new FakeTarget();
                string before = Snapshot(mods);
                Assert.IsTrue(b.Usable(mods, target), b.Id);
                b.Apply(mods, target);
                Assert.IsTrue(Snapshot(mods) != before || target.Touched, b.Id + " changed nothing");
                Assert.AreEqual(1, mods.CountOf(b), b.Id);
            }
        }

        [Test]
        public void Roll_DealsDifferentUsableCards()
        {
            var mods = new RunMods();
            var target = new FakeTarget();
            var rng = new Rng(7);
            for (int i = 0; i < 200; i++)
            {
                Buff[] cards = Buffs.Roll(rng, 3, mods, target);
                Assert.AreEqual(3, cards.Length);
                Assert.AreEqual(3, new HashSet<Buff>(cards).Count, "no card twice in one deal");
                foreach (Buff b in cards) Assert.IsTrue(b.Usable(mods, target), b.Id);
                cards[i % 3].Apply(mods, target);
            }
        }

        [Test]
        public void StackLimits_AndConditions_AreKept()
        {
            var mods = new RunMods();
            var target = new FakeTarget { Fallen = 0, CanClassUp = false };
            Assert.IsFalse(Buffs.Get("medic").Usable(mods, target), "nobody to heal");
            Assert.IsFalse(Buffs.Get("promotion").Usable(mods, target), "everyone is at the top class");
            target.BaseHealth = 1f;
            Assert.IsFalse(Buffs.Get("repair").Usable(mods, target), "the war wagon is not damaged");
            Buff frost = Buffs.Get("frost");
            frost.Apply(mods, target);
            Assert.IsFalse(frost.Usable(mods, target), "one per run");
            Buff sharp = Buffs.Get("sharp");
            for (int i = 0; i < 10; i++) sharp.Apply(mods, target);
            Assert.IsTrue(sharp.Usable(mods, target), "plain power buffs stack freely");
            Assert.AreEqual(Mathf.Pow(1.2f, 10f), mods.Damage, 1e-3f);
        }

        [Test]
        public void RareCards_ShowUpLessOften()
        {
            var seen = new Dictionary<BuffRarity, int>();
            var total = new Dictionary<BuffRarity, int>();
            foreach (Buff b in Buffs.All) total[b.Rarity] = (total.TryGetValue(b.Rarity, out int n) ? n : 0) + 1;
            var rng = new Rng(3);
            var target = new FakeTarget();
            for (int i = 0; i < 6000; i++)
            {
                foreach (Buff b in Buffs.Roll(rng, 1, new RunMods(), target)) seen[b.Rarity] = (seen.TryGetValue(b.Rarity, out int n) ? n : 0) + 1;
            }
            float common = seen[BuffRarity.Common] / (float)total[BuffRarity.Common];
            float rare = seen[BuffRarity.Rare] / (float)total[BuffRarity.Rare];
            float epic = seen[BuffRarity.Epic] / (float)total[BuffRarity.Epic];
            Assert.Greater(common, rare * 1.5f);
            Assert.Greater(rare, epic * 1.5f);
        }

        [Test]
        public void ComboBonus_IsCapped()
        {
            var mods = new RunMods { ComboDamage = 0.02f };
            Assert.AreEqual(1f, mods.ComboFactor(5), 1e-5f);
            Assert.AreEqual(1.02f, mods.ComboFactor(10), 1e-5f);
            Assert.AreEqual(1.6f, mods.ComboFactor(100000), 1e-5f);
        }

        [Test]
        public void Summary_ListsTakenBuffs_WithCounts()
        {
            var mods = new RunMods();
            var target = new FakeTarget();
            Assert.AreEqual(string.Empty, Buffs.Summary(mods));
            Buffs.Get("sharp").Apply(mods, target);
            Buffs.Get("flame").Apply(mods, target);
            Buffs.Get("sharp").Apply(mods, target);
            Assert.AreEqual("SHARP TIPS x2, FLAME ROUNDS", Buffs.Summary(mods));
        }
    }

    public class WaveTests
    {
        [Test]
        public void Levels_AreSplitIntoWaves([NUnit.Framework.Range(1, 64, 7)] int level)
        {
            LevelDefinition def = LevelGenerator.Generate(level);
            Assert.That(def.waves, Is.InRange(3, 6));
            Assert.AreEqual(def.waves - 1, def.waveEnds.Count);
            float previous = 0f;
            int wave = 0;
            var hordes = new int[def.waves];
            foreach (SpawnEvent ev in def.events)
            {
                while (wave < def.waveEnds.Count && ev.z > def.waveEnds[wave]) wave++;
                if (ev.kind == SpawnKind.Horde) hordes[wave]++;
            }
            foreach (float end in def.waveEnds)
            {
                Assert.Greater(end, previous + 20f, "every wave is long enough to fight");
                Assert.Less(end, def.length);
                previous = end;
            }
            for (int w = 0; w < def.waves; w++) Assert.Greater(hordes[w], 0, $"wave {w + 1} has a horde");
        }

        [Test]
        public void LaterLevels_HaveMoreWaves()
        {
            Assert.AreEqual(3, LevelGenerator.Generate(1).waves);
            Assert.Greater(LevelGenerator.Generate(30).waves, 3);
        }

        [Test]
        public void FirstLevel_AlreadyHasAHugeHorde()
        {
            Assert.GreaterOrEqual(Balance.HordeSize(1), 100);
            Assert.GreaterOrEqual(LevelGenerator.Generate(1).hordeTotal, 300, "several big hordes from the start");
        }
    }

    public class EndlessTests
    {
        [Test]
        public void EveryBoss_MakesTheEnemiesOnePercentStronger()
        {
            Assert.AreEqual(1f, EndlessRun.Power(0), 1e-6f);
            Assert.AreEqual(1.01f, EndlessRun.Power(1), 1e-5f);
            Assert.AreEqual(Mathf.Pow(1.01f, 10f), EndlessRun.Power(10), 1e-4f);
            Assert.AreEqual(1f, EndlessRun.Power(-3), 1e-6f);
        }

        [Test]
        public void Stages_FollowOnFromTheBestLevel_AndAddUp()
        {
            var run = new EndlessRun(7, 123, 2);
            Assert.AreEqual(1, run.Stage);
            Assert.AreEqual(7, run.Level);
            Assert.AreEqual(0, run.PowerPercent);
            int firstSalt = run.Salt;
            run.StageCleared(100, 50, 40, 12);
            run.StageCleared(300, 80, 90, 8);
            Assert.AreEqual(3, run.Stage);
            Assert.AreEqual(9, run.Level);
            Assert.AreEqual(2, run.Bosses);
            Assert.AreEqual(400, run.Kills);
            Assert.AreEqual(130, run.Coins);
            Assert.AreEqual(90, run.Peak);
            Assert.AreEqual(12, run.BestCombo);
            Assert.AreEqual(2, run.PowerPercent);
            Assert.AreNotEqual(firstSalt, run.Salt, "every stage gets its own road");
            Assert.IsFalse(run.NewBest, "two bosses only ties the record");
            run.StageCleared(0, 0, 0, 0);
            Assert.IsTrue(run.NewBest);
            Assert.AreEqual(1, new EndlessRun(0, 1, 0).Level, "starts at level 1 at the earliest");
        }

        [Test]
        public void NextStage_IsSizedForTheArmyThatArrives()
        {
            LevelDefinition fresh = LevelGenerator.Generate(5);
            var big = new Army(UnitLine.Bow, 2000);
            LevelDefinition strong = LevelGenerator.Generate(5, false, 0, big, WeaponStats.Create(2f, 2f));
            Assert.Greater(strong.bossHp, fresh.bossHp * 5f);
            Assert.Greater(strong.escortHp, fresh.escortHp * 5f);
            Assert.AreEqual(fresh.waves, strong.waves, "the same road plan");
            Assert.AreEqual(2000, big.Total, "the army passed in is left alone");
        }

        [Test]
        public void Score_RewardsBossesBeaten()
        {
            Assert.Greater(Ranking.Score(100, 50, 10, 5, false, 0, 3), Ranking.Score(100, 50, 10, 5, false, 0));
        }

        [Test]
        public void SpeedSettings_AreClamped_AndSurviveAReset()
        {
            var fresh = new SaveData();
            Assert.AreEqual(1, fresh.gameSpeed);
            Assert.IsFalse(fresh.fastMarch);
            Assert.AreEqual(1, SaveSystem.FromJson("{\"level\":3}").gameSpeed, "older saves play at normal speed");

            var save = new SaveData { gameSpeed = 9, fastMarch = true, endlessBest = 4 };
            save.Sanitize();
            Assert.AreEqual(2, save.gameSpeed);
            SaveData copy = SaveSystem.FromJson(SaveSystem.ToJson(save));
            Assert.AreEqual(2, copy.gameSpeed);
            Assert.IsTrue(copy.fastMarch);
            Assert.AreEqual(4, copy.endlessBest);
            save.ResetProgress();
            Assert.AreEqual(2, save.gameSpeed, "settings survive a reset");
            Assert.IsTrue(save.fastMarch);
            Assert.AreEqual(0, save.endlessBest, "records go with the progress");
        }
    }

    public class WarWagonTests
    {
        [Test]
        public void BaseHp_GrowsWithTheHordes_AndTheUpgradeToughensIt()
        {
            Assert.Greater(Balance.BaseHp(1), Balance.HordeSize(1) * 0.3f * 0.5f, "half a lane of enemies does not wreck it");
            Assert.LessOrEqual(Balance.BaseHp(1), Balance.HordeSize(1), "a whole horde does");
            Assert.Greater(Balance.BaseHp(20), Balance.BaseHp(1) * 3f);
            Assert.AreEqual(1f, Balance.BaseToughness(0), 1e-5f);
            Assert.Greater(Balance.BaseToughness(5), Balance.BaseToughness(4));
        }

        [Test]
        public void OldVolleyUpgrade_CarriesOverToTheBase()
        {
            SaveData save = SaveSystem.FromJson("{\"level\":4,\"bestLevel\":4,\"skillLevel\":3}");
            Assert.AreEqual(3, save.GetUpgrade(UpgradeKind.Base));
            Assert.AreEqual(0, save.skillLevel);
            save.Sanitize();
            Assert.AreEqual(3, save.GetUpgrade(UpgradeKind.Base), "moved over once, not twice");
        }

        [Test]
        public void VolleyIsGone_FromTheCards()
        {
            foreach (Buff b in Buffs.All)
            {
                StringAssert.DoesNotContain("VOLLEY", b.Description, b.Id);
            }
            Assert.IsNotNull(Buffs.Get("repair"));
            Assert.IsNotNull(Buffs.Get("plating"));
        }
    }

    public class LocTests
    {
        [Test]
        public void EveryNameAndDescription_HasJapanese()
        {
            var missing = new List<string>();

            void Need(string en)
            {
                if (!Loc.Translated(en)) missing.Add(en);
            }

            foreach (UnitClass u in Units.All)
            {
                Need(u.Name);
                Need(u.Description);
            }
            foreach (EnemyType t in Enemies.All)
            {
                Need(t.Name);
                Need(t.Description);
            }
            foreach (BossType b in Enemies.Bosses)
            {
                Need(b.Name);
                Need(b.Description);
            }
            foreach (Buff b in Buffs.All)
            {
                Need(b.Name);
                Need(b.Description);
                Need(Buffs.KindName(b.Kind));
            }
            foreach (Biome b in Biomes.All) Need(b.Name);
            foreach (Achievement a in Achievements.All) Need(a.Name);
            for (int l = 0; l < Units.Lines; l++) Need(Units.LineName((UnitLine)l));
            foreach (AttackStyle s in System.Enum.GetValues(typeof(AttackStyle))) Need(Units.StyleName(s));
            foreach (Element e in System.Enum.GetValues(typeof(Element))) Need(Units.ElementName(e));
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        [Test]
        public void Japanese_FillsThePlaceholders_AndFallsBackToEnglish()
        {
            Language before = Loc.Language;
            try
            {
                Loc.Language = Language.Japanese;
                Assert.AreEqual("レベル 7", Loc.F("LEVEL {0}", 7));
                Assert.AreEqual("not in the table", Loc.T("not in the table"));
                Assert.AreEqual("x1.4", Loc.Upper("x1.4"), "Japanese is not upper-cased");
                Assert.AreEqual("討伐者 II", Achievements.TitleOf(Achievements.All[1]));
                Loc.Language = Language.English;
                Assert.AreEqual("LEVEL 7", Loc.F("LEVEL {0}", 7));
                Assert.AreEqual("X1.4", Loc.Upper("x1.4"));
                Assert.AreEqual("SLAYER II", Achievements.TitleOf(Achievements.All[1]));
            }
            finally
            {
                Loc.Language = before;
            }
        }

        [Test]
        public void NewSaves_AreJapanese_AndTheChoiceSurvivesAReset()
        {
            Assert.AreEqual((int)Language.Japanese, new SaveData().language);
            var save = new SaveData { language = (int)Language.English, level = 9, bestLevel = 9 };
            save.ResetProgress();
            Assert.AreEqual((int)Language.English, save.language);
            Assert.That(SaveSystem.FromJson("{\"language\":7}").language, Is.InRange(0, 1), "an unknown language falls back to one the game has");
        }
    }

    public class EconomyTests
    {
        [Test]
        public void KillValue_StartsSmall_AndGrows()
        {
            Assert.That(Balance.KillValue(1), Is.InRange(0.05f, 0.2f), "a fraction of a coin per kill at first");
            float previous = 0f;
            for (int level = 1; level <= 60; level++)
            {
                float v = Balance.KillValue(level);
                Assert.GreaterOrEqual(v, previous);
                Assert.LessOrEqual(v, 1f);
                previous = v;
            }
            // the kills of a whole level still pay more and more
            Assert.Greater(Balance.KillValue(10) * LevelGenerator.Generate(10).hordeTotal, Balance.KillValue(1) * LevelGenerator.Generate(1).hordeTotal * 3f);
        }
    }

    public class RewardTests
    {
        [Test]
        public void ClaimAll_CollectsEverythingReady_Once()
        {
            var save = new SaveData { statKills = 600 };
            const int today = 100;
            int awards = Achievements.ReadyCount(save);
            Assert.Greater(awards, 0);
            Assert.AreEqual(awards + 2, Rewards.ReadyCount(save, today), "awards, daily bonus and lucky draw");

            RewardSummary s = Rewards.ClaimAll(save, today, () => UpgradeKind.Power);
            Assert.IsTrue(s.Any);
            Assert.IsTrue(s.Login);
            Assert.IsTrue(s.Lucky);
            Assert.AreEqual(awards, s.Awards);
            Assert.AreEqual(3, s.Lines.Count);
            Assert.Greater(s.Coins + s.Gems, 0);
            Assert.AreEqual(0, Rewards.ReadyCount(save, today));

            int coins = save.coins, gems = save.gems;
            RewardSummary again = Rewards.ClaimAll(save, today, () => UpgradeKind.Power);
            Assert.IsFalse(again.Any);
            Assert.AreEqual(coins, save.coins);
            Assert.AreEqual(gems, save.gems);
            Assert.AreEqual(2, Rewards.ReadyCount(save, today + 1), "tomorrow brings a new bonus and draw");
        }

        [Test]
        public void ClaimAll_WithNothingReady_ChangesNothing()
        {
            var save = new SaveData { lastLoginRewardDay = 5, lastLuckyDay = 5 };
            RewardSummary s = Rewards.ClaimAll(save, 5, () => UpgradeKind.Power);
            Assert.IsFalse(s.Any);
            Assert.AreEqual(0, s.Coins);
            Assert.IsEmpty(s.Lines);
        }
    }
}
