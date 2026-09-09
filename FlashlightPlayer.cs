using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using System.Collections.Generic;

namespace MinerHelmetFlashlight
{
    public class FlashlightPlayer : ModPlayer
    {
        public Vector2 BeamDirection = Vector2.UnitX;
        public float HeadRotation = 0f;
        public float EffectiveBeamLength = 0f; // длина луча, обрезанная первым твёрдым блоком на пути

        // Множитель яркости от мигания (1 = нормально, меньше = притухло).
        // Используется и лучом (FlashlightRenderSystem), и освещением тайлов
        // (ApplyDynamicLighting) — чтобы оба гасли синхронно.
        public float FlickerIntensity = 1f;

        private const float FlashlightLocalX = 1f;
        private const float FlashlightLocalY = -4f;
        private float _dustSpawnTimer;
        private Vector2 _previousFlashlightPosition;
        private float _dashCooldown;
        private bool _isDashing;

        // --- Мигание "садящейся батарейки" ---
        private bool _isFlickering;
        private int _flickerElapsedTicks;
        private int _flickerTotalTicks;
        private readonly List<(int start, int end, float minIntensity)> _flickerBlips = new();
        private int _fallTimer; // сколько тиков подряд падаем быстрее порога
        private const float BaseFlickerChancePerTick = 1f / (90f * 60f); // в среднем раз в ~90 сек
        private const float FallingFlickerChancePerTick = 1f / (2f * 60f); // в среднем раз в ~2 сек, пока падаем
        private const float FallSpeedThreshold = 10f; // скорость падения, начиная с которой считаем "с высоты"
        private const int FallSustainTicks = 25; // сколько тиков подряд нужно падать быстрее порога
        private const float DamageFlickerChance = 0.5f;

        // === Накопитель "тряски" от движения ===
        private float _movementShake = 0f; // 0 = стоит на месте, 1 = максимально трясётся
        private const float MovementShakeGainRate = 0.05f;  // скорость накопления при движении
        private const float MovementShakeDecayRate = 0.02f; // скорость затухания в покое
        private const float MovementSpeedThreshold = 2f;    // порог скорости, чтобы считалось "движением"
        private const float MaxFlickerMultiplier = 8f;      // максимальный множитель шанса от тряски

        // Стандартные значения (вместо настроек)
        private const float BeamLength = 900f;
        private const float LightIntensity = 1.0f;
        private const float DustFrequency = 1.0f;

        public bool HasFlashlight
        {
            get
            {
                // 1. Сначала проверяем слоты аксессуаров (3-9)
                // Если каска там — она всегда видна, свет есть
                for (int i = 3; i < Player.armor.Length; i++)
                {
                    if (Player.armor[i] != null && Player.armor[i].type == ItemID.MiningHelmet)
                        return true;
                }
                
                // 2. Если каски нет в аксессуарах, проверяем слот шлема брони
                // Но только если в слотах аксессуаров НЕТ других шлемов
                bool hasAccessoryHelmet = false;
                for (int i = 3; i < Player.armor.Length; i++)
                {
                    if (Player.armor[i] != null && Player.armor[i].headSlot >= 0)
                    {
                        // В аксессуарах есть предмет с визуальным отображением головы
                        hasAccessoryHelmet = true;
                        break;
                    }
                }
                
                // Если в аксессуарах нет шлема, проверяем каску в слоте брони
                if (!hasAccessoryHelmet && 
                    Player.armor[0] != null && 
                    Player.armor[0].type == ItemID.MiningHelmet)
                {
                    return true;
                }
                
                return false;
            }
        }

        public override void PostHurt(Player.HurtInfo info)
        {
            if (!HasFlashlight)
                return;
            if (Main.rand.NextFloat() < DamageFlickerChance)
            {
                StartFlicker();
            }
        }

        private void StartFlicker()
        {
            if (_isFlickering)
                return; // не перебиваем уже идущее мигание новым

            _isFlickering = true;
            _flickerElapsedTicks = 0;
            _flickerBlips.Clear();

            int blipCount = Main.rand.Next(3, 7); // 3-6 всплесков (было 2-4)
            int cursor = Main.rand.Next(0, 5);

            for (int i = 0; i < blipCount; i++)
            {
                // Разная длительность: от очень коротких (1-2 тика) до длинных (10-20 тиков)
                int duration = Main.rand.Next(1, 21);

                // Разная интенсивность: от почти полного выключения (0.05) до лёгкого потускнения (0.7)
                // Более реалистичное распределение: чаще слабые потускнения, реже сильные
                float intensityRoll = Main.rand.NextFloat();
                float minIntensity;
                if (intensityRoll < 0.3f)
                    minIntensity = Main.rand.NextFloat(0.05f, 0.2f); // сильное потускнение (30% случаев)
                else if (intensityRoll < 0.7f)
                    minIntensity = Main.rand.NextFloat(0.2f, 0.5f); // среднее потускнение (40% случаев)
                else
                    minIntensity = Main.rand.NextFloat(0.5f, 0.8f); // лёгкое потускнение (30% случаев)

                _flickerBlips.Add((cursor, cursor + duration, minIntensity));
                cursor += duration + Main.rand.Next(3, 10); // более длинные паузы между всплесками
            }

            _flickerTotalTicks = cursor + Main.rand.Next(5, 15);
        }

