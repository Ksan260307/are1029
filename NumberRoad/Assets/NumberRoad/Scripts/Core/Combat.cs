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
                text += (text.Length > 0 ? ", " : string.Empty) + Units.ElementName((Element)e);
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
    }
}
