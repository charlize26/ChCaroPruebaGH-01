/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Qualcomm.Snapdragon.Spaces;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;

internal class SpacesCpuImageApi : XRCpuImage.Api
{
    private class ConversionRequest
    {
        public readonly int RequestId;
        public readonly int NativeHandle;
        public readonly XRCpuImage.ConversionParams ConversionParams;
        public readonly OnImageRequestCompleteDelegate Callback;
        public readonly IntPtr Context;

        public XRCpuImage.AsyncConversionStatus Status;
        public IntPtr DataPtr;
        public int DataLength;

        public bool IsFrameCached => _isFrameCached;
        public ref byte[] SourcePixels => ref _sourcePixels;
        public ref byte[] OutPixels => ref _outPixels;
        public SpacesYUVFrame Frame => _frame;

        private bool _isFrameCached;
        private byte[] _sourcePixels;
        private byte[] _outPixels;
        private SpacesYUVFrame _frame;

        public ConversionRequest(int requestId, int nativeHandle, XRCpuImage.ConversionParams conversionParams, OnImageRequestCompleteDelegate callback, IntPtr context)
        {
            this.RequestId = requestId;
            this.NativeHandle = nativeHandle;
            this.ConversionParams = conversionParams;
            this.Callback = callback;
            this.Context = context;

            Status = XRCpuImage.AsyncConversionStatus.Pending;
            DataPtr = IntPtr.Zero;
            DataLength = 0;
        }

        public void Dispose()
        {
            Status = XRCpuImage.AsyncConversionStatus.Disposed;
            if (DataPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(DataPtr);
                DataPtr = IntPtr.Zero;
            }
        }

        public void CacheFrame(SpacesYUVFrame frame, int outDataLength)
        {
            _frame = frame;
            _sourcePixels = new byte[frame.DataLength];
            _outPixels = new byte[outDataLength];

            Marshal.Copy(frame.DataPtr, _sourcePixels, 0, frame.DataLength);
            AllocateDataPtr(outDataLength);

            _isFrameCached = true;
        }

        public void AllocateDataPtr(int outDataLength)
        {
            DataLength = Marshal.SizeOf<byte>() * outDataLength;
            DataPtr = Marshal.AllocHGlobal(DataLength);
        }
    }

    private List<XRCpuImage.Format> _supportedInputFormats = new List<XRCpuImage.Format> { XRCpuImage.Format.AndroidYuv420_888 };

    private List<TextureFormat> _supportedOutputFormats = new List<TextureFormat> { TextureFormat.RGB24, TextureFormat.RGBA32, TextureFormat.BGRA32 };

    private CameraAccessFeature _underlyingFeature = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();
    public static SpacesCpuImageApi instance { get; private set; }

    private Thread _conversionWorker;
    private SynchronizationContext _mainThreadContext;
    // Note(CH): _pendingConversionRequests is a queue of pending image conversions. _conversionRequests is a map of conversions yet to be disposed.
    private readonly ConcurrentQueue<ConversionRequest> _pendingConversionRequests = new();
    private ConcurrentDictionary<int, ConversionRequest> _conversionRequests = new();
    private ConversionRequest _currentConversionRequest;
    private int _conversionWorkerRunning = 0; // false = 0; true = 1
    private int _conversionRequestsIds;

    // Cache byte buffers to avoid re-allocation each frame
    // Note(CH): Main thread and conversion worker thread each get their own cache. Single cache works for the conversion worker because conversions are still sequential.
    private byte[] _sourcePixels;
    private byte[] _outPixels;
    private byte[] _asyncSourcePixels;
    private byte[] _asyncOutPixels;

    public static SpacesCpuImageApi CreateInstance()
    {
        instance ??= new SpacesCpuImageApi();
        instance.SpawnWorker();
        return instance;
    }

    public override bool NativeHandleValid(int nativeHandle)
    {
        return _underlyingFeature.CachedYuvFrames.ContainsKey(nativeHandle);
    }

    public override bool FormatSupported(XRCpuImage image, TextureFormat format)
    {
        if (!_supportedInputFormats.Contains(image.format))
        {
            return false;
        }

        if (!_supportedOutputFormats.Contains(format))
        {
            return false;
        }

        return true;
    }

