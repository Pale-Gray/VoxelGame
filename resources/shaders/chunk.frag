#version 410 core

out vec4 fColor;
out vec4 fNormal;
out vec4 fLight;

uniform sampler2D uTexture;

in vec3 vColor;
in vec3 vNormal;
in vec2 vTextureCoordinate;
in float vDirectionalLightIntensity;
in vec4 vLightValue;

void main() {
    vec4 textureColor = texture(uTexture, vTextureCoordinate);
    fColor = textureColor;
    fNormal = vec4(vNormal, 1.0);
    fLight = vLightValue;
}