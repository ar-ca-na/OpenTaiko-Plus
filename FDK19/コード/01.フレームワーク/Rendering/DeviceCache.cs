using System;
using System.Drawing;
using SlimDX;
using SlimDX.Direct3D9;

namespace SampleFramework
{
    public sealed class DeviceCache
    {
        private readonly Device _device;

        public DeviceCache(Device device)
        {
            _device = device;
        }

        public Device UnderlyingDevice => _device;

        public void Dispose()
        {
            _device.Dispose();
        }

        public object Tag
        {
            get => _device.Tag;
            set => _device.Tag = value;
        }

        public Result TestCooperativeLevel()
        {
            return _device.TestCooperativeLevel();
        }

        public Result Reset(PresentParameters presentParameters)
        {
            // Reset するとデバイス側の状態（束ねたテクスチャ・ブレンド設定）は
            // すべて既定値に戻る。ここで「前回設定した値」の記憶を捨てておかないと、
            // 次の SetTexture が「前回と同じだから呼ぶ必要なし」と判断して飛ばされ、
            // テクスチャの無い＝真っ白な矩形が描かれ続ける。
            // ウィンドウを最大化・リサイズすると Reset が走るので、そこで必ず踏む。
            InvalidateCache();
            return _device.Reset(presentParameters);
        }

        /// <summary>デバイスの状態を覚えている変数をすべて忘れる。</summary>
        private void InvalidateCache()
        {
            _lastBlendOperation = null;
            _lastSourceBlend = null;
            _lastDestinationBlend = null;
            _lastSetTextureSampler = null;
            _lastSetTextureTexture = null;
        }

        public Result Clear(ClearFlags clearFlags, in Color4 color, float zdepth, int stencil)
        {
            return _device.Clear(clearFlags, color, zdepth, stencil);
        }

        public Result BeginScene()
        {
            return _device.BeginScene();
        }

        public Result EndScene()
        {
            return _device.EndScene();
        }

        public Result Present()
        {
            return _device.Present();
        }

        public Surface GetBackBuffer(int swapChain, int backBuffer)
        {
            return _device.GetBackBuffer(swapChain, backBuffer);
        }

        public Surface GetRenderTarget(int index)
        {
            return _device.GetRenderTarget(index);
        }

        public Result SetRenderState<T>(RenderState state, T value) where T : Enum
        {
            return _device.SetRenderState(state, value);
        }

        private BlendOperation? _lastBlendOperation;
        public void SetRenderState(RenderState state, BlendOperation value)
        {
            if (state == RenderState.BlendOperation)
            {
                if (_lastBlendOperation == value)
                {
                    return;
                }

                _lastBlendOperation = value;
            }

            _device.SetRenderState(state, value);
        }

        private Blend? _lastSourceBlend;
        private Blend? _lastDestinationBlend;
        public void SetRenderState(RenderState state, Blend value)
        {
            if (state == RenderState.SourceBlend)
            {
                if (_lastSourceBlend == value)
                {
                    return;
                }

                _lastSourceBlend = value;
            }
            else if (state == RenderState.DestinationBlend)
            {
                if (_lastDestinationBlend == value)
                {
                    return;
                }

                _lastDestinationBlend = value;
            }

            _device.SetRenderState(state, value);
        }

        public Result SetRenderState(RenderState state, bool value)
        {
            return _device.SetRenderState(state, value);
        }

        public Result SetRenderState(RenderState state, int value)
        {
            return _device.SetRenderState(state, value);
        }

        public Result SetTextureStageState(int stage, TextureStage type, TextureOperation textureOperation)
        {
            return _device.SetTextureStageState(stage, type, textureOperation);
        }

        public Result SetTextureStageState(int stage, TextureStage type, int value)
        {
            return _device.SetTextureStageState(stage, type, value);
        }

        public Result SetSamplerState(int sampler, SamplerState type, TextureFilter textureFilter)
        {
            return _device.SetSamplerState(sampler, type, textureFilter);
        }

        public Result SetTransform(TransformState state, in Matrix value)
        {
            return _device.SetTransform(state, value);
        }

        private int? _lastSetTextureSampler;
        private object _lastSetTextureTexture;
        public void SetTexture(int sampler, BaseTexture texture)
        {
            if ( ReferenceEquals(_lastSetTextureTexture, texture) && _lastSetTextureSampler == sampler)
            {
                return;
            }

            _lastSetTextureSampler = sampler;
            _lastSetTextureTexture = texture;
            _device.SetTexture(sampler, texture);
        }

        public Result SetRenderTarget(int targetIndex, Surface target)
        {
            return _device.SetRenderTarget(targetIndex, target);
        }

        public Result DrawUserPrimitives<T>(PrimitiveType primitiveType, int startIndex, int primitiveCount, in T[] data) where T : struct//, new()
        {
            return _device.DrawUserPrimitives(primitiveType, startIndex, primitiveCount, data);
        }

        public Result DrawUserPrimitives<T>(PrimitiveType primitiveType, int primitiveCount, in T[] data) where T : struct//, new()
        {
            return _device.DrawUserPrimitives(primitiveType, primitiveCount, data);
        }

        public Result StretchRectangle(Surface source, Surface destination, TextureFilter filter)
        {
            return _device.StretchRectangle(source, destination, filter);
        }

        public Result UpdateSurface(Surface source, in Rectangle sourceRectangle, Surface destination, in Point destinationPoint)
        {
            return _device.UpdateSurface(source, sourceRectangle, destination, destinationPoint);
        }

        public Viewport Viewport
        {
            get => _device.Viewport;
            set => _device.Viewport = value;
        }

        public VertexFormat VertexFormat
        {
            get => _device.VertexFormat;
            set => _device.VertexFormat = value;
        }

        public Capabilities Capabilities => _device.Capabilities;
    }
}