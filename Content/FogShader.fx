// Подставить определения версии шейдеров и имени семантики позиции из общего файла.
#include "ShaderPlatform.fxh"
// Подставить параметры тумана и единую функцию CalculateFogAmount.
#include "SceneFog.fxh"
// Подставить функции PackDepth и UnpackDepth: запись одного расстояния в RGB и его восстановление.
#include "DepthPacking.fxh"

// Параметр из C#: расстояние, которое кодируется как 1. В нашем конвейере это проверенное FogEnd, а не дальняя плоскость камеры.
float DepthFar;

// SpriteBatch supplies the depth texture in slot 0. This shader never reads
// scene color, so it can blend fog over that color without switching targets.
// Читать двухмерную текстуру из первого слота sampler. SpriteBatch.Draw(depthTarget, ...) помещает туда depthTarget.
sampler2D DepthSampler : register(s0);

// Получить цвет вершины SpriteBatch (здесь не используется) и экранные UV; вернуть RGBA наложения тумана.
float4 FogPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // Прочитать RGB расстояния в этой точке кадра, распаковать число 0..1 и вернуть масштаб мировых единиц.
    float radialDistance = UnpackDepth(tex2D(DepthSampler, uv).rgb) * DepthFar;
    // Вернуть RGB цвета тумана; в альфе вернуть его долю. Смешивание с изображением выполнит состояние fogBlend в C#.
    return float4(FogColor, CalculateFogAmount(radialDistance));
}

// Именованная группа проходов полноэкранного тумана.
technique FogTechnique
{
    // Единственный проход наложения.
    pass Fog
    {
        // Назначить наш pixel shader. Vertex shader здесь не задан: для прямоугольника используется встроенный шейдер SpriteBatch.
        PixelShader = compile PS_SHADERMODEL FogPS();
    }
};