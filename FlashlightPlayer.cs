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

        // Таймер для контроля частоты появления пыли
        private float _dustSpawnTimer;

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
            
            // Спавн пыли в луче
            SpawnDust(flashlightPosition);
        }

        // ============================================================
        // ПЫЛЬ, ПЛАВАЮЩАЯ В ЛУЧЕ (как в репозитории)
        // ============================================================
        private void SpawnDust(Vector2 flashlightPosition)
        {
            _dustSpawnTimer += 1f;
            if (_dustSpawnTimer < 1f) // Спавним примерно раз в секунду (60 тиков)
                return;
            _dustSpawnTimer = 0f;

            // Случайная точка вдоль луча (от 5% до 100% длины)
            float t = Main.rand.NextFloat(0.05f, 1f);
            float dist = t * BeamLength;
            
            // Формула halfWidth совпадает с GenerateBeamTexture, чтобы пыль была строго внутри луча
            float halfWidth = MathHelper.Lerp(3f, 64f, (float)Math.Pow(t, 0.85));

            // Перпендикулярный вектор для разброса по ширине луча
            Vector2 perp = new Vector2(-BeamDirection.Y, BeamDirection.X);
            float offset = Main.rand.NextFloat(-1f, 1f) * halfWidth;

            Vector2 spawnPos = flashlightPosition + BeamDirection * dist + perp * offset;

            // Создаём нашу кастомную пыль
            int dustId = Dust.NewDust(spawnPos, 1, 1, ModContent.DustType<MinerDustParticle>(), 0f, 0f, 150, default, 1f);
            Dust dust = Main.dust[dustId];
            dust.noGravity = true;
            dust.velocity = BeamDirection * Main.rand.NextFloat(0.15f, 0.4f); // Лёгкое движение вперёд
            dust.customData = BeamDirection; // Передаём направление для расчёта "плавания"
        }

        // ============================================================
        // ПОЛОЖЕНИЕ ГОЛОВЫ (ИДЕАЛЬНО ДЛЯ МАУНТОВ)
        // ============================================================
        public Vector2 GetHeadWorldPosition()
        {
            // 1. Приоритет: визуальная позиция головы, которую использует сама игра.
            // Она автоматически учитывает маунты, анимации и все смещения.
            if (Player.headPosition != Vector2.Zero)
            {
                return Player.headPosition;
            }

            // 2. Если на маунте, используем MountedCenter как базу.
            // Player.position остаётся на уровне хитбокса, а визуальная модель смещена.
            if (Player.mount.Active)
            {
                Vector2 center = Player.MountedCenter;
                Vector2 headOffset = new Vector2(
                    Player.direction * 6f,
                    -14f - Player.gfxOffY // Стандартное визуальное смещение головы на маунте
                );
                return center + headOffset;
            }

            // 3. Запасной вариант для обычной ходьбы
            Vector2 defaultCenter = Player.position + new Vector2(Player.width / 2f, Player.height / 2f);
            Vector2 defaultOffset = new Vector2(
                Player.direction * 6f,
                -Player.height / 2f + 8f - Player.gfxOffY
            );
            return defaultCenter + defaultOffset;
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
                Vector2 samplePos = flashlightPosition + BeamDirection * (BeamLength * t);
                float intensity = MathHelper.Lerp(1.15f, 0.1f, t);
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