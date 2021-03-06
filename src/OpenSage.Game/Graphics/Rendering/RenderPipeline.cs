﻿using System.Numerics;
using System.Runtime.InteropServices;
using OpenSage.Graphics.Effects;
using OpenSage.Mathematics;
using Veldrid;

namespace OpenSage.Graphics.Rendering
{
    internal sealed class RenderPipeline : DisposableBase
    {
        private readonly RenderList _renderList;

        private readonly CommandList _commandList;

        private readonly ConstantBuffer<GlobalConstantsShared> _globalConstantBufferShared;
        private readonly ConstantBuffer<GlobalConstantsVS> _globalConstantBufferVS;
        private readonly ConstantBuffer<GlobalConstantsPS> _globalConstantBufferPS;
        private readonly ConstantBuffer<RenderItemConstantsVS> _renderItemConstantsBufferVS;
        private readonly ConstantBuffer<LightingConstants> _globalLightingTerrainBuffer;
        private readonly ConstantBuffer<LightingConstants> _globalLightingObjectBuffer;

        private readonly SpriteBatch _spriteBatch;

        public RenderPipeline(Game game)
        {
            _renderList = new RenderList();

            var graphicsDevice = game.GraphicsDevice;

            _globalConstantBufferShared = AddDisposable(new ConstantBuffer<GlobalConstantsShared>(graphicsDevice));
            _globalConstantBufferVS = AddDisposable(new ConstantBuffer<GlobalConstantsVS>(graphicsDevice));
            _renderItemConstantsBufferVS = AddDisposable(new ConstantBuffer<RenderItemConstantsVS>(graphicsDevice));
            _globalConstantBufferPS = AddDisposable(new ConstantBuffer<GlobalConstantsPS>(graphicsDevice));
            _globalLightingTerrainBuffer = AddDisposable(new ConstantBuffer<LightingConstants>(graphicsDevice));
            _globalLightingObjectBuffer = AddDisposable(new ConstantBuffer<LightingConstants>(graphicsDevice));

            _spriteBatch = AddDisposable(new SpriteBatch(game.ContentManager));

            _commandList = AddDisposable(graphicsDevice.ResourceFactory.CreateCommandList());
        }

        public void Execute(RenderContext context)
        {
            _renderList.Clear();

            context.Scene.Scene3D?.World.Terrain.BuildRenderList(_renderList);

            foreach (var system in context.Game.GameSystems)
            {
                system.BuildRenderList(_renderList);
            }

            var commandEncoder = _commandList;

            commandEncoder.Begin();

            commandEncoder.SetFramebuffer(context.RenderTarget);

            commandEncoder.ClearColorTarget(0, context.Camera.BackgroundColor.ToColorRgbaF().ToRgbaFloat());
            commandEncoder.ClearDepthStencil(1);

            commandEncoder.SetViewport(0, context.Camera.Viewport);

            UpdateGlobalConstantBuffers(commandEncoder, context);

            void doRenderPass(RenderBucket bucket)
            {
                Culler.Cull(bucket.RenderItems, bucket.CulledItems, context);

                if (bucket.CulledItems.Count == 0)
                {
                    return;
                }

                bucket.CulledItems.Sort();

                RenderItem? lastRenderItem = null;
                foreach (var renderItem in bucket.CulledItems)
                {
                    if (lastRenderItem == null || lastRenderItem.Value.Effect != renderItem.Effect)
                    {
                        var effect = renderItem.Effect;

                        effect.Begin(commandEncoder);

                        SetDefaultConstantBuffers(renderItem.Material);
                    }

                    if (lastRenderItem == null || lastRenderItem.Value.Material != renderItem.Material)
                    {
                        renderItem.Material.ApplyPipelineState(context.RenderTarget.OutputDescription);
                        renderItem.Effect.ApplyPipelineState(commandEncoder);
                    }

                    if (lastRenderItem == null || lastRenderItem.Value.VertexBuffer0 != renderItem.VertexBuffer0)
                    {
                        commandEncoder.SetVertexBuffer(0, renderItem.VertexBuffer0);
                    }

                    if (lastRenderItem == null || lastRenderItem.Value.VertexBuffer1 != renderItem.VertexBuffer1)
                    {
                        if (renderItem.VertexBuffer1 != null)
                        {
                            commandEncoder.SetVertexBuffer(1, renderItem.VertexBuffer1);
                        }
                    }

                    var renderItemConstantsVSParameter = renderItem.Effect.GetParameter("RenderItemConstantsVS");
                    if (renderItemConstantsVSParameter != null)
                    {
                        _renderItemConstantsBufferVS.Value.World = renderItem.World;
                        _renderItemConstantsBufferVS.Update(commandEncoder);

                        renderItem.Material.SetProperty(
                            "RenderItemConstantsVS",
                            _renderItemConstantsBufferVS.Buffer);
                    }

                    renderItem.Material.ApplyProperties();
                    renderItem.Effect.ApplyParameters(commandEncoder);

                    switch (renderItem.DrawCommand)
                    {
                        case DrawCommand.Draw:
                            commandEncoder.Draw(
                                renderItem.VertexCount,
                                1,
                                renderItem.VertexStart,
                                0);
                            break;

                        case DrawCommand.DrawIndexed:
                            commandEncoder.SetIndexBuffer(renderItem.IndexBuffer, IndexFormat.UInt16);
                            commandEncoder.DrawIndexed(
                                renderItem.IndexCount,
                                1,
                                renderItem.StartIndex,
                                0,
                                0);
                            break;

                        default:
                            throw new System.Exception();
                    }

                    lastRenderItem = renderItem;
                }
            }

            doRenderPass(_renderList.Opaque);
            doRenderPass(_renderList.Transparent);

            // GUI
            {
                _spriteBatch.Begin(
                    commandEncoder,
                    context.Game.ContentManager.LinearClampSampler,
                    context.RenderTarget.OutputDescription,
                    context.Camera.Viewport);

                context.Scene.Scene2D.Render(_spriteBatch);

                context.Game.RaiseRendering2D(new Rendering2DEventArgs(_spriteBatch));

                _spriteBatch.End();
            }

            commandEncoder.End();

            context.GraphicsDevice.SubmitCommands(commandEncoder);

            context.GraphicsDevice.SwapBuffers();
        }

