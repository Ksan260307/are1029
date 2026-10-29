using UnityEngine;

namespace NumberRoad.Core
{
    public enum Weather
    {
        None,
        Snow,
        Embers,
        Fireflies,
        Petals,
        Sparkles,
    }

    /// <summary>A region with its own palette and weather. The region changes every five levels.</summary>
    public sealed class Biome
    {
        public readonly string Name;
        public readonly Color Sky;
        public readonly Color Light;
        public readonly Color SkyAmbient;
        public readonly Color GroundAmbient;
        public readonly Color WaterDeep;
        public readonly Color WaterShallow;
        public readonly Color WaterHighlight;
        public readonly Color Road;
        public readonly Color Wall;
        public readonly Weather Weather;

        public Biome(string name, string sky, string light, string skyAmbient, string groundAmbient, string deep, string shallow,
            string highlight, string road, string wall, Weather weather)
        {
            Name = name;
            Sky = C(sky);
            Light = C(light);
            SkyAmbient = C(skyAmbient);
            GroundAmbient = C(groundAmbient);
            WaterDeep = C(deep);
            WaterShallow = C(shallow);
            WaterHighlight = C(highlight);
            Road = C(road);
            Wall = C(wall);
            Weather = weather;
        }

        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }

    public static class Biomes
    {
        public const int LevelsPerBiome = 5;

        public static readonly Biome[] All =
        {
            new Biome("CASTLE BAY", "#8fd1ff", "#948f85", "#707a8f", "#4d4747", "#2178d1", "#5cbdf7", "#d9f2ff", "#ffffff", "#ffffff", Weather.Petals),
            new Biome("DESERT OASIS", "#ffcf8f", "#a8927a", "#8a7a66", "#5c4a3d", "#1f9fa8", "#5fd6c8", "#f0fff6", "#ffe2b3", "#ffd9a6", Weather.Sparkles),
            new Biome("FROST FORT", "#cfe6ff", "#9aa6b8", "#7f93ad", "#525c6b", "#3f7fbf", "#a8d8ff", "#ffffff", "#e8f2ff", "#dfeaff", Weather.Snow),
            new Biome("VOLCANO PASS", "#5c3a3a", "#b0806a", "#6b4a4a", "#3d2626", "#b8321f", "#ff7a2e", "#ffd04d", "#c9b3a8", "#a8908a", Weather.Embers),
            new Biome("NIGHT SWAMP", "#2a3a5c", "#6f7f99", "#4a5a78", "#262f3d", "#1f4a3d", "#3f8f6b", "#b8ffd0", "#a8b8c9", "#8f9fb3", Weather.Fireflies),
            new Biome("SKY CITADEL", "#ffd6f0", "#a899a8", "#8f849e", "#5a5266", "#8f7fe6", "#d6c8ff", "#ffffff", "#fff2fb", "#ffe6f7", Weather.Sparkles),
        };

        public static int IndexForLevel(int level) => (Mathf.Max(1, level) - 1) / LevelsPerBiome % All.Length;

        public static Biome ForLevel(int level) => All[IndexForLevel(level)];
    }
}
