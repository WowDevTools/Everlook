vec4 CombinersAdd(vec4 colour, vec4 texture)
{
	return colour + texture;
}

vec4 CombinersDecal(vec4 colour, vec4 texture)
{
	return vec4
	(
		mix(colour.rgb, texture.rgb, colour.a),
		colour.a
	);
}

vec4 CombinersFade(vec4 colour, vec4 texture)
{
	return vec4
	(
		mix(texture.rgb, colour.rgb, colour.a),
		colour.a
	);
}

vec4 CombinersMod(vec4 colour, vec4 texture)
{
	return colour * texture;
}

vec4 CombinersMod2x(vec4 colour, vec4 texture)
{
	return CombinersMod(colour, texture) * 2.0;
}

vec4 CombinersOpaque(vec4 colour, vec4 texture)
{
	return vec4
	(
		colour.rgb * texture.rgb,
		colour.a
	);
}

vec4 CombinersAddAdd(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersAdd(colour, texture0) + texture1;
}

vec4 CombinersAddMod(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersAdd(colour, texture0) * texture1;
}

vec4 CombinersAddMod2x(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersAddMod(colour, texture0, texture1) * 2.0;
}

vec4 CombinersAddOpaque(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		(colour.rgb + texture0.rgb) * texture1.rgb,
		colour.a + texture0.a
	);
}

vec4 CombinersModAdd(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersMod(colour, texture0) + texture1;
}

vec4 CombinersModAddNoAlpha(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		(colour.rgb * texture0.rgb) + texture1.rgb,
		colour.a * texture0.a
	);
}

vec4 CombinersModMod(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersMod(colour, texture0) * texture1;
}

vec4 CombinersModMod2x(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersModMod(colour, texture0, texture1) * 2.0;
}

vec4 CombinersModMod2xNoAlpha(vec4 colour, vec4 texture0, vec4 texture1)
{
	vec4 temp = CombinersModMod2x(colour, texture0, texture1);
	return vec4
	(
		temp.rgb,
		colour.a * texture0.a
	);
}

vec4 CombinersModOpaque(vec4 colour, vec4 texture0, vec4 texture1)
{
	vec4 temp = CombinersModMod(colour, texture0, texture1);
	return vec4
	(
		temp.rgb,
		colour.a * texture0.a
	);
}

vec4 CombinersMod2xAdd(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersMod2x(colour, texture0) + texture1;
}

vec4 CombinersMod2xMod2x(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersMod2x(CombinersMod2x(colour, texture0), texture1);
}

vec4 CombinersModOpaque(vec4 colour, vec4 texture0, vec4 texture1)
{
	// TODO
	return vec4(1.0);
}

vec4 CombinersOpaqueAdd(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersAdd(CombinersOpaque(colour, texture0), texture1);
}

vec4 CombinersOpaqueAddAlpha(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		CombinersMod(colour, texture0).rgb + (texture1.rgb * texture1.a),
		colour.a
	);
}

vec4 CombinersOpaqueAddAlphaAlpha(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		CombinersMod(colour, texture0).rgb + (texture1.rgb * texture1.a * texture0.a),
		colour.a
	);
}

vec4 CombinersOpaqueAddNoAlpha(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		CombinersMod(colour, texture0).rgb + texture1.rgb,
		colour.a
	);
}

vec4 CombinersOpaqueMod(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		CombinersModMod(colour, texture0, texture1).rgb,
		colour.a * texture1.a
	);
}

vec4 CombinersOpaqueMod2x(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersOpaqueMod(colour, texture0, texture1) * 2.0;
}

vec4 CombinersOpaqueMod2xNoAlpha(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		CombinersOpaqueMod2x(colour, texture0, texture1).rgb,
		colour.a
	);
}

vec4 CombinersOpaqueMod2xNoAlphaAlpha(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		CombinersMod(colour, texture0).rgb * mix(texture1.rgb * 2.0, vec3(1.0), texture0.a),
		colour.a
	);
}

vec4 CombinersOpaqueOpaque(vec4 colour, vec4 texture0, vec4 texture1)
{
	return vec4
	(
		CombinersModMod(colour, texture0, texture1).rgb,
		colour.a
	);
}
