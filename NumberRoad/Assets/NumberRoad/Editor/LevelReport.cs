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
            Print(1, 2, 3, 5, 8, 10, 15, 20, 30, 40);
            EditorApplication.Exit(0);
        }

        static void Print(params int[] levels)
        {
            foreach (int level in levels)
            {
                LevelDefinition def = LevelGenerator.Generate(level);
                int escorts = 0;
                foreach (HordeUnit u in def.escorts) escorts += u.count;
                var sb = new StringBuilder();
                sb.AppendLine($"[NumberRoad] Level {level}: length {def.length:0} m, {def.events.Count} events, {def.GateRows} gate rows, " +
                              $"{def.hordeTotal} enemies, expected squad {def.expectedCount:0} (dps {def.expectedDps:0}), " +
                              $"boss {Enemies.Bosses[def.bossType].Key} hp {def.bossHp:0} smash {def.bossSmash}, escorts {escorts} x {def.escortHp:0.0} hp" +
                              (def.goldRush ? ", GOLD RUSH" : string.Empty) + (def.castle ? ", CASTLE" : string.Empty) +
                              (def.introduces >= 0 ? ", new " + Enemies.Get(def.introduces).Key : string.Empty));
                foreach (SpawnEvent ev in def.events) sb.AppendLine("    " + ev);
                Debug.Log(sb.ToString());
            }
        }
    }
}