        private void SetDefaultConstantBuffers(EffectMaterial material)
        {
            void setDefaultConstantBuffer(string name, DeviceBuffer buffer)
            {
                var parameter = material.Effect.GetParameter(name, throwIfMissing: false);
                if (parameter != null)
                {
                    material.SetProperty(name, buffer);
                }
            }

            setDefaultConstantBuffer("GlobalConstantsShared", _globalConstantBufferShared.Buffer);
            setDefaultConstantBuffer("GlobalConstantsVS", _globalConstantBufferVS.Buffer);
            setDefaultConstantBuffer("GlobalConstantsPS", _globalConstantBufferPS.Buffer);
            setDefaultConstantBuffer("LightingConstants_Object", _globalLightingObjectBuffer.Buffer);
            setDefaultConstantBuffer("LightingConstants_Terrain", _globalLightingTerrainBuffer.Buffer);
        }

        private void UpdateGlobalConstantBuffers(CommandList commandEncoder, RenderContext context)
        {
            var cameraPosition = Matrix4x4Utility.Invert(context.Camera.View).Translation;

            _globalConstantBufferShared.Value.CameraPosition = cameraPosition;
            _globalConstantBufferShared.Update(commandEncoder);

            _globalConstantBufferVS.Value.ViewProjection = context.Camera.View * context.Camera.Projection;
            _globalConstantBufferVS.Update(commandEncoder);

            _globalConstantBufferPS.Value.TimeInSeconds = (float) context.GameTime.TotalGameTime.TotalSeconds;
            _globalConstantBufferPS.Value.ViewportSize = new Vector2(context.Camera.Viewport.Width, context.Camera.Viewport.Height);
            _globalConstantBufferPS.Update(commandEncoder);

            _globalLightingTerrainBuffer.Value = context.Scene.Settings.CurrentLightingConfiguration.TerrainLights;
            _globalLightingObjectBuffer.Value = context.Scene.Settings.CurrentLightingConfiguration.ObjectLights;
            _globalLightingTerrainBuffer.Update(commandEncoder);
            _globalLightingObjectBuffer.Update(commandEncoder);
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct GlobalConstantsShared
        {
            public Vector3 CameraPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GlobalConstantsVS
        {
            public Matrix4x4 ViewProjection;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RenderItemConstantsVS
        {
            public Matrix4x4 World;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct GlobalConstantsPS
        {
            public float TimeInSeconds;
            public Vector2 ViewportSize;
        }
    }
}
