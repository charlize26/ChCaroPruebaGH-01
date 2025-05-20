/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
    public class CameraSubsystem : XRCameraSubsystem
    {
        public const string ID = "Spaces-CameraSubsystem";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDescriptor()
        {
#if AR_FOUNDATION_6_0_OR_NEWER
            XRCameraSubsystemDescriptor.Cinfo _cinfo = new XRCameraSubsystemDescriptor.Cinfo
#else
            XRCameraSubsystemCinfo _cinfo = new XRCameraSubsystemCinfo
#endif
            {
                id = ID,
                providerType = typeof(CameraProvider),
                subsystemTypeOverride = typeof(CameraSubsystem),
                supportsAverageBrightness = false,
                supportsAverageColorTemperature = false,
                supportsColorCorrection = false,
                supportsDisplayMatrix = true,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraConfigurations = true,
                supportsCameraImage = true,
                supportsAverageIntensityInLumens = false,
                supportsFaceTrackingAmbientIntensityLightEstimation = false,
                supportsFaceTrackingHDRLightEstimation = false,
                supportsWorldTrackingAmbientIntensityLightEstimation = false,
                supportsWorldTrackingHDRLightEstimation = false,
                supportsFocusModes = false,
                supportsCameraGrain = false
            };
#if AR_FOUNDATION_6_0_OR_NEWER
            XRCameraSubsystemDescriptor.Register(_cinfo);
#else
            Register(_cinfo);
#endif
        }

        internal class CameraProvider : Provider
        {
            private XRCpuImage.Api _cpuImageApi;
            private Feature _currentCamera;
            private XRCameraConfiguration? _currentConfiguration;
            private List<XRCameraConfiguration> _cameraConfigurations;
            private XRCameraFrame _lastFrame;
            private CameraAccessFeature _underlyingFeature;

            public override XRCpuImage.Api cpuImageApi => _cpuImageApi;

            public override bool permissionGranted => _underlyingFeature?.AreApplicationCameraPermissionsGranted ?? false;

            public override Feature currentCamera => _currentCamera;

            public override XRCameraConfiguration? currentConfiguration
            {
                get => _currentConfiguration;
                set => SetCameraConfiguration(value);
            }

            public override void Start()
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!permissionGranted)
                {
                    var nativeXRSupportChecker = new AndroidJavaClass("com.qualcomm.snapdragon.spaces.serviceshelper.NativeXRSupportChecker");

                    if (nativeXRSupportChecker.CallStatic<bool>("CanShowPermissions"))
                    {
                        Permission.RequestUserPermission(Permission.Camera);
                    }
                }
#endif
                _underlyingFeature = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();

                if (!FeatureUseCheckUtility.IsFeatureUseable(_underlyingFeature))
                {
#if !UNITY_EDITOR
                    Debug.LogError("Spaces CameraAccessFeature is missing or not enabled.");
#endif
                    Destroy();
                    return;
                }

                var arCameraBackgrounds = GameObject.FindObjectsByType<ARCameraBackground>(FindObjectsSortMode.None).Where(a=>a.enabled);
                foreach (var arCameraBackground in arCameraBackgrounds )
                {
                    arCameraBackground.enabled = false;
                    Debug.LogWarning("Disabling ARCameraBackground component. Snapdragon Spaces Camera Frame Access does not support ARCameraBackground.");
                }

                _cpuImageApi = SpacesCpuImageApi.CreateInstance();
                _currentCamera = Feature.WorldFacingCamera;
                _currentConfiguration = null;

                InitialiseCamera();
            }

            public override void Stop()
            {
                if (_underlyingFeature == null)
                {
                    return;
                }
                _underlyingFeature.ReleaseResources();
                SpacesCpuImageApi.instance.MarkPendingRequestsForDisposal();
            }

            public override NativeArray<XRCameraConfiguration> GetConfigurations(XRCameraConfiguration defaultCameraConfiguration, Allocator allocator)
            {
                // In this case, a null configuration should still return a NativeArray<..> of length == 0
                if ((_cameraConfigurations is null ||_cameraConfigurations.Count == 0) && !_underlyingFeature.TryGetSupportedConfigurations(out _cameraConfigurations))
                {
                    return new NativeArray<XRCameraConfiguration>(0, allocator);
                }

                // Place the suggested configuration at the start of the list, so developers can always find it
                XRCameraConfiguration? suggested = _underlyingFeature.SuggestCameraConfiguration(_cameraConfigurations);
                if (_cameraConfigurations.Remove(suggested!.Value))
                {
                    _cameraConfigurations.Insert(0, suggested!.Value);
                }

                NativeArray<XRCameraConfiguration> configurations = new NativeArray<XRCameraConfiguration>(_cameraConfigurations.Count, allocator);
                configurations.CopyFrom(_cameraConfigurations.ToArray());
                return configurations;
            }

            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
            {
                return _underlyingFeature.TryGetIntrinsics(out cameraIntrinsics);
            }

            public override bool TryAcquireLatestCpuImage(out XRCpuImage.Cinfo cameraImageCinfo)
            {
                return _underlyingFeature.TryGetLatestCpuImage(out cameraImageCinfo);
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                cameraFrame = default;

                if (!_underlyingFeature.IsCameraOpen() && !InitialiseCamera())
                {
                    Debug.LogError("Failed to get frame: Could not initialize camera.");
                    return false;
                }
                return _underlyingFeature.TryGetNewFrame(out cameraFrame);
            }

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator)
            {
                List<XRTextureDescriptor> textureDescriptors = _underlyingFeature.GetGpuTextureDescriptors();
                if (textureDescriptors.Count == 0)
                {
                    return new NativeArray<XRTextureDescriptor>(0, allocator);
                }
                return new NativeArray<XRTextureDescriptor>(textureDescriptors.ToArray(), allocator);
            }

            private bool InitialiseCamera()
            {
                if (!_underlyingFeature.TryGetSupportedConfigurations(out _cameraConfigurations))
                {
                    Debug.LogError("Failed to get supported camera configurations.");
                    Destroy();
                    return false;
                }
                if (_cameraConfigurations is null || _cameraConfigurations.Count == 0)
                {
                    Debug.LogError("No supported camera configuration found.");
                    Destroy();
                    return false;
                }

                XRCameraConfiguration? configuration = _underlyingFeature.SuggestCameraConfiguration(_cameraConfigurations);
                try
                {
                    currentConfiguration = configuration ?? _cameraConfigurations[0];
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to initialise camera: {e.Message}");
                    Destroy();
                    return false;
                }

                _currentCamera = GetCurrentFeatureFlags();

                return true;
            }

            private void SetCameraConfiguration(XRCameraConfiguration? configuration)
            {
                if (configuration == null)
                {
                    throw new ArgumentNullException();
                }

                if (!_underlyingFeature.IsSupportedCameraConfiguration(configuration.Value))
                {
                    throw new ArgumentException("The provided XRCameraConfiguration does not match any supported camera configurations.");
                }

                if (!_underlyingFeature.SetCameraConfiguration(configuration.Value))
                {
                    throw new InvalidOperationException($"Failed to set XRCameraConfiguration: {configuration}");
                }

                _currentConfiguration = configuration;
            }

            private Feature GetCurrentFeatureFlags()
            {
                bool userFacing = _underlyingFeature.IsUserFacing();

                Feature cameraFeatureFlags =
                        (userFacing ? Feature.UserFacingCamera : Feature.WorldFacingCamera) |
                        Feature.PositionAndRotation
                    ;

                return cameraFeatureFlags;
            }
        }
    }
}
