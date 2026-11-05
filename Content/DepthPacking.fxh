// Защита от повторного объявления функций упаковки.
#ifndef ARS3D_DEPTH_PACKING
// Отметить, что функции уже подключены.
#define ARS3D_DEPTH_PACKING

// RGB stores a normalized radial distance with substantially more precision
// than a single 8-bit channel. Point sampling is required when decoding it.
// Принять одно нормализованное расстояние и вернуть три числа для каналов R, G, B.
float3 PackDepth(float depth)
{
    // Проверить дальнюю границу. Без отдельной ветки frac(1) дал бы 0 и даль превратилась бы в ближнее расстояние.
    if (depth >= 1.0)
        // Специальное точное представление значения 1; при декодировании оно снова даст 1.
        return float3(1.0, 0.0, 0.0);

    // Ограничить depth диапазоном 0..1, вычислить три масштаба числа и взять дробные части каждого: frac(12.3) = 0.3.
    float3 packed = frac(saturate(depth) * float3(1.0, 255.0, 65025.0));
    // Убрать перекрытие соседних разрядов: packed.yzz = (G, B, B), поэтому из R вычитается G/255, из G — B/255, B сохраняется.
    packed -= packed.yzz * float3(1.0 / 255.0, 1.0 / 255.0, 0.0);
    // Вернуть RGB, готовые к записи в цветовую текстуру.
    return packed;
}

// Принять три сохранённых канала и восстановить одно нормализованное расстояние.
float UnpackDepth(float3 packed)
{
    // A white clear means empty background at the far end of the fog range.
    // Вычислить R + G/255 + B/65025 и ограничить результат 0..1. Это обратное сложение разрядов.
    return saturate(dot(packed, float3(1.0, 1.0 / 255.0, 1.0 / 65025.0)));
}

// Конец условного блока препроцессора: это обработка исходника при сборке, а не условие во время кадра.
#endif