    public override bool TryGetPlane(int nativeHandle, int planeIndex, out XRCpuImage.Plane.Cinfo planeCinfo)
    {
        planeCinfo = new XRCpuImage.Plane.Cinfo();

        if (!NativeHandleValid(nativeHandle))
        {
            Debug.LogWarning("Native handle [" + nativeHandle + "] is not valid. The frame might have expired.");
            return false;
        }

        if (!GetFrameFromHandle(nativeHandle, out SpacesYUVFrame frame))
        {
            Debug.LogError($"Failed to retrieve cached frame for handle [{nativeHandle}]");
            return false;
        }

        IntPtr dataPtr;
        int dataLength;

        switch (frame.Format)
        {
            // YUV420 format -  2 Planes: Y, UV
            case XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV12_QCOMX:
            case XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV21_QCOMX:
                switch (planeIndex)
                {
                    // XRCpuImage.GetPlane(0) : Y plane
                    case 0:
                        dataPtr = frame.DataPtr + (int)frame.YPlane.Root;
                        dataLength = (int)frame.YPlane.Stride * frame.Dimensions.y;
                        planeCinfo = new XRCpuImage.Plane.Cinfo(dataPtr, dataLength, (int) frame.YPlane.Stride, 1);
                        break;
                    // XRCpuImage.GetPlane(1) : UV / VU plane
                    case 1:
                        // We use the same Root and Stride for NV12 and NV21 since they share the underlying plane
                        dataPtr = frame.DataPtr + (int)frame.UPlane.Root;
                        dataLength = (int)frame.UPlane.Stride * (frame.Dimensions.y / 2);
                        planeCinfo = new XRCpuImage.Plane.Cinfo(dataPtr, dataLength, (int) frame.UPlane.Stride, 2);
                        break;
                }
                break;
            // YUYV format -    1 Plane, : YUYV
            case XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUYV_QCOMX:
                switch (planeIndex)
                {
                    // XRCpuImage.GetPlane(0) : YUYV plane
                    case 0:
                        dataPtr = frame.DataPtr + (int)frame.YPlane.Root;
                        dataLength = (int)frame.YPlane.Stride * frame.Dimensions.y;
                        planeCinfo = new XRCpuImage.Plane.Cinfo(dataPtr, dataLength, (int) frame.UPlane.Stride, 4);
                        break;
                }
                break;
        }

        return true;
    }

    public override bool TryGetConvertedDataSize(int nativeHandle, Vector2Int dimensions, TextureFormat format, out int size)
    {
        size = 0;

        if (dimensions.x < 0 || dimensions.y < 0)
        {
            return false;
        }

        if (!_supportedOutputFormats.Contains(format))
        {
            return false;
        }

        switch (format)
        {
            case TextureFormat.RGB24:
                size = dimensions.x * dimensions.y * 3;
                break;
            case TextureFormat.RGBA32:
            case TextureFormat.BGRA32:
                size = dimensions.x * dimensions.y * 4;
                break;
        }

        return true;
    }

    public override bool TryConvert(int nativeHandle, XRCpuImage.ConversionParams conversionParams, IntPtr destinationBuffer, int bufferLength)
    {
        if (_underlyingFeature.DirectMemoryAccessConversion)
        {
            return TryConvertUsingNativeArrays(nativeHandle, conversionParams, destinationBuffer, bufferLength);
        }
        return TryConvertUsingCachedBuffers(nativeHandle, conversionParams, destinationBuffer, bufferLength, ref _sourcePixels, ref _outPixels);
    }

    public override void DisposeImage(int nativeHandle)
    {
        // NOTE(CH): No need to dispose images. The underlying feature takes care of
        // releasing frames from the runtime after frame cache is full and of managing the
        // frame cache's native memory. We override to avoid a NotImplementedException.
    }

    public override int ConvertAsync(int nativeHandle, XRCpuImage.ConversionParams conversionParams)
    {
        ConvertAsync(nativeHandle, conversionParams, null, IntPtr.Zero);
        return _pendingConversionRequests.Last().RequestId;
    }

