using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Presentation
{
    /// <summary>The 128px character portraits Blender renders into Resources/Portraits (barracks, bestiary, banners).</summary>
    public static class Portraits
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            if (!Cache.TryGetValue(model, out Texture2D texture))
            {
                texture = Resources.Load<Texture2D>("Portraits/" + model);
                Cache[model] = texture;
            }
            return texture;
        }
    }
}
