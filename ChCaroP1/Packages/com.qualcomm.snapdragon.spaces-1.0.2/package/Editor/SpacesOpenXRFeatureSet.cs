/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

#if QCHT_UNITY_CORE
using QCHT.Interactions.Core;
#endif

using UnityEditor;
using UnityEditor.XR.OpenXR.Features;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    [OpenXRFeatureSet(FeatureIds = new[]
        {
            BaseRuntimeFeature.FeatureID,
            SpatialAnchorsFeature.FeatureID,
            PlaneDetectionFeature.FeatureID,
            ImageTrackingFeature.FeatureID,
            HitTestingFeature.FeatureID,
            FoveatedRenderingFeature.FeatureID,
            SpatialMeshingFeature.FeatureID,
            QrCodeTrackingFeature.FeatureID,
            FusionFeature.FeatureID,
            CameraAccessFeature.FeatureID,
#if QCHT_UNITY_CORE
            HandTrackingFeature.FeatureId,
#if QCHT_UNITY_CORE_4_1_13_OR_NEWER
            HandTrackingMeshMSFTFeature.FeatureId,
            HandTrackingMeshFBFeature.FeatureId,
#endif
#endif
        },
        DefaultFeatureIds = new[]
        {
            BaseRuntimeFeature.FeatureID,
            SpatialAnchorsFeature.FeatureID,
            PlaneDetectionFeature.FeatureID,
            ImageTrackingFeature.FeatureID,
            HitTestingFeature.FeatureID,
            FoveatedRenderingFeature.FeatureID,
            SpatialMeshingFeature.FeatureID,
            QrCodeTrackingFeature.FeatureID,
            FusionFeature.FeatureID,
            CameraAccessFeature.FeatureID,
#if QCHT_UNITY_CORE
            HandTrackingFeature.FeatureId,
#if QCHT_UNITY_CORE_4_1_13_OR_NEWER
            HandTrackingMeshMSFTFeature.FeatureId,
            HandTrackingMeshFBFeature.FeatureId,
#endif
#endif
        },
        UiName = "Snapdragon Spaces",
        Description = "Feature set with all of Snapdragon Spaces' glorious capabilities.",
        FeatureSetId = "com.qualcomm.snapdragon.spaces",
        SupportedBuildTargets = new[]
        {
            BuildTargetGroup.Android
        })]
    internal class SpacesOpenXRFeatureSet
    {
    }

    [OpenXRFeatureSet(FeatureIds = new[]
        {
            OlderRuntimeCompatibilityFeature.FeatureID,
            CompositionLayersFeature.FeatureID,
            PassthroughLayerFeature.FeatureID
        },
        DefaultFeatureIds = new[]
        {
            OlderRuntimeCompatibilityFeature.FeatureID,
            CompositionLayersFeature.FeatureID,
            PassthroughLayerFeature.FeatureID
        },
        RequiredFeatureIds = new[]
        {
            OlderRuntimeCompatibilityFeature.FeatureID
        },
        UiName = "Snapdragon Spaces (Experimental)",
        Description = "Experimental features coming to Snapdragon Spaces.",
        FeatureSetId = "com.qualcomm.snapdragon.spaces.experimental",
        SupportedBuildTargets = new[]
        {
            BuildTargetGroup.Android
        })]
    internal class SpacesOpenXRExperimentalFeatureSet
    {
    }
}
