/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = FeatureName,
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "Qualcomm",
        Desc = "Enables full simultaneous use of the mobile touchscreen and AR glasses on supported Snapdragon Spaces Development Kits",
        DocumentationLink = "",
        OpenxrExtensionStrings = FeatureExtensions,
        Version = "1.0.2",
        Required = true,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID)]
#endif
    internal partial class FusionFeature : SpacesOpenXRFeature
    {
        public const string FeatureName = "Dual Render Fusion";
        public const string FeatureID = "com.qualcomm.snapdragon.spaces.fusion";
        public const string FeatureExtensions = "XR_UNITY_android_present";


        [Tooltip("If enabled, runs validation checks on the open Scene for required components to enable dual-rendering capabilities (recommended for setting up a Scene with dual render capabilities.)\n\nIf disabled, no validation checks will be run on the open Scene (recommended to prevent build errors if the open Scene does not need to be equipped with dual render capabilities.")]
        public bool ValidateOpenScene = true;

        [Tooltip("If enabled, no validation check will be run on the OpenXR plugin, allowing the use of versions 1.11.0 and higher.\n\nIf disabled, runs the validation check on the OpenXR plugin, preventing the use of versions 1.11.0 and higher (highly recommended to avoid rendering issues.)\n\nSnapdragon Spaces officially supports version 1.10.0.")]
        public bool IgnoreOpenXRVersion = false;

        protected override void OnEnable()
        {
            base.OnEnable();

#if UNITY_ANDROID && !UNITY_EDITOR
            FeatureUseCheckUtility.ImposeFeatureChecks_OpenXrNotRunning += FeatureUseCheckUtilityFusion.FusionChecksForOpenXRNotRunning;
#endif
        }
    }
}
