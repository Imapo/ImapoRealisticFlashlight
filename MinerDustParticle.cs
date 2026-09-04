using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace MinerHelmetFlashlight
{
    /// <summary>
    /// Пылинка, плавно "плавающая" в луче фонарика. Текстура — мягкая
    /// радиальная точка (premultiplied alpha, как и BeamTexture в
    /// MinerHelmetFlashlight.cs).
    /// </summary>
    public class MinerDustParticle : ModDust
    {
        public override string Texture => "MinerHelmetFlashlight/MinerDustParticle";

        private static float _time;

        public override void OnSpawn(Dust dust)
        {
            dust.scale = Main.rand.NextFloat(0.12f, 0.22f);
            dust.alpha = 60;
            dust.frame = new Rectangle(0, 0, 16, 16); // вся текстура целиком, один кадр
        }

        public override bool Update(Dust dust)
        {
            _time += 0.016f;

            // Направление луча записано в customData при спавне
            Vector2 dir = dust.customData is Vector2 v ? v : Vector2.UnitX;
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            // Сумма синусов даёт мягкий, не механический дрейф ("плавание" пыли)
            float seed = dust.dustIndex * 0.37f;
            float sway = (float)(System.Math.Sin(_time * 1.3 + seed) * 0.4
                               + System.Math.Sin(_time * 0.7 + seed * 2f) * 0.25);

            dust.velocity += perp * sway * 0.02f;
            dust.velocity *= 0.98f; // лёгкое торможение
            dust.position += dust.velocity;
            dust.rotation += 0.01f;

            // Плавное угасание вместо резкого исчезновения
            dust.alpha += 2;
            dust.scale -= 0.003f;

            if (dust.alpha >= 255 || dust.scale <= 0.05f)
                dust.active = false;

            return false; // сами управляем движением, отменяя стандартную физику
        }
    }
}