    public override void ConvertAsync(int nativeHandle, XRCpuImage.ConversionParams conversionParams, OnImageRequestCompleteDelegate callback, IntPtr context)
    {
        int requestId = ++_conversionRequestsIds;

        ConversionRequest request = new ConversionRequest(
            requestId,
            nativeHandle,
            conversionParams,
            callback,
            context
        );

        if (!_underlyingFeature.DirectMemoryAccessConversion && _underlyingFeature.CacheFrameBeforeAsyncConversion)
        {
            GetFrameFromHandle(nativeHandle, out SpacesYUVFrame frame);
            TryGetConvertedDataSize(nativeHandle, conversionParams.outputDimensions, conversionParams.outputFormat, out int outSize);
            request.CacheFrame(frame, outSize);
        }

        _conversionRequests[requestId] = request;
        _pendingConversionRequests.Enqueue(request);
        StartWorker();
    }

    public override bool TryGetAsyncRequestData(int requestId, out IntPtr dataPtr, out int dataLength)
    {
        dataPtr = IntPtr.Zero;
        dataLength = 0;

        if (!_conversionRequests.ContainsKey(requestId))
        {
            return false;
        }

        ConversionRequest request = _conversionRequests[requestId];
        if (request.Status != XRCpuImage.AsyncConversionStatus.Ready)
        {
            return false;
        }

        dataPtr = request.DataPtr;
        dataLength = request.DataLength;
        return true;
    }

    public override void DisposeAsyncRequest(int requestId)
    {
        if (!_conversionRequests.ContainsKey(requestId))
        {
            return;
        }
        _conversionRequests.Remove(requestId, out var request);
        if (request.Status != XRCpuImage.AsyncConversionStatus.Disposed)
        {
            request.Dispose();
        }
    }

    public override XRCpuImage.AsyncConversionStatus GetAsyncRequestStatus(int requestId)
    {
        if (!_conversionRequests.ContainsKey(requestId))
        {
            return XRCpuImage.AsyncConversionStatus.Disposed;
        }
        return _conversionRequests[requestId].Status;
    }

    public void MarkPendingRequestsForDisposal()
    {
        if (_currentConversionRequest != null)
        {
            _currentConversionRequest.Status = XRCpuImage.AsyncConversionStatus.Disposed;
        }
        foreach (var request in _pendingConversionRequests.ToArray())
        {
            request.Status = XRCpuImage.AsyncConversionStatus.Disposed;
        }
    }

    private void SpawnWorker()
    {
        if (_conversionWorker != null)
        {
            return;
        }
        _mainThreadContext = SynchronizationContext.Current;
        _conversionWorker = new Thread(ConversionWorker) { Name = "ConversionWorker" };
    }

    private void StartWorker()
    {
        if (_conversionWorker == null)
        {
            Debug.LogError("Failed to start conversion worker: Worker cannot be null.");
            return;
        }

        if (Interlocked.CompareExchange(ref _conversionWorkerRunning, 1, 0) == 1)
        {
            return;
        }

        // The thread has finished working, but is still alive.
        if (_conversionWorker.IsAlive)
        {
            _conversionWorker.Join();
        }
        _conversionWorker.Start();
    }

