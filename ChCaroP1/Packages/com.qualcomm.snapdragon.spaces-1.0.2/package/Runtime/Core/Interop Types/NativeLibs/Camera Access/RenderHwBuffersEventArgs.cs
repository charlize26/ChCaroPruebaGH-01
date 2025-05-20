/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RenderHwBuffersEventArgs
    {
        private int _bufferCount;
        private int _bufferWidth;
        private int _bufferHeight;
        private IntPtr _hwBuffers;
        private IntPtr _renderTargets;

        public RenderHwBuffersEventArgs(int count, int width, int height, IntPtr hwBuffers, IntPtr renderTargets)
        {
            _bufferCount = count;
            _bufferWidth = width;
            _bufferHeight = height;
            _hwBuffers = hwBuffers;
            _renderTargets = renderTargets;
        }

        public override string ToString()
        {
            return String.Join("\n",
                "[RenderHwBuffersEventArgs]",
                $"Count:\t{_bufferCount}",
                $"Width:\t{_bufferWidth}",
                $"Height:\t{_bufferHeight}",
                $"HwBuffers:\t{_hwBuffers}",
                $"RenderTargets:\t{_renderTargets}");
        }
    }
}
