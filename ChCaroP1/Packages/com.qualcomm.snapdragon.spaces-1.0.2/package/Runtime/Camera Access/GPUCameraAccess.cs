/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Qualcomm.Snapdragon.Spaces
{
    internal partial class GPUCameraAccess
    {
        private CommandBuffer _commandBuffer;

        private bool _initialized = false;
        private int _hwBufferCount = 0;
        private int _width = 0;
        private int _height = 0;

        private RenderHwBuffersEventArgs _renderHwBuffersEventArgs;
        private IntPtr _renderHwBuffersEventArgsPtr = IntPtr.Zero;

        private XrCameraFrameHardwareBufferQCOM[] _hwBuffers;
        private IntPtr _hwBuffersArrayPtr = IntPtr.Zero;

        private Texture2D[] _renderTargets = new Texture2D[0];
        private IntPtr _renderTargetsArrayPtr = IntPtr.Zero;

        internal List<XRTextureDescriptor> GetTextureDescriptors(XrCameraSensorPropertiesQCOM[] sensorProperties)
        {
            if (!_initialized)
            {
                return new List<XRTextureDescriptor>();
            }

            var textureDescriptors = new List<XRTextureDescriptor>();

            for (int i = 0; i < _renderTargets.Length; i++)
            {
                XRTextureDescriptor descriptor = new XRTextureDescriptor(
                    _renderTargets[i].GetNativeTexturePtr(),
                    (int) sensorProperties[i].ImageDimensions.Width,
                    (int) sensorProperties[i].ImageDimensions.Height,
                    0,
                    TextureFormat.RGBA32,
                    Shader.PropertyToID($"_CameraTex{i}"),
                    1,
                    TextureDimension.Tex2D
                    );
                textureDescriptors.Add(descriptor);
            }

            return textureDescriptors;
        }

        private void InitializeOrResize(int hwBufferCount, int width, int height)
        {
            if (!_initialized)
            {
                _hwBufferCount = hwBufferCount;
                _width = width;
                _height = height;

                Assert.AreEqual(_hwBuffersArrayPtr, IntPtr.Zero);
                _hwBuffersArrayPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IntPtr>() * _hwBufferCount);
                Assert.AreEqual(_renderTargetsArrayPtr, IntPtr.Zero);
                _renderTargetsArrayPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IntPtr>() * _hwBufferCount);
                Assert.AreEqual(_renderHwBuffersEventArgsPtr, IntPtr.Zero);
                _renderHwBuffersEventArgsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RenderHwBuffersEventArgs>());

                Assert.IsNull(_commandBuffer);
                _commandBuffer = new CommandBuffer();
                _commandBuffer.IssuePluginEventAndData(Internal_GetRenderEventFuncPtr(), (int) SpacesPluginEvent.SPACES_PLUGIN_EVENT_RENDER_HW_BUFFERS, _renderHwBuffersEventArgsPtr);

                _renderTargets = CreateTextures(_hwBufferCount, _width, _height);

                _initialized = true;
            }

            if (_hwBufferCount != hwBufferCount || _width != width || _height != height)
            {
                Assert.IsTrue(_initialized);
                _hwBufferCount = hwBufferCount;
                _width = width;
                _height = height;

                Assert.AreNotEqual(_hwBuffersArrayPtr, IntPtr.Zero);
                Marshal.FreeHGlobal(_hwBuffersArrayPtr);
                Assert.AreNotEqual(_renderTargetsArrayPtr, IntPtr.Zero);
                Marshal.FreeHGlobal(_renderTargetsArrayPtr);
                Assert.AreNotEqual(_renderHwBuffersEventArgsPtr, IntPtr.Zero);
                Marshal.FreeHGlobal(_renderHwBuffersEventArgsPtr);

                _hwBuffersArrayPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IntPtr>() * _hwBufferCount);
                _renderTargetsArrayPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IntPtr>() * _hwBufferCount);
                _renderHwBuffersEventArgsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RenderHwBuffersEventArgs>());

                // NOTE: Re-issue event because the pointer has changed
                _commandBuffer.Clear();
                _commandBuffer.IssuePluginEventAndData(Internal_GetRenderEventFuncPtr(), (int) SpacesPluginEvent.SPACES_PLUGIN_EVENT_RENDER_HW_BUFFERS, _renderHwBuffersEventArgsPtr);

                DestroyTextures(_renderTargets);
                _renderTargets = CreateTextures(_hwBufferCount, _width, _height);
            }
        }

        internal void RenderHwBuffers(XrCameraFrameHardwareBufferQCOM[] hwBuffers, int width, int height)
        {
            // NOTE: According to the logs, Unity schedules the _commandBuffer almost immediately after on the render thread, so a mutex might not be needed.
            // But I only tested with OpenGL. Might be that with other rendering APIs or with other configurations, this is not the case.
            Internal_LockMutex();
            InitializeOrResize(hwBuffers.Length, width, height);

            _hwBuffers = hwBuffers;

            for(int i = 0; i < _renderTargets.Length; i++)
            {
                Marshal.StructureToPtr(_hwBuffers[i].Buffer, _hwBuffersArrayPtr + Marshal.SizeOf<IntPtr>() * i, false);
                Marshal.StructureToPtr(_renderTargets[i].GetNativeTexturePtr(), _renderTargetsArrayPtr + Marshal.SizeOf<IntPtr>() * i, false);
            }
            Internal_UnlockMutex();

            var args = new RenderHwBuffersEventArgs(
                _renderTargets.Length,
                _renderTargets[0].width,
                _renderTargets[0].height,
                _hwBuffersArrayPtr,
                _renderTargetsArrayPtr
            );

            Marshal.StructureToPtr(args, _renderHwBuffersEventArgsPtr, false);
            Graphics.ExecuteCommandBuffer(_commandBuffer);
        }

        internal void ReleaseResources()
        {
            Assert.AreNotEqual(_hwBuffersArrayPtr, IntPtr.Zero);
            Marshal.FreeHGlobal(_hwBuffersArrayPtr);
            Assert.AreNotEqual(_renderTargetsArrayPtr, IntPtr.Zero);
            Marshal.FreeHGlobal(_renderTargetsArrayPtr);
            Assert.AreNotEqual(_renderHwBuffersEventArgsPtr, IntPtr.Zero);
            Marshal.FreeHGlobal(_renderHwBuffersEventArgsPtr);

            _hwBuffersArrayPtr = IntPtr.Zero;
            _renderTargetsArrayPtr = IntPtr.Zero;
            _renderHwBuffersEventArgsPtr = IntPtr.Zero;

            _commandBuffer?.Clear();
            _commandBuffer = null;

            DestroyTextures(_renderTargets);

            _initialized = false;
        }

        private static void DestroyTextures(Texture2D[] textures)
        {
            foreach (var texture in textures)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private static Texture2D[] CreateTextures(int count, int width, int height)
        {
            var textures = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                textures[i] = new Texture2D(width, height, textureFormat: TextureFormat.RGBA32, false);
                // NOTE: Toggle this section to initialize the texture to a custom color for debugging purposes
                // #define DEBUG_GPU_RENDERING
#if DEBUG_GPU_RENDERING
                var pixels = textures[i].GetPixels32();
                for (int j = 0; j < pixels.Length; j++)
                {
                    pixels[j] = Color.green;
                }
                textures[i].SetPixels32(pixels);
                textures[i].Apply();
#endif
            }
            return textures;
        }
    }
}
