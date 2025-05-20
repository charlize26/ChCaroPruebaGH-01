/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    public class SpacesPermissionHelper : MonoBehaviour
    {
        [Tooltip("OnApplicationPermissionsNotGranted is broadcast on application start if camera permissions are not granted in the application.")]
        public UnityEvent OnApplicationCameraPermissionsNotGranted;

        [Tooltip("OnApplicationPermissionsGranted is broadcast when the application camera permissions are granted.")]
        public UnityEvent OnApplicationCameraPermissionsGranted;

        [Tooltip("OnRuntimeNotInstalled is broadcast on application start if a compatible OpenXR runtime is not installed.")]
        public UnityEvent OnRuntimeNotInstalled;

        private void OnEnable()
        {
#if !UNITY_EDITOR
            DynamicOpenXRLoader.Instance.OnOpenXRAvailable?.AddListener(PerformRuntimeChecks);
#endif
        }

        private void PerformRuntimeChecks()
        {
            // Checks if the runtime has not been installed
            if (!DynamicOpenXRLoader.Instance.IsRuntimeInstalled())
            {
#if !UNITY_EDITOR
                Debug.LogWarning("Attempt to start a Snapdragon Spaces apk but runtime is not installed.");
                OnRuntimeNotInstalled.Invoke();
                return;
#endif
            }

            // Checks if there are features enabled that need application camera permission and if the permissions have not been granted yet.
            if (OpenXRSettings.Instance.GetFeature<BaseRuntimeFeature>().CheckSpacesFeaturesApplicationCameraPermission() && !Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
#if !UNITY_EDITOR
                Debug.LogWarning("Attempt to start the application without runtime camera permissions. Some features might not work without camera permissions.");
                OnApplicationCameraPermissionsNotGranted.Invoke();
#endif
            }
        }

        /// <summary>
        /// Requests application camera permissions
        /// </summary>
        public void RequestApplicationCameraPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                var nativeXRSupportChecker = new AndroidJavaClass("com.qualcomm.snapdragon.spaces.serviceshelper.NativeXRSupportChecker");
                if (nativeXRSupportChecker.CallStatic<bool>("CanShowPermissions") || OpenXRSettings.Instance.GetFeature<FusionFeature>().enabled)
                {
                    Debug.LogWarning("Application has no camera permissions!");
                    var permissionCallbacks = new PermissionCallbacks();
                    Permission.RequestUserPermission(Permission.Camera, permissionCallbacks);
                    permissionCallbacks.PermissionGranted += PermissionsGranted;
                }
            }
#endif
        }

        private void PermissionsGranted(string permissions)
        {
            if (permissions == Permission.Camera)
            {
                OnApplicationCameraPermissionsGranted.Invoke();
            }
        }
    }
}
