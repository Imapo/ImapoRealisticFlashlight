using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace MinerHelmetFlashlight
{
    public class MinerHelmetFlashlight : Mod
    {
        public static Texture2D BeamTexture;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                Main.QueueMainThreadAction(GenerateBeamTexture);
            }
        }

        public override void Unload()
        {
            Texture2D texture = BeamTexture;
            BeamTexture = null;

            if (texture != null)
            {
                Main.QueueMainThreadAction(texture.Dispose);
            }
        }

        private void GenerateBeamTexture()
        {
            const int width = 128;
            const int height = 512;

            Color[] data = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                float halfWidth = MathHelper.Lerp(3f, width / 2f, (float)Math.Pow(t, 0.85));

                // ПЛАВНОЕ ЗАТУХАНИЕ С 50% ДЛИНЫ
                // Используем квадратичное затухание для мягкости
                float fadeStart = 0.5f;
                float lengthFade;
                
                if (t < fadeStart)
                {
                    lengthFade = 1f;
                }
                else
                {
                    // Квадратичное затухание: (1 - x)^2
                    float fadeT = (t - fadeStart) / (1f - fadeStart);
                    lengthFade = (float)Math.Pow(1f - fadeT, 2f);
                }

                for (int x = 0; x < width; x++)
                {
                    float dx = Math.Abs(x - width / 2f);
                    float alpha = 0f;

                    if (dx <= halfWidth)
                    {
                        float edgeFactor = 1f - (dx / halfWidth);
                        edgeFactor = (float)Math.Pow(edgeFactor, 0.6);
                        alpha = edgeFactor * lengthFade;
                    }

                    Color c;
                    if (alpha <= 0f)
                    {
                        c = Color.Transparent;
                    }
                    else
                    {
                        byte a = (byte)MathHelper.Clamp(alpha * 255f, 0, 255);
                        c = new Color(a, a, a, a);
                    }
                    data[y * width + x] = c;
                }
            }

            BeamTexture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
            BeamTexture.SetData(data);
        }
    }
}