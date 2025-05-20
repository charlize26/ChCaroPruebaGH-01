/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.OpenXR;
#if !UNITY_EDITOR
using UnityEngine.XR.Management;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    ///     Utility class which unifies the checking of OpenXrFeature instances.
    /// </summary>
    public static class FeatureUseCheckUtility
    {
        /// <summary>
        ///     A list of all SpacesOpenXRFeatures which can be queried with this utility class.
        /// </summary>
        public enum SpacesFeature
        {
            BaseRuntime,
            SpatialAnchors,
            PlaneDetection,
            ImageTracking,
            HandTracking,
            HitTesting,
            FoveatedRendering,
            QrCodeTracking,
            SpatialMeshing,
            CameraAccess,
            CompositionLayers,
            PassthroughLayer,
            Fusion
        }

        private static readonly Dictionary<SpacesFeature, string> _featureLookup = new()
        {
            { SpacesFeature.BaseRuntime, nameof(BaseRuntimeFeature) },
            { SpacesFeature.SpatialAnchors, nameof(SpatialAnchorsFeature) },
            { SpacesFeature.PlaneDetection, nameof(PlaneDetectionFeature) },
            { SpacesFeature.ImageTracking, nameof(ImageTrackingFeature) },
            // cannot reference QCHT.Interactions.Core.HandTrackingFeature from Snapdragon.Spaces.Runtime assembly because it would create a cyclical dependency
            // otherwise this whole dictionary could refer to Type. Instead, this string is stored manually.
            { SpacesFeature.HandTracking, "HandTrackingFeature" },
            { SpacesFeature.HitTesting, nameof(HitTestingFeature) },
            { SpacesFeature.FoveatedRendering, nameof(FoveatedRenderingFeature) },
            { SpacesFeature.QrCodeTracking, nameof(QrCodeTrackingFeature) },
            { SpacesFeature.SpatialMeshing, nameof(SpatialMeshingFeature) },
            { SpacesFeature.CameraAccess, nameof(CameraAccessFeature) },
            { SpacesFeature.CompositionLayers, nameof(CompositionLayersFeature) },
            { SpacesFeature.PassthroughLayer, nameof(PassthroughLayerFeature) },
            { SpacesFeature.Fusion, nameof(FusionFeature) }
        };

        /// <summary>
        ///     Some checks may be imposed on all features as a result of another feature being enabled
        ///     (e.g. Fusion requires additional checks about the state of openXr, or other components in the scene when it is enabled).
        ///     These checks can be added to the ImposedFeatureChecks delegates created here.
        /// </summary>
        internal delegate void ImposedFeatureChecks(ref CheckResult checkResult);

        /// <summary>
        ///     Invoked when OpenXR is not running, and a feature was requested.
        ///     Can be used to log at runtime some additional useful information to a developer, such as that they avoid doing this, or how to check the scene is setup correctly.
        ///     The results of these feature checks are cached for the lifetime of the openXr loader and are assumed to not change at runtime except when openXr is destroyed / created.
        /// </summary>
        internal static ImposedFeatureChecks ImposeFeatureChecks_OpenXrNotRunning = null;

        private static readonly ConcurrentDictionary<Type, CheckResult> _cachedResults = new();
#if !UNITY_EDITOR
        // Do not access directly. This private field should be get/set from the private property IsLoaderLoaded.
        private static bool _isLoaderLoaded;

        private static bool IsLoaderLoaded
        {
            get => _isLoaderLoaded;
            set
            {
                if (_isLoaderLoaded == value)
                    return;

                _isLoaderLoaded = value;
                // Clear all cached results if the state of the loader changes
                _cachedResults.Clear();
            }
        }
#endif

#pragma warning disable CS0414
        private static readonly string _log_XrNotInitialised = "XR has not completed initialisation!";
        private static readonly string _log_NoActiveLoader = "No active XR loader exists!";
        private static readonly string _log_FeatureNotEnabled = "Feature is not enabled.";
#pragma warning restore CS0414

        internal class CheckResult
        {
            public bool Enabled { get; set; }

            private List<string> _diagnosticMessages;

            private readonly string _errorHeader;

            public readonly Type FeatureType;

            public CheckResult(Type featureType)
            {
                Enabled = true;
                FeatureType = featureType;
                _errorHeader = $"Failed to use feature of type {FeatureType} - (Diagnostic output is logged once per openXr session)";
                _diagnosticMessages = new List<string>();
            }

            public void LogResult(bool logDiagnosticMessages = false)
            {
                if (!Enabled)
                {
                    Debug.LogError(_errorHeader);

                    if (logDiagnosticMessages)
                    {
                        string finalMessage = "";
                        _diagnosticMessages.ForEach((message => finalMessage += message + "\n\n"));
                        Debug.LogWarning(finalMessage);
                    }
                }
            }

            public void AddDiagnosticMessage(string message)
            {
                _diagnosticMessages.Add(message);
            }
        }

        private static bool IsValid<TFeature>(TFeature feature)
            where TFeature : SpacesOpenXRFeature
        {
            // Immediate fail if the feature instance is null regardless of type
            if (!feature)
            {
#if !UNITY_EDITOR
                Debug.LogError($"Instance of feature of type {typeof(TFeature)} is null.");
#endif
                return false;
            }

            return true;
        }

        private static bool HasCachedResult<TFeature>(TFeature feature, out CheckResult result)
            where TFeature : SpacesOpenXRFeature
        {
            // If cached results exist, they will persist while the openxr loader remains in the same state - loaded or not loaded
            if (_cachedResults.TryGetValue(typeof(TFeature), out result))
            {
                result.LogResult();
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Check to see if an instance of an OpenXR feature is currently usable.
        /// </summary>
        /// <param name="feature">Instance of the feature to check.</param>
        /// <typeparam name="TFeature">Type of feature to be checked.</typeparam>
        /// <param name="suppressDiagnosticLogs">If true will suppress diagnostic logs from being made and fail silently instead. It is not recommended to suppress diagnostic logs.</param>
        /// <returns>True if the feature is usable: that is, the feature instance is non-null, the feature is enabled, OpenXr is running and (by default) there is a valid session.
        /// False if any of those conditions are not met. Additionally, once per session, this will output diagnostic logs (on a failure) attempting to explain why this did not succeed.
        /// This will (unless specific behaviour is overridden for the feature) return false for UNITY_EDITOR without logging.
        /// When handling a false result, consider carefully if you should log if in UNITY_EDITOR.
        /// </returns>
        public static bool IsFeatureUseable<TFeature>(TFeature feature, bool suppressDiagnosticLogs = false)
            where TFeature : SpacesOpenXRFeature
        {
            if (!IsValid(feature))
            {
                return false;
            }

            if (HasCachedResult(feature, out CheckResult result))
            {
                return result.Enabled && feature.OnCheckIsFeatureUseable();
            }

            CheckResult newResult = new CheckResult(typeof(TFeature));
            // In the editor checking for an active loader is pointless
#if !UNITY_EDITOR
            // If there are no cached results for this feature type
            // run all checks - this should happen only once while the openxr loader remains in the same state - loaded or not loaded
            // If/when the session ends or a new session starts these checks will be run once more
            var managerSettings = XRGeneralSettings.Instance.Manager;
            IsLoaderLoaded = managerSettings.isInitializationComplete && managerSettings.activeLoader;
            if (!IsLoaderLoaded)
            {
                newResult.Enabled = false;
                if (!managerSettings.isInitializationComplete)
                {
                    newResult.AddDiagnosticMessage(_log_XrNotInitialised);
                }

                if (!managerSettings.activeLoader)
                {
                    newResult.AddDiagnosticMessage(_log_NoActiveLoader);
                }

                ImposeFeatureChecks_OpenXrNotRunning?.Invoke(ref newResult);
            }
#endif
            bool isEnabled = IsFeatureEnabled_Unvalidated(feature, ref newResult);
            _cachedResults.TryAdd(typeof(TFeature), newResult);
            newResult.LogResult(!suppressDiagnosticLogs);

            return isEnabled && feature.OnCheckIsFeatureUseable();
            // NOTE: in UnityEditor this should cache a result with enabled == true, and not log anything
            // and then return false because there is not a valid openXR session (SessionHandle == 0 && SystemIDHandle == 0)
        }

        /// <summary>
        ///     Check to see if an OpenXR feature is currently usable.
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <param name="suppressDiagnosticLogs">If true will suppress diagnostic logs from being made and fail silently instead. It is not recommended to suppress diagnostic logs.</param>
        /// <returns>True if the feature is usable: that is, a non-null feature instance is found for the active build target, the feature is enabled, OpenXr is running and (by default) there is a valid session.
        /// False if any of those conditions are not met. Additionally, once per session, this will output diagnostic logs (on a failure) attempting to explain why this did not succeed.
        /// This will (unless specific behaviour is overridden for the feature) return false for UNITY_EDITOR without logging.
        /// When handling a false result, consider carefully if you should log if in UNITY_EDITOR.</returns>
        public static bool IsFeatureUseable(SpacesFeature feature, bool suppressDiagnosticLogs = false)
        {
            var foundFeature = OpenXRSettings.ActiveBuildTargetInstance.GetFeatures<SpacesOpenXRFeature>().First(Feature => Feature.name.Contains(_featureLookup[feature])) as SpacesOpenXRFeature;
            return IsFeatureUseable(foundFeature, suppressDiagnosticLogs);
        }

        /// <summary>
        ///     Check to see if an instance of an OpenXR feature is enabled.
        /// </summary>
        /// <param name="feature">Instance of the feature to check.</param>
        /// <param name="suppressDiagnosticLogs">If true will suppress diagnostic logs from being made and fail silently instead. It is not recommended to suppress diagnostic logs.</param>
        /// <typeparam name="TFeature">Type of feature to be checked.</typeparam>
        /// <returns>False if the feature is disabled. A feature can be disabled in two cases:
        /// (1) there is no intent to use the feature.
        /// (2) the feature is intended for use, but initialization failed!
        /// Returns true if the feature is intended for use, both if OpenXR is __not__ running, and if OpenXR __is__ running (and initialization succeeded).</returns>
        public static bool IsFeatureEnabled<TFeature>(TFeature feature, bool suppressDiagnosticLogs = false)
            where TFeature : SpacesOpenXRFeature
        {
            if (!IsValid(feature))
            {
                return false;
            }

            if (HasCachedResult(feature, out CheckResult result))
            {
                return result.Enabled;
            }

            CheckResult newResult = new CheckResult(typeof(TFeature));
            bool isEnabled = IsFeatureEnabled_Unvalidated(feature, ref newResult);
            _cachedResults.TryAdd(typeof(TFeature), newResult);
            newResult.LogResult(!suppressDiagnosticLogs);

            return isEnabled;
        }

        /// <summary>
        ///     Check to see if an OpenXR feature is enabled.
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <param name="suppressDiagnosticLogs">If true will suppress diagnostic logs from being made and fail silently instead. It is not recommended to suppress diagnostic logs.</param>
        /// <returns>False if the feature is disabled. A feature can be disabled in two cases:
        /// (1) there is no intent to use the feature.
        /// (2) the feature is intended for use, but initialization failed!
        /// Returns true if the feature is intended for use, both if OpenXR is __not__ running, and if OpenXR __is__ running (and initialization succeeded).</returns>
        public static bool IsFeatureEnabled(SpacesFeature feature, bool suppressDiagnosticLogs = false)
        {
#if UNITY_EDITOR
            var foundFeature = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeatures<SpacesOpenXRFeature>().First(Feature => Feature.name.Contains(_featureLookup[feature])) as SpacesOpenXRFeature;
#else
            var foundFeature = OpenXRSettings.ActiveBuildTargetInstance.GetFeatures<SpacesOpenXRFeature>().First(Feature => Feature.name.Contains(_featureLookup[feature])) as SpacesOpenXRFeature;
#endif
            return IsFeatureEnabled(foundFeature, suppressDiagnosticLogs);
        }

        private static bool IsFeatureEnabled_Unvalidated<TFeature>(TFeature feature, ref CheckResult result)
            where TFeature : SpacesOpenXRFeature
        {
            if (!feature.enabled)
            {
                result.Enabled = false;
                result.AddDiagnosticMessage(_log_FeatureNotEnabled);
            }

            return result.Enabled;
        }
    }
}