    private bool TryConvertUsingNativeArrays(int nativeHandle, XRCpuImage.ConversionParams conversionParams, IntPtr destinationBuffer, int bufferLength)
    {
        if (!NativeHandleValid(nativeHandle) || !_supportedOutputFormats.Contains(conversionParams.outputFormat))
        {
            return false;
        }

        // Conversion parameters
        var inputRect = conversionParams.inputRect;
        var outputDimensions = conversionParams.outputDimensions;
        var mirrorX = (conversionParams.transformation & XRCpuImage.Transformation.MirrorX) != 0;
        var mirrorY = (conversionParams.transformation & XRCpuImage.Transformation.MirrorY) != 0;

        if (!GetFrameFromHandle(nativeHandle, out SpacesYUVFrame frame))
        {
            Debug.LogError($"Failed to retrieve cached frame for handle [{nativeHandle}]");
            return false;
        }

        unsafe
        {
            NativeArray<byte> nativeSourcePixels = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>((void*)frame.DataPtr, frame.DataLength, Allocator.Invalid);
            NativeArray<byte> nativeOutPixels = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>((void*)destinationBuffer, bufferLength, Allocator.Invalid);
            ImagePlane yPlane = frame.YPlane;
            ImagePlane uPlane = frame.UPlane;
            ImagePlane vPlane = frame.VPlane;

            for (int row = 0; row < outputDimensions.y; row++)
            {
                for (int col = 0; col < outputDimensions.x; col++)
                {
                    // Nearest neighbour mapping from the output rectangle (target buffer) to the input rectangle (source image)
                    int sourceRow = (int)((inputRect.yMin + inputRect.height * (row / (float)outputDimensions.y)));
                    int sourceCol = (int)((inputRect.xMin + inputRect.width * (col / (float)outputDimensions.x)));

                    var y = nativeSourcePixels[yPlane.GetOffset(sourceCol, sourceRow)];
                    sbyte u = (sbyte)(nativeSourcePixels[uPlane.GetOffset(sourceCol, sourceRow)] - 128);
                    sbyte v = (sbyte)(nativeSourcePixels[vPlane.GetOffset(sourceCol, sourceRow)] - 128);

                    // YUV NV21 to RGB conversion
                    // https://en.wikipedia.org/wiki/YUV#Y%E2%80%B2UV420sp_(NV21)_to_RGB_conversion_(Android)

                    var r = y + (1.370705f * v);
                    var g = y - (0.698001f * v) - (0.337633f * u);
                    var b = y + (1.732446f * u);

                    r = r > 255 ? 255 : r < 0 ? 0 : r;
                    g = g > 255 ? 255 : g < 0 ? 0 : g;
                    b = b > 255 ? 255 : b < 0 ? 0 : b;

                    // Mirror output pixel across X axis (mirror rows) and Y axis (mirror columns)
                    int outputRow = mirrorX ? row : outputDimensions.y - row - 1;
                    int outputCol = mirrorY ? outputDimensions.x - col - 1 : col;
                    int pixelIndex = (outputRow * outputDimensions.x) + outputCol;

                    switch (conversionParams.outputFormat)
                    {
                        case TextureFormat.RGB24:
                            nativeOutPixels[3 * pixelIndex] = (byte)r;
                            nativeOutPixels[(3 * pixelIndex) + 1] = (byte)g;
                            nativeOutPixels[(3 * pixelIndex) + 2] = (byte)b;
                            break;
                        case TextureFormat.RGBA32:
                            nativeOutPixels[4 * pixelIndex] = (byte)r;
                            nativeOutPixels[(4 * pixelIndex) + 1] = (byte)g;
                            nativeOutPixels[(4 * pixelIndex) + 2] = (byte)b;
                            nativeOutPixels[(4 * pixelIndex) + 3] = 255;
                            break;
                        case TextureFormat.BGRA32:
                            nativeOutPixels[4 * pixelIndex] = (byte)b;
                            nativeOutPixels[(4 * pixelIndex) + 1] = (byte)g;
                            nativeOutPixels[(4 * pixelIndex) + 2] = (byte)r;
                            nativeOutPixels[(4 * pixelIndex) + 3] = 255;
                            break;
                    }
                }
            }
        }

        // If frame was disposed during conversion, avoid garbage output.
        if (!NativeHandleValid(nativeHandle))
        {
            return false;
        }
        return true;
    }

