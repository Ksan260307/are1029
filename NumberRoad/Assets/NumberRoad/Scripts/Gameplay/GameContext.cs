using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>Services shared by everything inside a level.</summary>
    public sealed class GameContext
    {
        public FxSystem Fx;
        public AudioSystem Audio;
        public LabelLayer Labels;
        public CameraRig Camera;
        public WorldView World;

        public static readonly Color AllyColor = new Color(0.25f, 0.5f, 1f);
        public static readonly Color EnemyColor = new Color(0.95f, 0.25f, 0.2f);
        public static readonly Color GoldColor = new Color(1f, 0.78f, 0.2f);
        public static readonly Color WoodColor = new Color(0.72f, 0.5f, 0.28f);
    }
}
