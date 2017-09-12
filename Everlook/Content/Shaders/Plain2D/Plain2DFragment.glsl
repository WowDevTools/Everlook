#version 330 core

in vec2 UV;
out vec4 color;

uniform sampler2D Texture0;
uniform vec4 channelMask;

void main()
{
    vec4 texCol = texture(Texture0, UV);
	vec4 inter1 = vec4(texCol.rgb * channelMask.rgb, texCol.a);

	if (channelMask.a == 0.0f)
	{
		inter1.a = 1.0f;
	}

	color = inter1;
}