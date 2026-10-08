#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix World;
matrix View;
matrix Projection;

texture ModelTexture;

// Освещение.
float LightingEnabled;

float3 AmbientColor;

float3 SunDirection;
float3 SunColor;
float SunIntensity;

float3 PointLightPosition;
float3 PointLightColor;
float PointLightIntensity;
float PointLightRange;

// Туман.
float FogEnabled;
float3 FogColor;
float FogStart;
float FogEnd;

// Кости персонажа.
#define MAX_BONES 72
matrix Bones[MAX_BONES];

sampler2D TextureSampler = sampler_state
{
    Texture = <ModelTexture>;

    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TextureCoordinate : TEXCOORD0;

    int4 BoneIndices : BLENDINDICES0;
    float4 BoneWeights : BLENDWEIGHT0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;

    float3 Normal : TEXCOORD0;
    float2 TextureCoordinate : TEXCOORD1;
    float3 WorldPosition : TEXCOORD2;
    float ViewDepth : TEXCOORD3;
};

// Общие преобразования вершины.
VertexShaderOutput TransformVertex(
    float4 position,
    float3 normal,
    float2 textureCoordinate)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(position, World);
    float4 viewPosition = mul(worldPosition, View);

    output.Position = mul(viewPosition, Projection);

    output.WorldPosition = worldPosition.xyz;
    output.Normal = mul(normal, (float3x3)World);
    output.TextureCoordinate = textureCoordinate;

    output.ViewDepth = max(0.0, -viewPosition.z);

    return output;
}

// Персонажи: сначала применяются матрицы костей.
VertexShaderOutput MainVS(VertexShaderInput input)
{
    matrix skinTransform = 0;

    skinTransform +=
        Bones[input.BoneIndices.x] * input.BoneWeights.x;

    skinTransform +=
        Bones[input.BoneIndices.y] * input.BoneWeights.y;

    skinTransform +=
        Bones[input.BoneIndices.z] * input.BoneWeights.z;

    skinTransform +=
        Bones[input.BoneIndices.w] * input.BoneWeights.w;

    float4 skinnedPosition =
        mul(input.Position, skinTransform);

    float3 skinnedNormal =
        mul(input.Normal, (float3x3)skinTransform);

    return TransformVertex(
        skinnedPosition,
        skinnedNormal,
        input.TextureCoordinate);
}

// Статический уровень: расчёт костей не нужен.
VertexShaderOutput StaticVS(VertexShaderInput input)
{
    return TransformVertex(
        input.Position,
        input.Normal,
        input.TextureCoordinate);
}

// Текстура → освещение → туман.
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(
        TextureSampler,
        input.TextureCoordinate);

    // Полностью скрытый пиксель не требует расчёта света.
    if (FogEnabled > 0.5 && input.ViewDepth >= FogEnd)
        return float4(FogColor, texColor.a);

    float3 finalColor = texColor.rgb;

    if (LightingEnabled > 0.5)
    {
        float3 normal = normalize(input.Normal);

        // Солнечный свет.
        float3 sunToSurface = normalize(-SunDirection);

        float sunDiffuse = max(
            0.0,
            dot(normal, sunToSurface));

        float3 sunLight =
            SunColor * sunDiffuse * SunIntensity;

        // Точечный свет.
        float3 toPointLight =
            PointLightPosition - input.WorldPosition;

        float pointDistance = length(toPointLight);

        float3 pointDirection =
            toPointLight / max(pointDistance, 0.0001);

        float pointDiffuse = max(
            0.0,
            dot(normal, pointDirection));

        float pointAttenuation = saturate(
            1.0 -
            pointDistance / max(PointLightRange, 0.001));

        pointAttenuation *= pointAttenuation;

        float3 pointLight =
            PointLightColor *
            pointDiffuse *
            pointAttenuation *
            PointLightIntensity;


        float3 totalLight = saturate(
            AmbientColor + sunLight + pointLight);

        finalColor = texColor.rgb * totalLight;
    }

    // Туман применяется после освещения.
    if (FogEnabled > 0.5)
    {
        float fogAmount = saturate(
            (input.ViewDepth - FogStart) /
            max(FogEnd - FogStart, 0.001));

        // Плавное начало и окончание перехода.
        fogAmount =
            fogAmount * fogAmount *
            (3.0 - 2.0 * fogAmount);

        finalColor = lerp(
            finalColor,
            FogColor,
            fogAmount);
    }

    return float4(finalColor, texColor.a);
}

// Техника для анимированных моделей.
technique ToonTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

// Техника для статической геометрии.
technique StaticTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL StaticVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
