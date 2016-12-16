#version 330 core

in vec2 UV;
in vec3 Normal;

out vec4 color;

uniform sampler2D texture0;
uniform sampler2D texture1;
uniform sampler2D texture2;

uniform vec4 colour0;
uniform vec4 colour1;
uniform vec4 baseDiffuseColour;


float near = 0.1;
float far  = 1000.0;

float LinearizeDepth(float depth)
{
    float z = depth * 2.0 - 1.0; // Back to NDC
    return (2.0 * near * far) / (far + near - z * (far - near));
}

void main()
{
	color = texture(texture0, UV);
    //float depth = LinearizeDepth(gl_FragCoord.z) / far; // divide by far for demonstration
    //color = vec4(vec3(depth), 1.0f);
}