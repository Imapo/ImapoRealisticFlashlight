using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace MinerHelmetFlashlight
{
	/// <summary>
	/// Главный класс мода. Здесь мы процедурно генерируем текстуру луча света
	/// (узкая у основания — у каски, широкая на конце — как в примере на фото),
	/// чтобы не тащить в мод лишний .png файл.
	/// </summary>
	public class MinerHelmetFlashlight : Mod
	{
		// Текстура луча, используется в FlashlightDrawLayer
		public static Texture2D BeamTexture;

		public override void Load()
		{
			// На сервере (dedicated server) графика не нужна и недоступна.
			// Создавать Texture2D можно только в главном потоке — Load()
			// выполняется в фоновом, поэтому откладываем через QueueMainThreadAction.
			if (!Main.dedServ)
			{
				Main.QueueMainThreadAction(GenerateBeamTexture);
			}
		}

		public override void Unload()
		{
			// Dispose текстуры тоже требует главного потока — как и создание.
			Texture2D texture = BeamTexture;
			BeamTexture = null;

			if (texture != null)
			{
				Main.QueueMainThreadAction(texture.Dispose);
			}
		}

		private void GenerateBeamTexture()
		{
			const int width = 128;   // ширина текстуры (в конце луча)
			const int height = 512;  // "длина" луча в текстурных пикселях

			Color[] data = new Color[width * height];

			for (int y = 0; y < height; y++)
			{
				// t = 0 у каски (узкий кончик), t = 1 на дальнем конце (широкий раструб)
				float t = y / (float)(height - 1);

				// Насколько широк луч в этой точке (плавное расширение конусом)
				float halfWidth = MathHelper.Lerp(3f, width / 2f, (float)Math.Pow(t, 0.85));

				// Общая яркость луча по длине: у источника ярче, к концу гаснет
				float lengthFade = MathHelper.Lerp(1f, 0.12f, t);

				for (int x = 0; x < width; x++)
				{
					float dx = Math.Abs(x - width / 2f);
					float alpha = 0f;

					if (dx <= halfWidth)
					{
						// Мягкий край луча (от центра к краю яркость падает)
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
						// ВАЖНО: Terraria рисует спрайты с предумноженной
						// альфой (premultiplied alpha) — RGB должен быть
						// равен альфа-каналу (для белого цвета), иначе
						// прозрачные края рисуются как почти непрозрачный
						// белый прямоугольник вместо мягкого затухания.
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
