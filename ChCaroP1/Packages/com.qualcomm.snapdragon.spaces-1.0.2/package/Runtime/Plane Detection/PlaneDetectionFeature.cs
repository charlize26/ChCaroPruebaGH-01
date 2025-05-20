/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
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
        Desc = "Enables Plane Detection feature on Snapdragon Spaces enabled devices",
        DocumentationLink = "",
        OpenxrExtensionStrings = XR_MSFT_FeatureExtensions,
        Version = "1.0.2",
        Required = false,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID)]
#endif
    internal sealed partial class PlaneDetectionFeature : SpacesOpenXRFeature
    {
        public const string FeatureName = "Plane Detection";
        public const string FeatureID = "com.qualcomm.snapdragon.spaces.planedetection";
        public const string XR_MSFT_FeatureExtensions = "XR_MSFT_scene_understanding";
        public bool ConvexHullEnabled = true;
        public bool RestrictToAligned;
        private static readonly List<XRPlaneSubsystemDescriptor> _planeSubsystemDescriptors = new();

        private static readonly Dictionary<ulong, PlaneDataCollection> _planesDataMap = new();

        private BaseRuntimeFeature _baseRuntimeFeature;
        private ulong _activePlaneDetectionHandle;
        private List<string> _subscribedSubsystems;
        public bool IsRunning => _activePlaneDetectionHandle != 0;
        public ulong ActiveHandle => _activePlaneDetectionHandle;
        protected override bool IsRequiringBaseRuntimeFeature => true;
        internal override bool RequiresRuntimeCameraPermissions => true;

        public void RegisterProviderWithSceneObserver(string subsystemId)
        {
            Internal_RegisterProviderWithSceneObserver(subsystemId,
                (int)(SpacesSceneComputeFeature.PLANE |
                    SpacesSceneComputeFeature.PLANE_MESH));
        }

        public void UnregisterProviderWithSceneObserver(string subsystemId)
        {
            Internal_UnregisterProviderWithSceneObserver(subsystemId);
        }

        public bool TryDestroyPlaneDetection(string subsystemID)
        {
            _subscribedSubsystems.Remove(subsystemID);
            if (_subscribedSubsystems.Count > 1)
            {
                Debug.LogWarning("Plane Detection is still needed and won't be destroyed!");
                return false;
            }

            return true;
        }

        public bool TryLocatePlanes(out List<Plane> updatedPlanes)
        {
            updatedPlanes = new List<Plane>();
            if (!Internal_UpdateObservedScene(PlaneDetectionSubsystem.ID))
            {
                Debug.LogError("Failed to update observed scene!");
                return false;
            }

            if (!Internal_UpdatePlanes())
            {
                // This failure occurs frequently, and has sufficient diagnostic logging from 3dr when it's a serious problem.
                // We will refrain from logging it.
                return false;
            }

            uint scenePlaneCount = 0;
            if (!Internal_CountScenePlanes(ref scenePlaneCount))
            {
                Debug.LogError("Failed to count planes!");
                return false;
            }

            if (scenePlaneCount == 0)
            {
                return true;
            }

            using ScopeArrayPtr<SceneUnderstandingMSFTPlane> scenePlanesPtr = new((int)scenePlaneCount);
            if (!Internal_FetchScenePlanes(scenePlaneCount, scenePlanesPtr.Raw))
            {
                Debug.LogError("Failed to fetch planes from the scene!");
                return false;
            }

            Quaternion openXrCorrection = Quaternion.AngleAxis(-90.0f, Vector3.right);
            for (int planeIx = 0; planeIx < (int)scenePlaneCount; ++planeIx)
            {
                IntPtr planePtr = scenePlanesPtr.AtIndexRaw(planeIx);
                SceneUnderstandingMSFTPlane plane = scenePlanesPtr.AtIndex(planeIx);

                using ScopeArrayPtr<XrVector3f> verticesPtr = new((int)plane.VertexCount);
                using ScopeArrayPtr<uint> indicesPtr = new((int)plane.IndexCount);

                if (!Internal_FetchPlaneVertices(planePtr, verticesPtr.Raw, indicesPtr.Raw))
                {
                    Debug.LogError($"Failed to fetch vertices from plane {planeIx}");
                    continue;
                }

                List<Vector3> vertexList = new List<Vector3>();
                for (int vertexIx = 0; vertexIx < plane.VertexCount; ++vertexIx)
                {
                    Vector3 vertex = verticesPtr.AtIndex(vertexIx).ToVector3();
                    vertexList.Add(vertex);
                }

                List<uint> indexList = new List<uint>();
                for (int indexIx = 0; indexIx < plane.IndexCount; ++indexIx)
                {
                    uint index = indicesPtr.AtIndex(indexIx);
                    indexList.Add(index);
                }

                Pose replacementPose = plane.Pose;
                replacementPose.rotation *= openXrCorrection;
                BoundedPlane boundedPlane = plane.GetBoundedPlane(replacementPose);
                updatedPlanes.Add(new Plane(boundedPlane, boundedPlane.trackableId.subId2));
                PlaneDataCollection planeData = new PlaneDataCollection();
                planeData.vertices = vertexList;
                planeData.indices = indexList;
                planeData.extents = boundedPlane.extents;
                if (!ConvexHullEnabled)
                {
                    if (vertexList.Count >= 3 && indexList.Count >= 3)
                    {
                        // Fetch 3 vertices.
                        var v1 = vertexList[(int)indexList[0]];
                        // Next two indices flipped because of winding order changes
                        var v2 = vertexList[(int)indexList[2]];
                        var v3 = vertexList[(int)indexList[1]];

                        // Calculate the determinant of any 3 vertices in the vertex list
                        // to work out winding order for extents planes.
                        // This is necessary to draw extents planes on ceilings oriented correctly.
                        // Otherwise extents plane can point in opposite direction to convex hull plane
                        planeData.reverseExtentPlaneWindingOrder = (v3.x * v2.y) + (v1.x * v3.y) + (v1.y * v2.x) - ((v1.y * v3.x) + (v3.y * v2.x) + (v1.x * v2.y)) < 0;
                    }
                }

                if (_planesDataMap.ContainsKey(boundedPlane.trackableId.subId2))
                {
                    _planesDataMap[boundedPlane.trackableId.subId2] = planeData;
                }
                else
                {
                    _planesDataMap.Add(boundedPlane.trackableId.subId2, planeData);
                }
            }
            return true;
        }

        public bool TryGetPlaneConvexHullVertexBuffer(ulong convexHullBufferId, ref List<Vector2> vertexPositions)
        {
            if (!_planesDataMap.ContainsKey(convexHullBufferId))
            {
                Debug.LogError($"Could not find a convex hull with id: {convexHullBufferId}!");
                return false;
            }

            PlaneDataCollection planeData = _planesDataMap[convexHullBufferId];
            if (ConvexHullEnabled)
            {
                List<uint> indexBuffer = planeData.indices;
                List<Vector3> vertexBuffer = planeData.vertices;
                // NOTE(LE): Traverse the OpenXR indices in inverse order because changing
                // the coordinate system handedness also changes the winding order.
                // Without this, the plane meshes would face the wrong way.
                // For some reason with MSFT this fails to render anything if just iterating the vertices in vertexBuffer, however.
                // And iterating the indices in reverse winding order does some slightly strange things around adding vertices out of order
                // So - iterate forwards over unique vertexIds. Keep track of vertexIds to add. Then reverse the order we added them.
                HashSet<int> done = new HashSet<int>();
                List<int> order = new List<int>();
                for (int indexIx = 0; indexIx < indexBuffer.Count; ++indexIx)
                {
                    int vertexIx = (int)indexBuffer[indexIx];
                    if (done.Contains(vertexIx))
                    {
                        continue;
                    }

                    if (vertexIx >= vertexBuffer.Count)
                    {
                        Debug.LogWarning($"Cannot add vertex with index {vertexIx} because the vertex buffer only contains {vertexBuffer.Count} vertices");
                        continue;
                    }

                    done.Add(vertexIx);
                    order.Add(vertexIx);
                }

                order.Reverse();
                foreach (var vertexIx in order)
                {
                    Vector3 vertex = vertexBuffer[vertexIx];
                    vertexPositions.Add(new Vector2(vertex.x, vertex.y));
                }

                return true;
            }

            float xOffset = planeData.extents.x;
            float yOffset = planeData.extents.y;
            vertexPositions.Add(new Vector2(-xOffset, planeData.reverseExtentPlaneWindingOrder ? +yOffset : -yOffset));
            vertexPositions.Add(new Vector2(-xOffset, planeData.reverseExtentPlaneWindingOrder ? -yOffset : +yOffset));
            vertexPositions.Add(new Vector2(+xOffset, planeData.reverseExtentPlaneWindingOrder ? -yOffset : +yOffset));
            vertexPositions.Add(new Vector2(+xOffset, planeData.reverseExtentPlaneWindingOrder ? +yOffset : -yOffset));
            return true;
        }

        public void SetPlaneFilters(PlaneDetectionMode detectionMode)
        {
            // If the detectionMode is Horizontal and Vertical, the unity inspector displays 'Everything'.
            // If the developer wants to filter for both Horizontal and Vertical planes they cannot.
            // If RestrictToAligned is false, when both are requested, planes filters can include non-orthogonal planes (Everything).
            // If RestrictToAligned is true, when both are requested, planes filters will not include non-orthogonal planes.
            // TODO: (LE) Revisit this if we upgrade to AR Foundation 6 which includes a "NotAxisAligned" detection mode.
            PlaneDetectionMode allAlignedPlanes = (PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical);
            if ((detectionMode & allAlignedPlanes) == allAlignedPlanes)
            {
                if (!RestrictToAligned)
                {
                    Internal_SetPlanesFilterFlags(~(uint)0);
                    return;
                }
            }

            Internal_SetPlanesFilterFlags((uint) detectionMode);
        }

        protected override string GetXrLayersToLoad()
        {
            return "XR_APILAYER_QCOM_scene_understanding";
        }

        protected override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            return Internal_GetInterceptedInstanceProcAddr(func);
        }

        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            base.OnInstanceCreate(instanceHandle);
            _baseRuntimeFeature = OpenXRSettings.Instance.GetFeature<BaseRuntimeFeature>();
            _subscribedSubsystems = new List<string>();

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_baseRuntimeFeature.CheckServicesCameraPermissions())
            {
                Debug.LogError("The Plane Detection Feature is missing the camera permissions and can't be created therefore!");
                return false;
            }
#endif
            Internal_SetInstanceHandle(instanceHandle);

            IEnumerable<string> missingExtensions = GetMissingExtensions(XR_MSFT_FeatureExtensions);
            var extensions = missingExtensions.ToList();

            if (extensions.Any())
            {
                Debug.Log(FeatureName + " is missing following extension in the runtime: " + String.Join(",", extensions));
                return false;
            }

            return true;
        }

        protected override void OnSubsystemCreate()
        {
            CreateSubsystem<XRPlaneSubsystemDescriptor, XRPlaneSubsystem>(_planeSubsystemDescriptors, PlaneDetectionSubsystem.ID);
        }

        protected override void OnSubsystemStop()
        {
            StopSubsystem<XRPlaneSubsystem>();
        }

        protected override void OnSubsystemDestroy()
        {
            DestroySubsystem<XRPlaneSubsystem>();
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
