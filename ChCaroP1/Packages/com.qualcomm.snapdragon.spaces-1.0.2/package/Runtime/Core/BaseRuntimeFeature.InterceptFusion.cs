/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Runtime.InteropServices;
using UnityEngine.XR.OpenXR;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetFusionSupported")]
        private static extern void SetFusionSupported_Internal(bool enable);

        public bool IsFusionSupported()
        {
#if UNITY_EDITOR
            if (OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<FusionFeature>()?.enabled ?? false)
            {
                return true;
            }
#else
            if (OpenXRSettings.ActiveBuildTargetInstance.GetFeature<FusionFeature>()?.enabled ?? false)
            {
#pragma warning disable CS0162
                return DeviceAccessHelper.GetDeviceType() != DeviceTypes.Aio;
#pragma warning restore CS0162
            }
#endif
            return false;
        }
    }
}
