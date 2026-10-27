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
float3 CameraPosition;
float FogEnabled;
float3 FogColor;
float FogStart;
float FogEnd;
texture ModelTexture;

float LightingEnabled;
float3 AmbientColor;
float3 SunDirection;
float3 SunColor;
float SunIntensity;
float3 PointLightPosition;
float3 PointLightColor;
float PointLightIntensity;
float PointLightRange;

float3 FlashlightPosition;
float3 FlashlightDirection;
float3 FlashlightColor = float3(1.0, 0.96, 0.85);
float FlashlightRange = 25.0;
float FlashlightConeAngle = 0.86;
float FlashlightIntensity = 0.0;

float AlphaCutoff = 0.0;

float Alpha = 1.0; // По умолчанию 1.0 (полностью непрозрачный)
float UseTexture = 1.0; // 1.0 = брать цвет из текстуры, 0.0 = использовать GlassColor
float3 GlassColor = float3(1.0, 1.0, 1.0);

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
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output;
    matrix skinTransform = 0;
    skinTransform += Bones[input.BoneIndices.x] * input.BoneWeights.x;
    skinTransform += Bones[input.BoneIndices.y] * input.BoneWeights.y;
    skinTransform += Bones[input.BoneIndices.z] * input.BoneWeights.z;
    skinTransform += Bones[input.BoneIndices.w] * input.BoneWeights.w;
    float4 skinnedPosition = mul(input.Position, skinTransform);
    float3 skinnedNormal = mul(input.Normal, (float3x3)skinTransform);
    float4 worldPosition = mul(skinnedPosition, World);
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldPosition = worldPosition.xyz;
    output.Normal = mul(skinnedNormal, (float3x3)World);
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float3 N = normalize(input.Normal);
    
    float4 rawTex = tex2D(TextureSampler, input.TextureCoordinate);
    clip(rawTex.a - AlphaCutoff);
    float3 baseColor = lerp(GlassColor, rawTex.rgb, UseTexture);
    float4 texColor = float4(baseColor, rawTex.a);

    // Направленный свет: одинаково освещает всю сцену, как солнце.
    float3 sunToSurface = normalize(-SunDirection);
    float sunDiffuse = max(0.0, dot(N, sunToSurface));
    float3 sunLight = SunColor * sunDiffuse * SunIntensity;

    // Точечный свет: яркость плавно затухает к границе радиуса.
    float3 toPointLight = PointLightPosition - input.WorldPosition;
    float pointDistance = length(toPointLight);
    float3 pointDirection = toPointLight / max(pointDistance, 0.0001);
    float pointDiffuse = max(0.0, dot(N, pointDirection));
    float pointAttenuation = saturate(1.0 - pointDistance / PointLightRange);
    pointAttenuation *= pointAttenuation;
    float3 pointLight = PointLightColor * pointDiffuse *
        pointAttenuation * PointLightIntensity;
    
    float3 flashlightLight = float3(0.0, 0.0, 0.0);

    if (FlashlightIntensity > 0.0)
    {
        float3 toFlashlight = FlashlightPosition - input.WorldPosition;
        float flashDistance = length(toFlashlight);
        
        if (flashDistance < FlashlightRange)
        {
            float3 flashDir = toFlashlight / max(flashDistance, 0.0001);
            
            // Направление света ИЗ фонарика на поверхность
            float3 lightForward = normalize(FlashlightDirection);
            
            // Считаем угол между направлением фонарика и вектором до пикселя
            float spotEffect = dot(lightForward, -flashDir);
            
            // Если пиксель находится внутри конуса света
            if (spotEffect > FlashlightConeAngle)
            {
                // Плавное размытие краев конуса, чтобы свет не обрывался резко
                float spotIntensity = smoothstep(FlashlightConeAngle, FlashlightConeAngle + 0.04, spotEffect);
                
                // Квадратичное затухание света с расстоянием (как у точечного источника)
                float flashAttenuation = saturate(1.0 - flashDistance / FlashlightRange);
                flashAttenuation *= flashAttenuation;
                
                // Диффузное освещение пикселя
                float flashDiffuse = max(0.0, dot(N, flashDir));
                
                flashlightLight = FlashlightColor * flashDiffuse * flashAttenuation * spotIntensity * FlashlightIntensity;
            }
        }
    }

    // Фоновое освещение уже может достигать 1 на полу. Не срезаем вместе с ним
    // свет фонарика: его прибавка должна оставаться видимой на освещённых поверхностях.
    float3 sceneLight = saturate(AmbientColor + sunLight + pointLight);
    float3 litColor = texColor.rgb * (sceneLight + flashlightLight);
    float3 finalColor = lerp(texColor.rgb, litColor, LightingEnabled);
    float finalAlpha = saturate(rawTex.a * Alpha);
    
    float fogDistance = distance(CameraPosition, input.WorldPosition);
    float fogAmount = saturate((fogDistance - FogStart) / max(FogEnd - FogStart, 0.001)) * FogEnabled;
    finalColor = lerp(finalColor, FogColor, fogAmount);
    return float4(finalColor, finalAlpha);
}

technique ToonTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
