// Подставить определения версии шейдеров и имени семантики позиции из общего файла.
#include "ShaderPlatform.fxh"

// Множитель яркости RGB: 1 сохраняет её, 0.5 уменьшает значения вдвое, 2 увеличивает вдвое. Это множитель, а не экспозиция в EV.
float Exposure = 1.0;
// Насыщенность: 0 выбирает серый, 1 сохраняет цвет, больше 1 усиливает отличие каналов от серого.
float Saturation = 1.0;
// Сила затемнения краёв: 0 отключает виньетку, 1 даёт максимальное затемнение.
float VignetteStrength = 0.0;
// Отношение ширины цели к высоте. C# передаёт, например, 1920/1080 = 1.7778.
float VignetteAspectRatio = 1.0;

// Прочитать готовый цвет сцены из первого слота. SpriteBatch помещает туда источник sceneTarget.
sampler2D SceneSampler : register(s0);

// Получить данные полноэкранного прямоугольника. uv — координаты на кадре; vertexColor сейчас не используется. Вернуть итоговый RGBA.
float4 PostProcessPS(float4 vertexColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // Прочитать пиксель исходного изображения с настроенной в C# фильтрацией LinearClamp.
    float4 source = tex2D(SceneSampler, uv);
    // Изменить яркость каждого RGB-канала одинаковым множителем; альфа пока остаётся в source.a.
    float3 color = source.rgb * Exposure;
    // Вычислить взвешенную оценку яркости: R*0.2126 + G*0.7152 + B*0.0722.
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    // Смешать серый цвет той же оценочной яркости с цветным. lerp не ограничивает третий аргумент диапазоном 0..1.
    color = lerp(float3(luminance, luminance, luminance), color, Saturation);

    // Перенести начало UV в центр изображения и учесть пропорции кадра умножением X на aspect ratio.
    float2 centered = (uv - 0.5) * float2(VignetteAspectRatio, 1.0);
    // По квадрату расстояния до центра плавно получить 0 в центральной области и 1 ближе к дальним краям.
    float edge = smoothstep(0.15, 0.75, dot(centered, centered));
    // Умножить RGB на затемняющий коэффициент; saturate ограничивает силу виньетки диапазоном 0..1.
    color *= 1.0 - saturate(VignetteStrength) * edge;
    // Собрать обработанные RGB и исходную альфу: прозрачность здесь сохраняется.
    return float4(color, source.a);
}

// Именованная группа проходов обработки изображения.
technique PostProcessTechnique
{
    // Единственный проход постобработки этого эффекта.
    pass PostProcess
    {
        // Использовать наш pixel shader и встроенную обработку вершин SpriteBatch.
        PixelShader = compile PS_SHADERMODEL PostProcessPS();
    }
};