    private bool TryConvertUsingCachedBuffers(int nativeHandle, XRCpuImage.ConversionParams conversionParams, IntPtr destinationBuffer, int bufferLength, ref byte[] sourcePixels, ref byte[] outPixels, bool dataPreCached = false, SpacesYUVFrame frame = null)
    {
        if (!NativeHandleValid(nativeHandle) || !_supportedOutputFormats.Contains(conversionParams.outputFormat))
        {
            return false;
        }

        // Conversion parameters
        var inputRect = conversionParams.inputRect;
        var outputDimensions = conversionParams.outputDimensions;
        var mirrorX = (conversionParams.transformation & XRCpuImage.Transformation.MirrorX) != 0;
        var mirrorY = (conversionParams.transformation & XRCpuImage.Transformation.MirrorY) != 0;

        if (frame == null && !GetFrameFromHandle(nativeHandle, out frame))
        {
            Debug.LogError($"Failed to retrieve cached frame for handle [{nativeHandle}]");
            return false;
        }

        if (!dataPreCached)
        {
            // Initialise pixel cache
            if (sourcePixels == null || sourcePixels.Length != frame.DataLength)
            {
                sourcePixels = new byte[frame.DataLength];
            }
            if (outPixels == null || outPixels.Length != bufferLength)
            {
                outPixels = new byte[bufferLength];
            }

            Marshal.Copy(frame.DataPtr, sourcePixels, 0, frame.DataLength);
        }
        ImagePlane yPlane = frame.YPlane;
        ImagePlane uPlane = frame.UPlane;
        ImagePlane vPlane = frame.VPlane;

        for (int row = 0; row < outputDimensions.y; row++)
        {
            for (int col = 0; col < outputDimensions.x; col++)
            {
                // Nearest neighbour mapping from the output rectangle (target buffer) to the input rectangle (source image)
                int sourceRow = (int)((inputRect.yMin + inputRect.height * (row / (float)outputDimensions.y)));
                int sourceCol = (int)((inputRect.xMin + inputRect.width * (col / (float)outputDimensions.x)));

                var y = sourcePixels[yPlane.GetOffset(sourceCol, sourceRow)];
                sbyte u = (sbyte)(sourcePixels[uPlane.GetOffset(sourceCol, sourceRow)] - 128);
                sbyte v = (sbyte)(sourcePixels[vPlane.GetOffset(sourceCol, sourceRow)] - 128);

                // YUV NV21 to RGB conversion
                // https://en.wikipedia.org/wiki/YUV#Y%E2%80%B2UV420sp_(NV21)_to_RGB_conversion_(Android)

                var r = y + (1.370705f * v);
                var g = y - (0.698001f * v) - (0.337633f * u);
                var b = y + (1.732446f * u);

                r = r > 255 ? 255 : r < 0 ? 0 : r;
                g = g > 255 ? 255 : g < 0 ? 0 : g;
                b = b > 255 ? 255 : b < 0 ? 0 : b;

                // Mirror output pixel across X axis (mirror rows) and Y axis (mirror columns)
                int outputRow = mirrorX ? row : outputDimensions.y - row - 1;
                int outputCol = mirrorY ? outputDimensions.x - col - 1 : col;
                int pixelIndex = (outputRow * outputDimensions.x) + outputCol;

                switch (conversionParams.outputFormat)
                {
                    case TextureFormat.RGB24:
                        outPixels[3 * pixelIndex] = (byte)r;
                        outPixels[(3 * pixelIndex) + 1] = (byte)g;
                        outPixels[(3 * pixelIndex) + 2] = (byte)b;
                        break;
                    case TextureFormat.RGBA32:
                        outPixels[4 * pixelIndex] = (byte)r;
                        outPixels[(4 * pixelIndex) + 1] = (byte)g;
                        outPixels[(4 * pixelIndex) + 2] = (byte)b;
                        outPixels[(4 * pixelIndex) + 3] = 255;
                        break;
                    case TextureFormat.BGRA32:
                        outPixels[4 * pixelIndex] = (byte)b;
                        outPixels[(4 * pixelIndex) + 1] = (byte)g;
                        outPixels[(4 * pixelIndex) + 2] = (byte)r;
                        outPixels[(4 * pixelIndex) + 3] = 255;
                        break;
                }
            }
        }

        Marshal.Copy(outPixels, 0, destinationBuffer, bufferLength);
        return true;
    }

    // There are 3 types of async conversion, depending on user settings:
    // Type 1: ConversionRequest contains all the necessary data for conversion, which was previously copied into its private cache
    // Type 2: ConversionRequest doesn't contain any cached data, data will be requested to the underlying feature and referenced natively
    // Type 3: ConversionRequest doesn't contain any cached data, data will be requested to the underlying feature and copied into a shared cache
    private bool TryConvertAsync(ConversionRequest request)
    {
        if (request.IsFrameCached)
        {
            return TryConvertUsingCachedBuffers(
                request.NativeHandle,
                request.ConversionParams,
                request.DataPtr,
                request.DataLength,
                ref request.SourcePixels,
                ref request.OutPixels,
                true,
                request.Frame);
        }

        if (_underlyingFeature.DirectMemoryAccessConversion)
        {
            return TryConvertUsingNativeArrays(
                request.NativeHandle,
                request.ConversionParams,
                request.DataPtr,
                request.DataLength);
        }

        return TryConvertUsingCachedBuffers(
            request.NativeHandle,
            request.ConversionParams,
            request.DataPtr,
            request.DataLength,
            ref _asyncSourcePixels,
            ref _asyncOutPixels);
    }

