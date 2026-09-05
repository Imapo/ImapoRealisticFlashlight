using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace MinerHelmetFlashlight
{
    public class MinerHelmetFlashlight : Mod
    {
        // Своя гарантированная текстура 1x1 — не полагаемся на размеры
        // TextureAssets.MagicPixel (в актуальных версиях tModLoader текстуры
        // пакуются в атлас, и её реальный исходный размер может оказаться
        // не тем, что ожидается при масштабировании через scale).
        public static Texture2D PixelTexture;

        public override void Load()
        {
            // На сервере графики нет — ни шейдер, ни текстуру грузить нельзя
            if (!Main.dedServ)
            {
                GameShaders.Misc["MinerHelmetFlashlight:Beam"] =
                    new MiscShaderData(Assets.Request<Effect>("Assets/Effects/FlashlightBeam"), "Flashlight");

                Main.QueueMainThreadAction(() =>
                {
                    PixelTexture = new Texture2D(Main.graphics.GraphicsDevice, 1, 1);
                    PixelTexture.SetData(new[] { Color.White });
                });
            }
        }

        public override void Unload()
        {
            Texture2D texture = PixelTexture;
            PixelTexture = null;

            if (texture != null)
            {
                Main.QueueMainThreadAction(texture.Dispose);
            }
        }
    }
}
