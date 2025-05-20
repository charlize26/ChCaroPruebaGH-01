/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Qualcomm.Snapdragon.Spaces
{
    internal partial class FusionFeature
    {
        const string MainCameraTag = "MainCamera";
        const string UntaggedTag = "Untagged";

        Camera FindActiveHostCamera()
        {
            SpacesHostView hostView = FindFirstObjectByType<SpacesHostView>(FindObjectsInactive.Include);
            if (!hostView)
                return null;

            return hostView.phoneCamera;
        }

        private ValidationRule Recommend_Scene_ARSessionObjectExists()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: Dual Render Fusion recommends an AR Session in the scene.",
                checkPredicate = () => FindFirstObjectByType<ARSession>(FindObjectsInactive.Include),
                fixIt = () =>
                {
                    if (FindFirstObjectByType<ARSession>(FindObjectsInactive.Include))
                    {
                        return;
                    }

                    GameObject arSessionGO= new GameObject("AR Session");
                    arSessionGO.AddComponent<ARSession>();
                    Undo.RegisterCreatedObjectUndo(arSessionGO, "Create AR Session");

                    if (!FindFirstObjectByType<ARInputManager>(FindObjectsInactive.Include))
                    {
                        arSessionGO.AddComponent<ARInputManager>();
                    }
                    Debug.Log("Added AR Session Object to the Scene (" + arSessionGO.name + ")");
                },
                error = false,
                fixItMessage = "Adds a new GameObject \"AR Session\" to the scene. This object has the \"AR Session\" and \"AR Input Manager\" components."
            };
        }

        private ValidationRule Recommend_Scene_URP_MobileCameraTargetEyeNone()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: URP Projects need to manually check the Mobile Camera to set the Target Eye to None in the Inspector. 'Fix' will not handle this.",
#if UNITY_6000_0_OR_NEWER
                checkPredicate = () => !UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline,
#else
                checkPredicate = () => !UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset,
#endif
                fixIt = () =>
                {
#if UNITY_6000_0_OR_NEWER
                    if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline)
#else
                    if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset)
