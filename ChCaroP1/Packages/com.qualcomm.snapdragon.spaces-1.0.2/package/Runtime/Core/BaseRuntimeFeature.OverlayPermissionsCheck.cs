/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

#if !UNITY_EDITOR
using UnityEngine;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
        public bool CheckServicesOverlayPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            var runtimeChecker = new AndroidJavaClass("com.qualcomm.snapdragon.spaces.serviceshelper.RuntimeChecker");

            if (runtimeChecker.CallStatic<bool>("CheckOverlayPermissions", new object[] { activity }))
            {
                return true;
            }

            Debug.LogError("The OpenXR runtime has not been granted the permission to 'Display over other apps'.");
#endif
            return false;
        }

    	public void RequestServicesOverlayPermissions(SimpleDialogOptions options)
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

            if (!runtimeChecker.CallStatic<bool>("RequestOverlayPermissions", new object[] { activity, options != null ? javaOptions : null }))
			{
				Debug.LogWarning("The request to allow the OpenXR runtime to 'Display over other apps' failed.");
			}
#endif
        }
	}
}
