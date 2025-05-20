/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Runtime.InteropServices;
using UnityEngine.XR.OpenXR.NativeTypes;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
        [SerializeField, Tooltip("Triggered when the passthrough mode changes.")]
        public delegate void OnPassthroughChangedEvent(XrEnvironmentBlendMode blendMode);

        public OnPassthroughChangedEvent OnPassthroughChangedEventDelegate;

        public void SetPassthroughEnabled(bool enable)
        {
            if (!IsPassthroughSupported())
            {
#if !UNITY_EDITOR
                Debug.LogWarning("This device does not support Passthrough.");
#endif
                return;
            }

            var originTransform = OriginLocationUtility.GetOriginTransform(true);
            if (enable && originTransform != null)
            {
                var originCameras = originTransform.GetComponentsInChildren<Camera>(true);
                foreach (var camera in originCameras)
                {
                    if (camera != null && camera.backgroundColor.a > 0.0f)
                    {
                        Debug.LogWarning("Passthrough will be obstructed by the session origin's camera '" + camera.name + "'. Consider changing the background alpha channel from '" + camera.backgroundColor.a.ToString("F1") + "' to '0.0'");
                    }
                }
            }

            XrEnvironmentBlendMode blendMode = enable ? XrEnvironmentBlendMode.AlphaBlend : XrEnvironmentBlendMode.Opaque;

            SetEnvironmentBlendMode(blendMode);

            OnEnvironmentBlendModeChange(blendMode);
        }

        public bool GetPassthroughEnabled()
        {
#if !UNITY_EDITOR
            return GetEnvironmentBlendMode() == XrEnvironmentBlendMode.AlphaBlend;
#else
            return false;
#endif
        }

        public bool IsPassthroughSupported()
        {
#if !UNITY_EDITOR
            return IsPassthroughSupported_Native();
#else
            return false;
#endif
        }

        protected override void OnEnvironmentBlendModeChange(XrEnvironmentBlendMode xrEnvironmentBlendMode)
        {
            if (OnPassthroughChangedEventDelegate != null)
            {
                OnPassthroughChangedEventDelegate(xrEnvironmentBlendMode);
            }
        }

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "IsPassthroughSupported")]
        private static extern bool IsPassthroughSupported_Native();
    }
}
