/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    /// An extension for <see cref="UnityEngine.XR.ARFoundation.ARCameraManager"/>. Provides additional control over the camera configuration.
    /// </summary>

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARCameraManager))]
    public class SpacesARCameraManagerConfig : MonoBehaviour
    {
        /// <summary>
        /// Get the number of active device cameras associated with the current <see cref="UnityEngine.XR.ARSubsystems.XRCameraConfiguration"/>.
        /// </summary>
        public uint ActiveCameraCount
        {
            get
            {
                if (_cameraAccess == null)
                {
                    Debug.LogWarning("Failed to retrieve active camera count: Could not get valid camera access feature");
                    return 0;
                }
                return _cameraAccess.IsCameraOpen() ? (uint) _cameraAccess.SensorProperties.Length : 0;
            }
        }

        /// <summary>
        /// Get or set whether to use Direct Memory Access during <see cref="UnityEngine.XR.ARSubsystems.XRCpuImage"/> conversion.
        /// <b><i>May heavily impact performance on some architectures, use at your own risk</i></b>.
        /// Can also be set in the Snapdragon Spaces OpenXR project settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no Camera Access OpenXR feature can be found.
        /// </exception>
        public bool DirectMemoryAccess
        {
            get
            {
                if (_cameraAccess == null)
                {
                    throw new InvalidOperationException("Could not get valid camera access feature");
                }
                return _cameraAccess.DirectMemoryAccessConversion;
            }

            set
            {
                if (_cameraAccess == null)
                {
                    throw new InvalidOperationException("Could not get valid camera access feature");
                }
                _cameraAccess.DirectMemoryAccessConversion = value;
            }
        }

        /// <summary>
        /// Get or set whether the async conversion thread can be treated as high-priority by the OpenXR runtime.
        /// Can also be set in the Snapdragon Spaces OpenXR project settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no Camera Access OpenXR feature can be found.
        /// </exception>
        public bool HighPriorityAsyncConversion
        {
            get
            {
                if (_cameraAccess == null)
                {
                    throw new InvalidOperationException("Could not get valid camera access feature");
                }
                return _cameraAccess.HighPriorityAsyncConversion;
            }

            set
            {
                if (_cameraAccess == null)
                {
                    throw new InvalidOperationException("Could not get valid camera access feature");
                }
                _cameraAccess.HighPriorityAsyncConversion = value;
            }
        }

        /// <summary>
        /// Get or set whether async conversion requests will cache frame data upon request, thus guaranteeing conversion without relying on the CPU frame cache.
        /// Comes at a slight performance and memory cost. <b>May significantly increase the memory footprint of the application over time</b>.
        /// This setting will be ignored if <see cref="DirectMemoryAccess"/> is enabled.
        /// Can also be set in the Snapdragon Spaces OpenXR project settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no Camera Access OpenXR feature can be found.
        /// </exception>
        public bool CacheFrameBeforeAsyncConversion
        {
            get
            {
                if (_cameraAccess == null)
                {
                    throw new InvalidOperationException("Could not get valid camera access feature");
                }
                return _cameraAccess.CacheFrameBeforeAsyncConversion;
            }

            set
            {
                if (_cameraAccess == null)
                {
                    throw new InvalidOperationException("Could not get valid camera access feature");
                }
                _cameraAccess.DirectMemoryAccessConversion = value;
            }
        }

        private CameraAccessFeature _cameraAccess;

        void Start()
        {
            _cameraAccess = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();
            if (!FeatureUseCheckUtility.IsFeatureUseable(_cameraAccess))
            {
#if !UNITY_EDITOR
                Debug.LogError("Could not get valid camera access feature");
#endif
            }
        }
    }
}
