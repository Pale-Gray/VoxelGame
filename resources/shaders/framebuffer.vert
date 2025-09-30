#version 410 core

layout (location = 0) in vec2 aPosition;

out vec2 vPosition;

uniform float off;

void main()
{
    vPosition = aPosition;
    
    gl_Position = vec4((aPosition * 2.0) - 1.0, off, 1.0);
}