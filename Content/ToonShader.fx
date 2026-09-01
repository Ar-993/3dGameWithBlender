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
    float4 texColor = tex2D(TextureSampler, input.TextureCoordinate);

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

    float3 totalLight = saturate(AmbientColor + sunLight + pointLight);
    float3 litColor = texColor.rgb * totalLight;
    float3 finalColor = lerp(texColor.rgb, litColor, LightingEnabled);
    return float4(finalColor, texColor.a);
}

technique ToonTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
