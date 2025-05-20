/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if USING_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
    [DefaultExecutionOrder(int.MinValue)]
    [MovedFrom(false, null, null, "FusionSimulator")]
    public class SpacesXRSimulator : MonoBehaviour
    {
#if UNITY_EDITOR
        private static SpacesXRSimulator Instance;

        [Header("Simulation Settings")]
        [Tooltip("Simulate starting the application with glasses connected.")]
        public bool StartConnected;

        [Tooltip("Run the XR Camera on Display 1, instead of 2 in the simulation (when Dual Render Fusion is enabled)." +
            "\nWorkaround for a Unity issue which was resolved in 2022.3.21f1." +
            "\nSee https://issuetracker.unity3d.com/issues/in-game-ui-events-from-the-secondary-display-point-to-the-main-display-in-player-when-displays-have-the-same-resolution")]
        [SerializeField]
        private bool _invertSimCameraDisplay;

        public bool InvertSimCameraDisplay
        {
            get => _invertSimCameraDisplay && _hasHostView;
            set => _invertSimCameraDisplay = value;
        }

        private static Scene? _loadedScene = null;
        private static bool _relaunchXR = true;
        private bool _hasHostView;

        private static bool _isFusionEnabled
        {
            get
            {
                return OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)?.GetFeature<FusionFeature>()?.enabled ?? false;
            }
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this.gameObject);
                return;
            }

            Instance = this;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            DontDestroyOnLoad(this.gameObject);

            if (!_isFusionEnabled)
            {
                TriggerLoaderInit_NoDynamicOpenXRLoader();
            }
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (_loadedScene == scene)
            {
                // The loader needs to be taken down and brought back up when a scene switch is about to happen.
                // There must be an active loader when Awake + OnEnable is called on ARSession after scene load.
                LoaderDownAndUp();
            }
        }

        private static void OnSceneLoaded(Scene justLoadedScene, LoadSceneMode mode)
        {
            _loadedScene = justLoadedScene;
            Instance._hasHostView = (FindFirstObjectByType<SpacesHostView>(FindObjectsInactive.Include) != null);

            if (_isFusionEnabled)
            {
                if (Instance != null)
                {
                    Instance.ConfigureCamera();
                    Instance.ConfigureHostView();

                    // this is undocumented behaviour
                    // in native unity code there are additional scene loading modes
                    // 4 corresponds to """Preload manager, load scene editor""" and is only called in the editor
                    if ((mode == LoadSceneMode.Single || (int)mode == 4) && Instance.InvertSimCameraDisplay)
                    {
                        Instance.InvertCanvasTargetDisplay();
                    }
                }
            }
        }

        private void ConfigureCamera()
        {
            Camera xrCamera = OriginLocationUtility.GetOriginCamera(true);
            if (xrCamera != null)
            {
                if (InvertSimCameraDisplay)
                {
                    xrCamera.targetDisplay = 0;
                }
                else
                {
                    xrCamera.targetDisplay = 1;
                }
            }

            Camera phoneCamera = FindFirstObjectByType<SpacesHostView>(FindObjectsInactive.Include)?.phoneCamera;
            if (phoneCamera != null)
            {
                if (InvertSimCameraDisplay)
                {
                    phoneCamera.targetDisplay = 1;
                }
                else
                {
                    phoneCamera.targetDisplay = 0;
                }
            }

#if USING_URP
            foreach (Camera otherCamera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (otherCamera != xrCamera && otherCamera != phoneCamera)
                {
                    var cameraData = otherCamera.GetUniversalAdditionalCameraData();
                    if (cameraData && cameraData.cameraStack is { Count: > 0 })
                    {
                        if (xrCamera != null && cameraData.cameraStack.Contains(xrCamera))
                        {
                            foreach (var cameraInStack in cameraData.cameraStack)
                            {
                                cameraInStack.targetDisplay = xrCamera.targetDisplay;
                            }

                            otherCamera.targetDisplay = xrCamera.targetDisplay;
                        }
                        else if (phoneCamera!= null && cameraData.cameraStack.Contains(phoneCamera))
                        {
                            foreach (var cameraInStack in cameraData.cameraStack)
                            {
                                cameraInStack.targetDisplay = phoneCamera.targetDisplay;
                            }

                            otherCamera.targetDisplay = phoneCamera.targetDisplay;
                        }
                    }
                }
            }
#endif // USING_URP
        }

        private void InvertCanvasTargetDisplay()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    if (canvas.targetDisplay == 1)
                    {
                        canvas.targetDisplay = 0;
                    }
                    else if (canvas.targetDisplay == 0)
                    {
                        canvas.targetDisplay = 1;
                    }
                }
            }
        }

        private void ConfigureHostView()
        {
            var spacesHostView = SpacesHostView.Instance;
            if (spacesHostView != null)
            {
                spacesHostView.OnHostViewEnabled?.Invoke();
            }
        }

        public void Start()
        {
            if (_isFusionEnabled)
            {
                if (StartConnected)
                {
                    SimulateGlassActive();
                    SimulateGlassConnection();
                }
            }
        }

        public void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                SceneManager.sceneLoaded -= OnSceneLoaded;

                Instance = null;
            }
        }

        private static void LoaderDownAndUp()
        {
            if (_isFusionEnabled)
            {
                _relaunchXR = DynamicOpenXRLoader.Instance && DynamicOpenXRLoader.Instance.AreSubsystemsRunning;
            }

            TriggerLoaderDeinit_NoDynamicOpenXRLoader();

            if (_relaunchXR)
            {
                TriggerLoaderInit_NoDynamicOpenXRLoader();
            }
        }

        private static void TriggerLoaderInit()
        {
            if (_isFusionEnabled)
            {
                DynamicOpenXRLoader.Instance.StartOpenXR();
            }
            else
            {
                TriggerLoaderInit_NoDynamicOpenXRLoader();
            }
        }

        private static void TriggerLoaderInit_NoDynamicOpenXRLoader()
        {
            var xrManager = XRGeneralSettings.Instance?.Manager;
            if (xrManager != null && !xrManager.isInitializationComplete)
            {
                xrManager.InitializeLoaderSync();
            }
        }

        private static void TriggerLoaderDeinit()
        {
            if (_isFusionEnabled && DynamicOpenXRLoader.Instance.AreSubsystemsRunning)
            {
                DynamicOpenXRLoader.Instance.StopOpenXR(true);
            }
            else
            {
                TriggerLoaderDeinit_NoDynamicOpenXRLoader();
            }
        }

        private static void TriggerLoaderDeinit_NoDynamicOpenXRLoader()
        {
            var xrManager = XRGeneralSettings.Instance?.Manager;
            if (xrManager != null && xrManager.isInitializationComplete)
            {
                xrManager.DeinitializeLoader();
            }
        }

        [MenuItem("Window/XR/Snapdragon Spaces/Dual Render Fusion/Simulation/Connect Glasses #&c")]
        public static void SimulateGlassConnection()
        {
            if (!Application.isPlaying)
                return;

            if (SpacesGlassStatus.Instance)
            {
                Debug.Log("Fusion Simulator: Glasses Connected");
                SpacesGlassStatus.Instance.GlassConnectionState = SpacesGlassStatus.ConnectionState.Connected;
                SpacesGlassStatus.Instance.OnConnected?.Invoke();
            }
        }

        [MenuItem("Window/XR/Snapdragon Spaces/Dual Render Fusion/Simulation/Disconnect Glasses #&d")]
        public static void SimulateGlassDisconnection()
        {
            if (!Application.isPlaying)
                return;

            if (SpacesGlassStatus.Instance)
            {
                Debug.Log("Fusion Simulator: Glasses Disconnected");
                SpacesGlassStatus.Instance.GlassConnectionState = SpacesGlassStatus.ConnectionState.Disconnected;
                SpacesGlassStatus.Instance.OnDisconnected?.Invoke();
            }
        }

        [MenuItem("Window/XR/Snapdragon Spaces/Dual Render Fusion/Simulation/Glasses Active #&g")]
        public static void SimulateGlassActive()
        {
            if (!Application.isPlaying)
                return;

            if (SpacesGlassStatus.Instance)
            {
                Debug.Log("Fusion Simulator: Glasses Active");
                SpacesGlassStatus.Instance.GlassActiveState = SpacesGlassStatus.ActiveState.Active;
                SpacesGlassStatus.Instance.OnActive?.Invoke();
            }
        }

        [MenuItem("Window/XR/Snapdragon Spaces/Dual Render Fusion/Simulation/Glasses Idle #&h")]
        public static void SimulateGlassIdle()
        {
            if (!Application.isPlaying)
                return;

            if (SpacesGlassStatus.Instance)
            {
                Debug.Log("Fusion Simulator: Glasses Idle");
                SpacesGlassStatus.Instance.GlassActiveState = SpacesGlassStatus.ActiveState.Idle;
                SpacesGlassStatus.Instance.OnIdle?.Invoke();
            }
        }

        [MenuItem("Window/XR/Snapdragon Spaces/Dual Render Fusion/Simulation/Invert Simulation Displays #&x")]
        public static void InvertSimulationDisplays()
        {
            if (!Application.isPlaying)
                return;

            if (Instance)
            {
                Debug.Log("Fusion Simulator: Invert Displays");
                Instance.InvertSimCameraDisplay = !Instance.InvertSimCameraDisplay;
                Instance.ConfigureCamera();
                Instance.InvertCanvasTargetDisplay();
            }
        }
#endif // UNITY_EDITOR
    }
}
