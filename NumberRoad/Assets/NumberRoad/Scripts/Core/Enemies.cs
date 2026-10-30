using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Core
{
    [Flags]
    public enum EnemyTrait
    {
        None = 0,
        /// <summary>A front shield absorbs most damage until it breaks.</summary>
        Shield = 1 << 0,
        /// <summary>Takes reduced damage from every hit.</summary>
        Armor = 1 << 1,
        /// <summary>Splits into two mini slimes when killed.</summary>
        Split = 1 << 2,
        /// <summary>Hovers above the road.</summary>
        Flying = 1 << 3,
        /// <summary>Turns translucent and invulnerable now and then.</summary>
        Phase = 1 << 4,
        /// <summary>Explodes on death (damaging nearby enemies) and on contact (killing extra soldiers).</summary>
        Explode = 1 << 5,
        /// <summary>Heals nearby enemies.</summary>
        Healer = 1 << 6,
        /// <summary>Speeds up nearby enemies.</summary>
        Drummer = 1 << 7,
        /// <summary>Stops at range and shoots at the squad.</summary>
        Ranged = 1 << 8,
        /// <summary>Gets back up once after being killed.</summary>
        Revive = 1 << 9,
        /// <summary>Moves faster the more it is hurt.</summary>
        Berserk = 1 << 10,
        /// <summary>Regenerates health.</summary>
        Regen = 1 << 11,
        /// <summary>Keeps summoning skeletons.</summary>
        Summoner = 1 << 12,
        /// <summary>Teleports forward.</summary>
        Blink = 1 << 13,
        /// <summary>Dashes at the squad when close.</summary>
        Charge = 1 << 14,
        /// <summary>Runs away when hit (gold goblin).</summary>
        Runaway = 1 << 15,
        /// <summary>Stops and smashes the squad with a weapon until one side dies.</summary>
        Melee = 1 << 16,
        /// <summary>Weaves left and right.</summary>
        Zigzag = 1 << 17,
        /// <summary>Reaches the squad from further away.</summary>
        LongReach = 1 << 18,
        /// <summary>Slows the squad's rate of fire while close.</summary>
        Chill = 1 << 19,
        /// <summary>Waits, disguised, until the squad is close (mimic).</summary>
        Ambush = 1 << 20,
        /// <summary>Skeletons, zombies and spirits: holy damage hurts them most.</summary>
        Undead = 1 << 21,
        /// <summary>Too heavy to be knocked back.</summary>
        Steady = 1 << 22,
    }

    /// <summary>A set of elements (weaknesses and resistances).</summary>
    [Flags]
    public enum ElementMask
    {
        None = 0,
        Physical = 1 << Element.Physical,
        Fire = 1 << Element.Fire,
        Frost = 1 << Element.Frost,
        Lightning = 1 << Element.Lightning,
        Holy = 1 << Element.Holy,
    }

    public sealed class EnemyType
    {
        public readonly int Id;
        public readonly string Key;
        public readonly string Name;
        public readonly string Description;
        public readonly string Model;
        public readonly float ModelScale;
        public readonly float Speed;
        public readonly float Hp;
        public readonly float Radius;
        public readonly int Contact;
        public readonly int Coins;
        public readonly EnemyTrait Traits;
        public readonly int Unlock;
        public readonly float Weight;
        public readonly Color32 Tint;
        public readonly Color32 Debris;
        public readonly ElementMask Weak;
        public readonly ElementMask Resist;

        public EnemyType(int id, string key, string name, string description, string model, float modelScale, float speed, float hp,
            float radius, int contact, int coins, EnemyTrait traits, int unlock, float weight, Color32 tint, Color32 debris,
            ElementMask weak = ElementMask.None, ElementMask resist = ElementMask.None)
        {
            Weak = weak;
            Resist = resist;
            Id = id;
            Key = key;
            Name = name;
            Description = description;
            Model = model;
            ModelScale = modelScale;
            Speed = speed;
            Hp = hp;
            Radius = radius;
            Contact = contact;
            Coins = coins;
            Traits = traits;
            Unlock = unlock;
            Weight = weight;
            Tint = tint;
            Debris = debris;
        }

        public bool Has(EnemyTrait trait) => (Traits & trait) != 0;
        public bool IsBig => Radius >= 0.5f;

        /// <summary>Damage multiplier for an element (weak x1.75, resistant x0.45).</summary>
        public float Multiplier(Element element) => Combat.ElementMultiplier(Weak, Resist, element);
        public float AttackInterval => Has(EnemyTrait.Melee) ? 1.2f + Radius * 0.6f : 0f;
    }

    public enum BossSkill
    {
        Smash,
        Summon,
        Shockwave,
        FireBreath,
        Blink,
        Dash,
    }

    public sealed class BossType
    {
        public readonly int Id;
        public readonly string Key;
        public readonly string Name;
        public readonly string Description;
        public readonly string Model;
        public readonly float Scale;
        public readonly float Speed;
        public readonly float Radius;
        public readonly float AttackInterval;
        public readonly BossSkill Skill;
        public readonly float Armor;
        public readonly ElementMask Weak;
        public readonly ElementMask Resist;

        public BossType(int id, string key, string name, string description, string model, float scale, float speed, float radius,
            float attackInterval, BossSkill skill, float armor, ElementMask weak = ElementMask.None, ElementMask resist = ElementMask.None)
        {
            Weak = weak;
            Resist = resist;
            Id = id;
            Key = key;
            Name = name;
            Description = description;
            Model = model;
            Scale = scale;
            Speed = speed;
            Radius = radius;
            AttackInterval = attackInterval;
            Skill = skill;
            Armor = armor;
        }

        public float Multiplier(Element element) => Combat.ElementMultiplier(Weak, Resist, element);
    }

    public static class Enemies
    {
        static readonly Color32 White = new Color32(255, 255, 255, 255);

        static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        static EnemyType E(int id, string key, string name, string desc, string model, float scale, float speed, float hp, float radius,
            int contact, int coins, EnemyTrait traits, int unlock, float weight, string debris, string tint = "#ffffff",
            ElementMask weak = ElementMask.None, ElementMask resist = ElementMask.None)
        {
            return new EnemyType(id, key, name, desc, model, scale, speed, hp, radius, contact, coins, traits, unlock, weight, C(tint), C(debris), weak, resist);
        }

        public static readonly EnemyType[] All =
        {
            E(0, "goblin", "GOBLIN", "Small and quick. Comes in huge swarms.", "e_goblin", 0.8f, 2.7f, 0.55f, 0.26f, 1, 1, EnemyTrait.None, 1, 1.4f, "#7fbf3f", "#ffffff"),
            E(1, "grunt", "ORC GRUNT", "The backbone of the enemy army.", "grunt", 0.82f, 2.3f, 1f, 0.32f, 1, 1, EnemyTrait.None, 1, 1f, "#e2382f"),
            E(2, "runner", "ORC RUNNER", "Fast and reckless.", "e_runner", 0.72f, 4.1f, 0.7f, 0.28f, 1, 1, EnemyTrait.None, 2, 0.8f, "#ffb02e"),
            E(3, "brute", "OGRE BRUTE", "Stops and smashes your squad with a club.", "brute", 1f, 1.5f, 14f, 0.85f, 3, 8, EnemyTrait.Melee, 3, 0.12f, "#e9a37e"),
            E(4, "skeleton", "SKELETON", "Gets back up once after falling. Holy light keeps it down.", "e_skeleton", 0.82f, 2.2f, 0.8f, 0.3f, 1, 1, EnemyTrait.Revive | EnemyTrait.Undead, 4, 0.9f, "#efe9dc", "#ffffff", ElementMask.Holy, ElementMask.Frost),
            E(5, "zombie", "ZOMBIE", "Slow but tough. Burns well.", "e_zombie", 0.85f, 1.4f, 2f, 0.32f, 1, 2, EnemyTrait.Undead, 5, 0.7f, "#8fae6a", "#ffffff", ElementMask.Holy | ElementMask.Fire, ElementMask.None),
            E(6, "shield", "SHIELD ORC", "Its shield stops straight shots. Lob, smite or sweep around it.", "e_shield", 0.85f, 2f, 1.3f, 0.34f, 1, 2, EnemyTrait.Shield, 6, 0.6f, "#9e9483", "#ffffff", ElementMask.Lightning, ElementMask.None),
            E(7, "slime", "SLIME", "Splits in two. Blades bounce off, fire melts it.", "e_slime", 0.9f, 1.8f, 1.6f, 0.36f, 1, 2, EnemyTrait.Split, 7, 0.6f, "#52d6a0", "#ffffff", ElementMask.Fire, ElementMask.Physical),
            E(8, "minislime", "MINI SLIME", "A piece of a slime.", "e_slime", 0.5f, 2.6f, 0.35f, 0.22f, 1, 0, EnemyTrait.None, 7, 0f, "#52d6a0", "#ffffff", ElementMask.Fire, ElementMask.Physical),
            E(9, "bat", "BAT", "Flies in a zigzag. Straight shots often miss it.", "e_bat", 0.85f, 3.6f, 0.45f, 0.25f, 1, 1, EnemyTrait.Flying | EnemyTrait.Zigzag, 8, 0.7f, "#5b4a7a", "#ffffff", ElementMask.Fire, ElementMask.None),
            E(10, "imp", "BOMBER IMP", "Explodes: hurts nearby enemies when shot, your squad when it touches.", "e_imp", 0.8f, 3f, 0.6f, 0.28f, 3, 2, EnemyTrait.Explode, 9, 0.4f, "#ff5a2e", "#ffffff", ElementMask.Frost, ElementMask.Fire),
            E(11, "knight", "DARK KNIGHT", "Heavy armour turns blades. Lightning goes right through.", "e_knight", 0.88f, 1.9f, 2.6f, 0.34f, 1, 3, EnemyTrait.Armor, 10, 0.5f, "#4a3f66", "#ffffff", ElementMask.Lightning, ElementMask.Physical),
            E(12, "shaman", "ORC SHAMAN", "Heals the enemies around it. Take it out first.", "e_shaman", 0.85f, 1.8f, 1.2f, 0.3f, 1, 3, EnemyTrait.Healer, 11, 0.25f, "#6bd0ff", "#ffffff"),
            E(13, "archer", "GOBLIN ARCHER", "Stops and shoots arrows. Keep moving!", "e_archer", 0.8f, 1.6f, 0.8f, 0.28f, 1, 2, EnemyTrait.Ranged, 12, 0.35f, "#a3d46b"),
            E(14, "ghost", "GHOST", "Turns see-through: only holy light and lightning touch it then.", "e_ghost", 0.9f, 2f, 1.1f, 0.3f, 1, 3, EnemyTrait.Phase | EnemyTrait.Flying | EnemyTrait.Undead, 13, 0.4f, "#dfe8ff", "#ffffff", ElementMask.Holy, ElementMask.Physical),
            E(15, "wolf", "WOLF RIDER", "Charges when it gets close.", "e_wolf", 0.85f, 3f, 1.4f, 0.4f, 2, 3, EnemyTrait.Charge, 14, 0.4f, "#8a8a96", "#ffffff", ElementMask.Fire, ElementMask.None),
            E(16, "berserker", "BERSERKER", "Gets faster the more it is hurt.", "e_berserker", 0.95f, 2.2f, 1.8f, 0.34f, 1, 3, EnemyTrait.Berserk, 15, 0.4f, "#ff6b3d", "#ffffff"),
            E(17, "drummer", "WAR DRUMMER", "Its drum speeds up every enemy around it.", "e_drummer", 0.9f, 1.7f, 1.6f, 0.36f, 1, 3, EnemyTrait.Drummer, 16, 0.2f, "#c8823a", "#ffffff"),
            E(18, "troll", "TROLL", "Regenerates and smashes. Fire stops the healing.", "e_troll", 1f, 1.5f, 5f, 0.55f, 3, 6, EnemyTrait.Regen | EnemyTrait.Melee | EnemyTrait.Steady, 17, 0.15f, "#7a9aa8", "#ffffff", ElementMask.Fire, ElementMask.None),
            E(19, "spearman", "PIKEMAN", "Its long spear reaches you from further away.", "e_spearman", 0.88f, 2f, 1.3f, 0.32f, 1, 2, EnemyTrait.LongReach, 18, 0.4f, "#b08850"),
            E(20, "necro", "NECROMANCER", "Keeps raising skeletons.", "e_necro", 0.88f, 1.4f, 1.8f, 0.3f, 1, 5, EnemyTrait.Summoner | EnemyTrait.Undead, 19, 0.12f, "#7d4bd6", "#ffffff", ElementMask.Holy, ElementMask.None),
            E(21, "beetle", "ARMOR BEETLE", "A fast armoured shell.", "e_beetle", 0.9f, 2.8f, 2.2f, 0.42f, 1, 3, EnemyTrait.Armor, 20, 0.4f, "#3f8f6f", "#ffffff", ElementMask.Lightning, ElementMask.Physical),
            E(22, "mimic", "MIMIC", "A crate with teeth. Waits until you come close.", "e_mimic", 1f, 3.4f, 4f, 0.6f, 4, 8, EnemyTrait.Ambush | EnemyTrait.Steady, 21, 0f, "#b07a45", "#ffffff", ElementMask.Fire, ElementMask.None),
            E(23, "blinker", "BLINK MAGE", "Teleports forward.", "e_blinker", 0.85f, 1.8f, 1.2f, 0.3f, 1, 4, EnemyTrait.Blink, 22, 0.25f, "#c66bff", "#ffffff", ElementMask.None, ElementMask.Frost),
            E(24, "golem", "GOLEM", "A walking wall of stone. Cannot be pushed back.", "e_golem", 1f, 1.1f, 9f, 0.8f, 4, 10, EnemyTrait.Melee | EnemyTrait.Armor | EnemyTrait.Steady, 23, 0.1f, "#9a9a9a", "#ffffff", ElementMask.Lightning, ElementMask.Physical),
            E(25, "icewraith", "ICE WRAITH", "Its cold slows your rate of fire. Fire breaks it.", "e_icewraith", 0.9f, 2.2f, 1.5f, 0.32f, 1, 4, EnemyTrait.Chill | EnemyTrait.Flying, 24, 0.2f, "#9fe6ff", "#ffffff", ElementMask.Fire, ElementMask.Frost),
            E(26, "goldgoblin", "GOLD GOBLIN", "Runs away with a sack of gold. Catch it!", "e_goldgoblin", 0.85f, 3f, 2.5f, 0.28f, 0, 30, EnemyTrait.Runaway, 3, 0f, "#ffd34d"),
            E(27, "captain", "ORC CAPTAIN", "Leads the charge and drives the troops on.", "e_captain", 1f, 2f, 7f, 0.45f, 2, 12, EnemyTrait.Drummer | EnemyTrait.Melee | EnemyTrait.Steady, 25, 0.08f, "#b02a2a", "#ffffff"),
            E(28, "ram", "SIEGE RAM", "Extremely tough. Crushes many soldiers. Wood burns.", "e_ram", 1f, 1f, 14f, 0.9f, 12, 15, EnemyTrait.Armor | EnemyTrait.Steady, 26, 0.06f, "#7a5230", "#ffffff", ElementMask.Fire, ElementMask.Physical),
            E(29, "fireimp", "FIRE IMP", "Bigger explosions that leave the ground burning.", "e_fireimp", 0.85f, 3.2f, 0.9f, 0.3f, 4, 3, EnemyTrait.Explode, 27, 0.3f, "#ffb02e", "#ffffff", ElementMask.Frost, ElementMask.Fire),
            E(30, "whelp", "DRAGON WHELP", "Flies and spits fireballs.", "e_whelp", 0.95f, 2.4f, 5f, 0.6f, 2, 10, EnemyTrait.Flying | EnemyTrait.Ranged, 28, 0.08f, "#ff4a2e", "#ffffff", ElementMask.Frost, ElementMask.Fire),
        };

        public static readonly BossType[] Bosses =
        {
            new BossType(0, "ogreking", "OGRE KING", "Smashes the front line with his club. Fire makes him flinch.", "boss", 1.75f, 0.95f, 1.45f, 1.6f, BossSkill.Smash, 1f,
                ElementMask.Fire, ElementMask.None),
            new BossType(1, "lichking", "LICH KING", "Raises skeletons around him.", "b_lich", 1.6f, 0.8f, 1.3f, 1.8f, BossSkill.Summon, 1f, ElementMask.Holy, ElementMask.None),
            new BossType(2, "golemking", "GOLEM KING", "Armoured. His shockwave crushes many.", "b_golemking", 1.8f, 0.7f, 1.6f, 2f, BossSkill.Shockwave, 0.6f, ElementMask.Lightning, ElementMask.Physical),
            new BossType(3, "dragon", "RED DRAGON", "Breathes fireballs from afar.", "b_dragon", 1.7f, 1.2f, 1.6f, 3.2f, BossSkill.FireBreath, 1f, ElementMask.Frost, ElementMask.Fire),
            new BossType(4, "demonlord", "DEMON LORD", "Teleports towards you.", "b_demon", 1.7f, 1f, 1.45f, 1.5f, BossSkill.Blink, 1f, ElementMask.Holy, ElementMask.None),
            new BossType(5, "shogun", "IRON SHOGUN", "Dashes in and slashes.", "b_shogun", 1.75f, 1.1f, 1.45f, 1.3f, BossSkill.Dash, 0.85f, ElementMask.Lightning, ElementMask.None),
        };

        public const int MiniSlime = 8;
        public const int Skeleton = 4;
        public const int Mimic = 22;
        public const int GoldGoblin = 26;

        public static EnemyType Get(int id) => All[Mathf.Clamp(id, 0, All.Length - 1)];

        public static BossType BossForLevel(int level) => Bosses[(Mathf.Max(1, level) - 1) % Bosses.Length];

        /// <summary>Types that can appear in regular hordes at this level.</summary>
        public static List<EnemyType> Spawnable(int level)
        {
            var list = new List<EnemyType>();
            foreach (EnemyType t in All)
            {
                if (t.Weight > 0f && t.Unlock <= level) list.Add(t);
            }
            return list;
        }

        /// <summary>The regular enemy introduced at exactly this level, if any.</summary>
        public static EnemyType IntroducedAt(int level)
        {
            foreach (EnemyType t in All)
            {
                if (t.Unlock == level && t.Weight > 0f) return t;
            }
            return null;
        }
    }
}
