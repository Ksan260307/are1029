using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>The five ally lines. Each has four tiers; CLASS UP promotes every soldier one tier.</summary>
    public enum UnitLine
    {
        Bow,
        Guard,
        Magic,
        Gun,
        Holy,
    }

    public enum ProjectileKind
    {
        Arrow,
        Spear,
        Orb,
        Bullet,
        Cannon,
        Holy,
    }

    /// <summary>One ally class. Numbers are per soldier and per bullet, before squad scaling.</summary>
    public sealed class UnitClass
    {
        public readonly int Id;
        public readonly UnitLine Line;
        public readonly int Tier;
        public readonly string Name;
        public readonly string Model;
        public readonly string Description;
        public readonly float Rate;
        public readonly float Damage;
        public readonly int Shots;
        public readonly float Range;
        public readonly ProjectileKind Projectile;
        public readonly float Splash;
        public readonly int Pierce;
        public readonly float Guard;
        public readonly float Heal;
        public readonly Color32 Color;

        public UnitClass(int id, UnitLine line, int tier, string name, string model, string description, float rate, float damage,
            int shots, float range, ProjectileKind projectile, float splash, int pierce, float guard, float heal, Color32 color)
        {
            Id = id;
            Line = line;
            Tier = tier;
            Name = name;
            Model = model;
            Description = description;
            Rate = rate;
            Damage = damage;
            Shots = shots;
            Range = range;
            Projectile = projectile;
            Splash = splash;
            Pierce = pierce;
            Guard = guard;
            Heal = heal;
            Color = color;
        }

        public bool IsMaxTier => Tier >= Units.Tiers;
        public UnitClass Next => IsMaxTier ? null : Units.All[Id + 1];

        /// <summary>Single-target damage per second of one soldier.</summary>
        public float Dps => Rate * Damage * Balance.MultiShotFactor(Shots);

        /// <summary>Rough effectiveness against dense hordes (splash and pierce hit several enemies).</summary>
        public float CrowdFactor => 1f + Splash * 2.2f + Pierce * 0.6f;
    }

    public static class Units
    {
        public const int Tiers = 4;
        public const int Lines = 5;
        public const int Count = Tiers * Lines;

        static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // id = line * 4 + (tier - 1)
        public static readonly UnitClass[] All =
        {
            new UnitClass(0, UnitLine.Bow, 1, "ARCHER", "u_archer", "Quick arrows at a single target.", 2.0f, 1.0f, 1, 26f, ProjectileKind.Arrow, 0f, 0, 0f, 0f, C("#5fd068")),
            new UnitClass(1, UnitLine.Bow, 2, "CROSSBOW", "u_crossbow", "Heavier bolts that hit harder.", 1.8f, 1.8f, 1, 28f, ProjectileKind.Arrow, 0f, 0, 0f, 0f, C("#3c8dff")),
            new UnitClass(2, UnitLine.Bow, 3, "RANGER", "u_ranger", "Looses two arrows at once.", 2.2f, 1.9f, 2, 28f, ProjectileKind.Arrow, 0f, 0, 0f, 0f, C("#2fbf8f")),
            new UnitClass(3, UnitLine.Bow, 4, "SNIPER", "u_sniper", "Arrows pierce four enemies.", 1.1f, 8.0f, 1, 34f, ProjectileKind.Arrow, 0f, 4, 0f, 0f, C("#ffd34d")),

            new UnitClass(4, UnitLine.Guard, 1, "SQUIRE", "u_squire", "Short spears. Blocks some body slams.", 1.6f, 1.4f, 1, 15f, ProjectileKind.Spear, 0f, 0, 0.25f, 0f, C("#c9a27a")),
            new UnitClass(5, UnitLine.Guard, 2, "KNIGHT", "u_knight", "Sturdy shield, strong spear throws.", 1.6f, 2.3f, 1, 16f, ProjectileKind.Spear, 0f, 0, 0.4f, 0f, C("#a8c4ff")),
            new UnitClass(6, UnitLine.Guard, 3, "PALADIN", "u_paladin", "Holy spears pierce one enemy.", 1.7f, 3.6f, 1, 17f, ProjectileKind.Spear, 0f, 1, 0.55f, 0f, C("#fff0a8")),
            new UnitClass(7, UnitLine.Guard, 4, "CHAMPION", "u_champion", "Unbreakable wall of steel.", 1.8f, 5.8f, 1, 18f, ProjectileKind.Spear, 0f, 2, 0.7f, 0f, C("#ffb347")),

            new UnitClass(8, UnitLine.Magic, 1, "APPRENTICE", "u_apprentice", "Small magic bursts hit groups.", 1.3f, 1.1f, 1, 24f, ProjectileKind.Orb, 0.9f, 0, 0f, 0f, C("#c48bff")),
            new UnitClass(9, UnitLine.Magic, 2, "MAGE", "u_mage", "Bigger blasts for bigger crowds.", 1.4f, 1.8f, 1, 25f, ProjectileKind.Orb, 1.2f, 0, 0f, 0f, C("#8f6bff")),
            new UnitClass(10, UnitLine.Magic, 3, "WIZARD", "u_wizard", "Wide blasts melt hordes.", 1.5f, 2.8f, 1, 26f, ProjectileKind.Orb, 1.6f, 0, 0f, 0f, C("#5b8cff")),
            new UnitClass(11, UnitLine.Magic, 4, "ARCHMAGE", "u_archmage", "Huge blasts clear the road.", 1.5f, 4.6f, 1, 27f, ProjectileKind.Orb, 2.2f, 0, 0f, 0f, C("#ff6bd6")),

            new UnitClass(12, UnitLine.Gun, 1, "MUSKETEER", "u_musketeer", "Fast shots.", 2.6f, 0.9f, 1, 26f, ProjectileKind.Bullet, 0f, 0, 0f, 0f, C("#ffcf6b")),
            new UnitClass(13, UnitLine.Gun, 2, "RIFLEMAN", "u_rifleman", "Faster, longer reach.", 3.2f, 1.2f, 1, 30f, ProjectileKind.Bullet, 0f, 0, 0f, 0f, C("#ffad33")),
            new UnitClass(14, UnitLine.Gun, 3, "GATLING", "u_gatling", "A hail of bullets.", 6.0f, 1.05f, 1, 26f, ProjectileKind.Bullet, 0f, 0, 0f, 0f, C("#ff8a3d")),
            new UnitClass(15, UnitLine.Gun, 4, "CANNONEER", "u_cannoneer", "Explosive cannon balls.", 1.0f, 7.0f, 1, 30f, ProjectileKind.Cannon, 2.6f, 0, 0f, 0f, C("#ff5a3d")),

            new UnitClass(16, UnitLine.Holy, 1, "ACOLYTE", "u_acolyte", "Revives fallen soldiers over time.", 1.6f, 0.8f, 1, 24f, ProjectileKind.Holy, 0f, 0, 0f, 0.02f, C("#fff6c2")),
            new UnitClass(17, UnitLine.Holy, 2, "CLERIC", "u_cleric", "Faster revives.", 1.7f, 1.3f, 1, 25f, ProjectileKind.Holy, 0f, 0, 0f, 0.035f, C("#ffe98a")),
            new UnitClass(18, UnitLine.Holy, 3, "BISHOP", "u_bishop", "Revives and holy bursts.", 1.8f, 2.0f, 1, 26f, ProjectileKind.Holy, 0.6f, 0, 0f, 0.05f, C("#ffd64d")),
            new UnitClass(19, UnitLine.Holy, 4, "SAINT", "u_saint", "Miracles: fast revives, big bursts.", 2.0f, 3.0f, 1, 27f, ProjectileKind.Holy, 1.0f, 0, 0f, 0.07f, C("#fffbe0")),
        };

        public static UnitClass Get(int id) => All[Mathf.Clamp(id, 0, Count - 1)];

        public static UnitClass First(UnitLine line) => All[(int)line * Tiers];

        /// <summary>Player level at which a line can be chosen at the start and appears in recruit gates.</summary>
        public static int UnlockLevel(UnitLine line)
        {
            switch (line)
            {
                case UnitLine.Guard: return 2;
                case UnitLine.Magic: return 3;
                case UnitLine.Gun: return 5;
                case UnitLine.Holy: return 7;
                default: return 1;
            }
        }

        public static bool IsUnlocked(UnitLine line, int level) => level >= UnlockLevel(line);

        public static string LineName(UnitLine line)
        {
            switch (line)
            {
                case UnitLine.Guard: return "KNIGHT";
                case UnitLine.Magic: return "MAGE";
                case UnitLine.Gun: return "GUNNER";
                case UnitLine.Holy: return "CLERIC";
                default: return "ARCHER";
            }
        }

        public static Color LineColor(UnitLine line)
        {
            switch (line)
            {
                case UnitLine.Guard: return new Color(0.75f, 0.8f, 0.9f);
                case UnitLine.Magic: return new Color(0.65f, 0.45f, 1f);
                case UnitLine.Gun: return new Color(1f, 0.62f, 0.2f);
                case UnitLine.Holy: return new Color(1f, 0.92f, 0.45f);
                default: return new Color(0.35f, 0.8f, 0.4f);
            }
        }

        /// <summary>The next line the player will unlock, or null when every line is open.</summary>
        public static UnitLine? NextUnlock(int level)
        {
            UnitLine? best = null;
            for (int i = 0; i < Lines; i++)
            {
                var line = (UnitLine)i;
                if (level >= UnlockLevel(line)) continue;
                if (best == null || UnlockLevel(line) < UnlockLevel(best.Value)) best = line;
            }
            return best;
        }
    }
}
