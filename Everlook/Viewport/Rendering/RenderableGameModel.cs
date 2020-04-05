//
//  RenderableGameModel.cs
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
using System.IO;
using System.Linq;
using System.Numerics;
using Everlook.Configuration;
using Everlook.Exceptions.Shader;
using Everlook.Utility;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using Silk.NET.OpenGL;
using Warcraft.Core;
using Warcraft.Core.Extensions;
using Warcraft.Core.Shading.MDX;
using Warcraft.Core.Structures;
using Warcraft.DBC.Definitions;
using Warcraft.DBC.SpecialFields;
using Warcraft.MDX;
using Warcraft.MDX.Geometry;
using Warcraft.MDX.Geometry.Skin;
using Warcraft.MDX.Visual;

namespace Everlook.Viewport.Rendering
{
    /// <summary>
    /// Represents a renderable Game Object Model.
    /// </summary>
    public sealed class RenderableGameModel :
        GraphicsObject,
        IInstancedRenderable,
        ITickingActor,
        IDefaultCameraPositionProvider,
        IModelInfoProvider,
        IBoundedModel
    {
        /// <inheritdoc />
        public bool IsStatic => false;

        /// <inheritdoc />
        public ProjectionType Projection => ProjectionType.Perspective;

        /// <inheritdoc />
        public Vector3 DefaultCameraPosition
        {
            get
            {
                if (!this.IsInitialized)
                {
                    return Vector3.Zero;
                }

                var vec4 = Vector4.Transform
                (
                    new Vector4
                    (
                        _model.BoundingBox.GetCenterCoordinates(),
                        1.0f
                    ),
                    this.ActorTransform.GetModelMatrix()
                );

                return new Vector3(vec4.X, vec4.Y, vec4.Z);
            }
        }

        /// <summary>
        /// The model contained by this renderable game object.
        /// </summary>
        private readonly MDX _model;

        /// <summary>
        /// Gets or sets a value indicating whether this object has been disposed.
        /// </summary>
        private bool IsDisposed { get; set; }

        /// <inheritdoc />
        public Transform ActorTransform { get; set; }

        /// <inheritdoc />
        public int PolygonCount => (int)_model.Skins.Sum(s => s.Triangles.Count / 3);

        /// <inheritdoc />
        public int VertexCount => (int)_model.Vertices.Count;

        private readonly string? _modelPath;
        private readonly RenderCache _renderCache;
        private readonly WarcraftGameContext _gameContext;

        /// <summary>
        /// Dictionary that maps texture paths to OpenGL textures.
        /// </summary>
        private readonly Dictionary<string, Texture2D> _textureLookup = new Dictionary<string, Texture2D>();

        private readonly Dictionary<MDXSkin, Buffer<ushort>> _skinIndexArrayBuffers =
            new Dictionary<MDXSkin, Buffer<ushort>>();

        private Buffer<byte>? _vertexBuffer;

        private GameModelShader? _shader;

        private RenderableBoundingBox? _boundingBox;

        /// <inheritdoc />
        public bool IsInitialized { get; set; }

        /// <inheritdoc />
        public bool ShouldRenderBounds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the wireframe of the object should be rendered.
        /// </summary>
        public bool ShouldRenderWireframe { get; set; }

        /// <summary>
        /// Gets or sets the current display info for this model.
        /// </summary>
        public CreatureDisplayInfoRecord? CurrentDisplayInfo { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableGameModel"/> class.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        /// <param name="renderCache">The rendering cache.</param>
        /// <param name="inModel">The model to render.</param>
        /// <param name="gameContext">The game context.</param>
        /// <param name="modelPath">The full path of the model in the package group.</param>
        public RenderableGameModel(GL gl, RenderCache renderCache, MDX inModel, WarcraftGameContext gameContext, string modelPath)
            : this(gl, renderCache, inModel, gameContext)
        {
            _modelPath = modelPath;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableGameModel"/> class.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        /// <param name="renderCache">The rendering cache.</param>
        /// <param name="inModel">The model to render.</param>
        /// <param name="gameContext">The game context.</param>
        public RenderableGameModel(GL gl, RenderCache renderCache, MDX inModel, WarcraftGameContext gameContext)
            : base(gl)
        {
            _renderCache = renderCache;
            _model = inModel;
            _gameContext = gameContext;

            this.ActorTransform = new Transform();

            // Set a default display info for this model
            var displayInfo = GetSkinVariations().FirstOrDefault();
            if (!(displayInfo is null))
            {
                this.CurrentDisplayInfo = displayInfo;
            }

            this.IsInitialized = false;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            ThrowIfDisposed();

            if (this.IsInitialized)
            {
                return;
            }

            _shader = _renderCache.GetShader(EverlookShader.GameModel) as GameModelShader;

            if (_shader is null)
            {
                throw new ShaderNullException(typeof(GameModelShader));
            }

            _vertexBuffer = new Buffer<byte>(this.GL, BufferTargetARB.ArrayBuffer, BufferUsageARB.StaticDraw)
            {
                Data = _model.Vertices.Select(v => v.PackForOpenGL()).SelectMany(b => b).ToArray()
            };

            var attributePointers = new[]
            {
                // Position
                new VertexAttributePointer(this.GL, 0, 3, VertexAttribPointerType.Float, (uint)MDXVertex.GetSize(), 0),

                // Bone weights
                new VertexAttributePointer
                (
                    this.GL,
                    1,
                    4,
                    VertexAttribPointerType.UnsignedByte,
                    (uint)MDXVertex.GetSize(),
                    12
                ),

                // Bone indexes
                new VertexAttributePointer
                (
                    this.GL,
                    2,
                    4,
                    VertexAttribPointerType.UnsignedByte,
                    (uint)MDXVertex.GetSize(),
                    16
                ),

                // Normal
                new VertexAttributePointer(this.GL, 3, 3, VertexAttribPointerType.Float, (uint)MDXVertex.GetSize(), 20),

                // UV1
                new VertexAttributePointer(this.GL, 4, 2, VertexAttribPointerType.Float, (uint)MDXVertex.GetSize(), 32),

                // UV2
                new VertexAttributePointer(this.GL, 5, 2, VertexAttribPointerType.Float, (uint)MDXVertex.GetSize(), 40)
            };

            _vertexBuffer.AttachAttributePointers(attributePointers);

            _boundingBox = new RenderableBoundingBox(this.GL, _renderCache, _model.BoundingBox, this.ActorTransform);
            _boundingBox.Initialize();

            foreach (var texture in _model.Textures)
            {
                if (!_textureLookup.ContainsKey(texture.Filename))
                {
                    _textureLookup.Add
                    (
                        texture.Filename,
                        _renderCache.GetTexture(texture, _gameContext)
                    );
                }
            }

            foreach (var skin in _model.Skins)
            {
                var absoluteTriangleVertexIndexes = skin.Triangles.Select
                (
                    relativeIndex => skin.VertexIndices[relativeIndex]
                ).ToArray();

                var skinIndexBuffer = new Buffer<ushort>
                (
                    this.GL,
                    BufferTargetARB.ElementArrayBuffer,
                    BufferUsageARB.StaticDraw
                )
                {
                    Data = absoluteTriangleVertexIndexes
                };

                _skinIndexArrayBuffers.Add(skin, skinIndexBuffer);

                if (_model.Version > WarcraftVersion.Wrath)
                {
                    continue;
                }

                // In models earlier than Cata, we need to calculate the shader selector value at runtime.
                foreach (var renderBatch in skin.RenderBatches)
                {
                    var shaderSelector = MDXShaderHelper.GetRuntimeShaderID
                    (
                        renderBatch.ShaderID,
                        renderBatch,
                        _model
                    );

                    renderBatch.ShaderID = shaderSelector;
                }
            }

            // Cache the default display info
            if (!(this.CurrentDisplayInfo is null))
            {
                CacheDisplayInfo(this.CurrentDisplayInfo);
            }

            this.IsInitialized = true;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // TODO: Tick animations
        }

        /// <inheritdoc />
        public void RenderInstances(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ViewportCamera camera, int count)
        {
            ThrowIfDisposed();

            if (!this.IsInitialized)
            {
                return;
            }

            if (_vertexBuffer is null || _shader is null)
            {
                return;
            }

            _vertexBuffer.Bind();
            _vertexBuffer.EnableAttributes();

            this.GL.Enable(EnableCap.DepthTest);

            var modelViewMatrix = this.ActorTransform.GetModelMatrix() * viewMatrix;
            var modelViewProjection = modelViewMatrix * projectionMatrix;

            _shader.Enable();
            _shader.SetIsInstance(true);
            _shader.SetModelMatrix(this.ActorTransform.GetModelMatrix());
            _shader.SetViewMatrix(viewMatrix);
            _shader.SetProjectionMatrix(projectionMatrix);
            _shader.SetMVPMatrix(modelViewProjection);

            _shader.Wireframe.Enabled = this.ShouldRenderWireframe;
            if (this.ShouldRenderWireframe)
            {
                _shader.Wireframe.SetWireframeColour(EverlookConfiguration.Instance.WireframeColour);
                _shader.Wireframe.SetViewportMatrix(camera.GetViewportMatrix());

                // Override blend setting
                this.GL.Enable(EnableCap.Blend);
            }

            foreach (var skin in _model.Skins)
            {
                _skinIndexArrayBuffers[skin].Bind();
                if (this.ShouldRenderWireframe)
                {
                    // Override blend setting
                    this.GL.Enable(EnableCap.Blend);
                }

                foreach (var renderBatch in skin.RenderBatches)
                {
                    if (renderBatch.ShaderID == 0xFFFFu)
                    {
                        continue;
                    }

                    PrepareBatchForRender(renderBatch);

                    var skinSection = skin.Sections[renderBatch.SkinSectionIndex];

                    unsafe
                    {
                        this.GL.DrawElementsInstanced
                        (
                            PrimitiveType.Triangles,
                            skinSection.TriangleCount,
                            DrawElementsType.UnsignedShort,
                            (void*)(skinSection.StartTriangleIndex * 2),
                            (uint)count
                        );
                    }
                }
            }

            // Render bounding boxes
            if (this.ShouldRenderBounds)
            {
                _boundingBox?.RenderInstances(viewMatrix, projectionMatrix, camera, count);
            }

            // Release the attribute arrays
            _vertexBuffer.DisableAttributes();
        }

        /// <inheritdoc />
        public void Render(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ViewportCamera camera)
        {
            ThrowIfDisposed();

            if (!this.IsInitialized)
            {
                return;
            }

            if (_vertexBuffer is null || _shader is null)
            {
                return;
            }

            _vertexBuffer.Bind();
            _vertexBuffer.EnableAttributes();

            this.GL.Enable(EnableCap.DepthTest);

            var modelViewMatrix = this.ActorTransform.GetModelMatrix() * viewMatrix;
            var modelViewProjection = modelViewMatrix * projectionMatrix;

            _shader.Enable();
            _shader.SetIsInstance(false);
            _shader.SetModelMatrix(this.ActorTransform.GetModelMatrix());
            _shader.SetViewMatrix(viewMatrix);
            _shader.SetProjectionMatrix(projectionMatrix);
            _shader.SetMVPMatrix(modelViewProjection);

            _shader.Wireframe.Enabled = this.ShouldRenderWireframe;
            if (this.ShouldRenderWireframe)
            {
                _shader.Wireframe.SetWireframeColour(EverlookConfiguration.Instance.WireframeColour);
                _shader.Wireframe.SetViewportMatrix(camera.GetViewportMatrix());

                // Override blend setting
                this.GL.Enable(EnableCap.Blend);
            }

            foreach (var skin in _model.Skins)
            {
                _skinIndexArrayBuffers[skin].Bind();
                if (this.ShouldRenderWireframe)
                {
                    // Override blend setting
                    this.GL.Enable(EnableCap.Blend);
                }

                foreach (var renderBatch in skin.RenderBatches)
                {
                    if (renderBatch.ShaderID == 0xFFFFu)
                    {
                        continue;
                    }

                    PrepareBatchForRender(renderBatch);

                    var skinSection = skin.Sections[renderBatch.SkinSectionIndex];
                    unsafe
                    {
                        this.GL.DrawElements
                        (
                            PrimitiveType.Triangles,
                            skinSection.TriangleCount,
                            DrawElementsType.UnsignedShort,
                            (void*)(skinSection.StartTriangleIndex * 2)
                        );
                    }
                }
            }

            // Render bounding boxes
            if (this.ShouldRenderBounds)
            {
                _boundingBox?.Render(viewMatrix, projectionMatrix, camera);
            }

            // Release the attribute arrays
            _vertexBuffer.DisableAttributes();
        }

        /// <summary>
        /// Prepares the OpenGL state for rendering the specified batch.
        /// </summary>
        /// <param name="renderBatch">The batch to prepare for rendering.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the batch has more than four textures.</exception>
        private void PrepareBatchForRender(MDXRenderBatch renderBatch)
        {
            var fragmentShader = MDXShaderHelper.GetFragmentShaderType(renderBatch.TextureCount, renderBatch.ShaderID);
            var vertexShader = MDXShaderHelper.GetVertexShaderType(renderBatch.TextureCount, renderBatch.ShaderID);
            var batchMaterial = _model.Materials[renderBatch.MaterialIndex];

            if (_shader is null)
            {
                return;
            }

            _shader.SetVertexShaderType(vertexShader);
            _shader.SetFragmentShaderType(fragmentShader);
            _shader.SetMaterial(batchMaterial);

            var baseColour = Vector4.One;
            if (renderBatch.ColorIndex >= 0)
            {
                var colorAnimation = _model.ColourAnimations[renderBatch.ColorIndex];

                // TODO: Sample based on animated values
                RGB rgb;
                float alpha;

                if (colorAnimation.ColourTrack.IsComposite)
                {
                    rgb = colorAnimation.ColourTrack.CompositeTimelineValues.First();
                    alpha = (float)colorAnimation.OpacityTrack.CompositeTimelineValues.First() / 0x7fff;
                }
                else
                {
                    rgb = colorAnimation.ColourTrack.Values.First().First();
                    alpha = (float)colorAnimation.OpacityTrack.Values.First().First() / 0x7fff;
                }

                baseColour = new Vector4
                (
                    Math.Clamp(rgb.R, 0.0f, 1.0f),
                    Math.Clamp(rgb.G, 0.0f, 1.0f),
                    Math.Clamp(rgb.B, 0.0f, 1.0f),
                    Math.Clamp(alpha, 0.0f, 1.0f)
                );
            }

            if ((short)renderBatch.TransparencyLookupTableIndex >= 0)
            {
                var transparencyAnimationIndex = _model.TransparencyLookupTable[renderBatch.TransparencyLookupTableIndex];
                var transparencyAnimation = _model.TransparencyAnimations[transparencyAnimationIndex];

                float alphaWeight;
                if (transparencyAnimation.Weight.IsComposite)
                {
                    alphaWeight = (float)transparencyAnimation.Weight.CompositeTimelineValues.First() / 0x7fff;
                }
                else
                {
                    alphaWeight = (float)transparencyAnimation.Weight.Values.First().First() / 0x7fff;
                }

                baseColour.W *= alphaWeight;
            }

            _shader.SetBaseInputColour(baseColour);

            var textureIndexes = _model.TextureLookupTable.Skip(renderBatch.TextureLookupTableIndex)
                .Take(renderBatch.TextureCount);
            var textures = _model.Textures.Where((t, i) => textureIndexes.Contains((short)i)).ToList();

            for (var i = 0; i < textures.Count; ++i)
            {
                var texture = textures[i];
                string textureName;
                switch (texture.TextureType)
                {
                    case MDXTextureType.Regular:
                    {
                        textureName = texture.Filename;
                        break;
                    }
                    case MDXTextureType.MonsterSkin1:
                    {
                        textureName = GetDisplayInfoTexturePath(this.CurrentDisplayInfo?.TextureVariation1.Value);
                        break;
                    }
                    case MDXTextureType.MonsterSkin2:
                    {
                        textureName = GetDisplayInfoTexturePath(this.CurrentDisplayInfo?.TextureVariation2.Value);
                        break;
                    }
                    case MDXTextureType.MonsterSkin3:
                    {
                        textureName = GetDisplayInfoTexturePath(this.CurrentDisplayInfo?.TextureVariation3.Value);
                        break;
                    }
                    default:
                    {
                        // Use the fallback texture if we don't know how to load the texture type
                        textureName = string.Empty;
                        break;
                    }
                }

                var textureObject = _textureLookup[textureName];
                switch (i)
                {
                    case 0:
                    {
                        _shader.BindTexture0(textureObject);
                        break;
                    }
                    case 1:
                    {
                        _shader.BindTexture1(textureObject);
                        break;
                    }
                    case 2:
                    {
                        _shader.BindTexture2(textureObject);
                        break;
                    }
                    case 3:
                    {
                        _shader.BindTexture3(textureObject);
                        break;
                    }
                    default:
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the names of the skin variations of this model.
        /// </summary>
        /// <returns>The names of the variations.</returns>
        public IEnumerable<CreatureDisplayInfoRecord> GetSkinVariations()
        {
            // Just like other places, sometimes the files are stored as *.mdx. We'll force that extension on both.
            // Get any model data record which uses this model
            var modelDataRecords = _gameContext.Database.GetDatabase<CreatureModelDataRecord>().Where
            (
                r =>
                string.Equals
                (
                    Path.ChangeExtension(r.ModelPath.Value, "mdx"),
                    Path.ChangeExtension(_modelPath, "mdx"),
                    StringComparison.InvariantCultureIgnoreCase
                )
            ).ToList();

            if (!modelDataRecords.Any())
            {
                yield break;
            }

            // Then flatten out their IDs
            var modelDataRecordIDs = modelDataRecords.Select(r => r.ID).ToList();

            // Then get any display info record which references this model
            var displayInfoDatabase = _gameContext.Database.GetDatabase<CreatureDisplayInfoRecord>();
            var modelDisplayRecords = displayInfoDatabase.Where
            (
                r => modelDataRecordIDs.Contains(r.Model.Key)
            ).ToList();

            if (!modelDisplayRecords.Any())
            {
                yield break;
            }

            var textureListMapping = new Dictionary<IReadOnlyList<StringReference>, CreatureDisplayInfoRecord>
            (
                new StringReferenceListComparer()
            );

            // Finally, return any record with a unique set of textures
            foreach (var displayRecord in modelDisplayRecords)
            {
                if (textureListMapping.ContainsKey(displayRecord.TextureVariations))
                {
                    continue;
                }

                textureListMapping.Add(displayRecord.TextureVariations, displayRecord);
                yield return displayRecord;
            }
        }

        /// <summary>
        /// Gets the full texture path for a given texture name.
        /// </summary>
        /// <param name="textureName">The name of the texture.</param>
        /// <returns>The full path to the texture.</returns>
        private string GetDisplayInfoTexturePath(string? textureName)
        {
            // An empty string represents the fallback texture
            if (textureName is null)
            {
                return string.Empty;
            }

            if (_modelPath is null)
            {
                return string.Empty;
            }

            var modelDirectory = _modelPath.Remove(_modelPath.LastIndexOf('\\'));
            return $"{modelDirectory}\\{textureName}.blp";
        }

        /// <summary>
        /// Sets the current display info to the record pointed to by the given ID.
        /// </summary>
        /// <param name="variationID">The ID of the record.</param>
        public void SetDisplayInfoByID(int variationID)
        {
            this.CurrentDisplayInfo = _gameContext.Database.GetDatabase<CreatureDisplayInfoRecord>()
                .GetRecordByID(variationID);
            CacheDisplayInfo(this.CurrentDisplayInfo);
        }

        /// <summary>
        /// Caches the textures used in a display info record for use.
        /// </summary>
        /// <param name="displayInfoRecord">The display info record to cache.</param>
        private void CacheDisplayInfo(CreatureDisplayInfoRecord displayInfoRecord)
        {
            if (_modelPath is null)
            {
                throw new InvalidOperationException();
            }

            foreach (var texture in _model.Textures)
            {
                int textureIndex;
                switch (texture.TextureType)
                {
                    case MDXTextureType.MonsterSkin1:
                    {
                        textureIndex = 0;
                        break;
                    }
                    case MDXTextureType.MonsterSkin2:
                    {
                        textureIndex = 1;
                        break;
                    }
                    case MDXTextureType.MonsterSkin3:
                    {
                        textureIndex = 2;
                        break;
                    }
                    default:
                    {
                        continue;
                    }
                }

                var textureName = displayInfoRecord.TextureVariations[textureIndex].Value;
                var modelDirectory = _modelPath.Remove(_modelPath.LastIndexOf('\\'));
                var texturePath = $"{modelDirectory}\\{textureName}.blp";

                if (_textureLookup.ContainsKey(texturePath))
                {
                    continue;
                }

                _textureLookup.Add
                (
                    texturePath,
                    _renderCache.GetTexture(texture, _gameContext, texturePath)
                );
            }
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(ToString() ?? nameof(RenderableGameModel));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.IsDisposed = true;

            _vertexBuffer?.Dispose();

            foreach (var skinIndexArrayBuffer in _skinIndexArrayBuffers)
            {
                skinIndexArrayBuffer.Value.Dispose();
            }
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (!(obj is RenderableGameModel otherModel))
            {
                return false;
            }

            return (otherModel._model == _model) &&
                    (otherModel._gameContext == _gameContext) &&
                    (otherModel.IsStatic == this.IsStatic);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (this.IsStatic.GetHashCode() + _model.GetHashCode() + _gameContext.GetHashCode()).GetHashCode();
        }
    }
}
