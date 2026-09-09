using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace MinerHelmetFlashlight
{
    public class FlashlightPlayer : ModPlayer
    {
        public Vector2 BeamDirection = Vector2.UnitX;
        public float HeadRotation = 0f;
        public float EffectiveBeamLength = 0f; // длина луча, обрезанная первым твёрдым блоком на пути

        private const float FlashlightLocalX = 1f;
        private const float FlashlightLocalY = -4f;
        private float _dustSpawnTimer;
        private int _debugLogTimer;
        private Vector2 _previousFlashlightPosition;
        private float _dashCooldown;
        private bool _isDashing;

        // Стандартные значения (вместо настроек)
        private const float BeamLength = 900f;
        private const float LightIntensity = 1.0f;
        private const float DustFrequency = 1.0f;

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

            // Player.fullRotation — наклон ВСЕГО тела (например, на вагонетке)
            // Применяем его отдельным поворотом к готовому вектору
            if (Player.fullRotation != 0f)
            {
                float fc = (float)Math.Cos(Player.fullRotation);
                float fs = (float)Math.Sin(Player.fullRotation);
                direction = new Vector2(
                    direction.X * fc - direction.Y * fs,
                    direction.X * fs + direction.Y * fc
                );
            }

            if (direction.LengthSquared() > 0.0001f)
                direction.Normalize();

            BeamDirection = direction;

            Vector2 flashlightPosition = GetFlashlightWorldPosition();

            // ВРЕМЕННАЯ ОТЛАДКА: вывод в чат раз в секунду (60 тиков логики).
            // Уберите этот блок после диагностики.
            _debugLogTimer++;
            if (_debugLogTimer >= 60)
            {
                _debugLogTimer = 0;
                Main.NewText(
                    $"headPos.Y={Player.headPosition.Y:F1}  beamOrigin.Y={flashlightPosition.Y:F1}  " +
                    $"pos.Y={Player.position.Y:F1}  gfxOffY={Player.gfxOffY:F1}  mount={Player.mount.Active}  fullRotation={Player.fullRotation:F2}",
                    Color.Yellow
                );
            }

            // Raycasting: вычисляем реальную длину луча до первого блока
            EffectiveBeamLength = RaycastBeamLength(flashlightPosition, BeamDirection, BeamLength);

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

            if (!_isDashing)
            {
                ApplyDynamicLighting(flashlightPosition);
            }

            SpawnDust(flashlightPosition);
        }

        private void SpawnDust(Vector2 flashlightPosition)
        {
            _dustSpawnTimer += 1f;
            float spawnInterval = 1f / DustFrequency;
            if (_dustSpawnTimer < spawnInterval)
                return;
            _dustSpawnTimer = 0f;

            float beamLength = EffectiveBeamLength;
            float t = Main.rand.NextFloat(0.05f, 1f);
            float dist = t * beamLength;
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

        public Vector2 GetHeadWorldPosition()
        {
            if (Player.headPosition != Vector2.Zero)
            {
                return Player.headPosition;
            }

            // gfxOffY — визуальное сглаживающее смещение спрайта.
            // Оно уже представляет разницу между визуальной и логической позицией,
            // поэтому добавляем, а не вычитаем (см. предыдущий фикс).
            float gfxOffY = Player.gfxOffY;

            if (Player.mount.Active)
            {
                if (Player.mount.Type == MountID.Minecart || Player.mount.Type == MountID.MinecartMech)
                {
                    return GetMinecartHeadPosition(gfxOffY);
                }

                // Прочие маунты (УФО, единорог, бур и т.п.) — общая формула,
                // без специфичных для вагонетки поправок.
                Vector2 center = Player.MountedCenter;
                Vector2 localHeadOffset = new Vector2(Player.direction * 6f, -14f + gfxOffY);

                if (Player.fullRotation != 0f)
                {
                    float fc = (float)Math.Cos(Player.fullRotation);
                    float fs = (float)Math.Sin(Player.fullRotation);
                    localHeadOffset = new Vector2(
                        localHeadOffset.X * fc - localHeadOffset.Y * fs,
                        localHeadOffset.X * fs + localHeadOffset.Y * fc
                    );
                }

                return center + localHeadOffset;
            }

            Vector2 defaultCenter = Player.position + new Vector2(Player.width / 2f, Player.height / 2f);
            return defaultCenter + new Vector2(
                Player.direction * 6f,
                -Player.height / 2f + 8f + gfxOffY
            );
        }

        /// <summary>
        /// Отдельная формула позиционирования головы для вагонетки.
        /// Раскладываем смещение на две ОСМЫСЛЕННЫЕ, независимые компоненты:
        ///   AlongRail — вдоль направления рельсов (вперёд по ходу вагонетки)
        ///   AcrossRail — поперёк рельсов (вверх от сиденья к голове)
        /// вместо одного смешанного вектора (direction*6, -14). Так их можно
        /// откалибровать по отдельности: по вашим наблюдениям на подъёме и
        /// спуске ошибка ведёт себя по-разному (то дальше, то ближе), а
        /// единственной универсальной константой такое не описать — тут два
        /// независимых неизвестных, а не одно.
        ///
        /// ТЕКУЩИЕ ЗНАЧЕНИЯ — ПРИБЛИЗИТЕЛЬНЫЕ, требуют калибровки по вашим
        /// данным (см. чат).
        /// </summary>
        private Vector2 GetMinecartHeadPosition(float gfxOffY)
        {
            const float AlongRail = 6f;   // вперёд по ходу движения вагонетки
            const float AcrossRail = -14f; // от сиденья вверх к голове

            Vector2 center = Player.MountedCenter;

            float fc = (float)Math.Cos(Player.fullRotation);
            float fs = (float)Math.Sin(Player.fullRotation);

            float forward = AlongRail * Player.direction;
            float across = AcrossRail + gfxOffY;

            Vector2 localHeadOffset = new Vector2(
                forward * fc - across * fs,
                forward * fs + across * fc
            );

            // Простой мировой сдвиг (не локальный, не поворачивается вместе
            // с вагонеткой) — подвинуть луч на несколько пикселей на экране.
            // Подберите значения по глазу; отрицательные = влево/вверх.
            const float WorldOffsetX = -14f;
            const float WorldOffsetY = 5f;
            localHeadOffset.X += WorldOffsetX;
            localHeadOffset.Y += WorldOffsetY;

            return center + localHeadOffset;
        }

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

            // Применяем fullRotation (наклон тела на вагонетке)
            if (Player.fullRotation != 0f)
            {
                float fc = (float)Math.Cos(Player.fullRotation);
                float fs = (float)Math.Sin(Player.fullRotation);
                float newX = rotatedX * fc - rotatedY * fs;
                float newY = rotatedX * fs + rotatedY * fc;
                rotatedX = newX;
                rotatedY = newY;
            }

            // Коррекция резкого взгляда вниз
            float angleDegrees = HeadRotation * (180f / (float)Math.PI);
            float effectiveAngle = angleDegrees * Player.direction;

            if (effectiveAngle < -10f && effectiveAngle >= -55f)
            {
                float correctionFactor = (Math.Abs(effectiveAngle) - 10f) / 45f;
                float offsetX = -correctionFactor * 5f * Player.direction;
                float offsetY = correctionFactor * -5f;

                rotatedX += offsetX;
                rotatedY += offsetY;
            }

            return headPos + new Vector2(rotatedX, rotatedY);
        }

        /// <summary>
        /// Тайл блокирует луч только если на нём есть активный полноценно твёрдый блок.
        /// Платформы и жидкость НЕ блокируют.
        /// </summary>
        private bool IsTileBlocking(int tileX, int tileY)
        {
            Tile tile = Main.tile[tileX, tileY];
            if (tile == null || !tile.HasTile)
                return false;

            if (tile.LiquidAmount > 0)
                return false;

            return Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        private const float OverdrawDistance = 16f;

        private float RaycastBeamLength(Vector2 origin, Vector2 direction, float maxLength)
        {
            if (direction.LengthSquared() < 0.0001f)
                return maxLength;

            const float step = 8f;
            int steps = (int)(maxLength / step);

            float lastClearDist = 0f;

            for (int i = 1; i <= steps; i++)
            {
                float dist = i * step;
                Vector2 samplePos = origin + direction * dist;
                int tileX = (int)(samplePos.X / 16f);
                int tileY = (int)(samplePos.Y / 16f);

                if (!WorldGen.InWorld(tileX, tileY, 1))
                    return lastClearDist;

                if (IsTileBlocking(tileX, tileY))
                {
                    float hitDist = RefineHitDistance(origin, direction, lastClearDist, dist);
                    return Math.Min(hitDist + OverdrawDistance, maxLength);
                }

                lastClearDist = dist;
            }

            return maxLength;
        }

        private float RefineHitDistance(Vector2 origin, Vector2 direction, float clearDist, float blockedDist)
        {
            for (int i = 0; i < 6; i++)
            {
                float mid = (clearDist + blockedDist) * 0.5f;
                Vector2 samplePos = origin + direction * mid;
                int tileX = (int)(samplePos.X / 16f);
                int tileY = (int)(samplePos.Y / 16f);

                if (IsTileBlocking(tileX, tileY))
                    blockedDist = mid;
                else
                    clearDist = mid;
            }

            return clearDist;
        }

        private void ApplyDynamicLighting(Vector2 flashlightPosition)
        {
            float beamLength = EffectiveBeamLength;

            const int samples = 28;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 samplePos = flashlightPosition + BeamDirection * (beamLength * t);
                float baseIntensity = MathHelper.Lerp(1.15f, 0.1f, t);
                float intensity = baseIntensity * LightIntensity;
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