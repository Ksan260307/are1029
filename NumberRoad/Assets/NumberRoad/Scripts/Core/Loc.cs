using System.Globalization;

namespace NumberRoad.Core
{
    public enum Language
    {
        Japanese,
        English,
    }

    /// <summary>
    /// The text the player reads, in Japanese or English. The English text is the key: <see cref="T"/> returns its
    /// Japanese version (the table in LocJa.cs) while Japanese is on, and anything without one stays English.
    /// Composed text goes through <see cref="F"/> with {0}, {1}... so each language can order the words its own way.
    /// </summary>
    public static class Loc
    {
        /// <summary>English until the game applies the saved choice (Japanese for a new save).</summary>
        public static Language Language { get; set; } = Language.English;

        public static bool Japanese => Language == Language.Japanese;

        public static string T(string en)
        {
            if (!Japanese || string.IsNullOrEmpty(en)) return en;
            return LocJa.Text.TryGetValue(en, out string ja) ? ja : en;
        }

        public static string F(string en, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(en), args);

        /// <summary>Capitals for the English look; Japanese is left alone (it would turn "x1.4" into "X1.4").</summary>
        public static string Upper(string text) => Japanese || text == null ? text : text.ToUpperInvariant();

        /// <summary>True when the English text has a Japanese version (tests).</summary>
        public static bool Translated(string en) => LocJa.Text.ContainsKey(en);
    }
}
