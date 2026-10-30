using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>Small combat rules shared by the game and the level generator (pure functions, unit tested).</summary>
    public static class Combat
    {
        public const float WeakMultiplier = 1.75f;
        public const float ResistMultiplier = 0.45f;

        /// <summary>A hit that kills keeps flying with what is left, as long as at least this share of its damage remains.</summary>
        public const float CarryOverMinimum = 0.15f;

        /// <summary>Chance that a straight (not seeking) shot passes under a flying enemy.</summary>
        public const float FlyerMiss = 0.55f;

        public static float ElementMultiplier(ElementMask weak, ElementMask resist, Element element)
        {
            var bit = (ElementMask)(1 << (int)element);
            float m = 1f;
            if ((weak & bit) != 0) m *= WeakMultiplier;
            if ((resist & bit) != 0) m *= ResistMultiplier;
            return m;
        }

        /// <summary>Element names in a mask, e.g. "FIRE, FROST" (empty for none).</summary>
        public static string Names(ElementMask mask)
        {
            string text = string.Empty;
            for (int e = 0; e <= (int)Element.Holy; e++)
            {
                if ((mask & (ElementMask)(1 << e)) == 0) continue;
                text += (text.Length > 0 ? Loc.T(", ") : string.Empty) + Loc.T(Units.ElementName((Element)e));
            }
            return text;
        }

        /// <summary>
        /// Damage a projectile still carries after killing an enemy that had <paramref name="hpLeft"/> HP,
        /// or 0 when it should stop. This is what lets one strong shot mow down a whole line of weak enemies.
        /// </summary>
        public static float CarryOver(float damage, float hpLeft, float original)
        {
            float rest = damage - Mathf.Max(0f, hpLeft);
            return rest >= original * CarryOverMinimum ? rest : 0f;
        }

        /// <summary>Average number of enemies of <paramref name="hp"/> one hit of <paramref name="damage"/> takes down with carry-over.</summary>
        public static float KillsPerHit(float damage, float hp)
        {
            if (hp <= 0f) return 1f;
            float ratio = damage / hp;
            if (ratio < 1f) return ratio;
            // what is left after a kill wounds the next enemy, which the next shot finishes; only the scraps below
            // the carry-over minimum are lost
            return Mathf.Max(1f, ratio * (1f - CarryOverMinimum));
        }

        /// <summary>
        /// Average shots of <paramref name="hit"/> damage it takes to bring down one enemy of type <paramref name="t"/>
        /// (<paramref name="hp"/>: its HP before the type's own multiplier), counting its tricks: a skeleton gets back
        /// up at half HP, a slime splits in two, a shield soaks twice its HP from the shots it stops
        /// (<paramref name="frontal"/>: their share of the damage), straight shots (<paramref name="straight"/>: their
        /// share) often pass under flyers and a ghost is out of reach about a third of the time.
        /// <paramref name="crowd"/> is how many enemies one shot reaches (pierce, splash).
        /// </summary>
        public static float ShotsToKill(EnemyType t, float hp, float hit, float crowd, float frontal, float straight)
        {
            float body = hp * t.Hp / t.Multiplier(Element.Physical);
            float shots = 1f / KillsPerHit(hit, body);
            if (t.Has(EnemyTrait.Revive)) shots += 1f / KillsPerHit(hit, body * 0.5f);
            if (t.Has(EnemyTrait.Split))
            {
                EnemyType mini = Enemies.Get(Enemies.MiniSlime);
                shots += 2f / KillsPerHit(hit, hp * mini.Hp / mini.Multiplier(Element.Physical));
            }
            shots /= Mathf.Max(1f, crowd);
            // a shot that a shield stops goes no further, however much damage it had left
            if (t.Has(EnemyTrait.Shield)) shots += frontal * Mathf.Max(1f, Mathf.Ceil(2f * body / Mathf.Max(0.01f, hit)));
            if (t.Has(EnemyTrait.Flying)) shots /= 1f - FlyerMiss * Mathf.Clamp01(straight);
            if (t.Has(EnemyTrait.Phase)) shots /= 1f - 0.37f;
            return shots;
        }
    }
}