#endif
                    {
                        return;
                    }

                    Debug.LogWarning("Camera Target Eye checks cannot be done programmatically for URP at this time. Manually check the Cameras for Target Eye and ensure all non-XR Cameras are set to 'None' instead of 'Both'.");
                },
                error = false,
                fixItMessage = "Cannot fix automatically."
            };
        }

        private ValidationRule Recommend_Scene_NonXRCameraTargetEyeNone()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: For Dual Render Fusion, each non-XR Camera needs to be set to Target Eye (none).",
                checkPredicate = () =>
                {
                    Camera xrCamera = OriginLocationUtility.GetOriginCamera();

                    Camera[] cameras = FindObjectsOfType<Camera>(true);
                    foreach (Camera camera in cameras)
                    {
                        if (xrCamera != camera && !camera.targetTexture)
                        {
                            if (camera.stereoTargetEye != StereoTargetEyeMask.None)
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                },
                fixIt = () =>
                {
                    Camera xrCamera = OriginLocationUtility.GetOriginCamera();

                    int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Set Target Eye for non-XR Cameras");
                    Camera[] cameras = FindObjectsOfType<Camera>(true);
                    foreach (Camera camera in cameras)
                    {
                        if (xrCamera != camera)
                        {
                            if (camera.stereoTargetEye != StereoTargetEyeMask.None && !camera.targetTexture )
                            {
                                Undo.RecordObject(camera, "Set Target Eye for " + camera.name);
                                camera.stereoTargetEye = StereoTargetEyeMask.None;
                                Debug.Log("Updated Camera Target Eye to None (" + camera.name + ")");
                            }
                        }
                    }
                    Undo.CollapseUndoOperations(group);
                },
                error = false,
                fixItMessage = "Sets the Target Eye for all non-XR cameras to None."
            };
        }

        private bool Check_MultipleCamerasTaggedMain()
        {
            bool foundMainTag = false;
#if UNITY_6000_0_OR_NEWER
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
            Camera[] cameras = FindObjectsOfType<Camera>(true);
#endif
            foreach (Camera camera in cameras)
            {
                if (Check_IsCameraTaggedMain(camera))
                {
                    if (!foundMainTag)
                    {
                        foundMainTag = true;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Check_IsCameraTaggedMain(Camera camera)
        {
            return camera && !string.IsNullOrEmpty(camera.tag) && camera.tag.Equals(MainCameraTag);
        }

        private ValidationRule Recommend_Scene_XrCameraIsMain()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: Multiple cameras are tagged as MainCamera. Select 'Fix' to untag the XR Camera.",
                checkPredicate = () =>
                {
                    if (Check_MultipleCamerasTaggedMain())
                    {
                        return !Check_IsCameraTaggedMain(OriginLocationUtility.GetOriginCamera());
                    }

                    return true;
                },
                fixIt = () =>
                {
                    if (!Check_MultipleCamerasTaggedMain() || !Check_IsCameraTaggedMain(OriginLocationUtility.GetOriginCamera()))
                    {
                        return;
                    }

                    Camera xrCamera = OriginLocationUtility.GetOriginCamera();

                    Undo.RecordObject(xrCamera, "Untag XR Camera");
                    xrCamera.tag = UntaggedTag;
                    Debug.Log("Untagged XR Camera (" + xrCamera.name + ")");
                },
                error = false,
                fixItAutomatic = false,
                fixItMessage = "Removes the MainCamera tag from the XR Camera."
            };
        }

        private ValidationRule Recommend_Scene_HostViewCameraIsMain()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: Multiple cameras are tagged as MainCamera. Select 'Fix' to untag Host View Camera.",
                checkPredicate = () =>
                {
                    if (Check_MultipleCamerasTaggedMain())
                    {
                        return !Check_IsCameraTaggedMain(FindActiveHostCamera());
                    }

                    return true;
                },
                fixIt = () =>
                {
                    if (!Check_MultipleCamerasTaggedMain() || !Check_IsCameraTaggedMain(FindActiveHostCamera()))
                    {
                        return;
                    }

                    Camera hostCamera = FindActiveHostCamera();
                    Undo.RecordObject(hostCamera, "Untag camera " + hostCamera.name);
                    hostCamera.tag = UntaggedTag;
                    Debug.Log("Untagged Camera (" + hostCamera.name + ")");
                },
                error = false,
                fixItAutomatic = false,
                fixItMessage = "Removes the MainCamera tag from the Host View Camera."
            };
        }

        private ValidationRule Recommend_Scene_MultipleCamerasTaggedMain()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: Multiple cameras are tagged as MainCamera. 'Fix' will not handle this.",
                checkPredicate = () => !Check_MultipleCamerasTaggedMain(),
                fixIt = () =>
                {
                    if (!Check_MultipleCamerasTaggedMain())
                    {
                        return;
                    }

                    Debug.LogWarning("Multiple cameras are tagged as the Main camera. Cannot programmatically resolve the intent. Check each camera and ensure the Main tag is set on the correct camera.");
                },
                error = false,
                fixItAutomatic = false,
                fixItMessage = "Cannot fix automatically. Check each camera in the scene manually and ensure that only one camera has the MainCamera tag."
            };
        }

        private ValidationRule Recommend_Scene_XrCameraTargetDisplay1()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: XR Camera target display is not Display 1, recommend adding Spaces XR Simulator for adjusting the target display at Runtime.",
                checkPredicate = () =>
                {
                    Camera xrCamera = OriginLocationUtility.GetOriginCamera();
                    if (xrCamera)
                    {
                        if (xrCamera.targetDisplay != 0)
                        {
                            return FindFirstObjectByType<SpacesXRSimulator>(FindObjectsInactive.Include);
                        }
                    }
                    return true;
                },
                fixIt = () =>
                {
                    if (!FindFirstObjectByType<SpacesXRSimulator>(FindObjectsInactive.Include))
                    {
                        DualRenderFusionGameObjectHelper.AddSpacesXRSimulator(new MenuCommand(null));
                    }
                },
                error = false,
                fixItMessage = "Set the \"Target Display\" for the XR Camera to Display 1." +
                    "\nAdds a new Game Object \"Spaces XR Simulator\". This object contains the \"Spaces XR Simulator\" component."
            };
        }

        private static List<Type> _disabledByFusionLifecycleBlacklist = new List<Type>()
        {
            typeof(InputActionManager),
            typeof(EventSystem)
        };

        private struct LifecycleEventCallee
        {
            public GameObject calleeGO;
            public string eventName;
        }

        private struct ControlledByLifecycleEvent
        {
            public LifecycleEventCallee eventCallee;
            public Type componentType;
            public GameObject componentGO;
        }

        private void GetObjectsCallingMethodOnUnityEvent(UnityEvent ev, string loggableEventName, string methodName,  ref HashSet<LifecycleEventCallee> objectEventNamePair)
        {
            for (int ix = 0; ix < ev.GetPersistentEventCount(); ++ix)
            {
                if (ev.GetPersistentMethodName(ix) == methodName)
                {
                    var obj = ev.GetPersistentTarget(ix) as GameObject;
                    objectEventNamePair.Add(new LifecycleEventCallee() { calleeGO = obj, eventName = loggableEventName });
                }
            }
        }

        private void GetObjectsSetActiveByFusionLifecycleEvents(out HashSet<LifecycleEventCallee> objectsSetActive)
        {
            objectsSetActive = new HashSet<LifecycleEventCallee>();
            // Check all fusion lifecycle events for any calls to SetActive
            // Can't differentiate between calls to SetActive (true) and (false)
            var spacesLifecycleEvents = FindFirstObjectByType<SpacesLifecycleEvents>(FindObjectsInactive.Include);
            if (spacesLifecycleEvents)
            {
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnHostViewDisabled, nameof(spacesLifecycleEvents.OnHostViewDisabled), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnHostViewEnabled, nameof(spacesLifecycleEvents.OnHostViewEnabled), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnActive, nameof(spacesLifecycleEvents.OnActive), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnIdle, nameof(spacesLifecycleEvents.OnIdle), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnOpenXRAvailable, nameof(spacesLifecycleEvents.OnOpenXRAvailable), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnOpenXRUnavailable, nameof(spacesLifecycleEvents.OnOpenXRUnavailable), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnOpenXRStarted, nameof(spacesLifecycleEvents.OnOpenXRStarted), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnOpenXRStarting, nameof(spacesLifecycleEvents.OnOpenXRStarting), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnOpenXRStopped, nameof(spacesLifecycleEvents.OnOpenXRStopped), "SetActive", ref objectsSetActive);
                GetObjectsCallingMethodOnUnityEvent(spacesLifecycleEvents.OnOpenXRStopping, nameof(spacesLifecycleEvents.OnOpenXRStopping), "SetActive", ref objectsSetActive);
            }

            // additionally check AR Session, AR Session Origin
            // cant differentiate between calls to SetActive (true) and (false)
            var sessionOriginObject = OriginLocationUtility.GetOriginTransform(true).gameObject;
            var session = FindFirstObjectByType<ARSession>(FindObjectsInactive.Include).gameObject;

            objectsSetActive.Add(new LifecycleEventCallee() { calleeGO = session, eventName = "OnGlassConnected"});
            objectsSetActive.Add(new LifecycleEventCallee() { calleeGO = session, eventName =  "OnGlassDisconnected"});
            objectsSetActive.Add(new LifecycleEventCallee() { calleeGO = sessionOriginObject, eventName = "OnGlassConnected"});
            objectsSetActive.Add(new LifecycleEventCallee() { calleeGO = sessionOriginObject, eventName = "OnGlassDisconnected"});
        }

        private ValidationRule Recommend_Scene_BlacklistedComponentsDisabledByFusionLifecycleEvents()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: Input Action Manager should not be a child of any GameObject which is disabled by fusion lifecycle events.",
                checkPredicate = () =>
                {
                    var inputActionManager = FindFirstObjectByType<InputActionManager>(FindObjectsInactive.Include);
                    if (inputActionManager)
                    {
                        GetObjectsSetActiveByFusionLifecycleEvents(out var objectsSetActive);
                        foreach (var callee in objectsSetActive)
                        {
                            foreach (var type in _disabledByFusionLifecycleBlacklist)
                            {
                                if (callee.calleeGO.transform.GetComponentInChildren(type, true))
                                {
                                    return false;
                                }
                            }
                        }
                    }

                    return true;
                },
                fixItAutomatic = false,
                fixIt = () =>
                {
                    List<ControlledByLifecycleEvent> data = new();
                    GetObjectsSetActiveByFusionLifecycleEvents(out var objectsSetActive);
                    foreach (var callee in objectsSetActive)
                    {
                        foreach (var type in _disabledByFusionLifecycleBlacklist)
                        {
                            var component = callee.calleeGO.transform.GetComponentInChildren(type, true);
                            if (component)
                            {
                                data.Add(new ControlledByLifecycleEvent()
                                {
                                    eventCallee = callee,
                                    componentType = type,
                                    componentGO = component.gameObject
                                });
                            }
                        }
                    }

                    string LogOutput(List<ControlledByLifecycleEvent> info)
                    {
                        string loggedOutput = "";
                        foreach (var entry in info)
                        {
                            loggedOutput += $"- Component of type [{entry.componentType}] found on object [{entry.componentGO}] is a child of [{entry.eventCallee.calleeGO}] which calls SetActive as a result of the fusion lifecycle event {entry.eventCallee.eventName}.\n";
                        }

                        return loggedOutput;
                    }

                    Debug.LogWarning("Certain components should not be attached to Game Objects which are children of any Game Object being disabled by Fusion Lifecycle Events." +
                        $"\nCannot programmatically tell which calls to SetActive on the following objects might disable these components:\n" + LogOutput(data) +
                        "\nThese objects should be checked manually, and the restricted components should be reparented if possible." +
                        "\nFailure to do this can result in side-effects." +
                        "\ne.g. In the case of InputActionManager / EventSystem types on disabled parent objects -> the Host View (mobile phone) display might not respond to touch inputs when the glasses are disconnected.");
                },
                error = false
            };
        }

        private ValidationRule Recommend_Scene_LifecycleEventsExists()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: There is no Spaces Lifecycle Events in the current scene.",
                checkPredicate = () => FindFirstObjectByType<SpacesLifecycleEvents>(FindObjectsInactive.Include),
                fixIt = () =>
                {
                    if (!FindFirstObjectByType<SpacesLifecycleEvents>(FindObjectsInactive.Include))
                    {
                        DualRenderFusionGameObjectHelper.AddLifecycleEvents(new MenuCommand(null));
                    }
                },
                fixItMessage = "Add a GameObject \"Spaces Lifecycle Events\" to the scene. This object has the \"Spaces Lifecycle Events\" component."
            };
        }

        private bool Check_MoreThanOneOrigin()
        {
            return FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length > 1;
        }

        private ValidationRule Required_Scene_OnlyOneXrOrigin()
        {
            return new ValidationRule(this)
            {
                message = "Scene Requirement: There should be only one active XR Origin in the scene.",
                checkPredicate = () => !Check_MoreThanOneOrigin(),
                fixIt = () =>
                {
#if UNITY_6000_0_OR_NEWER
                    XROrigin[] origins = FindObjectsByType<XROrigin>(FindObjectsSortMode.None);
#else
                    XROrigin[] origins = FindObjectsByType<XROrigin>(FindObjectsSortMode.None);
#endif
                    if (origins != null && origins.Length >= 1)
                    {
                        string[] names = new string[origins.Length];
                        for (int i = 0; i < origins.Length; i++)
                        {
                            names[i] = origins[i].gameObject.name;
                        }
                        Debug.LogError("Please manually disable or remove unneeded XR Origin objects in the scene ([" + string.Join("],[", names) + "]).");
                    }
                },
                error = true,
                fixItMessage = "Cannot fix automatically. Will log a list of all GameObjects with XR Origin or AR Session Origin components. Manually remove the extra components from the scene until only 1 remains."
            };
        }

        private ValidationRule Required_Scene_HostViewRendersAfterXr()
        {
            return new ValidationRule(this)
            {
                message = "Scene Requirement: Dual Render Fusion requires the mobile Camera to render after the XR Camera.",
                checkPredicate = () =>
                {
                    Camera xrCamera = OriginLocationUtility.GetOriginCamera();
                    Camera hostCamera = FindActiveHostCamera();

                    if (xrCamera && hostCamera)
                    {
                        if (hostCamera.depth <= xrCamera.depth)
                        {
                            return false;
                        }
                    }
                    return true;

                },
                fixIt = () =>
                {
                    Camera xrCamera = OriginLocationUtility.GetOriginCamera();
                    Camera hostCamera = FindActiveHostCamera();

                    Undo.RecordObject(hostCamera, "Modified Depth in " + hostCamera.name);

                    float oldDepth = hostCamera.depth;
                    hostCamera.depth = xrCamera.depth + 1;
                    Debug.Log("Fixed Camera (" + hostCamera.name + ") depth to " + hostCamera.depth + " from " + oldDepth);
                },
                error = true,
                fixItMessage = "Set the \"Depth\" of the Host View (mobile) Camera to render immediately after the XR Camera."
            };
        }

        private ValidationRule Required_Scene_XrCameraExists()
        {
            return new ValidationRule(this)
            {
                message = "Scene Requirement: Dual Render Fusion requires a camera attached to an XR Origin.",
                checkPredicate = () => OriginLocationUtility.GetOriginCamera(),
                fixIt = () =>
                {
                    if (OriginLocationUtility.GetOriginCamera())
                    {
                        return;
                    }

                    XROrigin xro = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);

                    int group = Undo.GetCurrentGroup();

                    Transform cameraParentTransform;

                    // If no origins, add XROrigin
                    if (!xro)
                    {
                        GameObject originObject = new GameObject("XR Origin");
                        xro = originObject.AddComponent<XROrigin>();
                        Debug.Log("Added XR Origin to the Scene (" + originObject.name + ")");

                        Undo.RegisterCreatedObjectUndo(originObject, "Create XR Origin");

                        Undo.SetCurrentGroupName("Create XR Origin");
                    }
                    else
                    {
                        Undo.SetCurrentGroupName("Add XR Camera");
                    }

                    GameObject xrCameraObject = new GameObject("XR Camera");
                    var xrCamera = xrCameraObject.AddComponent<Camera>();
                    xrCamera.tag = UntaggedTag;

                    if (!xro.CameraFloorOffsetObject)
                    {
                        xro.CameraFloorOffsetObject = new GameObject("Camera Offset");
                        xro.CameraFloorOffsetObject.transform.SetParent(xro.transform, false);
                        xro.CameraFloorOffsetObject.transform.position = new Vector3(0, xro.CameraYOffset, 0);
                        xro.CameraFloorOffsetObject.transform.rotation = Quaternion.identity;
                    }
                    xro.Camera = xrCamera;
                    cameraParentTransform = xro.CameraFloorOffsetObject.transform;

                    xrCameraObject.transform.SetParent(cameraParentTransform, false);
                    xrCamera.clearFlags = CameraClearFlags.SolidColor;
                    xrCamera.backgroundColor = Color.black;
                    xrCamera.farClipPlane = 1000;
                    xrCamera.stereoTargetEye = StereoTargetEyeMask.Both;
                    xrCamera.targetDisplay = 1;

                    xrCameraObject.AddComponent<ARCameraManager>();
                    xrCameraObject.AddComponent<ARCameraBackground>();
                    Debug.Log("Added XR Camera to the Scene (" + xrCamera.name + ")");

                    TrackedPoseDriver trackedPoseDriver = xrCameraObject.AddComponent<TrackedPoseDriver>();
                    var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
                    positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
                    var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
                    rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
                    trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
                    trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);

                    DualRenderFusionGameObjectHelper.AddSpacesXRSimulator(new MenuCommand(null));

                    Undo.CollapseUndoOperations(group);
                },
                error = true,
                fixItMessage = "Adds an XR Origin if necessary. Add a new Game Object \"XR Camera\" as a child of the session origin. This object contains the \"Camera\", \"AR Camera Manager\", \"AR Camera Background\", and \"Tracked Posed Driver\" components." +
                    "\nAdds a new Game Object \"Spaces XR Simulator\". This object contains the \"Spaces XR Simulator\" component."
            };
        }

        private ValidationRule Required_Scene_SpacesHostViewExists()
        {
            return new ValidationRule(this)
            {
                message = "Scene Requirement: Dual Render Fusion requires a Spaces Host View component in order to receive events about the availability of the host viewer."
                    + "\nThis allows a single apk to run on both Dual Render Fusion compatible Host/Viewer device combinations, and MR/VR devices." +
                    "\nAdditionally the camera attached to this Game Object should be used to display the fusion host (mobile) display.",
                checkPredicate = () =>
                {
                    return FindFirstObjectByType<SpacesHostView>(FindObjectsInactive.Include);
                },
                fixIt = () =>
                {
                    if (!FindFirstObjectByType<SpacesHostView>(FindObjectsInactive.Include))
                    {
                        DualRenderFusionGameObjectHelper.AddSpacesHostViewGameObjectToScene(new MenuCommand(null));
                    }
                },
                error = true,
                fixItMessage = "Adds a new Game Object \"Spaces Host View\". This object has the \"Spaces Host View\" component."
            };
        }

        private ValidationRule Recommend_Scene_DynamicOpenXrLoaderExists(XRGeneralSettings generalSettings)
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: Use the Dynamic OpenXR Loader component to manage the OpenXR lifecycle of the application.",
                checkPredicate = () =>
                {
                    if (generalSettings.InitManagerOnStart)
                    {
                        return true;
                    }

                    return FindFirstObjectByType<DynamicOpenXRLoader>(FindObjectsInactive.Include);
                },
                fixIt = () =>
                {
                    if (!FindFirstObjectByType<DynamicOpenXRLoader>(FindObjectsInactive.Include))
                    {
                        DualRenderFusionGameObjectHelper.AddDynamicOpenXRLoaderGameObjectToScene(new MenuCommand(null));
                        Debug.Log("Added a new Game Object \"Dynamic OpenXR Loader\". This object has the \"Dynamic OpenXR Loader\" and the \"Spaces Glass Status\" components. ");
                    }
                },
                error = true,
                fixItMessage = "Adds a new Game Object \"Dynamic OpenXR Loader\". This object has the \"Dynamic OpenXR Loader\" and the \"Spaces Glass Status\" components."
            };
        }

        private ValidationRule Required_Project_InitManagerOnStart(XRGeneralSettings generalSettings)
        {
            return new ValidationRule(this)
            {
                message = "Project Requirement: Dual Render Fusion projects should not \"Initialize XR on Startup\". Instead, make use of the Dynamic OpenXR Loader component to manage the lifecycle of OpenXR in the application.",
                checkPredicate = () => !generalSettings.InitManagerOnStart,
                fixIt = () =>
                {
                    generalSettings.InitManagerOnStart = false;
                },
                fixItMessage = "Disables Project Settings > XR Plug-In Management > Initialize Xr on Startup",
                error = true
            };
        }

        private ValidationRule Recommend_Scene_SpacesXRSimulatorExists()
        {
            return new ValidationRule(this)
            {
                message = "Scene Recommendation: Use the Spaces XR Simulator component to preview the different displays of XR and Host View cameras.",
                checkPredicate = () =>
                {
                    return FindFirstObjectByType<SpacesXRSimulator>(FindObjectsInactive.Include);
                },
                fixIt = () =>
                {
                    if (FindFirstObjectByType<SpacesXRSimulator>(FindObjectsInactive.Include))
                    {
                        return;
                    }

                    DualRenderFusionGameObjectHelper.AddSpacesXRSimulator(new MenuCommand(null));
                },
                error = false,
                fixItMessage = "Adds a new Game Object \"Spaces XR Simulator\". This object has the \"Spaces XR Simulator\" component."
            };
        }

        private ValidationRule Required_Project_OpenXrPluginNot1_11OrNewer()
        {
            return new ValidationRule(this)
            {
                message = "Project Requirement: Dual Render Fusion projects do not work with OpenXR Plugin versions 1.11.0 or later. When using these versions of the plugin no content will be rendered on the Phone.",
                checkPredicate = () =>
                {
#if OPENXR_1_11_0_OR_NEWER
                    return IgnoreOpenXRVersion;
#endif

                    return true;
                },
                fixIt = () =>
                {
                    const string openXrPackageName = "com.unity.xr.openxr";
                    Version problemVersion = new Version(1, 11, 0);

                    List<string> packageDependencyNames = new();
                    PackageInfo openXrPluginPackage = null;

                    var packageInfos = PackageInfo.GetAllRegisteredPackages();
                    foreach (var packageInfo in packageInfos)
                    {
                        if (packageInfo.name == openXrPackageName)
                        {
                            openXrPluginPackage = packageInfo;
                        }
                        else
                        {
                            foreach (var dependency in packageInfo.dependencies)
                            {
                                if (dependency.name == openXrPackageName && new Version(dependency.version) >= problemVersion)
                                {
                                    packageDependencyNames.Add(packageInfo.displayName);
                                }
                            }
                        }
                    }

                    if (packageDependencyNames.Any())
                    {
                        Debug.LogError($"The following packages depend directly on the OpenXR Plugin version {problemVersion}: {packageDependencyNames.Stringify()}\n" +
                            "This version is not supported alongside Dual Render Fusion.");
                    }

                    if (openXrPluginPackage is { isDirectDependency: true })
                    {
                        Debug.LogWarning($"This project depends on {openXrPackageName} directly. Version {problemVersion} of the OpenXR Plugin is not supported alongside Dual Render Fusion.\n" +
                            "Please check the Packages/manifest.json file manually to confirm that it is not using this version.");
                    }

                    UnityEditor.PackageManager.UI.Window.Open(openXrPackageName);
                },
                error = true,
                fixItMessage = "Display packages which rely on the OpenXR Plugin version 1.11.0 or later, and opens the PackageManager to allow the developer to try and select a different version of the plugin.",
                fixItAutomatic = false
            };
        }

