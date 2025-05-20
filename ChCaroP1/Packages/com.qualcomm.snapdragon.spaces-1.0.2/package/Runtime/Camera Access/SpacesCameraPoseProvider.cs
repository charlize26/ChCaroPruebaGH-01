/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;
using UnityEngine.Experimental.XR.Interaction;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    /// A provider for <see cref="UnityEngine.SpatialTracking.TrackedPoseDriver"/>. Can be manually queried for on-demand poses.
    /// </summary>
    public class SpacesCameraPoseProvider : BasePoseProvider
    {
        private CameraAccessFeature _cameraAccess;

        private void Start()
        {
            _cameraAccess = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();
            if (!FeatureUseCheckUtility.IsFeatureUseable(_cameraAccess))
            {
#if !UNITY_EDITOR
                Debug.LogError("Could not get valid camera access feature");
#endif
                return;
            }
        }

        /// <summary>
        /// Get the <see cref="UnityEngine.Pose"/> value associated with the <see cref="UnityEngine.XR.ARFoundation.ARCameraManager"/>'s latest <c>frameReceived</c> event.
        /// </summary>
        /// <param name="output">When this method returns, contains the Pose data from the Pose Provider.</param>
        /// <returns>Bitflags indicating whether position and/or rotation was set on the Pose struct returned with <paramref name="output"/>.</returns>
        public override PoseDataFlags GetPoseFromProvider(out Pose output)
        {
            output = default;
            if (!FeatureUseCheckUtility.IsFeatureUseable(_cameraAccess))
            {
                return PoseDataFlags.NoData;
            }
            output = _cameraAccess.LastFramePose;
            return PoseDataFlags.Position | PoseDataFlags.Rotation;
        }
    }
}
