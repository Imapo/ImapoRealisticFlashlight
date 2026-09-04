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
        private const float FlashlightLocalY = -4f;

        // ============================================================
        // ОТЛАДКА
        // ============================================================
        private uint lastDebugFrame = 0;

        // ============================================================
        // ПЫЛЬ
        // ============================================================
        private float _dustSpawnTimer;

        // ============================================================
        // ЗАЩИТА ОТ РЫВКОВ
        // ============================================================
        private Vector2 _previousFlashlightPosition;
        private float _dashCooldown;
        private bool _isDashing;

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

            // Обнаружение рывка
            if (_previousFlashlightPosition != Vector2.Zero)
            {
                float movementDistance = Vector2.Distance(flashlightPosition, _previousFlashlightPosition);
                if (movementDistance > 20f)
                {
                    _isDashing = true;
                    _dashCooldown = 15f;
                }
            }
            _previousFlashlightPosition = flashlightPosition;

            if (_dashCooldown > 0f)
            {
                _dashCooldown--;
            }
            else
            {
                _isDashing = false;
            }

            // Освещение (только если не рывок)
            if (!_isDashing)
            {
                ApplyDynamicLighting(flashlightPosition);
            }

            // Пыль (спавнится всегда)
            SpawnDust(flashlightPosition);

            // Отладка
            if (Main.GameUpdateCount - lastDebugFrame >= 60)
            {
                lastDebugFrame = Main.GameUpdateCount;
                float degrees = HeadRotation * (180f / (float)Math.PI);
                Main.NewText(
                    $"[Head] Угол: {degrees:F1}° | " +
                    $"Направление: {Player.direction} | " +
                    $"BeamDirection: ({BeamDirection.X:F2}, {BeamDirection.Y:F2})",
                    255, 255, 0
                );
            }
        }

        // ============================================================
        // ПЫЛЬ, ПЛАВАЮЩАЯ В ЛУЧЕ
        // ============================================================
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
        // ПОЛОЖЕНИЕ ГОЛОВЫ (С УЧЁТОМ МАУНТОВ)
        // ============================================================
        public Vector2 GetHeadWorldPosition()
        {
            // Приоритет: визуальная позиция головы
            if (Player.headPosition != Vector2.Zero)
            {
                return Player.headPosition;
            }

            // Если на маунте
            if (Player.mount.Active)
            {
                Vector2 center = Player.MountedCenter;
                Vector2 headOffset = new Vector2(
                    Player.direction * 6f,
                    -14f - Player.gfxOffY
                );
                return center + headOffset;
            }

            // Запасной вариант
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
            
            // --------------------------------------------------------
            // ЛОКАЛЬНАЯ СИСТЕМА КООРДИНАТ ФОНАРИКА
            // --------------------------------------------------------
            float localX = FlashlightLocalX;
            
            if (Player.direction < 0)
            {
                localX = -localX;
            }
            
            // --------------------------------------------------------
            // ПОВОРОТ ЛОКАЛЬНОГО СМЕЩЕНИЯ
            // --------------------------------------------------------
            float cos = (float)Math.Cos(HeadRotation);
            float sin = (float)Math.Sin(HeadRotation);
            
            float rotatedX = localX * cos - FlashlightLocalY * sin;
            float rotatedY = localX * sin + FlashlightLocalY * cos;
            
            // --------------------------------------------------------
            // КОМПЕНСАЦИЯ ПРИ ВЗГЛЯДЕ ВВЕРХ (10° - 55°)
            // --------------------------------------------------------
            float angleDegrees = HeadRotation * (180f / (float)Math.PI);
            float effectiveAngle = angleDegrees * Player.direction;
            
            // Если угол в диапазоне 10-55 градусов вверх
            if (effectiveAngle < -10f && effectiveAngle >= -55f)
            {
                // Пропорциональная коррекция от 0 до 1
                float correctionFactor = (Math.Abs(effectiveAngle) - 10f) / 45f;
                
                // Смещение для удержания луча на фонарике
                // Инвертированные знаки
                float offsetX = -correctionFactor * 5f * Player.direction;
                float offsetY = correctionFactor * -5f; // Положительное = вниз
                
                rotatedX += offsetX;
                rotatedY += offsetY;
            }
            
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