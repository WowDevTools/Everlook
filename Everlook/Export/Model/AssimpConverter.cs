//
//  AssimpConverter.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using Assimp;
using Warcraft.MDX;
using Warcraft.MDX.Data;
using Warcraft.MDX.Geometry.Skin;

namespace Everlook.Export.Model
{
    /// <summary>
    /// Converts models to Assimp representations.
    /// </summary>
    public static class AssimpConverter
    {
        /// <summary>
        /// Converts the given MDX model into an Assimp representation.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>The Assimp representation.</returns>
        public static Scene FromMDX(MDX model)
        {
            var scene = new Scene();

            var rootNode = new Node();
            scene.RootNode = rootNode;

            var modelNode = new Node(model.Name, rootNode);
            rootNode.Children.Add(modelNode);

            var defaultMaterial = new Material();
            scene.Materials.Add(defaultMaterial);

            if (model.Skins is null)
            {
                return scene;
            }

            foreach (var skin in model.Skins)
            {
                var skinNode = new Node($"LOD_{model.Skins.ToList().IndexOf(skin)}", modelNode);
                modelNode.Children.Add(skinNode);

                if (skin.VertexIndices is null)
                {
                    continue;
                }

                if (model.Vertices is null)
                {
                    continue;
                }

                var skinVerts = skin.VertexIndices.Select(i => model.Vertices?[i]).ToArray();

                if (skin.Sections is null)
                {
                    continue;
                }

                foreach (var section in skin.Sections)
                {
                    var mesh = new Mesh();
                    scene.Meshes.Add(mesh);

                    mesh.MaterialIndex = scene.Materials.IndexOf(defaultMaterial);

                    if (!(model.Bones is null))
                    {
                        var modelBones = model.Bones.Skip(section.StartBoneIndex).Take(section.BoneCount);
                        foreach (var modelBone in modelBones)
                        {
                            var bone = new Bone();

                            // TODO: Calculate offset matrices
                            mesh.Bones.Add(bone);
                        }
                    }

                    var batchNode = new Node($"Section_{skin.Sections.ToList().IndexOf(section)}", skinNode);
                    skinNode.Children.Add(batchNode);

                    batchNode.MeshIndices.Add(scene.Meshes.IndexOf(mesh));

                    var skinVertexIndexes = new Span<ushort>
                    (
                        skin.VertexIndices.ToArray(),
                        section.StartVertexIndex,
                        section.VertexCount
                    ).ToArray();

                    mesh.UVComponentCount[0] = 2;
                    mesh.UVComponentCount[1] = 2;

                    for (var i = 0; i < skinVertexIndexes.Length; ++i)
                    {
                        var localIndex = skinVertexIndexes[i];
                        var vertex = skinVerts[localIndex];

                        if (vertex is null)
                        {
                            continue;
                        }

                        mesh.Vertices.Add(new Vector3D(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
                        mesh.Normals.Add(new Vector3D(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z));

                        mesh.TextureCoordinateChannels[0].Add(new Vector3D(vertex.UV1.X, vertex.UV1.Y, 0.0f));
                        mesh.TextureCoordinateChannels[1].Add(new Vector3D(vertex.UV2.X, vertex.UV2.Y, 0.0f));

                        if (!mesh.HasBones)
                        {
                            continue;
                        }

                        for (var boneAttributeIndex = 0; boneAttributeIndex < 4; ++boneAttributeIndex)
                        {
                            var bone = mesh.Bones[vertex.BoneIndices[boneAttributeIndex]];

                            var weight = vertex.BoneWeights[boneAttributeIndex];
                            bone.VertexWeights.Add(new VertexWeight(i, weight));
                        }
                    }

                    if (skin.Triangles is null)
                    {
                        continue;
                    }

                    var triangleIndexes = new Span<ushort>
                    (
                        skin.Triangles.ToArray(),
                        section.StartTriangleIndex,
                        section.TriangleCount
                    ).ToArray();

                    mesh.SetIndices(triangleIndexes.Select(index => (int)index).ToArray(), 3);
                }
            }

            return scene;
        }
    }
}
