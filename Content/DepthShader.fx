// Подставить общий vertex shader, матрицы модели/камеры, кости, текстуру и отсечение альфы.
#include "ModelVertex.fxh"
// Подставить функции PackDepth и UnpackDepth: запись одного расстояния в RGB и его восстановление.
#include "DepthPacking.fxh"

// Параметр из C#: расстояние, которое кодируется как 1. В нашем конвейере это проверенное FogEnd, а не дальняя плоскость камеры.
float DepthFar;

// Pixel shader принимает выход общей обработки вершин и возвращает RGBA в первый цветовой выход. COLOR0 здесь обозначает выход, а не переменную.
float4 DepthPS(VertexShaderOutput input) : COLOR0
{
    // Keep foliage holes identical to the color pass.
    // Прочитать материал ради clip: прозрачные отверстия листвы должны пропускаться. Возвращаемый цвет здесь не нужен, но действие clip сохраняется.
    SampleMaterial(input.TextureCoordinate);
    // Вычислить евклидово расстояние от мировой позиции камеры до мировой позиции поверхности, учитывая X, Y и Z.
    float radialDistance = distance(CameraPosition, input.WorldPosition);
    // Разделить расстояние на DepthFar с защитой от нуля, упаковать его в RGB и установить альфу 1.
    return float4(PackDepth(radialDistance / max(DepthFar, 0.001)), 1.0);
}

// Объявить группу проходов эффекта с именем DepthTechnique.
technique DepthTechnique
{
    // Объявить единственный проход Depth. Его имя само по себе не определяет время запуска.
    pass Depth
    {
        // При сборке MGCB скомпилировать MainVS для выбранного профиля и назначить его vertex shader этого прохода.
        VertexShader = compile VS_SHADERMODEL MainVS();
        // При сборке эффекта скомпилировать DepthPS и назначить его pixel shader этого прохода.
        PixelShader = compile PS_SHADERMODEL DepthPS();
    }
};