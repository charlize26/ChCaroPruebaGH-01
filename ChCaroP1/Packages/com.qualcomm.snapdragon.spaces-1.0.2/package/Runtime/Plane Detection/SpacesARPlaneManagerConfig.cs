/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    /// An extension for <see cref="ARPlaneManager"/>. Manages and provides additional functionalities and features for plane detection.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARPlaneManager))]
    public class SpacesARPlaneManagerConfig : MonoBehaviour
    {
        public delegate void OnSpacesPlaneManagerConfigStartDelegate();

        [SerializeField] private bool _convexHullEnabled = true;

        // TODO: (LE) This should be serialized when the OpenXR runtime reports non-orthogonal planes.
        //[SerializeField]
        [Tooltip("The AR Plane Manager has it's Plane Detection Mode set to 'Everything' when selecting both Horizontal and Vertical detection modes.\n" +
            "This precludes detecting Horizontal and Vertical planes without also detecting non-orthogonally aligned planes.\n" +
            "When Restrict To Aligned is true, and the Plane Detection Mode is 'Everything', only Horizontal and Vertical planes will be detected.\n" +
            "When this is false (by default), all planes will be detected regardless of alignment when Plane Detection Mode is 'Everything'.\n\n" +
            "Note that detecting axis-aligned planes is much more likely in practice.")]
        private bool _restrictToAligned;

        private PlaneDetectionFeature _planeDetection;
        private ARPlaneManager _planeManager;

        /// <summary>
        /// A callback to run on start when a valid <see cref="PlaneDetectionFeature"/> feature is available.
        /// </summary>
        public OnSpacesPlaneManagerConfigStartDelegate OnSpacesPlaneManagerConfigStart;

        /// <summary>
        /// Detected planes generate more complex shapes. When disabled, it will generate planes based on the extents of the detected planes instead.
        /// </summary>
        public bool ConvexHullEnabled
        {
            get => _planeDetection?.ConvexHullEnabled ?? false;
            set
            {
                if (_planeDetection != null && _planeDetection.ConvexHullEnabled != value)
                {
                    _planeDetection.ConvexHullEnabled = _convexHullEnabled = value;
                }
            }
        }

        //TODO: (LE) This should be made public when the OpenXR runtime reports non-orthogonal planes.
        private bool RestrictPlaneDetectionModeToAlignedPlanes
        {
            get => _planeDetection?.RestrictToAligned ?? false;
            set
            {
                if (_planeDetection != null && _planeDetection.RestrictToAligned != value)
                {
                    _planeDetection.RestrictToAligned = _restrictToAligned = value;
                    _planeDetection.SetPlaneFilters(_planeManager.currentDetectionMode);
                }
            }
        }

        private void Awake()
        {
            _planeManager = GetComponent<ARPlaneManager>();
        }

        private void Start()
        {
            _planeDetection = OpenXRSettings.Instance.GetFeature<PlaneDetectionFeature>();
            if (!FeatureUseCheckUtility.IsFeatureUseable(_planeDetection))
            {
#if !UNITY_EDITOR
                Debug.LogError("Could not get valid plane detection feature");
#endif
                return;
            }

            ConvexHullEnabled = _convexHullEnabled;
            RestrictPlaneDetectionModeToAlignedPlanes = _restrictToAligned;
            OnSpacesPlaneManagerConfigStart.Invoke();
        }
    }
}
