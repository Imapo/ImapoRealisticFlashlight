// FlashlightBeam.fx
// Аналитическая форма луча (та же кривая, что раньше была запечена в растровую
// текстуру 128x512), но считается непрерывно для каждого экранного пикселя —
// поэтому не имеет разрешения и не даёт "лесенок" ни при каком угле поворота.
//
// UV квада, на который рисуется эффект:
//   uv.x = 0..1  поперёк луча (0.5 = центральная линия)
//   uv.y = 0..1  вдоль луча   (0 = у каски, 1 = дальний конец ТЕКУЩЕЙ, обрезанной длины)

sampler2D uImage0 : register(s0);

float3 uColor;          // цвет луча, например float3(1.0, 0.98, 0.86) — тёплый белый
float uReferenceLength;  // "полная" длина луча без препятствий (config.BeamLength), пиксели —
                         // определяет, на каком АБСОЛЮТНОМ расстоянии конус набирает макс. ширину
float uAbsoluteLength;   // фактическая (обрезанная преградой) длина текущего луча, пиксели

// Та же геометрия конуса, что и в MinerHelmetFlashlight.GenerateBeamTexture():
// halfWidth(dist) = lerp(3, maxHalfWidth, (dist/uReferenceLength)^0.85), выражена
// в долях половины ширины квада (3/64).
//
// ВАЖНО: раньше ширина считалась от ДОЛИ ТЕКУЩЕЙ (обрезанной) длины (uv.y),
// поэтому даже у стены в 40px от головы конус всё равно раздувался до полной
// ширины — просто потому что это был "конец" короткого квада. Реальный
// прожектор так не работает: ширина зависит от АБСОЛЮТНОГО расстояния от
// источника, а не от того, где оказалась ближайшая преграда. Теперь ширина
// считается от uAbsoluteLength (сколько реальных пикселей от головы), делённого
// на uReferenceLength (эталонная максимальная длина) — короткий луч у близкой
// стены остаётся узким, как и должно быть.
static const float MinHalfWidthFrac = 3.0 / 64.0;
static const float ConeExponent = 0.85;
static const float EdgeSoftness = 0.6;

// Плавное угасание на кончике теперь тоже в АБСОЛЮТНЫХ пикселях, а не в доле
// длины — иначе на коротких лучах (близкая преграда) зона угасания сжималась
// до нескольких пикселей и выглядела как резкий обрыв.
static const float FadeDistance = 24.0;

float4 FlashlightPS(float2 uv : TEXCOORD0) : COLOR0
{
    float dist = uv.y * uAbsoluteLength; // абсолютное расстояние от головы, пиксели
    float dx = abs(uv.x - 0.5) * 2.0;    // 0 в центре, 1 на краю квада

    float widthT = saturate(dist / max(uReferenceLength, 1.0));
    float coneHalfWidth = lerp(MinHalfWidthFrac, 1.0, pow(widthT, ConeExponent));

    float edgeFactor = saturate(1.0 - dx / coneHalfWidth);
    edgeFactor = pow(edgeFactor, EdgeSoftness);

    float distFromTip = uAbsoluteLength - dist;
    float lengthFade = saturate(distFromTip / FadeDistance);

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
