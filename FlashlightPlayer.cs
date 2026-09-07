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
        public float EffectiveBeamLength = 0f;

        private const float FlashlightLocalX = 1f;
        private const float FlashlightLocalY = -4f;

        private float _dustSpawnTimer;
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

            if (direction.LengthSquared() > 0.0001f)
                direction.Normalize();

            BeamDirection = direction;

            Vector2 flashlightPosition = GetFlashlightWorldPosition();

            EffectiveBeamLength = RaycastBeamLength(flashlightPosition, BeamDirection, BeamLength);

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

            if (Player.mount.Active)
            {
                Vector2 center = Player.MountedCenter;
                Vector2 headOffset = new Vector2(
                    Player.direction * 6f,
                    -14f - Player.gfxOffY
                );
                return center + headOffset;
            }

            Vector2 defaultCenter = Player.position + new Vector2(Player.width / 2f, Player.height / 2f);
            Vector2 defaultOffset = new Vector2(
                Player.direction * 6f,
                -Player.height / 2f + 8f - Player.gfxOffY
            );
            return defaultCenter + defaultOffset;
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