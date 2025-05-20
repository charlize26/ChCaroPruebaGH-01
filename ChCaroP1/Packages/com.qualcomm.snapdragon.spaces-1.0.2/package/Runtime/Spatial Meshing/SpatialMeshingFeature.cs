/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
#if UNITY_EDITOR
    [OpenXRFeature(UiName = FeatureName,
        BuildTargetGroups = new[]
        {
            BuildTargetGroup.Android
        },
        Company = "Qualcomm",
        Desc = "Enables Spatial Meshing feature on Snapdragon Spaces enabled devices",
        DocumentationLink = "",
        OpenxrExtensionStrings = FeatureExtensions,
        Version = "1.0.2",
        Required = false,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID)]
#endif
    internal sealed partial class SpatialMeshingFeature : SpacesOpenXRFeature
    {
        public const string FeatureName = "Spatial Meshing";
        public const string FeatureID = "com.qualcomm.snapdragon.spaces.sceneunderstanding";
        public const string FeatureExtensions = "XR_MSFT_scene_understanding";
        private static readonly List<XRMeshSubsystemDescriptor> _meshSubsystemDescriptors = new();
        protected override bool IsRequiringBaseRuntimeFeature => true;
        internal override bool RequiresRuntimeCameraPermissions => true;

        private static readonly FieldInfo _meshManageSubsystemField = typeof(ARMeshManager).GetField("m_Subsystem", BindingFlags.NonPublic | BindingFlags.Instance);

        protected override string GetXrLayersToLoad()
        {
            return "XR_APILAYER_QCOM_scene_understanding";
        }

        protected override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            RequestLayers(GetXrLayersToLoad());
            return Internal_GetInterceptedInstanceProcAddr(func);
        }

        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            base.OnInstanceCreate(instanceHandle);
            Internal_RegisterMeshingLifecycleProvider();
            Internal_SetInstanceHandle(instanceHandle);
            var missingExtensions = GetMissingExtensions(FeatureExtensions);
            if (missingExtensions.Any())
            {
                Debug.Log(FeatureName + " is missing following extension in the runtime: " + String.Join(",", missingExtensions));
                return false;
            }

            return true;
        }

        protected override void OnSubsystemCreate()
        {
            CreateSubsystem<XRMeshSubsystemDescriptor, XRMeshSubsystem>(_meshSubsystemDescriptors, "Spaces-MeshSubsystem");
        }

        protected override void OnSubsystemStop()
        {
            StopSubsystem<XRMeshSubsystem>();
        }

        protected override void OnSubsystemDestroy()
        {
            DestroySubsystem<XRMeshSubsystem>();

            if (_meshManageSubsystemField != null)
            {
                var meshManager = FindFirstObjectByType<ARMeshManager>(FindObjectsInactive.Include);
                if (meshManager != null)
                {
                    _meshManageSubsystemField.SetValue(meshManager, null);
                }
            }
        }

        protected override void OnSessionCreate(ulong sessionHandle)
        {
            base.OnSessionCreate(sessionHandle);
            Internal_SetSessionHandle(sessionHandle);
        }

        protected override void OnAppSpaceChange(ulong spaceHandle)
        {
            base.OnAppSpaceChange(spaceHandle);
            Internal_SetSpaceHandle(spaceHandle);
        }
    }
}