#if UNITY_2023_1_OR_NEWER
        private ValidationRule Required_Project_ActivityApplicationEntry()
        {
            return new ValidationRule(this)
            {
                message = "Project Settings requirement: Dual Render Fusion projects do not work with the 'GameActivity' as the application entry. The 'Activity' should be used instead.",
                checkPredicate = () =>
                {
                    switch (PlayerSettings.Android.applicationEntry)
                    {
                        case AndroidApplicationEntry.GameActivity:
                            return false;
                        case AndroidApplicationEntry.Activity:
                            return true;
                        default:
                            break;
                    }
                    return true;
                },
                fixIt = () =>
                {
                    PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
                },
                error = true,
                fixItMessage = "The application entry point will be changed to 'Activity' instead of 'GameActivity'."
            };
        }
#endif

        protected override void GetValidationChecks(List<ValidationRule> rules, BuildTargetGroup targetGroup)
        {
            if (!this.enabled)
            {
                return;
            }

            var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (!openXRSettings)
            {
                return;
            }

            var baseRuntimeFeature = openXRSettings.GetFeature<BaseRuntimeFeature>();
            if (!baseRuntimeFeature || !baseRuntimeFeature.enabled)
            {
                return;
            }

            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!settings || !settings.Manager)
            {
                return;
            }

            if (ValidateOpenScene)
            {
                rules.Add(Recommend_Scene_ARSessionObjectExists());
                rules.Add(Recommend_Scene_URP_MobileCameraTargetEyeNone());
                rules.Add(Recommend_Scene_NonXRCameraTargetEyeNone());
                rules.Add(Recommend_Scene_XrCameraIsMain());
                rules.Add(Recommend_Scene_HostViewCameraIsMain());
                rules.Add(Recommend_Scene_XrCameraTargetDisplay1());
                rules.Add(Recommend_Scene_MultipleCamerasTaggedMain());
                rules.Add(Recommend_Scene_DynamicOpenXrLoaderExists(settings));
                rules.Add(Recommend_Scene_LifecycleEventsExists());
                rules.Add(Recommend_Scene_SpacesXRSimulatorExists());
                rules.Add(Recommend_Scene_BlacklistedComponentsDisabledByFusionLifecycleEvents());
                rules.Add(Required_Scene_OnlyOneXrOrigin());
                rules.Add(Required_Scene_HostViewRendersAfterXr());
                rules.Add(Required_Scene_XrCameraExists());
                rules.Add(Required_Scene_SpacesHostViewExists());
            }

            rules.Add(Required_Project_InitManagerOnStart(settings));
            rules.Add(Required_Project_OpenXrPluginNot1_11OrNewer());
#if UNITY_2023_1_OR_NEWER
            rules.Add(Required_Project_ActivityApplicationEntry());
#endif
        }
    }
}
#endif
