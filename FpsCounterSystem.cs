using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using Terraria;
using Terraria.ModLoader;

namespace MinerHelmetFlashlight
{
    /// <summary>
    /// Диагностический счётчик FPS — выводит текущую частоту кадров в левом
    /// верхнем углу экрана. Нужен, чтобы точно видеть, при какой герцовке
    /// воспроизводится баг с раздвоением луча, а не полагаться на "около 120".
    ///
    /// Считает через PostDrawInterface — этот хук вызывается на каждый кадр
    /// ОТРИСОВКИ (а не тик логики), поэтому честно отражает реальный FPS,
    /// в отличие от чего-то завязанного на Update (тот всегда ровно 60/сек).
    /// </summary>
    public class FpsCounterSystem : ModSystem
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private int _frameCount;
        private double _lastSampleTime;
        private int _currentFps;

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            _frameCount++;

            double now = _stopwatch.Elapsed.TotalSeconds;
            if (now - _lastSampleTime >= 1.0)
            {
                _currentFps = _frameCount;
                _frameCount = 0;
                _lastSampleTime = now;
            }

            if (Main.gameMenu)
                return;

            Utils.DrawBorderString(spriteBatch, $"FPS: {_currentFps}", new Vector2(20f, 20f), Color.White);
        }
    }
}
