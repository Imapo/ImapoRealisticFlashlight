using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace MinerHelmetFlashlight
{
    public class FlashlightPlayer : ModPlayer
    {
        public const float BeamLength = 900f;
        public Vector2 BeamDirection = Vector2.UnitX;
        public float HeadRotation = 0f;

        // ============================================================
        // НАСТРОЙКА ПОЛОЖЕНИЯ ФОНАРИКА
        // ============================================================
        private const float FlashlightLocalX = 1f;
        private const float FlashlightLocalY = -10f;

        public bool HasFlashlight =>
            Player.armor[0] != null &&
            Player.armor[0].type == ItemID.MiningHelmet &&
            !Player.dead;

        public override void PostUpdateMiscEffects()
        {
            if (!HasFlashlight)
                return;

            HeadRotation = Player.headRotation;

            float rotation = HeadRotation;
            if (Player.direction < 0)
            {
                rotation = -rotation;
            }

            Vector2 direction = new Vector2(
                (float)Math.Cos(rotation),
                (float)Math.Sin(rotation)
            );

            if (Player.direction < 0)
            {
                direction.X = -direction.X;
            }

            if (direction.LengthSquared() > 0.0001f)
                direction.Normalize();

            BeamDirection = direction;

            Vector2 flashlightPosition = GetFlashlightWorldPosition();
            ApplyDynamicLighting(flashlightPosition);
            SpawnDust(flashlightPosition);
        }

        // ============================================================
        // ПЫЛЬ, ПЛАВАЮЩАЯ В ЛУЧЕ
        // ============================================================
        // Формула halfWidth здесь намеренно совпадает с той, что используется
        // в MinerHelmetFlashlight.GenerateBeamTexture(), чтобы пыль спавнилась
        // ровно внутри видимой формы луча, а не поверх/мимо него.
        private float _dustSpawnTimer;

        private void SpawnDust(Vector2 flashlightPosition)
        {
            _dustSpawnTimer += 1f;
            if (_dustSpawnTimer < 1f)
                return;
            _dustSpawnTimer = 0f;

            float t = Main.rand.NextFloat(0.05f, 1f);
            float dist = t * BeamLength;
            float halfWidth = MathHelper.Lerp(3f, 64f, (float)Math.Pow(t, 0.85));

            Vector2 perp = new Vector2(-BeamDirection.Y, BeamDirection.X);
            float offset = Main.rand.NextFloat(-1f, 1f) * halfWidth;

            Vector2 spawnPos = flashlightPosition + BeamDirection * dist + perp * offset;

            int dustId = Dust.NewDust(spawnPos, 1, 1, ModContent.DustType<MinerDustParticle>(), 0f, 0f, 150, default, 1f);
            Dust dust = Main.dust[dustId];
            dust.noGravity = true;
            dust.velocity = BeamDirection * Main.rand.NextFloat(0.15f, 0.4f);
            dust.customData = BeamDirection;
        }

        // ============================================================
        // ПОЛОЖЕНИЕ ГОЛОВЫ (используем визуальную позицию)
        // ============================================================
        public Vector2 GetHeadWorldPosition()
        {
            // Player.headPosition - визуальная позиция головы,
            // которую Terraria использует при отрисовке.
            // Она учитывает маунты, анимации и другие модификаторы.
            if (Player.headPosition != Vector2.Zero)
            {
                return Player.headPosition;
            }

            // Запасной вариант: стандартная формула
            Vector2 center =
                Player.position +
                new Vector2(
                    Player.width / 2f,
                    Player.height / 2f
                );
            Vector2 headOffset = new Vector2(
                Player.direction * 6f,
                -Player.height / 2f + 8f - Player.gfxOffY
            );
            return center + headOffset;
        }

        // ============================================================
        // ПОЛОЖЕНИЕ ФОНАРИКА
        // ============================================================
        public Vector2 GetFlashlightWorldPosition()
        {
            Vector2 headPos = GetHeadWorldPosition();

            float localX = FlashlightLocalX;

            if (Player.direction < 0)
            {
                localX = -localX;
            }

            float cos = (float)Math.Cos(HeadRotation);
            float sin = (float)Math.Sin(HeadRotation);

            float rotatedX = localX * cos - FlashlightLocalY * sin;
            float rotatedY = localX * sin + FlashlightLocalY * cos;

            return headPos + new Vector2(rotatedX, rotatedY);
        }

        // ============================================================
        // ДИНАМИЧЕСКОЕ ОСВЕЩЕНИЕ
        // ============================================================
        private void ApplyDynamicLighting(Vector2 flashlightPosition)
        {
            const int samples = 28;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 samplePos =
                    flashlightPosition +
                    BeamDirection * (BeamLength * t);
                float intensity =
                    MathHelper.Lerp(1.15f, 0.1f, t);
                Lighting.AddLight(
                    samplePos,
                    1.0f * intensity,
                    0.95f * intensity,
                    0.8f * intensity
                );
            }
        }
    }
}