    private bool GetFrameFromHandle(int nativeHandle, out SpacesYUVFrame frame)
    {
        frame = null;
        if (!_underlyingFeature.CachedYuvFrames.TryGetValue(nativeHandle, out SpacesYUVFrame[] frames))
        {
            return false;
        }
        frame = frames[0];
        return true;
    }

    private void ConversionWorker()
    {
        SpacesThreadUtility.SetThreadHint(
            _underlyingFeature.HighPriorityAsyncConversion ?
            SpacesThreadType.SPACES_THREAD_TYPE_RENDERER_WORKER :
            SpacesThreadType.SPACES_THREAD_TYPE_APPLICATION_WORKER);

        while (_pendingConversionRequests.TryDequeue(out _currentConversionRequest))
        {
            // Check that request hasn't been marked for disposal before processing.
            if (_currentConversionRequest.Status == XRCpuImage.AsyncConversionStatus.Disposed)
            {
                _currentConversionRequest.Dispose();
                _mainThreadContext.Post(OnConversionRequestCompleted, _currentConversionRequest);
                continue;
            }

            _currentConversionRequest.Status = XRCpuImage.AsyncConversionStatus.Processing;

            // If the frame data was not cached upon request, check that it exists in the underlying feature and allocate the output memory
            if (!_currentConversionRequest.IsFrameCached)
            {
                if (!NativeHandleValid(_currentConversionRequest.NativeHandle))
                {
                    _currentConversionRequest.Status = XRCpuImage.AsyncConversionStatus.Disposed;
                    _mainThreadContext.Post(OnConversionRequestCompleted, _currentConversionRequest);
                }

                if (!TryGetConvertedDataSize(
                        _currentConversionRequest.NativeHandle,
                        _currentConversionRequest.ConversionParams.outputDimensions,
                        _currentConversionRequest.ConversionParams.outputFormat,
                        out int dataLength))
                {
                    _currentConversionRequest.Status = XRCpuImage.AsyncConversionStatus.Failed;
                    _mainThreadContext.Post(OnConversionRequestCompleted, _currentConversionRequest);
                    continue;
                }

                _currentConversionRequest.AllocateDataPtr(dataLength);
            }

            var result = TryConvertAsync(_currentConversionRequest);

            // Check that request hasn't been marked for disposal during processing.
            if (_currentConversionRequest.Status == XRCpuImage.AsyncConversionStatus.Disposed)
            {
                _currentConversionRequest.Dispose();
                _mainThreadContext.Post(OnConversionRequestCompleted, _currentConversionRequest);
                continue;
            }

            if (result)
            {
                _currentConversionRequest.Status = XRCpuImage.AsyncConversionStatus.Ready;
            }
            else
            {
                _currentConversionRequest.Status = !_currentConversionRequest.IsFrameCached && NativeHandleValid(_currentConversionRequest.NativeHandle) ? XRCpuImage.AsyncConversionStatus.Failed : XRCpuImage.AsyncConversionStatus.Disposed;
            }

            _currentConversionRequest.Status = result ? XRCpuImage.AsyncConversionStatus.Ready : XRCpuImage.AsyncConversionStatus.Failed;
            _mainThreadContext.Post(OnConversionRequestCompleted, _currentConversionRequest);
        }

        _currentConversionRequest = null;
        Interlocked.Exchange(ref _conversionWorkerRunning, 0);
    }

    // Note(CH): This method will be executed on the Main Thread
    private void OnConversionRequestCompleted(object state)
    {
        ConversionRequest request = state as ConversionRequest;

        request!.Callback?.Invoke(
            request.Status,
            request.ConversionParams,
            request.DataPtr,
            request.DataLength,
            request.Context);

        // Note(CH): If the request has a callback, providers are expected to dispose the data. If a callback is present, the developer must dispose the AsyncRequest object themself.
        if(request.Callback != null)
        {
            DisposeAsyncRequest(request.RequestId);
        }
    }
}
