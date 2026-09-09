using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace MinerHelmetFlashlight
{
    public class FlashlightRenderSystem : ModSystem
    {
        public override void PostDrawTiles()
        {
            // При открытой полноэкранной карте (клавиша M) Terraria переключается
            // на другой путь отрисовки (иконки карты вместо обычной сцены), и
            // Main.GameViewMatrix/Main.screenPosition в этот момент не соответствуют
            // обычной игровой камере — из-за этого луч "улетал" в угол экрана.
            // Рисовать его при открытой карте всё равно не имеет смысла — сцена
            // и так не видна.
            if (Main.gameMenu || Main.mapFullscreen)
                return;

            SpriteBatch sb = Main.spriteBatch;
            bool batchOpen = false;

            foreach (Player player in Main.player)
            {
                if (player == null || !player.active || player.dead)
                    continue;

                FlashlightPlayer modPlayer = player.GetModPlayer<FlashlightPlayer>();
                if (!modPlayer.HasFlashlight)
                    continue;

                if (!batchOpen)
                {
                    sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                        DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchOpen = true;
                }

                DrawBeam(sb, player, modPlayer);
            }

            if (batchOpen)
                sb.End();
        }

        private void DrawBeam(SpriteBatch sb, Player player, FlashlightPlayer modPlayer)
        {
            Texture2D pixel = MinerHelmetFlashlight.PixelTexture;
            if (pixel == null)
                return;

            Vector2 direction = modPlayer.BeamDirection;
            if (direction.LengthSquared() < 0.0001f)
                return;
            direction.Normalize();

            Vector2 flashlightWorldPos = modPlayer.GetFlashlightWorldPosition();
            Vector2 screenPos = flashlightWorldPos - Main.screenPosition;
            float rotation = direction.ToRotation() - MathHelper.PiOver2;

            float beamLength = modPlayer.EffectiveBeamLength;
            const float maxWidth = 128f;

            var scale = new Vector2(maxWidth, beamLength);
            var origin = new Vector2(0.5f, 0f);

            MiscShaderData shader = GameShaders.Misc["MinerHelmetFlashlight:Beam"];
            Vector3 configColor = ModContent.GetInstance<FlashlightConfig>().LightColor;
            shader.Shader.Parameters["uColor"].SetValue(configColor * modPlayer.FlickerIntensity);
            shader.Shader.Parameters["uReferenceLength"].SetValue(900f);
            shader.Shader.Parameters["uAbsoluteLength"].SetValue(beamLength);
            shader.Shader.CurrentTechnique.Passes[0].Apply();

            sb.GraphicsDevice.Textures[0] = pixel;
            sb.Draw(pixel, screenPos, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}