/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class SpacesSetting
    {
        public static readonly SpacesSetting XRControllerProfile = new()
        {
            title = "Enable the \"Oculus Touch Controller Profile\"",
            tooltipMessage = "The \"Oculus Touch Controller Profile\" is the profile recommended for supporting XR controllers with the Snapdragon Spaces samples.",
            checkPredicate = () =>
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (!settings)
                {
                    return false;
                }

                var oculusTouchControllerProfile = settings.GetFeatures<OpenXRInteractionFeature>().SingleOrDefault(feature => feature.enabled && feature is OculusTouchControllerProfile);
                return oculusTouchControllerProfile;
            },
            performFix = () =>
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (!settings)
                {
                    return;
                }

                var oculusProfile = settings.GetFeatures<OpenXRInteractionFeature>().SingleOrDefault(oclususFeature => oclususFeature is OculusTouchControllerProfile);
                if (oculusProfile)
                {
                    oculusProfile.enabled = true;
                }
            }
        };

        public static readonly SpacesSetting ARControllerProfile = new()
        {
            title = "Enable the \"Microsoft Mixed Reality Motion Controller Profile\"",
            tooltipMessage = "The \"Microsoft Mixed Reality Motion Controller Profile\"  is the profile recommended for supporting Host Controller with the Snapdragon Spaces samples.",
            checkPredicate = () =>
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (!settings)
                {
                    return false;
                }

                var motionControllerProfile = settings.GetFeatures<OpenXRInteractionFeature>().SingleOrDefault(feature => feature.enabled && feature is SpacesMicrosoftMixedRealityMotionControllerProfile);
                return motionControllerProfile;
            },
            performFix = () =>
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (!settings)
                {
                    return;
                }

                var motionControllerProfile = settings.GetFeatures<OpenXRInteractionFeature>().SingleOrDefault(feature => feature is SpacesMicrosoftMixedRealityMotionControllerProfile);
                if (motionControllerProfile)
                {
                    motionControllerProfile.enabled = true;
                }
            }
        };

        public static readonly SpacesSetting GraphicsAPI = new()
        {
            title = "Use OpenGLES3 graphics API",
            tooltipMessage = "Only the OpenGLES3 graphics API is fully supported at the moment. Some provided samples might not work correctly with Vulkan.",
            checkPredicate = () =>
            {
                return PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)
                    .SequenceEqual(new[] { GraphicsDeviceType.OpenGLES3 });
            },
            performFix = () =>
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                    new[] { GraphicsDeviceType.OpenGLES3 });
            }
        };

        public static readonly SpacesSetting MinimumAndroidSDKVersion = new()
        {
            title = "Minimum Android SDK version has to be equal or greater than 29.",
            checkPredicate = () =>
            {
                return PlayerSettings.Android.minSdkVersion >= AndroidSdkVersions.AndroidApiLevel29;
            },
            performFix = () =>
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            }
        };

        public static readonly SpacesSetting TargetAndroidSDKVersion = new()
        {
            title = "Target Android SDK version has to be equal or greater than 31.",
            checkPredicate = () => PlayerSettings.Android.targetSdkVersion == 0 || PlayerSettings.Android.targetSdkVersion >= (AndroidSdkVersions)31,
            performFix = () =>
            {
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)31;
            }
        };

        public static readonly SpacesSetting ScriptingBackend = new()
        {
            title = "Set the scripting backend to IL2CPP for arm64.",
            checkPredicate = () =>
            {
                var isUsingIIL2CPP = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP;
                var isTargetingARM64 = PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64;
                return isUsingIIL2CPP && isTargetingARM64;
            },
            performFix = () =>
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }
        };

        public static readonly SpacesSetting Orientation = new()
        {
            title = "Set the default orientation to \"Landscape Left\".",
            tooltipMessage = "Only \"Landscape Left\" orientation is supported, when launching the application straight to the Viewer.",
            checkPredicate = () =>
            {
                if (SpacesSetup.IsFusionFeatureEnabled())
                {
                    return true;
                }

                if (OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<BaseRuntimeFeature>().LaunchAppOnViewer)
                {
                    var isUIOrientationLandscapeLeft = PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft;
                    return isUIOrientationLandscapeLeft;
                }

                return true;
            },
            performFix = () =>
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            }
        };

        public static readonly SpacesSetting MetaQuestForceRemoveInternet = new()
        {
            title = "[Optional] Uncheck 'Force Remove Internet Permission' from the Meta Quest Feature settings in OpenXR.",
            checkPredicate = () =>
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                var questFeature = settings.GetFeature<MetaQuestFeature>();
                if (!questFeature)
                {
                    return false;
                }
#if OPENXR_1_9_1_OR_NEWER
                return !questFeature.ForceRemoveInternetPermission;
#else
                var serializedFeature = new SerializedObject(questFeature);
                return !serializedFeature.FindProperty("forceRemoveInternetPermission").boolValue;
#endif
            },
            performFix = () =>
            {
#if OPENXR_1_9_1_OR_NEWER
                OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<MetaQuestFeature>().ForceRemoveInternetPermission = false;
#else
                var questFeature = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<MetaQuestFeature>();
                var serializedFeature = new SerializedObject(questFeature);
                serializedFeature.FindProperty("forceRemoveInternetPermission").boolValue = false;
                serializedFeature.ApplyModifiedProperties();
#endif
            }
        };

        public static readonly SpacesSetting SpacesDefineSymbol = new()
        {
            title = "[Optional] Add \"USING_SNAPDRAGON_SPACES_SDK\" to the scripting define symbols list if needed.",
            checkPredicate = () =>
            {
#if !USING_SNAPDRAGON_SPACES_SDK
                return false;
#else
                return true;
#endif
            },
            performFix = () =>
            {
#if UNITY_2021_3_OR_NEWER
                var target = NamedBuildTarget.Android;
                var newDefines = PlayerSettings.GetScriptingDefineSymbols(target);
                newDefines += ";USING_SNAPDRAGON_SPACES_SDK";
                PlayerSettings.SetScriptingDefineSymbols(target, newDefines);
#else
                var target = BuildTargetGroup.Android;
                var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
                defines += ";USING_SNAPDRAGON_SPACES_SDK";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
#endif
            }
        };

        public Func<bool> checkPredicate;
        public bool isSelected = true;
        public Action performFix;
        public string title;
        public string tooltipMessage = "";

        public bool failsCheck()
        {
            return !checkPredicate();
        }
    }
}
