using System;
using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>
    /// How many soldiers of each <see cref="UnitClass"/> the squad has. Pure logic (no Unity objects),
    /// so gate maths, class-ups and the damage model are unit tested.
    /// </summary>
    public sealed class Army
    {
        readonly int[] _counts = new int[Units.Count];

        // scratch buffers for Distribute / VisiblePerClass (called on every hit in big fights; single-threaded)
        static readonly int[] Shares = new int[Units.Count];
        static readonly double[] Remainders = new double[Units.Count];

        public int Total { get; private set; }
        /// <summary>Largest total reached this run (revives and heals never exceed it).</summary>
        public int Peak { get; private set; }
        public int ClassUps { get; private set; }

        public Army() { }

        public Army(UnitLine line, int count)
        {
            Add(Units.First(line).Id, count);
        }

        public Army Clone()
        {
            var copy = new Army();
            Array.Copy(_counts, copy._counts, _counts.Length);
            copy.Total = Total;
            copy.Peak = Peak;
            copy.ClassUps = ClassUps;
            return copy;
        }

        public int this[int classId] => _counts[classId];

        public int CountOf(UnitClass unit) => _counts[unit.Id];

        void Recount()
        {
            long total = 0;
            foreach (int c in _counts) total += c;
            Total = (int)Math.Min(Balance.MaxSquad, total);
            Peak = Math.Max(Peak, Total);
        }

        /// <summary>Adds soldiers of one class (recruits, revives).</summary>
        public int Add(int classId, int n)
        {
            n = Mathf.Clamp(n, 0, Balance.MaxSquad - Total);
            _counts[classId] += n;
            Recount();
            return n;
        }

        /// <summary>New soldiers join with the squad's current mix of classes.</summary>
        public int AddMixed(int n, UnitLine fallback)
        {
            n = Mathf.Clamp(n, 0, Balance.MaxSquad - Total);
            if (n == 0) return 0;
            if (Total == 0) return Add(Units.First(fallback).Id, n);
            Distribute(n, +1);
            return n;
        }

        /// <summary>Removes soldiers spread over the classes in proportion. Returns how many were removed.</summary>
        public int Remove(int n)
        {
            n = Mathf.Clamp(n, 0, Total);
            if (n == 0) return 0;
            if (n >= Total)
            {
                Array.Clear(_counts, 0, _counts.Length);
                Recount();
                return n;
            }
            Distribute(n, -1);
            return n;
        }

        /// <summary>Largest-remainder split of n soldiers over the current classes (sign +1 adds, -1 removes).</summary>
        void Distribute(int n, int sign)
        {
            int total = Total;
            int[] shares = Shares;
            double[] remainders = Remainders;
            Array.Clear(shares, 0, shares.Length);
            Array.Clear(remainders, 0, remainders.Length);
            int assigned = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                if (_counts[i] == 0) continue;
                double exact = (double)n * _counts[i] / total;
                shares[i] = (int)Math.Floor(exact);
                remainders[i] = exact - shares[i];
                assigned += shares[i];
            }
            while (assigned < n)
            {
                int best = -1;
                for (int i = 0; i < Units.Count; i++)
                {
                    if (_counts[i] == 0) continue;
                    if (sign < 0 && shares[i] >= _counts[i]) continue;
                    if (best < 0 || remainders[i] > remainders[best]) best = i;
                }
                if (best < 0) break;
                shares[best]++;
                remainders[best] = -1;
                assigned++;
            }
            for (int i = 0; i < Units.Count; i++)
            {
                _counts[i] = Math.Max(0, _counts[i] + sign * Math.Min(shares[i], sign < 0 ? _counts[i] : int.MaxValue));
            }
            Recount();
        }

        /// <summary>Applies a gate. Returns the change in soldiers (negative when some were lost).</summary>
        public int Apply(GateEffect effect, UnitLine fallback, float recruitBonus = 1f)
        {
            int before = Total;
            switch (effect.kind)
            {
                case GateKind.Add:
                    if (effect.value >= 0) AddMixed(Mathf.RoundToInt(effect.value * recruitBonus), fallback);
                    else Remove(-effect.value);
                    break;
                case GateKind.Subtract:
                    Remove(effect.value);
                    break;
                case GateKind.Multiply:
                    if (effect.value <= 0) Remove(Total);
                    else AddMixed(effect.ApplyToCount(Total) - Total, fallback);
                    break;
                case GateKind.Divide:
                    Remove(Total - effect.ApplyToCount(Total));
                    break;
                case GateKind.Recruit:
                    Add(Units.First(effect.line).Id, Mathf.RoundToInt(effect.value * recruitBonus));
                    break;
                case GateKind.ClassUp:
                    ClassUp();
                    break;
            }
            return Total - before;
        }

        /// <summary>True while at least one soldier can still move up a tier.</summary>
        public bool CanClassUp
        {
            get
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    if (_counts[i] > 0 && !Units.All[i].IsMaxTier) return true;
                }
                return false;
            }
        }

        /// <summary>Every soldier below the top tier moves one tier up its line.</summary>
        public bool ClassUp()
        {
            bool any = false;
            for (int i = Units.Count - 1; i >= 0; i--)
            {
                UnitClass unit = Units.All[i];
                if (unit.IsMaxTier || _counts[i] == 0) continue;
                _counts[unit.Next.Id] += _counts[i];
                _counts[i] = 0;
                any = true;
            }
            if (any) ClassUps++;
            Recount();
            return any;
        }

        /// <summary>Damage per second of the whole squad (single target).</summary>
        public float Dps(WeaponStats weapon)
        {
            double sum = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                if (_counts[i] == 0) continue;
                UnitClass unit = Units.All[i];
                int shots = unit.Shots + weapon.multiShot - 1;
                sum += (double)_counts[i] * unit.Rate * unit.Damage * Balance.MultiShotFactor(shots);
            }
            return (float)(sum * weapon.fireRateMul * weapon.damageMul * weapon.ExpectedCrit);
        }

        /// <summary>Average damage multiplier of this army against one target, weighted by each class's share of the damage.</summary>
        public float ElementFactor(Func<Element, float> multiplier)
        {
            double sum = 0, weight = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                if (_counts[i] == 0) continue;
                UnitClass unit = Units.All[i];
                double dps = (double)_counts[i] * unit.Rate * unit.Damage;
                sum += dps * multiplier(unit.Element);
                weight += dps;
            }
            return weight > 0 ? (float)(sum / weight) : 1f;
        }

        /// <summary>Average number of horde enemies one bullet takes down (splash / pierce).</summary>
        public float CrowdFactor
        {
            get
            {
                if (Total == 0) return 1f;
                double sum = 0;
                for (int i = 0; i < Units.Count; i++) sum += (double)_counts[i] * Units.All[i].CrowdFactor;
                return (float)(sum / Total);
            }
        }

        /// <summary>Chance that a soldier blocks an enemy's body slam instead of dying.</summary>
        public float GuardChance(float bonus)
        {
            if (Total == 0) return 0f;
            double sum = 0;
            for (int i = 0; i < Units.Count; i++) sum += (double)_counts[i] * Units.All[i].Guard;
            return Mathf.Clamp((float)(sum / Total) + bonus, 0f, 0.85f);
        }

        /// <summary>Soldiers revived per second by the holy line.</summary>
        public float HealPerSecond
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < Units.Count; i++) sum += (double)_counts[i] * Units.All[i].Heal;
                return (float)sum;
            }
        }

        public UnitLine DominantLine
        {
            get
            {
                var perLine = new int[Units.Lines];
                for (int i = 0; i < Units.Count; i++) perLine[(int)Units.All[i].Line] += _counts[i];
                int best = 0;
                for (int l = 1; l < Units.Lines; l++)
                {
                    if (perLine[l] > perLine[best]) best = l;
                }
                return (UnitLine)best;
            }
        }

        public float LineShare(UnitLine line)
        {
            if (Total == 0) return 0f;
            int sum = 0;
            for (int t = 0; t < Units.Tiers; t++) sum += _counts[(int)line * Units.Tiers + t];
            return sum / (float)Total;
        }

        /// <summary>
        /// Assigns classes to <paramref name="visible"/> drawn soldiers: every present class gets at least one
        /// (so none of its damage is lost), the rest follow the largest remainder of each class's share.
        /// </summary>
        public int[] VisiblePerClass(int visible) => VisiblePerClass(visible, new int[Units.Count]);

        /// <summary>Same as <see cref="VisiblePerClass(int)"/>, filling <paramref name="result"/> (no allocation).</summary>
        public int[] VisiblePerClass(int visible, int[] result)
        {
            Array.Clear(result, 0, result.Length);
            if (Total == 0 || visible <= 0) return result;
            int present = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                if (_counts[i] > 0) present++;
            }
            visible = Mathf.Min(visible, Total);
            int left = visible;
            if (visible >= present)
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    if (_counts[i] > 0)
                    {
                        result[i] = 1;
                        left--;
                    }
                }
            }
            double[] remainders = Remainders;
            Array.Clear(remainders, 0, remainders.Length);
            int baseAssigned = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                if (_counts[i] == 0) continue;
                double exact = (double)left * _counts[i] / Total;
                int share = (int)Math.Floor(exact);
                share = Math.Min(share, _counts[i] - result[i]);
                result[i] += share;
                remainders[i] = exact - Math.Floor(exact);
                baseAssigned += share;
            }
            left -= baseAssigned;
            while (left > 0)
            {
                int best = -1;
                for (int i = 0; i < Units.Count; i++)
                {
                    if (_counts[i] <= result[i]) continue;
                    if (best < 0 || remainders[i] > remainders[best]) best = i;
                }
                if (best < 0) break;
                result[best]++;
                remainders[best] = -1;
                left--;
            }
            return result;
        }
    }

    /// <summary>How the drawn soldiers of one class fire so that the whole squad deals exactly <see cref="Army.Dps"/>.</summary>
    public readonly struct ClassFire
    {
        public readonly float Interval;
        public readonly float Damage;
        public readonly int Shots;

        public ClassFire(float interval, float damage, int shots)
        {
            Interval = interval;
            Damage = damage;
            Shots = shots;
        }
    }

    public static class FireModel
    {
        /// <summary>Global bullet throttle (>= 1) keeping the drawn bullets per second under the cap.</summary>
        public static float Throttle(int[] visiblePerClass, WeaponStats weapon, float rateScale = 1f)
        {
            double bulletsPerSecond = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                if (visiblePerClass[i] == 0) continue;
                UnitClass unit = Units.All[i];
                bulletsPerSecond += visiblePerClass[i] * unit.Rate * weapon.fireRateMul * rateScale * (unit.Shots + weapon.multiShot - 1);
            }
            return Mathf.Max(1f, (float)(bulletsPerSecond / Balance.MaxBulletsPerSecond));
        }

        /// <summary>
        /// Fire plan for one class. Damage excludes crits (rolled per bullet) but includes the share of
        /// soldiers each drawn soldier stands for, the multi-shot split and the throttle.
        /// </summary>
        public static ClassFire Plan(UnitClass unit, int count, int visible, WeaponStats weapon, float throttle, float rateScale = 1f)
        {
            int shots = Mathf.Clamp(unit.Shots + weapon.multiShot - 1, 1, Balance.MaxMultiShot + 1);
            float rate = unit.Rate * weapon.fireRateMul * rateScale;
            float damage = unit.Damage * weapon.damageMul * (count / (float)Mathf.Max(1, visible))
                           * (Balance.MultiShotFactor(shots) / shots) * throttle;
            return new ClassFire(throttle / rate, damage, shots);
        }
    }
}
