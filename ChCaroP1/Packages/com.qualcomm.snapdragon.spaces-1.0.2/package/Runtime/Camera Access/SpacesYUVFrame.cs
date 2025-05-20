/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    internal class SpacesYUVFrame
    {
        public ulong Handle => _handle;
        public uint FrameNumber => _frameNumber;
        public long Timestamp => _timestamp;
        public Vector2Int Dimensions => _dimensions;
        public XrCameraFrameFormatQCOM Format => _format;
        public IntPtr DataPtr => _dataPtr;
        public int DataLength => _dataLength;
        public int NativePlaneCount => _nativePlaneCount;
        public ImagePlane YPlane => _yPlane;
        public ImagePlane UPlane => _uPlane;
        public ImagePlane VPlane => _vPlane;

        private ulong _handle;
        private uint _frameNumber;
        private long _timestamp;
        private Vector2Int _dimensions;
        private XrCameraFrameFormatQCOM _format;
        private IntPtr _dataPtr;
        private int _dataLength;
        private int _nativePlaneCount;
        private ImagePlane _yPlane;
        private ImagePlane _uPlane;
        private ImagePlane _vPlane;

        public SpacesYUVFrame(ulong handle, uint frameNumber, long timestamp, Vector2Int dimensions, XrCameraFrameFormatQCOM format, IntPtr dataPtr, int dataLength, int nativePlaneCount, ImagePlane yPlane, ImagePlane uPlane, ImagePlane vPlane)
        {
            _handle = handle;
            _frameNumber = frameNumber;
            _timestamp = timestamp;
            _dimensions = dimensions;
            _format = format;
            _dataPtr = dataPtr;
            _dataLength = dataLength;
            _nativePlaneCount = nativePlaneCount;
            _yPlane = yPlane;
            _uPlane = uPlane;
            _vPlane = vPlane;
        }

        public override string ToString()
        {
            return String.Join("\n",
                "[SpacesYUVFrame]",
                $"Handle:\t{_handle}",
                $"FrameNumber:\t{_frameNumber}",
                $"Timestamp:\t{_timestamp}",
                $"Dimensions:\t{_dimensions}",
                $"Format:\t{_format}",
                $"DataPtr:\t{_dataPtr}",
                $"DataLength:\t{_dataLength}",
                $"NativePlaneCount:\t{_nativePlaneCount}",
                $"YPlane:\t{_yPlane}",
                $"UPlane:\t{_uPlane}",
                $"VPlane:\t{_vPlane}");
        }
    }
}
