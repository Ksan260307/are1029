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

    /// <summary>What a projectile looks like.</summary>
    public enum ProjectileKind
    {
        Arrow,
        Spear,
        Orb,
        Bullet,
        Cannon,
        Holy,
    }

    /// <summary>How a class attacks. This is what makes the lines play differently.</summary>
    public enum AttackStyle
    {
        /// <summary>A projectile that flies straight ahead.</summary>
        Straight,
        /// <summary>A projectile that curves towards the nearest enemy (never misses flyers).</summary>
        Seek,
        /// <summary>Straight shots with a wide random spread.</summary>
        Spray,
        /// <summary>An instant shot along a line at a chosen target (the toughest enemy for snipers).</summary>
        Beam,
        /// <summary>An arcing shot that bursts on the thickest crowd (goes over shields).</summary>
        Lob,
        /// <summary>Instant lightning that leaps from enemy to enemy.</summary>
        Chain,
        /// <summary>Light called down on an enemy anywhere in range (goes over shields).</summary>
        Smite,
        /// <summary>Melee sweeps that hit every enemy close to the front of the squad.</summary>
        Sweep,
    }

    public enum Element
    {
        Physical,
        Fire,
        Frost,
        Lightning,
        Holy,
    }

    /// <summary>One ally class. Numbers are per soldier and per hit, before squad scaling.</summary>
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
        public readonly AttackStyle Style;
        public readonly Element Element;
        public readonly float Splash;
        public readonly int Pierce;
        public readonly int Chains;
        public readonly float Knockback;
        public readonly float Guard;
        public readonly float Heal;
        public readonly Color32 Color;

        public UnitClass(int id, UnitLine line, int tier, string name, string model, string description, AttackStyle style, Element element,
            float rate, float damage, int shots, float range, ProjectileKind projectile, float splash, int pierce, int chains, float knockback,
            float guard, float heal, Color32 color)
        {
            Id = id;
            Line = line;
            Tier = tier;
            Name = name;
            Model = model;
            Description = description;
            Style = style;
            Element = element;
            Rate = rate;
            Damage = damage;
            Shots = shots;
            Range = range;
            Projectile = projectile;
            Splash = splash;
            Pierce = pierce;
            Chains = chains;
            Knockback = knockback;
            Guard = guard;
            Heal = heal;
            Color = color;
        }

        public bool IsMaxTier => Tier >= Units.Tiers;
        public UnitClass Next => IsMaxTier ? null : Units.All[Id + 1];

        /// <summary>Single-target damage per second of one soldier.</summary>
        public float Dps => Rate * Damage * Balance.MultiShotFactor(Shots);

        /// <summary>Rough effectiveness against dense hordes (area and multi-target attacks hit several enemies).</summary>
        public float CrowdFactor
        {
            get
            {
                switch (Style)
                {
                    case AttackStyle.Lob:
                    case AttackStyle.Smite: return 1f + Splash * 2.2f;
                    case AttackStyle.Chain: return 1f + Chains * 0.7f;
                    case AttackStyle.Sweep: return 1f + Range * 0.5f;
                    case AttackStyle.Beam: return 1f + Mathf.Min(Pierce, 4) * 0.8f;
                    default: return 1f + Mathf.Min(Pierce, 4) * 0.6f + Splash * 2.2f;
                }
            }
        }

        /// <summary>True when the attack creates no projectile (it lands the moment the soldier attacks).</summary>
        public bool Instant => Style == AttackStyle.Beam || Style == AttackStyle.Chain || Style == AttackStyle.Smite || Style == AttackStyle.Sweep;
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

        static UnitClass U(int id, UnitLine line, string name, string model, string description, AttackStyle style, Element element,
            float rate, float damage, int shots, float range, ProjectileKind projectile, string color,
            float splash = 0f, int pierce = 0, int chains = 0, float knockback = 0f, float guard = 0f, float heal = 0f)
        {
            return new UnitClass(id, line, id % Tiers + 1, name, model, description, style, element, rate, damage, shots, range, projectile,
                splash, pierce, chains, knockback, guard, heal, C(color));
        }

        // id = line * 4 + (tier - 1)
        public static readonly UnitClass[] All =
        {
            U(0, UnitLine.Bow, "ARCHER", "u_archer", "Arrows curve into the nearest enemy. Never miss flyers.", AttackStyle.Seek, Element.Physical,
                2.0f, 1.0f, 1, 26f, ProjectileKind.Arrow, "#5fd068"),
            U(1, UnitLine.Bow, "CROSSBOW", "u_crossbow", "Heavy bolts punch through the first enemy.", AttackStyle.Straight, Element.Physical,
                1.6f, 2.2f, 1, 28f, ProjectileKind.Arrow, "#3c8dff", pierce: 1, knockback: 0.3f),
            U(2, UnitLine.Bow, "RANGER", "u_ranger", "Three seeking arrows at once.", AttackStyle.Seek, Element.Physical,
                1.7f, 1.6f, 3, 28f, ProjectileKind.Arrow, "#2fbf8f"),
            U(3, UnitLine.Bow, "SNIPER", "u_sniper", "Picks off the toughest enemy, piercing everything in between.", AttackStyle.Beam, Element.Physical,
                0.8f, 10f, 1, 36f, ProjectileKind.Arrow, "#ffd34d", pierce: 99),

            U(4, UnitLine.Guard, "SQUIRE", "u_squire", "Short spear throws push enemies back. Blocks some body slams.", AttackStyle.Straight, Element.Physical,
                1.6f, 1.4f, 1, 14f, ProjectileKind.Spear, "#c9a27a", knockback: 0.6f, guard: 0.25f),
            U(5, UnitLine.Guard, "KNIGHT", "u_knight", "Sword sweeps cut down every enemy that reaches the front.", AttackStyle.Sweep, Element.Physical,
                1.2f, 2.6f, 1, 4.5f, ProjectileKind.Spear, "#a8c4ff", knockback: 0.8f, guard: 0.4f),
            U(6, UnitLine.Guard, "PALADIN", "u_paladin", "Holy sweeps, deadly to the undead.", AttackStyle.Sweep, Element.Holy,
                1.2f, 3.8f, 1, 5f, ProjectileKind.Spear, "#fff0a8", knockback: 1f, guard: 0.55f),
            U(7, UnitLine.Guard, "CHAMPION", "u_champion", "Whirlwind sweeps hurl whole crowds back.", AttackStyle.Sweep, Element.Physical,
                1.3f, 5.6f, 1, 6f, ProjectileKind.Spear, "#ffb347", knockback: 1.6f, guard: 0.7f),

            U(8, UnitLine.Magic, "APPRENTICE", "u_apprentice", "Lobs fireballs into the thickest crowd. Burns.", AttackStyle.Lob, Element.Fire,
                1.2f, 1.2f, 1, 24f, ProjectileKind.Orb, "#c48bff", splash: 1f),
            U(9, UnitLine.Magic, "MAGE", "u_mage", "Frost bombs slow everything they hit.", AttackStyle.Lob, Element.Frost,
                1.1f, 2f, 1, 26f, ProjectileKind.Orb, "#8fd6ff", splash: 1.3f),
            U(10, UnitLine.Magic, "WIZARD", "u_wizard", "Lightning leaps between four enemies. Melts armour.", AttackStyle.Chain, Element.Lightning,
                1.2f, 2.6f, 1, 24f, ProjectileKind.Orb, "#5b8cff", chains: 4),
            U(11, UnitLine.Magic, "ARCHMAGE", "u_archmage", "Meteors flatten crowds and leave the ground burning.", AttackStyle.Lob, Element.Fire,
                0.7f, 7f, 1, 30f, ProjectileKind.Orb, "#ff6bd6", splash: 2.6f),

            U(12, UnitLine.Gun, "MUSKETEER", "u_musketeer", "Fast, straight shots.", AttackStyle.Straight, Element.Physical,
                2.6f, 0.95f, 1, 26f, ProjectileKind.Bullet, "#ffcf6b"),
            U(13, UnitLine.Gun, "RIFLEMAN", "u_rifleman", "Long rifle shots go through two enemies.", AttackStyle.Beam, Element.Physical,
                1.4f, 2.6f, 1, 34f, ProjectileKind.Bullet, "#ffad33", pierce: 2),
            U(14, UnitLine.Gun, "GATLING", "u_gatling", "Sprays a hail of bullets across the road.", AttackStyle.Spray, Element.Physical,
                7f, 0.85f, 1, 22f, ProjectileKind.Bullet, "#ff8a3d"),
            U(15, UnitLine.Gun, "CANNONEER", "u_cannoneer", "Cannonballs blast crowds apart.", AttackStyle.Lob, Element.Fire,
                0.9f, 8f, 1, 30f, ProjectileKind.Cannon, "#ff5a3d", splash: 2.4f, knockback: 2f),

            U(16, UnitLine.Holy, "ACOLYTE", "u_acolyte", "Calls light down on an enemy. Revives fallen soldiers.", AttackStyle.Smite, Element.Holy,
                1.4f, 1f, 1, 26f, ProjectileKind.Holy, "#fff6c2", splash: 0.4f, heal: 0.02f),
            U(17, UnitLine.Holy, "CLERIC", "u_cleric", "Brighter light and faster revives.", AttackStyle.Smite, Element.Holy,
                1.5f, 1.6f, 1, 28f, ProjectileKind.Holy, "#ffe98a", splash: 0.6f, heal: 0.035f),
            U(18, UnitLine.Holy, "BISHOP", "u_bishop", "Light bursts on groups and shields the squad.", AttackStyle.Smite, Element.Holy,
                1.5f, 2.4f, 1, 30f, ProjectileKind.Holy, "#ffd64d", splash: 1f, guard: 0.15f, heal: 0.05f),
            U(19, UnitLine.Holy, "SAINT", "u_saint", "Pillars of light and miracle revives.", AttackStyle.Smite, Element.Holy,
                1.6f, 3.6f, 1, 32f, ProjectileKind.Holy, "#fffbe0", splash: 1.4f, guard: 0.2f, heal: 0.07f),
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

        public static string StyleName(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.Seek: return "SEEKING";
                case AttackStyle.Spray: return "SPRAY";
                case AttackStyle.Beam: return "PIERCING";
                case AttackStyle.Lob: return "AREA";
                case AttackStyle.Chain: return "CHAIN";
                case AttackStyle.Smite: return "SKY STRIKE";
                case AttackStyle.Sweep: return "MELEE SWEEP";
                default: return "STRAIGHT";
            }
        }

        public static string ElementName(Element element)
        {
            switch (element)
            {
                case Element.Fire: return "FIRE";
                case Element.Frost: return "FROST";
                case Element.Lightning: return "LIGHTNING";
                case Element.Holy: return "HOLY";
                default: return "PHYSICAL";
            }
        }

        public static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Fire: return new Color(1f, 0.5f, 0.15f);
                case Element.Frost: return new Color(0.55f, 0.85f, 1f);
                case Element.Lightning: return new Color(0.7f, 0.75f, 1f);
                case Element.Holy: return new Color(1f, 0.95f, 0.55f);
                default: return new Color(0.85f, 0.85f, 0.85f);
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
