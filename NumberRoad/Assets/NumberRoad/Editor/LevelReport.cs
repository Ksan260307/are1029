using System.Text;
using NumberRoad.Core;
using UnityEditor;
using UnityEngine;

namespace NumberRoad.EditorTools
{
    /// <summary>Prints generated levels to the console - handy when tuning Balance.cs.</summary>
    public static class LevelReport
    {
        [MenuItem("Number Road/Debug/Print Levels 1-10", priority = 40)]
        public static void PrintFirstLevels() => Print(1, 2, 3, 5, 10);

        /// <summary>Command line: -executeMethod NumberRoad.EditorTools.LevelReport.PrintBatch</summary>
        public static void PrintBatch()
        {
            Print(1, 2, 3, 5, 10, 20, 40);
            EditorApplication.Exit(0);
        }

        static void Print(params int[] levels)
        {
            foreach (int level in levels)
            {
                LevelDefinition def = LevelGenerator.Generate(level);
                var sb = new StringBuilder();
                sb.AppendLine($"[NumberRoad] Level {level}: length {def.length:0} m, {def.events.Count} events, " +
                              $"expected squad {def.expectedCount:0} (dps {def.expectedDps:0}), boss hp {def.bossHp:0}, " +
                              $"smash {def.bossSmash}, escorts {def.escortCount} x {def.escortHp:0.0} hp");
                foreach (SpawnEvent ev in def.events) sb.AppendLine("    " + ev);
                Debug.Log(sb.ToString());
            }
        }
    }
}
