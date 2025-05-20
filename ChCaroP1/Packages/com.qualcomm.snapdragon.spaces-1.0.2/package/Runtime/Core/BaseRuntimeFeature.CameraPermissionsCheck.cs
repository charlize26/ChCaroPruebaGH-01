/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
        enum PermissionsRequestType
        {
            ApplicationCameraPermissions = 0,
            RuntimeCameraPermissions = 1
        }

        /// <summary>
        /// Returns if the camera permissions on the runtime have been granted
        /// </summary>
        /// <returns>True if camera permissions have been granted, false otherwise.</returns>
        public bool CheckServicesCameraPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            var runtimeChecker = new AndroidJavaClass("com.qualcomm.snapdragon.spaces.serviceshelper.RuntimeChecker");

            if (runtimeChecker.CallStatic<bool>("CheckCameraPermissions", new object[] { activity }))
            {
                return true;
            }
#endif
#if !UNITY_EDITOR
            Debug.LogWarning("The OpenXR runtime has no camera permissions!");
            return false;
#else
            return true;
#endif
        }

        /// <summary>
        /// Request runtime camera permissions
        /// </summary>
        /// <param name="options">An optional parameter defining localized strings to be displayed as part of the request for permissions.</param>
        public void RequestCameraPermissions(SimpleDialogOptions options = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            var runtimeChecker = new AndroidJavaClass("com.qualcomm.snapdragon.spaces.serviceshelper.RuntimeChecker");

            var javaOptions = new AndroidJavaObject("com.qualcomm.snapdragon.spaces.serviceshelper.SimpleDialogOptions");
            if (options != null)
			{
                javaOptions.Set("Title", options.Title);
                javaOptions.Set("Message", options.Message);
                javaOptions.Set("PositiveButtonText", options.PositiveButtonText);
                javaOptions.Set("NegativeButtonText", options.NegativeButtonText);
            }

            if (!runtimeChecker.CallStatic<bool>("RequestCameraPermissions", new object[] { activity, options != null ? javaOptions : null }))
            {
                Debug.LogWarning("The request to allow camera permissions on the OpenXR runtime failed.");
            }
#endif
        }

        /// <summary>
        /// Checks if there are enabled features that require runtime camera permissions
        /// </summary>
        /// <returns>True if camera permissions are required by any enabled features, false otherwise.</returns>
        public bool CheckSpacesFeaturesRuntimeCameraPermission()
        {
            return CheckPermissionsType(PermissionsRequestType.RuntimeCameraPermissions);
        }

        /// <summary>
        /// Checks if there are enabled features that require application camera permissions
        /// </summary>
        /// <returns>True if camera permissions are required by any enabled features, false otherwise.</returns>
        public bool CheckSpacesFeaturesApplicationCameraPermission()
        {
            return CheckPermissionsType(PermissionsRequestType.ApplicationCameraPermissions);
        }

        private bool CheckPermissionsType(PermissionsRequestType permissionsRequestType)
        {
            var spacesFeaturesList = new List<SpacesOpenXRFeature>();
            OpenXRSettings.Instance.GetFeatures(spacesFeaturesList);
            switch (permissionsRequestType)
            {
                case PermissionsRequestType.ApplicationCameraPermissions:
                    return spacesFeaturesList!.Any(feature =>feature == feature.enabled && feature == feature.RequiresApplicationCameraPermissions);
                case PermissionsRequestType.RuntimeCameraPermissions:
                    return spacesFeaturesList.Any(feature => feature == feature.enabled && feature == feature.RequiresRuntimeCameraPermissions);
            }

            return true;
        }
    }
}
