using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace MinerHelmetFlashlight
{
    /// <summary>
    /// Рисует луч фонарика отдельным проходом ПОСЛЕ того, как игроки/тайлы уже
    /// полностью отрисованы, в собственном изолированном Begin()/End().
    ///
    /// ПОЧЕМУ НЕ PlayerDrawLayer: PlayerDrawLayer.Draw() выполняется ВНУТРИ уже
    /// открытого Main.spriteBatch, которым управляет сама Terraria (собирает
    /// DrawData от всех слоёв в один общий batch). SpriteBatch в XNA/FNA не даёт
    /// узнать, с какими параметрами он был открыт — поэтому "прервать и вернуть
    /// как было" внутри такого слоя в принципе ненадёжно: ровно это и ломало
    /// рендер (аддитивный блендинг утекал на все последующие слои игрока).
    /// Здесь же мы открываем и закрываем свой собственный batch, не трогая
    /// ничей чужой — это безопасно в любом случае.
    /// </summary>
    public class FlashlightRenderSystem : ModSystem
    {
        public override void PostDrawTiles()
        {
            if (Main.gameMenu)
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
                return; // текстура ещё не успела создаться (первый кадр после загрузки)

            Vector2 direction = modPlayer.BeamDirection;
            if (direction.LengthSquared() < 0.0001f)
                return;
            direction.Normalize();

            Vector2 flashlightWorldPos = modPlayer.GetFlashlightWorldPosition();
            Vector2 screenPos = flashlightWorldPos - Main.screenPosition;
            float rotation = direction.ToRotation() - MathHelper.PiOver2;

            float beamLength = ModContent.GetInstance<FlashlightConfig>().BeamLength;
            const float maxWidth = 128f; // максимальная ширина конуса на дальнем конце, в пикселях

            var scale = new Vector2(maxWidth, beamLength);
            var origin = new Vector2(0.5f, 0f); // центр по ширине, у основания по длине (единицы текстуры 1x1)

            MiscShaderData shader = GameShaders.Misc["MinerHelmetFlashlight:Beam"];
            shader.Shader.CurrentTechnique.Passes[0].Apply();
            shader.Shader.Parameters["uColor"].SetValue(new Vector3(1f, 0.98f, 0.86f));

            // Внутри уже открытого нашего batch эффект должен быть привязан к
            // конкретному вызову Draw через смену текущего Effect графического
            // устройства перед этим конкретным Draw — SpriteSortMode.Immediate
            // рисует каждый Draw сразу, поэтому смена параметров шейдера между
            // вызовами Draw безопасна и не требует нового Begin/End.
            sb.GraphicsDevice.Textures[0] = pixel;
            sb.Draw(pixel, screenPos, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