        private void UpdateFlicker()
        {
            if (!_isFlickering)
            {
                bool fallingFromHeight = _fallTimer >= FallSustainTicks;

                // Базовый шанс
                float chance = BaseFlickerChancePerTick;

                // При падении — фиксированный высокий шанс
                if (fallingFromHeight)
                {
                    chance = FallingFlickerChancePerTick;
                }
                else
                {
                    // При движении — умножаем базовый шанс на множитель от тряски
                    // Формула: 1 + shake * (MaxFlickerMultiplier - 1)
                    // При shake=0 → множитель 1 (базовый шанс)
                    // При shake=1 → множитель MaxFlickerMultiplier (в 8 раз чаще)
                    float shakeMultiplier = 1f + _movementShake * (MaxFlickerMultiplier - 1f);
                    chance *= shakeMultiplier;
                }

                if (Main.rand.NextFloat() < chance)
                {
                    StartFlicker();
                }
            }

            if (!_isFlickering)
            {
                // Плавное возвращение к нормальной яркости
                if (FlickerIntensity < 1f)
                {
                    FlickerIntensity = MathHelper.Lerp(FlickerIntensity, 1f, 0.1f);
                    if (Math.Abs(FlickerIntensity - 1f) < 0.01f)
                        FlickerIntensity = 1f;
                }
                return;
            }

            float intensity = 1f;
            foreach (var blip in _flickerBlips)
            {
                if (_flickerElapsedTicks >= blip.start && _flickerElapsedTicks < blip.end)
                {
                    intensity = blip.minIntensity;

                    // Добавляем небольшой "шум" для реалистичности (дрожание яркости)
                    if (intensity < 0.9f)
                    {
                        float noise = Main.rand.NextFloat(-0.05f, 0.05f);
                        intensity += noise;
                        intensity = MathHelper.Clamp(intensity, 0.05f, 0.95f);
                    }

                    break;
                }
            }

            FlickerIntensity = intensity;
            _flickerElapsedTicks++;

            if (_flickerElapsedTicks >= _flickerTotalTicks)
            {
                _isFlickering = false;
                FlickerIntensity = 1f;
            }
        }

        private void UpdateFallTracking()
        {
            if (!Player.mount.Active && Player.velocity.Y > FallSpeedThreshold && !Player.wet)
            {
                _fallTimer++;
            }
            else
            {
                _fallTimer = 0;
            }
        }

        /// <summary>
        /// Обновляет "накопитель тряски": растёт при активном движении,
        /// плавно падает в покое. Используется как множитель шанса мигания.
        /// </summary>
        private void UpdateMovementShake()
        {
            // Скорость игрока (без учёта направления, только величина)
            float speed = Player.velocity.Length();

            if (speed > MovementSpeedThreshold)
            {
                // Чем быстрее движение — тем быстрее копится тряска
                // Нормализуем: на скорости ~8 (бег) копится максимально быстро
                float normalizedSpeed = Math.Min(speed / 8f, 1f);
                _movementShake += MovementShakeGainRate * normalizedSpeed;
            }
            else
            {
                // В покое тряска медленно затухает
                _movementShake -= MovementShakeDecayRate;
            }

            _movementShake = MathHelper.Clamp(_movementShake, 0f, 1f);
        }

        public override void PostUpdateMiscEffects()
        {
            if (!HasFlashlight)
                return;

            UpdateMovementShake(); // ← Обновляем накопитель тряски
            UpdateFallTracking();
            UpdateFlicker();

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

            Vector2 flashlightPosition = GetHeadWorldPosition();

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
        /// ИСПРАВЛЕНИЕ: сохранили минус в angleFactor и увеличили WorldOffsetY
        /// для лучшей компенсации при наклоне влево.
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

            // Поправка (WorldOffsetX/Y) откалибрована под конкретный угол наклона
            // рельсов (~45°, fullRotation≈±0.79). МИНУС в angleFactor КРИТИЧЕСКИ ВАЖЕН:
            // он обеспечивает правильную симметрию для обоих направлений наклона.
            // Без минуса при наклоне влево (fullRotation < 0) поправка применялась
            // в противоположную сторону, из-за чего голова оказывалась значительно выше.
            const float CalibrationAngle = 0.79f; // угол, на котором подбирали WorldOffsetX/Y
            const float WorldOffsetX = -14f;
            const float WorldOffsetY = 5f; // УВЕЛИЧИЛИ с 5f до 8f для лучшей компенсации
            float angleFactor = -(float)Math.Sin(Player.fullRotation) / (float)Math.Sin(CalibrationAngle);

            localHeadOffset.X += WorldOffsetX * angleFactor;
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
            
            // Получаем цвет из конфига один раз, вне цикла
            Vector3 configColor = ModContent.GetInstance<FlashlightConfig>().LightColor;
            
            const int samples = 28;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 samplePos = flashlightPosition + BeamDirection * (beamLength * t);
                float baseIntensity = MathHelper.Lerp(1.15f, 0.1f, t);
                float intensity = baseIntensity * LightIntensity * FlickerIntensity;
                
                // Используем цвет из конфига вместо захардкоженных значений
                Lighting.AddLight(
                    samplePos,
                    configColor.X * intensity, // R
                    configColor.Y * intensity, // G
                    configColor.Z * intensity  // B
                );
            }
        }
    }
}