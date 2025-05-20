/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    // Process each scene to find AR Mesh Manager.
    // Validate the values for the config and the ar mesh manager to ensure that they are set up sensibly for use with spaces mesh provider.
    internal class ARMeshManagerSceneProcessor : OpenXRFeatureBuildHooks, IProcessSceneWithReport
    {
        private bool isSpatialMeshingFeatureEnabled;
        public override Type featureType => typeof(SpatialMeshingFeature);
        public override int callbackOrder => 0;

        void IProcessSceneWithReport.OnProcessScene(Scene scene, BuildReport report)
        {
            var activeLoaders = XRGeneralSettings.Instance?.Manager?.activeLoaders;
            if (activeLoaders?.Any(loader => loader.GetType() == typeof(OpenXRLoader)) != true)
            {
                // No OpenXR Loader enabled. Don't need to process this
                return;
            }

            if (!isSpatialMeshingFeatureEnabled)
            {
                // Not attempting to use the spatial meshing feature so don't need to validate
                return;
            }

            ARMeshManager foundMeshManager = null;
            foreach (var component in UnityEngine.Object.FindObjectsByType(typeof(ARMeshManager), FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var monoBehaviour = component as MonoBehaviour;
                if (monoBehaviour != null)
                {
                    foundMeshManager = component as ARMeshManager;
                    break;
                }
            }

            if (foundMeshManager != null)
            {
                if (foundMeshManager.tangents)
                {
                    Debug.LogWarning("Configuration value from AR Mesh Manager: Tangents not currently supported by Snapdragon Spaces Meshing Provider.");
                }

                if (foundMeshManager.textureCoordinates)
                {
                    Debug.LogWarning("Configuration value from AR Mesh Manager: Texture Coordinates not currently supported by Snapdragon Spaces Meshing Provider.");
                }

                if (foundMeshManager.colors)
                {
                    Debug.LogWarning("Configuration value from AR Mesh Manager: Colors not currently supported by Snapdragon Spaces Meshing Provider.");
                }

                if (foundMeshManager.density != 0.5f)
                {
                    Debug.LogWarning("Configuration value from AR Mesh Manager: Density is not currently supported by Snapdragon Spaces Meshing Provider.");
                }

                if (foundMeshManager.concurrentQueueSize != 4)
                {
                    Debug.LogWarning("Configuration value from AR Mesh Manager: Concurrent Queue Size is not currently supported by Snapdragon Spaces Meshing Provider. Only returns 1 mesh.");
                }
            }
        }

        protected override void OnPreprocessBuildExt(BuildReport report)
        {
            isSpatialMeshingFeatureEnabled = true;
        }

        protected override void OnPostGenerateGradleAndroidProjectExt(string path)
        {
        }

        protected override void OnPostprocessBuildExt(BuildReport report)
        {
        }
    }
}
