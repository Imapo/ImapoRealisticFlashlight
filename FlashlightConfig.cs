using Terraria.ModLoader.Config;
using System.ComponentModel;

namespace MinerHelmetFlashlight
{
    public class FlashlightConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("BeamSettings")]
        [DefaultValue(900f)]
        [Range(100f, 2000f)]
        [Increment(50f)]
        public float BeamLength { get; set; }

        [DefaultValue(1.0f)]
        [Range(0.1f, 3.0f)]
        [Increment(0.05f)]
        public float LightIntensity { get; set; }

        [Header("DustSettings")]
        [DefaultValue(1.0f)]
        [Range(0.1f, 3.0f)]
        [Increment(0.1f)]
        [Tooltip("Чем больше значение, тем больше пылинок")]
        public float DustFrequency { get; set; }
    }
}