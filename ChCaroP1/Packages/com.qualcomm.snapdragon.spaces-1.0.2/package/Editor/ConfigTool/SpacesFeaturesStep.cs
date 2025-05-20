/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class SpacesFeaturesStep : EditorWindow, ISpacesEditorWindow
    {
        private Dictionary<Type, List<Type>> _activeFeaturesList;
        private ListView _experimentalFeaturesListView;
        private ListView _featuresListView;
        private Button _nextButton;
        private Dictionary<Type, List<Type>> _spacesExperimentalFeatures;
        private Dictionary<Type, List<Type>> _spacesFeatures;

        private void OnEnable()
        {
            CreateGUI();
        }

        public void CreateGUI()
        {
            _spacesFeatures = new Dictionary<Type, List<Type>>
            {
                { typeof(BaseRuntimeFeature), null },
                { typeof(FoveatedRenderingFeature), new List<Type> { typeof(BaseRuntimeFeature) } },
                { typeof(PlaneDetectionFeature), new List<Type> { typeof(BaseRuntimeFeature) } },
                { typeof(HitTestingFeature), new List<Type> { typeof(BaseRuntimeFeature) } },
                { typeof(ImageTrackingFeature), new List<Type> { typeof(BaseRuntimeFeature) } },
                { typeof(SpatialAnchorsFeature), new List<Type> { typeof(BaseRuntimeFeature) } },
                { typeof(QrCodeTrackingFeature), new List<Type> { typeof(BaseRuntimeFeature) } },
                { typeof(SpatialMeshingFeature), new List<Type> { typeof(BaseRuntimeFeature) } },
                { typeof(CameraAccessFeature), new List<Type> { typeof(BaseRuntimeFeature) } }
            };
#if QCHT_UNITY_CORE
            _spacesFeatures.Add(typeof(QCHT.Interactions.Core.HandTrackingFeature), new List<Type> {typeof(BaseRuntimeFeature)});
#endif
            _spacesExperimentalFeatures = new Dictionary<Type, List<Type>>
            {
                {typeof(CompositionLayersFeature), new List<Type> {typeof(BaseRuntimeFeature)}}
            };

            var root = rootVisualElement;

            // Title
            VisualElement titleLabel = new Label("Enable Snapdragon Spaces Features");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            var featuresListContainer = new VisualElement { name = "featuresListContainer" };
            root.Add(featuresListContainer);

            PopulateFeaturesLists();
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            _nextButton = nextButton;
            _nextButton.SetEnabled(false);

            if (targetPlatform == TargetPlatform.AugmentedReality && !_spacesFeatures.ContainsKey(typeof(FusionFeature)))
            {
                _spacesFeatures.Add(typeof(FusionFeature), new List<Type> { typeof(BaseRuntimeFeature) });
            }

            PopulateFeaturesLists();
            UpdateNextButton();
        }

        private void UpdateNextButton()
        {
            _nextButton.SetEnabled(OpenXRSettings.ActiveBuildTargetInstance.GetFeature<BaseRuntimeFeature>().enabled);
        }

        private void PopulateFeaturesLists()
        {
            var root = rootVisualElement;
            var parentView = root.Q("featuresListContainer");
            parentView.Clear();
            _featuresListView?.Clear();
            _experimentalFeaturesListView?.Clear();

            // Features List
            _featuresListView = new ListView();
            _featuresListView.AddToClassList("settings-list");
            _featuresListView.makeItem = (Func<Toggle>)MakeItem;
            _featuresListView.bindItem = BindSpacesFeatures;
            _featuresListView.itemsSource = _spacesFeatures.Keys.ToList();
            parentView.Add(_featuresListView);

            // Title
            VisualElement experimentalTitleLabel = new Label("Enable Snapdragon Spaces Experimental Features");
            experimentalTitleLabel.AddToClassList("title-label");
            parentView.Add(experimentalTitleLabel);

            // Experimental Features List
            _experimentalFeaturesListView = new ListView();
            _experimentalFeaturesListView.AddToClassList("settings-list");
            _experimentalFeaturesListView.makeItem = (Func<Toggle>)MakeItem;
            _experimentalFeaturesListView.bindItem = BindSpacesExperimentalFeatures;
            _experimentalFeaturesListView.itemsSource = _spacesExperimentalFeatures.Keys.ToList();
            parentView.Add(_experimentalFeaturesListView);
            return;

            // =======================================
            Toggle MakeItem()
            {
                var toggle = new Toggle();
                toggle.AddToClassList("settings-toggle");
                return toggle;
            }

            void BindSpacesFeatures(VisualElement element, int index)
            {
                _activeFeaturesList = _spacesFeatures;
                BindItem(element, index);
            }

            void BindSpacesExperimentalFeatures(VisualElement element, int index)
            {
                _activeFeaturesList = _spacesExperimentalFeatures;
                BindItem(element, index);
            }

            void BindItem(VisualElement element, int index)
            {
                var featureType = _activeFeaturesList.Keys.ToList()[index];
                var dependencies = _activeFeaturesList.Values.ToList()[index];
                if (element is not Toggle toggle)
                {
                    return;
                }

                var featureField = featureType.GetField("FeatureName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (featureField != null)
                {
                    var featureName = (string)featureField.GetRawConstantValue();
                    toggle.text = featureName;
                    var isFeatureEnabled = IsFeatureEnabled(featureType);
                    toggle.value = isFeatureEnabled;

                    if (featureName == BaseRuntimeFeature.FeatureName)
                    {
                        toggle.SetEnabled(!isFeatureEnabled);
                        toggle.text += " (required)";
                    }
                    else
                    {
                        toggle.SetEnabled(AreAllDependenciesEnabled(dependencies));
                    }

                    toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
                    {
                        SetFeatureEnabled(featureType, evt.newValue);
                        PopulateFeaturesLists();
                        UpdateNextButton();
                    });
                }
                else
                {
                    var featureName = featureType.Name;
                    toggle.text = featureName;
                    toggle.value = false;
                    toggle.SetEnabled(false);
                }
            }
        }

        private bool AreAllDependenciesEnabled(IEnumerable<Type> dependencies)
        {
            return dependencies == null || dependencies.All(IsFeatureEnabled);
        }

        private OpenXRFeature GetFeatureFromType(Type featureType)
        {
            var getFeatureMethod = typeof(OpenXRSettings).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => m.Name == "GetFeature" && m.GetParameters().Length == 0 && m.IsGenericMethod);
            if (getFeatureMethod == null)
            {
                return null;
            }

            var genericGetFeature = getFeatureMethod.MakeGenericMethod(featureType);
            var feature = (OpenXRFeature)genericGetFeature.Invoke(OpenXRSettings.ActiveBuildTargetInstance, null);
            return feature;
        }

        private bool IsFeatureEnabled(Type featureType)
        {
            return GetFeatureFromType(featureType)?.enabled ?? false;
        }

        private void SetFeatureEnabled(Type featureType, bool enabled)
        {
            var feature = GetFeatureFromType(featureType);
            if (feature != null)
            {
                feature.enabled = enabled;
            }
        }
    }
}
