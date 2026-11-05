// Подставить общий vertex shader, матрицы модели/камеры, кости, текстуру и отсечение альфы.
#include "ModelVertex.fxh"
// Подставить параметры тумана и единую функцию CalculateFogAmount.
#include "SceneFog.fxh"

// Переключатель освещения: 0 оставляет исходный цвет материала, 1 использует рассчитанный свет.
float LightingEnabled;
// Фоновая добавка освещения, присутствующая на любой поверхности без зависимости от направления нормали.
float3 AmbientColor;
// Направление движения солнечного света в мировых координатах.
float3 SunDirection;
// RGB солнечного света.
float3 SunColor;
// Множитель силы солнечного света.
float SunIntensity;
// Мировое положение точечного источника.
float3 PointLightPosition;
// RGB точечного источника.
float3 PointLightColor;
// Множитель силы точечного источника.
float PointLightIntensity;
// Радиус точечного света: на его границе затухание становится нулевым.
float PointLightRange;

// Мировое положение фонарика.
float3 FlashlightPosition;
// Направление, в котором светит фонарик.
float3 FlashlightDirection;
// Слегка тёплый цвет фонарика по умолчанию; C# может передать другое значение.
float3 FlashlightColor = float3(1.0, 0.96, 0.85);
// Дальность фонарика по умолчанию. Текущий SceneLighting.Apply передаёт 15, поэтому при игре используется значение из C#.
float FlashlightRange = 25.0;
// Порог скалярного произведения для конуса, то есть косинус угла, а не угол в градусах. C# передаёт 0.8944.
float FlashlightConeAngle = 0.86;
// Фонарик по умолчанию выключен; C# задаёт 0 или положительную интенсивность.
float FlashlightIntensity = 0.0;

