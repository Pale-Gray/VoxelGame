#version 410 core

out vec4 fColor;
out vec4 fNormal;

uniform sampler2D uTexture;

in vec3 vColor;
in vec3 vNormal;
in vec2 vTextureCoordinate;
in float vDirectionalLightIntensity;

void main() {
    
    vec4 textureColor = texture(uTexture, vTextureCoordinate);
    fColor = textureColor;
    fNormal = vec4(vNormal, 1.0);
}