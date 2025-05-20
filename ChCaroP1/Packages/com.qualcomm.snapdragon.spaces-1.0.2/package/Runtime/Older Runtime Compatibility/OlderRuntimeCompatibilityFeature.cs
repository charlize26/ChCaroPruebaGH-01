/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Runtime.InteropServices;
using UnityEngine;
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
        Desc = "Enables compatibility with older versions of the OpenXR runtime",
        DocumentationLink = "",
        OpenxrExtensionStrings = FeatureExtensions,
        Version = "1.0.0",
        Required = true,
        Hidden = true,
        Priority = int.MinValue,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID)]
#endif
    internal sealed partial class OlderRuntimeCompatibilityFeature : SpacesOpenXRFeature
    {
        public const string FeatureName = "Older Runtime Compatibility (Experimental)";
        public const string FeatureID = "com.qualcomm.snapdragon.spaces.olderruntimecompatibility";
        public const string FeatureExtensions = "";

        public ORCResult OrcResult { get; private set; }
        private bool _isOrcCheckDone;

        protected override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            if (GetOrcResult() != ORCResult.Success || !OpenXRSettings.Instance.GetFeature<FusionFeature>().enabled)
            {
                return func;
            }
            // ORC hook info result
            ORCHookInfo hookInfo = new(func);
            using ScopePtr<ORCHookInfo> hookInfoPtr = new(hookInfo);
            using ScopePtr<IntPtr> hookPtr = new();

            var hookResult = Internal_orcHookCompatibilitySystem(hookInfoPtr.Raw, hookPtr.Raw);

            return hookResult == ORCResult.Success ? hookPtr.AsStruct() : func;
        }

        protected override void OnSessionEnd(ulong spaceHandle)
        {
            base.OnSessionEnd(spaceHandle);
            ShutdownOrc();
        }

        private ORCResult CheckOrc()
        {
            Debug.Log("Checking Old runtime compatibility");
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            var context = activity.Call<AndroidJavaObject>("getApplicationContext");

            IntPtr javaVM = AndroidJNI.GetJavaVM();

            // ORC Setup Info result
            ORCInitInfo initInfo = new(javaVM, context.GetRawObject());
            using ScopePtr<ORCInitInfo> setupInfoPtr = new(initInfo);
            OrcResult = Internal_orcInitializeCompatibilitySystem(setupInfoPtr.Raw);

            return OrcResult;
        }

        internal ORCResult GetOrcResult()
        {
            if (!_isOrcCheckDone)
            {
                _isOrcCheckDone = true;
                return CheckOrc();
            }

            return OrcResult;
        }

        internal void ShutdownOrc()
        {
            Internal_orcShutdownCompatibilitySystem();
            _isOrcCheckDone = false;
        }

        internal void ShowOrcApplicationDialog(SimpleDialogOptions options = null)
        {
    #if UNITY_ANDROID && !UNITY_EDITOR
                var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
                var runtimeChecker = new AndroidJavaClass("com.qualcomm.snapdragon.spaces.serviceshelper.RuntimeChecker");

                var javaOptions = new AndroidJavaObject("com.qualcomm.snapdragon.spaces.serviceshelper.SimpleDialogOptions");
                var title = "";
                var message = "The application can still be used without XR. Would you like to continue?";
                var positiveButtonText = "Yes";
                var negativeButtonText = "Quit";
                if (options == null)
			    {
                    Debug.Log("ORC: ." + OrcResult);
                    switch (OrcResult)
                    {
                        case ORCResult.ErrorApplicationTooOldForRuntime:
                            title = "Application too old for runtime";
                            message = "This application is too old for the installed runtime. " + message;
                            break;
                        case ORCResult.ErrorRuntimeTooOldForApplication:
                            title = "Runtime too old for application";
                            message = "The installed runtime is too old for the application. " + message;
                            break;
                        default:
                            title = "Runtime validation failure";
                            break;
                    }
                }
                else
                {
                    title = options.Title;
                    message = options.Message;
                    positiveButtonText = options.PositiveButtonText;
                    negativeButtonText = options.NegativeButtonText;
                }
                javaOptions.Set("Title", title);
                javaOptions.Set("Message", message);
                javaOptions.Set("PositiveButtonText", positiveButtonText);
                javaOptions.Set("NegativeButtonText", negativeButtonText);

                if (!runtimeChecker.CallStatic<bool>("ShowOlderRuntimeDialog", new object[] { activity, javaOptions }))
                {
                    Debug.LogWarning("The request to show the older runtime dialog failed.");
                }
    #endif
        }
    }


    internal enum ORCResult
    {
        Success = 0,
        ErrorRuntimeFailure = -1,
        ErrorValidationFailure = -2,
        ErrorUninitializedSystem = -3,
        ErrorRuntimeTooOldForApplication = -4,
        ErrorApplicationTooOldForRuntime = -5
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ORCInitInfo
    {
        private IntPtr _applicationVM;
        private IntPtr _applicationContext;
        private IntPtr _appCompatibilityInfoOverrides;
        private IntPtr _runtimeCompatibilityInfoOverrides;

        public ORCInitInfo(IntPtr applicationVM, IntPtr applicationContext)
        {
            _applicationVM = applicationVM;
            _applicationContext = applicationContext;
            _appCompatibilityInfoOverrides = IntPtr.Zero;
            _runtimeCompatibilityInfoOverrides = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ORCHookInfo
    {
        private IntPtr _nextGetInstanceProcAddr;

        public ORCHookInfo(IntPtr nextGetInstanceProcAddr)
        {
            _nextGetInstanceProcAddr = nextGetInstanceProcAddr;
        }
    }
}
