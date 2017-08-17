#version 330 core

in GeometryOut
{
	bool IsSimpleWireframeCase;
    bool IsSingleVertexVisible;
    noperspective vec3 EdgeDistances;

    flat vec2 A;
    flat vec2 ADir;
    flat vec2 B;
    flat vec2 BDir;
    flat vec2 ABDir;

    vec2 UV;
    vec3 Normal;
} gIn;

layout(location = 0) out vec4 FragColour;

uniform float alphaThreshold;

uniform sampler2D Diffuse0;
uniform sampler2D Specular0;
uniform sampler2D Diffuse1;
uniform sampler2D Specular1;
uniform sampler2D EnvMap;

uniform vec4 colour0;
uniform vec4 colour1;
uniform vec4 baseDiffuseColour;

/*
	Lighting - global directional
*/

uniform vec3 LightVector;
uniform vec4 LightColour;
uniform float LightIntensity;

/*
	Lighting - ambient
*/

uniform vec4 AmbientColour;
uniform float AmbientIntensity;

/*
	Solid wireframe
*/

uniform bool IsWireframeEnabled;
uniform vec4 WireframeColour;
uniform int WireframeLineWidth;
uniform int WireframeFadeWidth;

float SmoothStepEdge(float distance)
{
	return exp2(-2.0f * pow(distance, 2));
}

float DistanceToLine(vec2 F, vec2 Q, vec2 QDir)
{
	return sqrt(dot((Q - F), (Q - F)) - dot(QDir, (Q - F)));
}

void main()
{
	vec4 texCol = texture(Diffuse0, gIn.UV);

	if (IsWireframeEnabled)
	{
		float distance = 0.0f;
		if (gIn.IsSimpleWireframeCase)
		{
			distance = min(min(gIn.EdgeDistances.x, gIn.EdgeDistances.y), gIn.EdgeDistances.z);
		}
		else
		{
			if (gIn.IsSingleVertexVisible)
			{
				float dist1 = DistanceToLine(gl_FragCoord.xy, gIn.A, gIn.ADir);
				float dist2 = DistanceToLine(gl_FragCoord.xy, gIn.B, gIn.BDir);

				distance = min(dist1, dist2);
			}
			else
			{
				float dist1 = DistanceToLine(gl_FragCoord.xy, gIn.A, gIn.ADir);
                float dist2 = DistanceToLine(gl_FragCoord.xy, gIn.B, gIn.BDir);
                float dist3 = DistanceToLine(gl_FragCoord.xy, gIn.A, gIn.ABDir);

                distance = min(min(dist1, dist2), dist3);
			}
		}

		if (distance < (WireframeLineWidth - WireframeFadeWidth))
		{
			FragColour = WireframeColour;
			return;
		}
		else if (distance < WireframeLineWidth)
		{
			float relativeDistance = distance - (WireframeLineWidth - WireframeFadeWidth);

			vec4 baseColour = texCol;

			// If the pixel should ordinarily be discarded, fade with a fully transparent pixel instead
			if (texCol.a < alphaThreshold)
			{
				baseColour = vec4(0, 0, 0, 0);
			}

			FragColour = mix(baseColour, WireframeColour, SmoothStepEdge(relativeDistance));
			return;
		}
	}

	if (texCol.a < alphaThreshold)
    {
        discard;
    }

    FragColour = texCol;
}
