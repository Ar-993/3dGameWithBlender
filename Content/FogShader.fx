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

float FogEnabled;
float3 FogColor;
float FogStart;
float FogEnd;

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
    float2 TextureCoordinate : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TextureCoordinate : TEXCOORD0;
    float ViewDepth : TEXCOORD1;
};

VertexShaderOutput FogVS(VertexShaderInput input)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);

    output.Position = mul(viewPosition, Projection);

    output.TextureCoordinate = input.TextureCoordinate;

    output.ViewDepth = max(0.0, -viewPosition.z);

    return output;
}

float4 FogPS(VertexShaderOutput input) : COLOR0
{
    float4 textureColor = tex2D(
        TextureSampler,
        input.TextureCoordinate);

    if (FogEnabled < 0.5)
        return textureColor;

    float fogAmount = saturate(
        (input.ViewDepth - FogStart) /
        max(FogEnd - FogStart, 0.001));

    fogAmount =
        fogAmount * fogAmount *
        (3.0 - 2.0 * fogAmount);

    float3 finalColor = lerp(
        textureColor.rgb,
        FogColor,
        fogAmount);

    return float4(finalColor, textureColor.a);
}

technique StaticTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL FogVS();
        PixelShader = compile PS_SHADERMODEL FogPS();
    }
};