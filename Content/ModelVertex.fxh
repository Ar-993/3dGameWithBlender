// Начало защиты от повторного подключения этого общего файла.
#ifndef ARS3D_MODEL_VERTEX
// Отметить, что объявления геометрии уже были подключены.
#define ARS3D_MODEL_VERTEX

// Подставить определения версии шейдеров и имени семантики позиции из общего файла.
#include "ShaderPlatform.fxh"

// Матрица положения, поворота и масштаба конкретного меша в игровом мире. Её задаёт C# перед draw call.
matrix World;
// Матрица перевода координат мира в систему координат камеры.
matrix View;
// Матрица перспективы или ортографической проекции: определяет, как пространство камеры отображается на экран.
matrix Projection;
// Положение камеры в мировых координатах. Нужно для расстояния до поверхности.
float3 CameraPosition;

// Максимальное количество матриц костей, которое содержит массив этого шейдера.
#define MAX_BONES 72
// Массив матриц текущей позы. Для статических мешей используется единичная матрица и соответствующие веса.
matrix Bones[MAX_BONES];

// Текстура материала конкретного меша. Это изображение материала модели, а не готовый кадр.
texture ModelTexture;
// Порог отсечения альфы: в C# для растительности передаётся 0.5, для остальных материалов обычно 0.
float AlphaCutoff = 0.0;

// Объявить способ чтения двухмерной текстуры материала.
sampler2D TextureSampler = sampler_state
{
    // Связать sampler с параметром ModelTexture, в который C# помещает Texture2D.
    Texture = <ModelTexture>;
    // При уменьшении изображения интерполировать соседние texel-значения.
    MinFilter = Linear;
    // При увеличении изображения интерполировать соседние texel-значения.
    MagFilter = Linear;
    // Если у текстуры есть mip-уровни, плавно интерполировать между подходящими уровнями детализации.
    MipFilter = Linear;
};

// Структура одной исходной вершины из VertexBuffer.
struct VertexShaderInput
{
    // Положение вершины модели; четвёртая компонента позволяет применять матрицы с переносом.
    float4 Position : POSITION0;
    // Нормаль поверхности в вершине: направление, перпендикулярное поверхности, для расчёта света.
    float3 Normal : NORMAL0;
    // UV модели: где эта вершина находится на текстуре материала. Это не экранные UV.
    float2 TextureCoordinate : TEXCOORD0;
    // Индексы четырёх костей, которые могут влиять на эту вершину.
    int4 BoneIndices : BLENDINDICES0;
    // Вес каждой из четырёх костей. Обычно веса в сумме дают 1.
    float4 BoneWeights : BLENDWEIGHT0;
};

// Структура результатов обработки вершины, передаваемых дальше по графическому конвейеру.
struct VertexShaderOutput
{
    // Позиция в пространстве отсечения: специальный выход, по которому GPU размещает треугольник на экране.
    float4 Position : SV_POSITION;
    // Первый пользовательский канал до pixel shader. Здесь передаём нормаль, несмотря на имя TEXCOORD.
    float3 Normal : TEXCOORD0;
    // Второй пользовательский канал: UV исходной текстуры модели.
    float2 TextureCoordinate : TEXCOORD1;
    // Третий пользовательский канал: координаты поверхности в игровом мире.
    float3 WorldPosition : TEXCOORD2;
};

// Color and depth use precisely the same skinning and world transforms.
// Vertex shader вызывается для вершин модели и возвращает структуру результатов.
VertexShaderOutput MainVS(VertexShaderInput input)
{
    // Создать переменную, в которой последовательно заполним выходные поля.
    VertexShaderOutput output;
    // Начать с нулевой матрицы; далее сложим четыре матрицы костей с их весами.
    matrix skinTransform = 0;
    // Вклад первой кости: её матрица умножается на первый вес вершины.
    skinTransform += Bones[input.BoneIndices.x] * input.BoneWeights.x;
    // Добавить взвешенную матрицу второй кости.
    skinTransform += Bones[input.BoneIndices.y] * input.BoneWeights.y;
    // Добавить взвешенную матрицу третьей кости.
    skinTransform += Bones[input.BoneIndices.z] * input.BoneWeights.z;
    // Добавить взвешенную матрицу четвёртой кости; при нулевом весе этот вклад равен нулю.
    skinTransform += Bones[input.BoneIndices.w] * input.BoneWeights.w;
    // Применить полученную смесь костей к исходной вершине: получить положение в текущей анимационной позе.
    float4 skinnedPosition = mul(input.Position, skinTransform);
    // Применить часть матрицы костей без переноса к нормали. Направление переносить как точку нельзя.
    float3 skinnedNormal = mul(input.Normal, (float3x3) skinTransform);
    // Перевести анимированную вершину в координаты мира.
    float4 worldPosition = mul(skinnedPosition, World);
    // Сначала перевести точку к камере, затем применить проекцию. Этот результат нужен GPU для рисования.
    output.Position = mul(mul(worldPosition, View), Projection);
    // Сохранить XYZ точки до View и Projection: по ним pixel shader вычислит расстояние и направление к свету.
    output.WorldPosition = worldPosition.xyz;
    // Перевести направление нормали в мировые координаты. Ниже в ToonShader оно нормализуется.
    output.Normal = mul(skinnedNormal, (float3x3) World);
    // Передать исходные UV модели дальше без изменения.
    output.TextureCoordinate = input.TextureCoordinate;
    // Вернуть собранные данные вершины. GPU затем интерполирует пользовательские каналы между вершинами треугольника.
    return output;
}

// Общая функция чтения цвета материала и удаления прозрачных отверстий.
float4 SampleMaterial(float2 uv)
{
    // Прочитать RGBA из текстуры материала в заданных UV; RGB — цвет, A — прозрачность.
    float4 color = tex2D(TextureSampler, uv);
    // Отбросить фрагмент, если выражение отрицательное. Отброшенный фрагмент не записывает ни цвет, ни глубину.
    clip(color.a - AlphaCutoff);
    // Вернуть прочитанный цвет материала оставшемуся пиксельному шейдеру.
    return color;
}

// Конец условного блока препроцессора: это обработка исходника при сборке, а не условие во время кадра.
#endif