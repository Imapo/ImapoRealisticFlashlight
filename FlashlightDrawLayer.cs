using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace MinerHelmetFlashlight
{
    public class FlashlightDrawLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            FlashlightPlayer modPlayer = drawInfo.drawPlayer.GetModPlayer<FlashlightPlayer>();
            return modPlayer.HasFlashlight && MinerHelmetFlashlight.BeamTexture != null;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            FlashlightPlayer modPlayer = player.GetModPlayer<FlashlightPlayer>();
            Texture2D texture = MinerHelmetFlashlight.BeamTexture;

            if (texture == null)
                return;

            Vector2 flashlightWorldPos = modPlayer.GetFlashlightWorldPosition();
            Vector2 screenPos = flashlightWorldPos - Main.screenPosition;

            Vector2 direction = modPlayer.BeamDirection;
            if (direction.LengthSquared() < 0.0001f)
                return;
            direction.Normalize();

            float rotation = direction.ToRotation() - MathHelper.PiOver2;
            
            // БЕРЁМ ДЛИНУ ИЗ НАСТРОЕК
            float beamLength = ModContent.GetInstance<FlashlightConfig>().BeamLength;
            float lengthScale = beamLength / texture.Height;
            
            Vector2 origin = new Vector2(texture.Width / 2f, 0f);
            Color beamColor = new Color(255, 250, 220, 255);

            DrawData beamData = new DrawData(
                texture,
                screenPos,
                null,
                beamColor,
                rotation,
                origin,
                1f,
                SpriteEffects.None,
                0
            );
            beamData.scale = new Vector2(1f, lengthScale);
            drawInfo.DrawDataCache.Add(beamData);
        }
    }
}