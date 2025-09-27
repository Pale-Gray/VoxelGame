#version 410 core

out vec4 fColor;

uniform sampler2D uDepthStencil;
uniform sampler2D uAlbedo;
uniform sampler2D uNormal;

in vec2 vPosition;

vec3 lightDirection = normalize(vec3(-0.4, -1, 0.8));

float linearizeDepth(float depth, float near, float far)
{
    return near * far / (far + depth * (near - far));
}

void main()
{
    vec2 normalizedTextureCoordinate = vPosition;
    ivec2 textureCoordinate = ivec2(gl_FragCoord.xy);
    
    vec4 albedoTexture = texelFetch(uAlbedo, textureCoordinate, 0);
    vec4 normalTexture = texelFetch(uNormal, textureCoordinate, 0);
    vec4 depthStencilTexture = texelFetch(uDepthStencil, textureCoordinate, 0);
    
    if (albedoTexture.a == 0.0) discard;
    
    vec3 normal = normalTexture.xyz;
    float depthValue = linearizeDepth(depthStencilTexture.r, 0.1, 1000.0);
    
    vec3 albedo = albedoTexture.rgb;
    albedo *= mix(0.5, 1.0, dot(max(normal, vec3(0)) + 1.0 / 2.0, -lightDirection));
    
    vec3 color = albedo;
    
    fColor = vec4(color, albedoTexture.a < 1.0 ? 0.75 : 1.0);
}