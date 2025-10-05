#version 410 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTextureCoordinate;
layout (location = 3) in vec4 aLightValue;

uniform mat4 uProjection;
uniform mat4 uView;
uniform vec3 uChunkPosition;
uniform float uDrawTime;

out vec3 vPosition;
out vec3 vNormal;
out vec2 vTextureCoordinate;
out float vDirectionalLightIntensity;
out vec4 vLightValue;

float easeOutQuint(float t)
{
    return 1 - pow(1 - t, 5);
}

float easeOutCubic(float t)
{
    return 1 - pow(1 - t, 3);
}

float easeOutElastic(float t)
{
    float c4 = (2.0 * 3.14159) / 3.0;
    
    if (t == 0.0) return 0.0;
    if (t == 1.0) return 1.0;
    return pow(2.0, -10.0 * t) * sin((t * 10.0 - 0.75) * c4) + 1.0;
}

void main() {
    vPosition = (vec4(aPosition + (uChunkPosition * 32.0), 1.0) * vec4(1, 1, -1, 1) * uView * uProjection).xyz;
    vNormal = aNormal;
    vTextureCoordinate = aTextureCoordinate;
    vLightValue = aLightValue;
    
    float fac = easeOutCubic(clamp(uDrawTime / 2.0, 0.0, 1.0));
    
    gl_Position = vec4((aPosition * vec3(1, fac, 1)) + (uChunkPosition * 32.0), 1.0) * vec4(1, 1, -1, 1) * uView * uProjection;
}