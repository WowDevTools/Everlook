//
//  Combiners.glsl
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

#ifndef Combiners_I
#define Combiners_I

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
	return CombinersAdd(CombinersAdd(colour, texture0), texture1);
}

vec4 CombinersAddMod(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersMod(CombinersAdd(colour, texture0), texture1);
}

vec4 CombinersAddMod2x(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersAddMod(colour, texture0, texture1) * 2.0;
}

vec4 CombinersAddOpaque(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersOpaque(CombinersAdd(colour, texture0), texture1);
}

vec4 CombinersModAdd(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersAdd(CombinersMod(colour, texture0), texture1);
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
	return CombinersMod(CombinersMod(colour, texture0), texture1);
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
	return CombinersOpaque(CombinersMod(colour, texture0), texture1);
}

vec4 CombinersMod2xAdd(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersAdd(CombinersMod2x(colour, texture0), texture1);
}

vec4 CombinersMod2xMod2x(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersMod2x(CombinersMod2x(colour, texture0), texture1);
}

vec4 CombinersMod2xOpaque(vec4 colour, vec4 texture0, vec4 texture1)
{
	return CombinersOpaque(CombinersMod2x(colour, texture0), texture1);
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
	return CombinersOpaque(CombinersMod(colour, texture0), texture1);
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
	return CombinersOpaque(CombinersOpaque(colour, texture0), texture1);
}

#endif