// Множитель прозрачности всего материала; для стекла очередь передаёт MaterialOpacity.
float Alpha = 1.0; // По умолчанию 1.0 (полностью непрозрачный)
// Вес текстуры: 1 выбирает текстурный цвет, 0 — GlassColor. Текущая очередь передаёт 1.
float UseTexture = 1.0; // 1.0 = брать цвет из текстуры, 0.0 = использовать GlassColor
// Запасной RGB материала при UseTexture = 0.
float3 GlassColor = float3(1.0, 1.0, 1.0);

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // Привести нормаль к длине 1: после интерполяции между вершинами её длина может измениться.
    float3 N = normalize(input.Normal);
    
    // Прочитать материал и выполнить общий clip для отверстий с отсечением альфы.
    float4 rawTex = SampleMaterial(input.TextureCoordinate);
    // Смешать запасной цвет с текстурой: lerp(a,b,t) = a + (b-a)*t.
    float3 baseColor = lerp(GlassColor, rawTex.rgb, UseTexture);
    // Собрать выбранный RGB и альфу исходной текстуры в один цвет материала.
    float4 texColor = float4(baseColor, rawTex.a);

    // Направленный свет: одинаково освещает всю сцену, как солнце.
    // Получить единичное направление от поверхности к солнцу: знак минус разворачивает направление движения солнечного света.
    float3 sunToSurface = normalize(-SunDirection);
    // Оценить обращённость поверхности к свету; отрицательный результат для обратной стороны заменить нулём.
    float sunDiffuse = max(0.0, dot(N, sunToSurface));
    // Получить вклад солнца: цвет умножается на угол освещения и интенсивность.
    float3 sunLight = SunColor * sunDiffuse * SunIntensity;

    // Точечный свет: яркость плавно затухает к границе радиуса.
    // Вектор от текущей точки поверхности к точечному источнику.
    float3 toPointLight = PointLightPosition - input.WorldPosition;
    // Длина этого вектора: расстояние до точечного источника.
    float pointDistance = length(toPointLight);
    // Нормализовать вектор к свету, защитив деление при почти совпадающих точках.
    float3 pointDirection = toPointLight / max(pointDistance, 0.0001);
    // Посчитать диффузную долю точечного света по направлению нормали.
    float pointDiffuse = max(0.0, dot(N, pointDirection));
    // Возвести затухание в квадрат для более быстрого спада яркости с расстоянием.
    // Получить линейное затухание: 1 у источника, 0 на границе радиуса и дальше.
    float pointAttenuation = saturate(1.0 - pointDistance / PointLightRange);
    pointAttenuation *= pointAttenuation;
    // Начать итоговый вклад точечного источника: цвет умножается на диффузную долю.
    float3 pointLight = PointLightColor * pointDiffuse *
        // Продолжение предыдущего выражения: учесть квадрат затухания и интенсивность.
    pointAttenuation * PointLightIntensity;
    
    float3 flashlightLight = float3(0.0, 0.0, 0.0);

    if (FlashlightIntensity > 0.0)
    {
        // Вектор от поверхности к фонарику.
        float3 toFlashlight = FlashlightPosition - input.WorldPosition;
        // Расстояние до фонарика.
        float flashDistance = length(toFlashlight);
        
        // Ограничить расчёт областью действия фонарика.
        if (flashDistance < FlashlightRange)
        {
            // Единичное направление от поверхности к фонарику с защитой деления.
            float3 flashDir = toFlashlight / max(flashDistance, 0.0001);
            
            // Направление света ИЗ фонарика на поверхность
            // Единичное направление самого луча фонарика.
            float3 lightForward = normalize(FlashlightDirection);
            
            // Считаем угол между направлением фонарика и вектором до пикселя
            // Сравнить направление луча с направлением от фонарика к поверхности. В центре конуса результат близок к 1.
            float spotEffect = dot(lightForward, -flashDir);
            
            // Если пиксель находится внутри конуса света
            // Оставить только точки внутри конуса света.
            if (spotEffect > FlashlightConeAngle)
            {
                // Плавное размытие краев конуса, чтобы свет не обрывался резко
                // Плавно увеличить силу света от 0 у края конуса до 1 чуть глубже внутри конуса.
                float spotIntensity = smoothstep(FlashlightConeAngle, FlashlightConeAngle + 0.04, spotEffect);
                
                // Квадратичное затухание света с расстоянием (как у точечного источника)
                // Линейное затухание фонарика по расстоянию до границы его радиуса.
                float flashAttenuation = saturate(1.0 - flashDistance / FlashlightRange);
                // Возвести затухание в квадрат, как у точечного источника.
                flashAttenuation *= flashAttenuation;
                
                // Диффузное освещение пикселя
                // Учесть угол между поверхностью и направлением к фонарику.
                float flashDiffuse = max(0.0, dot(N, flashDir));
                
                // Перемножить цвет, угол, расстояние, положение в конусе и интенсивность: получить итоговый вклад фонарика.
                flashlightLight = FlashlightColor * flashDiffuse * flashAttenuation * spotIntensity * FlashlightIntensity;
            }
        }
    }

    // Фоновое освещение уже может достигать 1 на полу. Не срезаем вместе с ним
    // свет фонарика: его прибавка должна оставаться видимой на освещённых поверхностях.
    // Сложить фон, солнце и точечный источник; ограничить каждый RGB-канал диапазоном 0..1.
    float3 sceneLight = saturate(AmbientColor + sunLight + pointLight);
    // Умножить материал на свет. Фонарик добавляется после saturate, чтобы его прибавка сохранялась на уже освещённой поверхности.
    float3 litColor = texColor.rgb * (sceneLight + flashlightLight);
    // При LightingEnabled = 0 взять исходный материал, при 1 — освещённый; промежуточные значения смешивают оба.
    float3 finalColor = lerp(texColor.rgb, litColor, LightingEnabled);
    // Итоговая прозрачность: альфа текстуры умножается на непрозрачность материала и ограничивается 0..1.
    float finalAlpha = saturate(rawTex.a * Alpha);
    
    // Вычислить собственное расстояние этой поверхности до камеры — оно особенно важно для стекла.
    float fogDistance = distance(CameraPosition, input.WorldPosition);
    // Получить долю тумана из общей функции. У непрозрачного прохода FogEnabled = 0, поэтому здесь получится 0.
    float fogAmount = CalculateFogAmount(fogDistance);
    // Смешать цвет поверхности с туманом. Для стекла туман считается до смешивания стекла с фоном.
    finalColor = lerp(finalColor, FogColor, fogAmount);
    // Вернуть цвет и альфу материала для записи или прозрачного смешивания с текущей целью.
    return float4(finalColor, finalAlpha);
}

// Именованная группа проходов поверхностного материала.
technique ToonTechnique
{
    // Один геометрический проход этого эффекта.
    pass Pass1
    {
        // При сборке MGCB скомпилировать MainVS для выбранного профиля и назначить его vertex shader этого прохода.
        VertexShader = compile VS_SHADERMODEL MainVS();
        // Назначить скомпилированную MainPS пиксельным шейдером прохода.
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
