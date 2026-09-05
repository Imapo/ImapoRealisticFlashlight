// FlashlightBeam.fx
// Аналитическая форма луча (та же кривая, что раньше была запечена в растровую
// текстуру 128x512), но считается непрерывно для каждого экранного пикселя —
// поэтому не имеет разрешения и не даёт "лесенок" ни при каком угле поворота.
//
// UV квада, на который рисуется эффект:
//   uv.x = 0..1  поперёк луча (0.5 = центральная линия)
//   uv.y = 0..1  вдоль луча   (0 = у каски, 1 = дальний конец)

sampler2D uImage0 : register(s0);

float3 uColor; // цвет луча, например float3(1.0, 0.98, 0.86) — тёплый белый

// Та же геометрия конуса, что и в MinerHelmetFlashlight.GenerateBeamTexture():
// halfWidth(t) = lerp(3, maxHalfWidth, t^0.85), только выражена в долях
// половины ширины квада (3/64), чтобы не зависеть от абсолютных пикселей.
static const float MinHalfWidthFrac = 3.0 / 64.0;
static const float ConeExponent = 0.85;
static const float EdgeSoftness = 0.6;

// Та же кривая затухания по длине: полная яркость до половины пути,
// дальше — квадратичное угасание.
static const float FadeStart = 0.5;

float4 FlashlightPS(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uv.y;
    float dx = abs(uv.x - 0.5) * 2.0; // 0 в центре, 1 на краю квада

    float coneHalfWidth = lerp(MinHalfWidthFrac, 1.0, pow(saturate(t), ConeExponent));

    float edgeFactor = saturate(1.0 - dx / coneHalfWidth);
    edgeFactor = pow(edgeFactor, EdgeSoftness);

    float lengthFade;
    if (t < FadeStart)
    {
        lengthFade = 1.0;
    }
    else
    {
        float fadeT = (t - FadeStart) / (1.0 - FadeStart);
        lengthFade = pow(saturate(1.0 - fadeT), 2.0);
    }

    float alpha = edgeFactor * lengthFade;

    // Премультиплицированная альфа — как и раньше в растровой текстуре
    return float4(uColor * alpha, alpha);
}

technique Flashlight
{
    pass P0
    {
        PixelShader = compile ps_2_0 FlashlightPS();
    }
}
