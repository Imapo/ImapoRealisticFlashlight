using Microsoft.Xna.Framework;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace MinerHelmetFlashlight
{
    public class FlashlightConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Label("Красный (R)")]
        [Tooltip("Интенсивность красного канала. 1.0 = максимум.")]
        [Range(0f, 1f)]
        [Increment(0.05f)]
        [DefaultValue(1f)]
        public float LightColorR { get; set; } = 1f;

        [Label("Зелёный (G)")]
        [Tooltip("Интенсивность зелёного канала. 1.0 = максимум.")]
        [Range(0f, 1f)]
        [Increment(0.05f)]
        [DefaultValue(0.98f)]
        public float LightColorG { get; set; } = 0.98f;

        [Label("Синий (B)")]
        [Tooltip("Интенсивность синего канала. 1.0 = максимум.")]
        [Range(0f, 1f)]
        [Increment(0.05f)]
        [DefaultValue(0.86f)]
        public float LightColorB { get; set; } = 0.86f;

        public Vector3 LightColor => new Vector3(LightColorR, LightColorG, LightColorB);
    }
}