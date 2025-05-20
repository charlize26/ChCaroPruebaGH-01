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
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = FeatureName,
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "Qualcomm",
        Desc = "Enables Camera Access feature on Snapdragon Spaces enabled devices",
        DocumentationLink = "",
        OpenxrExtensionStrings = FeatureExtensions,
        Version = "1.0.2",
        Required = false,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID)]
#endif
    internal sealed partial class CameraAccessFeature : SpacesOpenXRFeature
    {
        public const string FeatureName = "Camera Access";
        public const string FeatureID = "com.qualcomm.snapdragon.spaces.cameraaccess";
        public const string FeatureExtensions = "XR_QCOMX_camera_frame_access XR_KHR_convert_timespec_time";

        [Tooltip("XRCpuImage.Convert read/write can be optimized on certain devices through direct memory access. By default, Spaces moves frame data using Marshal.Copy. Enabling this setting allows Spaces to use NativeArray<byte> direct representations of the source and target buffers.\n\nThis setting may heavily impact performance on some architectures, use at your own risk.")]
        public bool DirectMemoryAccessConversion = false;

        [Tooltip("Size of the XRCpuImage.ConvertAsync frame cache, defining how many asynchronous conversion requests can be queued. When the limit is reached, older requests will expire.\n\nIncrease this number if too many requests are disposed before processing and you see dropped frames, or consider using the Cache Frame Before Async Conversion setting.")]
        // Note(CH): The runtime will cycle through a maximum of 16 data buffers, overwriting the oldest one.
        [Range(MinCacheSize, MaxCacheSize)]
        public uint CpuFrameCacheSize = 4;

        [Tooltip("XRCpuImage.ConvertAsync conversion requests are handled in a separate thread. Enabling this setting allows the thread to be treated as high-priority by the OpenXR runtime.\n\nMake sure to profile your application when using this setting.")]
        public bool HighPriorityAsyncConversion = false;

        [Tooltip("XRCpuImage.ConvertAsync conversion requests can be guaranteed by caching the frames upon request without relying on the CPU Frame Cache, with a slight performance and memory penalty. Use this if the device's conversion speed cannot keep up with the camera refresh rate, but you do not want to skip any frames.\n\nThis setting may significantly increase the memory footprint of your application over time, make sure to profile and use only if strictly necessary.\n\nThis setting will be ignored if Direct Memory Access Conversion is enabled.")]
        public bool CacheFrameBeforeAsyncConversion = false;

        private static List<XRCameraSubsystemDescriptor> _cameraSubsystemDescriptors = new();
        private BaseRuntimeFeature _baseRuntimeFeature;

        private const int NanosecondsPerSecond = 1000000000;
        private const int MinCacheSize = 1;
        private const int MaxCacheSize = 8;

        private readonly List<XrCameraTypeQCOM> _supportedCameraTypes = new()
        {
            XrCameraTypeQCOM.XR_CAMERA_TYPE_RGB_QCOMX
        };
        private readonly List<XrCameraFrameFormatQCOM> _supportedFrameFormats = new()
        {
            XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV12_QCOMX,
            XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV21_QCOMX,
            XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUYV_QCOMX
        };

        private Dictionary<uint, (XrCameraInfoQCOM, XrCameraFrameConfigurationQCOM)> _supportedConfigurations;

        // Camera configuration
        private ulong _cameraHandle;
        private XrCameraInfoQCOM? _cameraInfo;
        private XrCameraFrameConfigurationQCOM? _frameConfiguration;
        private XRCameraConfiguration? _cameraConfiguration;

        private XrCameraFrameBufferQCOM _defaultFrameBuffer;
        private XrCameraFrameHardwareBufferQCOM _defaultFrameHardwareBuffer;
        private XrCameraSensorPropertiesQCOM _defaultSensorProperties;

        private XrCameraFrameBufferQCOM[] _frameBuffers;

        private GPUCameraAccess _gpuCameraAccess;

        private XrCameraFrameDataQCOM? _cachedFrameData;
        private ConcurrentQueue<int> _cachedYuvFrameNumbers = new();
        private ConcurrentDictionary<int, SpacesYUVFrame[]> _cachedYuvFrames = new();
        private bool _deviceIsA3;

        private XrCameraSensorPropertiesQCOM[] _sensorProperties;
        private XrPosef _lastFramePose = new(XrQuaternionf.identity, XrVector3f.zero);
        private CameraAccessInputUpdate _cameraInputUpdate { get; } = new();

        internal XrCameraSensorPropertiesQCOM[] SensorProperties => _sensorProperties;
        internal ConcurrentQueue<int> CachedYuvFrameNumbers => _cachedYuvFrameNumbers;
        internal ConcurrentDictionary<int, SpacesYUVFrame[]> CachedYuvFrames => _cachedYuvFrames;
        internal Pose LastFramePose => _lastFramePose.ToPose();
        internal override bool RequiresApplicationCameraPermissions => true;

#if UNITY_ANDROID && !UNITY_EDITOR
        internal bool AreApplicationCameraPermissionsGranted => Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
        internal bool AreApplicationCameraPermissionsGranted => false;
#endif

        protected override bool IsRequiringBaseRuntimeFeature => true;

        protected override string GetXrLayersToLoad()
        {
            return "XR_APILAYER_QCOM_retina_tracking";
        }

        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            base.OnInstanceCreate(instanceHandle);

            _baseRuntimeFeature = OpenXRSettings.Instance.GetFeature<BaseRuntimeFeature>();

            var missingExtensions = GetMissingExtensions(FeatureExtensions);
            if (missingExtensions.Any())
            {
                Debug.Log(FeatureName + " is missing following extension in the runtime: " + String.Join(",", missingExtensions));
                return false;
            }

            _deviceIsA3 = SystemInfo.deviceModel.ToLower().Contains("motorola edge");
            _gpuCameraAccess = new GPUCameraAccess();

            // Initialise default frame access structures for convenience
            //
            // XR_MAX_CAMERA_RADIAL_DISTORSION_PARAMS_LENGTH_QCOMX == 6
            // XR_MAX_CAMERA_TANGENTIAL_DISTORSION_PARAMS_LENGTH_QCOMX == 2
            //
            // Marshal.SizeOf(XrCameraFramePlaneQCOMX) == 32
            // XR_CAMERA_FRAME_PLANES_SIZE_QCOMX == 4

            var defaultSensorIntrinsics = new XrCameraSensorIntrinsicsQCOM(
                new XrVector2f(Vector2.zero),
                new XrVector2f(Vector2.zero),
                new float[6],
                new float[2],
                0);
            _defaultSensorProperties = new XrCameraSensorPropertiesQCOM(
                defaultSensorIntrinsics,
                new XrPosef(new XrQuaternionf(Quaternion.identity), new XrVector3f(Vector3.zero)),
                new XrOffset2Di(Vector2Int.zero),
                new XrExtent2Di(Vector2Int.zero),
                0,
                0);
            _defaultFrameBuffer = new XrCameraFrameBufferQCOM(
                0,
                IntPtr.Zero,
                new XrOffset2Di(Vector2Int.zero),
                0,
                new byte[32 * 4]);
            _defaultFrameHardwareBuffer = new XrCameraFrameHardwareBufferQCOM(IntPtr.Zero);

            CpuFrameCacheSize = Math.Clamp(CpuFrameCacheSize, MinCacheSize, MaxCacheSize);

            return true;
        }

        protected override void OnSubsystemCreate()
        {
            CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>(_cameraSubsystemDescriptors, CameraSubsystem.ID);
        }

        protected override void OnSubsystemStop()
        {
            StopSubsystem<XRCameraSubsystem>();
        }

        protected override void OnSubsystemDestroy()
        {
            DestroySubsystem<XRCameraSubsystem>();
        }

        protected override void OnHookMethods()
        {
            HookMethod("xrEnumerateCamerasQCOMX", out _xrEnumerateCamerasQCOM);
            HookMethod("xrGetSupportedFrameConfigurationsQCOMX", out _xrGetSupportedFrameConfigurationsQCOM);
            HookMethod("xrCreateCameraHandleQCOMX", out _xrCreateCameraHandleQCOM);
            HookMethod("xrReleaseCameraHandleQCOMX", out _xrReleaseCameraHandleQCOM);
            HookMethod("xrAccessFrameQCOMX", out _xrAccessFrameQCOM);
            HookMethod("xrReleaseFrameQCOMX", out _xrReleaseFrameQCOM);
            HookMethod("xrConvertTimeToTimespecTimeKHR", out _xrConvertTimeToTimespecTimeKHR);
        }

        internal bool TryGetSupportedConfigurations(out List<XRCameraConfiguration> configurations)
        {
            configurations = new List<XRCameraConfiguration>();

            if (!TryEnumerateCameras(out List<XrCameraInfoQCOM> cameraInfos))
            {
                Debug.LogError("Failed to enumerate cameras.");
                return false;
            }

            uint configurationHandle = 0;
            _supportedConfigurations = new Dictionary<uint, (XrCameraInfoQCOM, XrCameraFrameConfigurationQCOM)>();
            foreach (var cameraInfo in cameraInfos)
            {
                if (!_supportedCameraTypes.Contains(cameraInfo.CameraType))
                {
                    continue;
                }

                // Retrieve target frame configuration for camera set
                if (!TryGetFrameConfigurationsForCamera(cameraInfo.CameraSet, out List<XrCameraFrameConfigurationQCOM> frameConfigurations))
                {
                    Debug.LogError("Failed to find frame configurations for camera set [" + cameraInfo.CameraSet + "].");
                    continue;
                }

                // Filter frame configurations by supported formats
                foreach (var frameConfig in frameConfigurations)
                {
                    if (_supportedFrameFormats.Contains(frameConfig.Format))
                    {
                        XRCameraConfiguration config = new XRCameraConfiguration(
                            // Note(CH): No native configuration handles exist, but developers should not be using these anyway. We use it as a unique ID instead.
                            (IntPtr)configurationHandle,
                            frameConfig.Dimensions.ToVector2Int(),
                            (int)frameConfig.MaxFPS
                        );
                        configurations.Add(config);
                        _supportedConfigurations[configurationHandle++] = (cameraInfo, frameConfig);
                    }
                }
            }

            return true;
        }

        internal XRCameraConfiguration? SuggestCameraConfiguration(List<XRCameraConfiguration> configurations)
        {
            if (configurations.Count != _supportedConfigurations.Count)
            {
                Debug.LogWarning("List of XRCameraConfiguration provided does not match internal configuration map. No frame configuration suggested.");
                return null;
            }

            XRCameraConfiguration? suggestedConfig = null;
            bool stereoConfigFound = false;

            // Returns first "full"-resolution configuration listed
            // If none, returns first stereo configuration listed
            foreach (var xrConfig in configurations)
            {
                XrCameraFrameConfigurationQCOM nativeConfig = _supportedConfigurations[(uint)xrConfig.nativeConfigurationHandle].Item2;

                // Suggest "full" resolutions
                if (nativeConfig.ResolutionName.Equals("full"))
                {
                    return xrConfig;
                }

                // Suggest configurations with 2 textures (likely front stereo RGB)
                if (stereoConfigFound)
                {
                    continue;
                }
                if (nativeConfig.FrameBufferCount == 2)
                {
                    stereoConfigFound = true;
                }
                suggestedConfig = xrConfig;
            }

            return suggestedConfig;
        }

        internal bool IsSupportedCameraConfiguration(XRCameraConfiguration configuration)
        {
            uint handle = (uint) configuration.nativeConfigurationHandle;
            if (!_supportedConfigurations.ContainsKey(handle))
            {
                return false;
            }

            // Check that the internal representation of the configuration matches the developer's expectations
            XrCameraFrameConfigurationQCOM qcomConfig = _supportedConfigurations[handle].Item2;
            return qcomConfig.Dimensions.ToVector2Int() == (configuration.resolution) && qcomConfig.MaxFPS == configuration.framerate;
        }

        internal bool SetCameraConfiguration(XRCameraConfiguration configuration)
        {
            if (!IsSupportedCameraConfiguration(configuration))
            {
                Debug.LogError($"Cannot set unsupported frame configuration: {configuration}.");
            }

            // If setting the same camera configuration, accept it but do nothing
            if (configuration == _cameraConfiguration)
            {
                return true;
            }

            XrCameraInfoQCOM cameraInfo;
            XrCameraFrameConfigurationQCOM frameConfiguration;
            (cameraInfo, frameConfiguration) = _supportedConfigurations[(uint) configuration.nativeConfigurationHandle];

            Debug.Log($"Camera configuration set to <{configuration}>");
            _frameConfiguration = frameConfiguration;

            // If the Camera Set remains the same and the camera is already open, no need to open a new camera
            if (_cameraInfo != null && _cameraInfo.Value.CameraSet == cameraInfo.CameraSet && IsCameraOpen())
            {
                return true;
            }
            _cameraInfo = cameraInfo;

            if (!CloseCamera())
            {
                return false;
            }
            return OpenCamera();
        }

        internal bool OpenCamera(bool skipListener = false)
        {
            if (_cameraHandle != 0)
            {
                Debug.LogError($"Failed to open camera: Camera already open with handle: [{_cameraHandle}].");
                return false;
            }

            if (_cameraInfo == null)
            {
                Debug.LogError("Failed to open camera: Camera information not available.");
                return false;
            }

            if (!TryCreateCameraHandle(out _cameraHandle, _cameraInfo.Value.CameraSet))
            {
                Debug.LogError($"Failed to create camera handle for camera set [{_cameraInfo.Value.CameraSet}].");
                _cameraHandle = 0;
                return false;
            }

            _cameraInputUpdate.AddDevice();
            if (!skipListener)
            {
                _baseRuntimeFeature.OnSpacesAppPauseStateChange += OnApplicationPauseStateChanged;
            }
            return true;
        }

        internal bool CloseCamera(bool skipListener = false)
        {
            FlushFrameCache();

            // If camera is already closed, exit silently
            if (_cameraHandle == 0)
            {
                return true;
            }

            // Release camera handle
            if (_cameraHandle != 0 && !TryReleaseCameraHandle(_cameraHandle))
            {
                Debug.LogError($"Failed to release camera handle for camera [{_cameraHandle}].");
                return false;
            }

            _cameraInputUpdate.RemoveDevice();
            if (!skipListener)
            {
                _baseRuntimeFeature.OnSpacesAppPauseStateChange -= OnApplicationPauseStateChanged;
            }
            return true;
        }

        internal bool IsCameraOpen()
        {
            return _cameraHandle != 0 && _cameraInfo != null && _frameConfiguration != null;
        }

        internal bool TryGetNewFrame(out XRCameraFrame cameraFrame)
        {
            cameraFrame = default;

            if (!IsCameraOpen())
            {
                Debug.LogError("Failed to get frame: No camera open.");
                return false;
            }

            if (!TryAccessFrame(_cameraHandle, _frameConfiguration!.Value, _cameraInfo!.Value.SensorCount))
            {
                return false;
            }

            XRCameraFrameProperties frameProperties = XRCameraFrameProperties.Timestamp |
                XRCameraFrameProperties.DisplayMatrix |
                XRCameraFrameProperties.ProjectionMatrix;

            Timespec timestamp = ConvertXrTimeToTimespec(_cachedFrameData!.Value.Timestamp);
            cameraFrame = new XRCameraFrame(
                timestamp.Seconds * NanosecondsPerSecond + timestamp.Nanoseconds,
                0,
                0,
                Color.white,
                GetProjectionMatrix(_sensorProperties[0].Extrinsic, _sensorProperties[0].Intrinsics),
                GetDisplayMatrix(),
                TrackingState.Tracking,
                (IntPtr) _cachedFrameData.Value.FrameNumber,
                frameProperties,
                0,
                0,
                0,
                0,
                Color.white,
                Vector3.zero,
                new SphericalHarmonicsL2(),
                new XRTextureDescriptor(),
                0);

            return true;
        }

        internal bool TryGetLatestCpuImage(out XRCpuImage.Cinfo cameraImageCinfo)
        {
            cameraImageCinfo = default;

            if (CachedYuvFrames.Count == 0)
            {
                Debug.LogError("Tried to acquire latest CPU image, but no CPU image is available yet.");
                return false;
            }

            // Note(CH): In case of multiple sensors (i.e Left/Right eyes), only the first sensor's data is exposed
            int recentFrameNumber = _cachedYuvFrameNumbers.Last();
            Timespec timestamp = ConvertXrTimeToTimespec(_cachedYuvFrames[recentFrameNumber][0].Timestamp);
            cameraImageCinfo = new XRCpuImage.Cinfo(
                recentFrameNumber,
                _cachedYuvFrames[recentFrameNumber][0].Dimensions,
                _cachedYuvFrames[recentFrameNumber][0].NativePlaneCount,
                timestamp.Seconds + timestamp.Nanoseconds / (double) NanosecondsPerSecond,
                XRCpuImage.Format.AndroidYuv420_888);

            return true;
        }

        internal bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
        {
            cameraIntrinsics = default;

            if (!IsCameraOpen())
            {
                Debug.LogError("Failed to get intrinsics: No camera open.");
                return false;
            }

            var shouldRequestSensorInfo = _sensorProperties is null || _sensorProperties.Length == 0;
            if (shouldRequestSensorInfo && !TryAccessFrame(_cameraHandle, _frameConfiguration!.Value, _cameraInfo!.Value.SensorCount))
            {
                Debug.LogError("Failed to get intrinsics: Frame request failed.");
                return false;
            }

            if (_sensorProperties == null)
            {
                Debug.LogError("Failed to get intrinsics: Sensor properties unavailable.");
                return false;
            }

            // Note(CH): In case of multiple sensors (i.e Left/Right eyes), only the first sensor's data is exposed
            cameraIntrinsics = new XRCameraIntrinsics(
                _sensorProperties![0].Intrinsics.FocalLength.ToVector2(),
                _sensorProperties![0].Intrinsics.PrincipalPoint.ToVector2(),
                _sensorProperties[0].ImageDimensions.ToVector2Int());
            return true;
        }

        internal List<XRTextureDescriptor> GetGpuTextureDescriptors()
        {
            if (_gpuCameraAccess == null)
            {
                Debug.LogError("No valid gpuCameraAccess");
                _gpuCameraAccess = new GPUCameraAccess();
            }

            return _gpuCameraAccess.GetTextureDescriptors(_sensorProperties);
        }

        internal void ReleaseResources()
        {
            if (IsCameraOpen() && !CloseCamera())
            {
                Debug.LogError("Failed to release camera resources: Could not close camera.");
            }

            ReleaseGPU();
            _cameraInputUpdate.RemoveDevice();
        }

        private bool TryEnumerateCameras(out List<XrCameraInfoQCOM> cameraInfos)
        {
            cameraInfos = new List<XrCameraInfoQCOM>();

            if (_xrEnumerateCamerasQCOM == null)
            {
                Debug.LogError("xrEnumerateCamerasQCOM method not found!");
                return false;
            }

            uint cameraInfoCountOutput = 0;

            var result = _xrEnumerateCamerasQCOM(SessionHandle, 0, ref cameraInfoCountOutput, IntPtr.Zero);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Enumerate device cameras (1) failed: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            using ScopeArrayPtr<XrCameraInfoQCOM> cameraInfosPtr = new((int)cameraInfoCountOutput);
            var defaultCameraInfo = new XrCameraInfoQCOM(String.Empty, 0, 0);
            for (int i = 0; i < cameraInfoCountOutput; i++)
            {
                cameraInfosPtr.Copy(defaultCameraInfo, i);
            }

            result = _xrEnumerateCamerasQCOM(SessionHandle, cameraInfoCountOutput, ref cameraInfoCountOutput, cameraInfosPtr.Raw);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Enumerate device cameras (2) failed: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            for (int i = 0; i < cameraInfoCountOutput; i++)
            {
                cameraInfos.Add(cameraInfosPtr.AtIndex(i));
            }

            return true;
        }

        private bool TryGetFrameConfigurationsForCamera(string cameraSet, out List<XrCameraFrameConfigurationQCOM> frameConfigurations)
        {
            frameConfigurations = new List<XrCameraFrameConfigurationQCOM>();

            if (_xrGetSupportedFrameConfigurationsQCOM == null)
            {
                Debug.LogError("xrGetSupportedFrameConfigurationsQCOM method not found!");
                return false;
            }

            uint frameConfigurationCountOutput = 0;

            var result = _xrGetSupportedFrameConfigurationsQCOM(SessionHandle, cameraSet, 0, ref frameConfigurationCountOutput, IntPtr.Zero);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to get Supported Frame Configurations (1): " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            using ScopeArrayPtr<XrCameraFrameConfigurationQCOM> frameConfigurationsPtr = new((int)frameConfigurationCountOutput);
            var defaultFrameConfig = new XrCameraFrameConfigurationQCOM(0, String.Empty, new XrExtent2Di(0, 0), 0, 0, 0, 0);
            for (int i = 0; i < frameConfigurationCountOutput; i++)
            {
                frameConfigurationsPtr.Copy(defaultFrameConfig, i);
            }

            result = _xrGetSupportedFrameConfigurationsQCOM(SessionHandle, cameraSet, frameConfigurationCountOutput, ref frameConfigurationCountOutput, frameConfigurationsPtr.Raw);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to get Supported Frame Configurations (2): " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            for (int i = 0; i < frameConfigurationCountOutput; i++)
            {
                frameConfigurations.Add(frameConfigurationsPtr.AtIndex(i));
            }

            return true;
        }

        private bool TryCreateCameraHandle(out ulong cameraHandle, string cameraSet)
        {
            cameraHandle = 0;

            if (_xrCreateCameraHandleQCOM == null)
            {
                Debug.LogError("xrCreateCameraHandleQCOM method not found!");
                return false;
            }

            XrCameraActivationInfoQCOM activationInfo = new XrCameraActivationInfoQCOM(cameraSet);

            var result = _xrCreateCameraHandleQCOM(SessionHandle, ref activationInfo, ref cameraHandle);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to create camera handle: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            return true;
        }

        private bool TryReleaseCameraHandle(ulong cameraHandle)
        {
            if (_xrReleaseCameraHandleQCOM == null)
            {
                Debug.LogError("xrReleaseCameraHandleQCOM method not found!");
                return false;
            }

            var result = _xrReleaseCameraHandleQCOM(cameraHandle);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to release camera handle: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            _cameraHandle = 0;
            return true;
        }

        private bool TryAccessFrame(ulong cameraHandle, XrCameraFrameConfigurationQCOM frameConfig, uint sensorCount)
        {
            if (_xrAccessFrameQCOM == null)
            {
                Debug.LogError("xrAccessFrameQCOM method not found!");
                return false;
            }

            // Create XrCameraSensorInfosQCOM structure
            //IntPtr sensorPropertiesPtr = IntPtr.Zero;
            IntPtr sensorInfosPtr = IntPtr.Zero;
            GCHandle pinnedSensorInfos = new GCHandle();

            using ScopeArrayPtr<XrCameraSensorPropertiesQCOM> sensorPropertiesPtr = new((int)sensorCount);
            for (int i = 0; i < sensorCount; i++)
            {
                sensorPropertiesPtr.Copy(_defaultSensorProperties, i);
            }

            XrCameraSensorInfosQCOM sensorInfos = new XrCameraSensorInfosQCOM(_baseRuntimeFeature.SpaceHandle, sensorCount, sensorPropertiesPtr.Raw);
            pinnedSensorInfos = GCHandle.Alloc(sensorInfos, GCHandleType.Pinned);
            sensorInfosPtr = pinnedSensorInfos.AddrOfPinnedObject();

            // Create XrCameraFrameBuffersQCOM structure
            using ScopeArrayPtr<XrCameraFrameBufferQCOM> fbArrayPtr = new((int)frameConfig.FrameBufferCount);
            using ScopeArrayPtr<XrCameraFrameHardwareBufferQCOM> fhwbArrayPtr = new((int)frameConfig.FrameBufferCount);
            bool hardwareBuffersAvailable = frameConfig.FrameHardwareBufferCount == frameConfig.FrameBufferCount;

            // If HardwareBuffers match FrameBuffers, assign a HardwareBuffer to every FrameBuffer based on the default FrameBuffer
            if (hardwareBuffersAvailable)
            {
                // Create HardwareBuffer array
                for (int i = 0; i < (int)frameConfig.FrameHardwareBufferCount; i++)
                {
                    fhwbArrayPtr.Copy(_defaultFrameHardwareBuffer, i);
                }
                // Create FrameBuffer array
                for (int i = 0; i < (int)frameConfig.FrameBufferCount; i++)
                {
                    XrCameraFrameBufferQCOM frameBuffer = new XrCameraFrameBufferQCOM(_defaultFrameBuffer, fhwbArrayPtr.AtIndexRaw(i));
                    fbArrayPtr.Copy(frameBuffer, i);
                }
            }
            // Otherwise, reuse the default FrameBuffer for all FrameBuffers
            else
            {
                for (int i = 0; i < (int)frameConfig.FrameBufferCount; i++)
                {
                    fbArrayPtr.Copy(_defaultFrameBuffer, i);
                }
            }

            XrCameraFrameBuffersQCOM frameBuffers = new XrCameraFrameBuffersQCOM(
                IntPtr.Zero,
                frameConfig.FrameBufferCount,
                fbArrayPtr.Raw
            );

            // Request data from runtime
            XrCameraFrameDataQCOM frameData = new XrCameraFrameDataQCOM(sensorInfosPtr);
            var result = _xrAccessFrameQCOM(cameraHandle, ref frameData, ref frameBuffers);
            if (result != XrResult.XR_SUCCESS)
            {
                if (result != XrResult.XR_ERROR_CAMERA_FRAME_NOT_READY_QCOMX)
                {
                    Debug.LogError("Failed to access frame: " + Enum.GetName(typeof(XrResult), result));
                }
                pinnedSensorInfos.Free();
                return false;
            }

            // Skip received frame if it is the same as the last one
            if (_cachedFrameData != null && _cachedFrameData.Value.FrameNumber == frameData.FrameNumber)
            {
                TryReleaseFrame(frameData.Handle);
                pinnedSensorInfos.Free();
                return false;
            }

            _cachedFrameData = frameData;

            // Extract sensor data
            _sensorProperties = new XrCameraSensorPropertiesQCOM[sensorCount];
            for (int i = 0; i < sensorCount; i++)
            {
                _sensorProperties[i] = sensorPropertiesPtr.AtIndex(i);
            }
            _lastFramePose = _sensorProperties[0].Extrinsic;
            _cameraInputUpdate.UpdateCameraDevice(_lastFramePose.ToPose());

            pinnedSensorInfos.Free();

            // Extract CPU frame buffers
            _frameBuffers = new XrCameraFrameBufferQCOM[frameConfig.FrameBufferCount];
            for (int i = 0; i < frameConfig.FrameBufferCount; i++)
            {
                _frameBuffers[i] = fbArrayPtr.AtIndex(i);
            }

            // Extract GPU frame buffers
            if (hardwareBuffersAvailable)
            {
                var frameHardwareBuffers = new XrCameraFrameHardwareBufferQCOM[frameConfig.FrameBufferCount];
                for (int i = 0; i < frameConfig.FrameBufferCount; i++)
                {
                    frameHardwareBuffers[i] = Marshal.PtrToStructure<XrCameraFrameHardwareBufferQCOM>(_frameBuffers[i].HardwareBuffer);
                }

                _gpuCameraAccess.RenderHwBuffers(frameHardwareBuffers, (int)_sensorProperties[0].ImageDimensions.Width, (int)_sensorProperties[0].ImageDimensions.Height);
            }

            // Cache YUV frame, 1 per frameBuffer/sensor
            SpacesYUVFrame[] frame = new SpacesYUVFrame[frameBuffers.FrameBufferCount];

            for (int i = 0; i < frameConfig.FrameBufferCount; i++)
            {
                // Abstract planes for later access

                // YCbCr format layout for a 4x4 image:
                //
                // Y-UV variant - Y at 1:1 resolution, UV at 1:2 resolution
                //
                // YYYY    UVUV
                // YYYY    UVUV
                // YYYY
                // YYYY
                //
                // YUYV variant - Y at 1:1 resolution, UV at 1:1 vertically, 1:2 horizontally
                //
                // YUYVYUYV
                // YUYVYUYV
                // YUYVYUYV
                // YUYVYUYV
                //
                // ImagePlane is an abstraction of Y, U or V plane to sample the frameBuffer correctly, given Row and Column values.
                // ImagePlane is defined by: Root, Stride, Offset, Step, ColumnRate and RowRate
                // Root:    Index where plane data begins
                // Stride:  Row size in bytes
                // Offset:  Position of the correct byte inside a Y, UV -- YU, YV or YUYV byte group
                // Step:    Length of the byte group, Y(1), UV(2) -- YU(2), YV(2), YUYV(4)
                // ColumnRate:  Pixels represented by each byte group, horizontally.
                // RowRate:     Pixels represented by each byte group, vertically.
                //
                // For more information: https://en.wikipedia.org/wiki/YCbCr#Packed_pixel_formats_and_conversion

                ImagePlane yPlane = default;
                ImagePlane uPlane = default;
                ImagePlane vPlane = default;

                // NV12 has UV, NV21 has VU byte order
                bool swapuv = frameData.Format == XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV21_QCOMX ^ _deviceIsA3;

                foreach (var plane in _frameBuffers[i].PlanesArray)
                {
                    switch (plane.PlaneType)
                    {
                        case XrCameraFramePlaneTypeQCOM.XR_CAMERA_FRAME_PLANE_TYPE_Y_QCOMX:
                            yPlane = new ImagePlane(plane.Offset, plane.Stride, 0, 1, 1, 1);
                            break;
                        case XrCameraFramePlaneTypeQCOM.XR_CAMERA_FRAME_PLANE_TYPE_UV_QCOMX:
                            uPlane = new ImagePlane(plane.Offset, plane.Stride, (uint)(swapuv ? 1 : 0), 2, 2, 2);
                            vPlane = new ImagePlane(plane.Offset, plane.Stride, (uint)(swapuv ? 0 : 1), 2, 2, 2);
                            break;
                        case XrCameraFramePlaneTypeQCOM.XR_CAMERA_FRAME_PLANE_TYPE_YUV_QCOMX:
                            yPlane = new ImagePlane(plane.Offset, plane.Stride, 0, 2, 1, 1);
                            uPlane = new ImagePlane(plane.Offset, plane.Stride, 1, 4, 2, 1);
                            vPlane = new ImagePlane(plane.Offset, plane.Stride, 3, 4, 2, 1);
                            break;
                    }
                }

                // Build and cache frame
                frame[i] = new SpacesYUVFrame(
                    frameData.Handle,
                    frameData.FrameNumber,
                    frameData.Timestamp,
                    _sensorProperties[i].ImageDimensions.ToVector2Int(),
                    frameData.Format,
                    _frameBuffers[i].Buffer,
                    (int)_frameBuffers[i].BufferSize,
                    (int)_frameBuffers[i].PlaneCount,
                    yPlane,
                    uPlane,
                    vPlane);
            }

            CacheYuvFrame(frame);

            return true;
        }

        private bool TryReleaseFrame(ulong handle)
        {
            if (_xrReleaseFrameQCOM == null)
            {
                Debug.LogError("xrReleaseFrameQCOM method not found!");
                return false;
            }

            var result = _xrReleaseFrameQCOM(handle);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to release frame: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            return true;
        }

        private void ReleaseGPU()
        {
            _gpuCameraAccess?.ReleaseResources();
        }

        private bool SpacesTimespecFromXRTime(long time, out Timespec convertedTime)
        {
            convertedTime = new Timespec();
            if (_xrConvertTimeToTimespecTimeKHR == null)
            {
                Debug.LogError("xrConvertTimeToTimespecTimeKHR method not found!");
                return false;
            }

            var result = _xrConvertTimeToTimespecTimeKHR(_baseRuntimeFeature.InstanceHandle, time, ref convertedTime);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Fail in xrConvertTimeToTimespecTimeKHR: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            return true;
        }

        private Matrix4x4 GetDisplayMatrix()
        {
            // Display matrix format & derivation:
            // Source: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/manual/features/camera/display-matrix-format-and-derivation.html

            // r = [1,0] (Right vector)
            var r = new Vector2(1, 0);
            // u = [0,1] (Up vector)
            var u = new Vector2(0, 1);
            // t = [0,0] (Texture origin)
            var t = new Vector2(0, 0);

            // Note(CH): Vector4s are columns, not rows
            return new Matrix4x4(
                new Vector4(r.x - t.x, t.x - u.x, u.x, 0),
                new Vector4(r.y - t.y, t.y - u.y, u.y, 0),
                new Vector4(0,0,1,0),
                new Vector4(0,0,0,1)
            );
        }

        private Matrix4x4 GetProjectionMatrix(XrPosef extrinsic, XrCameraSensorIntrinsicsQCOM intrinsic)
        {
            // Projection matrix:
            // P = K * Rt * [I|-T]
            // Source: https://dellaert.github.io/19F-4476/proj3.html (Georgia Tech CS 4476 Fall 2019 edition)
            // https://docs.opencv.org/4.x/d0/daf/group__projection.html#ga2daa5a23fd362c8950ff9256c690fed8 (OpenCV projectionFromKRt())
            // Note(CH): Vector4s are columns, not rows

            // K: Calibration matrix (intrinsics)
            // f: Focal length (in pixels)
            // c: Optical center (in pixels)
            Vector2 f = intrinsic.FocalLength.ToVector2();
            Vector2 c = intrinsic.PrincipalPoint.ToVector2();
            Matrix4x4 K = new Matrix4x4(
                new Vector4(f.x, 0, 0, 0),
                new Vector4(0, f.y, 0, 0),
                new Vector4(c.x, c.y, 1, 0),
                new Vector4(0, 0, 0, 0)
            );

            // R: Rotation matrix (extrinsics)
            Matrix4x4 R = Matrix4x4.Rotate(extrinsic.ToPose().rotation);

            // [I|-T]: Identity & -Translation matrix (extrinsics)
            Vector3 t = extrinsic.ToPose().position;
            Matrix4x4 It = new Matrix4x4(
                new Vector4(1, 0, 0, 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(0, 0, 1, 0),
                new Vector4(-t.x, -t.y, -t.z, 1)
            );

            return K * R.transpose * It;
        }

        private Timespec ConvertXrTimeToTimespec(long xrTime)
        {
            Timespec timeInTimespec;
            bool result = SpacesTimespecFromXRTime(xrTime, out timeInTimespec);
            if (!result)
            {
                Debug.LogError("SpacesTimespecFromXRTime failed.");
                return default;
            }
            return timeInTimespec;
        }

        private void CacheYuvFrame(SpacesYUVFrame[] frame)
        {
            // Add frame to cache
            if(!_cachedYuvFrames.TryAdd((int)frame[0].FrameNumber, frame))
            {
                Debug.LogError($"Failed to cache frame: {frame[0].FrameNumber}");
                return;
            }
            _cachedYuvFrameNumbers.Enqueue((int)frame[0].FrameNumber);

            // Remove and release oldest frame from cache if cache size exceeded
            if (_cachedYuvFrameNumbers.Count > CpuFrameCacheSize)
            {
                _cachedYuvFrameNumbers.TryDequeue(out var oldestFrameNumber);
                if(!_cachedYuvFrames.TryRemove(oldestFrameNumber, out SpacesYUVFrame[] oldestFrame))
                {
                    Debug.LogWarning($"Failed to un-cache frame: {oldestFrameNumber}");
                }
                if(!TryReleaseFrame(oldestFrame[0].Handle))
                {
                    Debug.LogWarning($"Failed to release frame from runtime: {oldestFrameNumber}");
                }
            }
        }

        private void FlushFrameCache()
        {
            var frames = _cachedYuvFrames.Values;
            _cachedYuvFrames.Clear();
            _cachedYuvFrameNumbers.Clear();

            foreach (var frame in frames)
            {
                TryReleaseFrame(frame[0].Handle);
            }
        }

        internal bool IsUserFacing()
        {
            if (_sensorProperties == null)
            {
                return false;
            }

            return (_sensorProperties[0].Facing & XrCameraSensorFacingFlagsQCOM.XR_CAMERA_SENSOR_FACING_BACK_BIT_QCOM) != 0;
        }

        private void OnApplicationPauseStateChanged(bool paused)
        {
            if (paused)
            {
                CloseCamera(true);
            }
            else
            {
                OpenCamera(true);
            }
        }
    }
}
