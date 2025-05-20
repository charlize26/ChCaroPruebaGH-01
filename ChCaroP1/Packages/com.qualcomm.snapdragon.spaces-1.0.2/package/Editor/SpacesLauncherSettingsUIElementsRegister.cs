/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    internal static class SpacesLauncherSettingsUIElementsRegister
    {
        [SettingsProvider]
        public static SettingsProvider SpacesLauncherSettingsProvider()
        {
            var provider = new SettingsProvider("Project/SnapdragonSpacesLauncherSettings", SettingsScope.Project)
            {
                label = "Snapdragon Spaces Launcher Settings",
                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = SpacesLauncherSettings.GetSerializedSettings();

                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.qualcomm.snapdragon.spaces/Editor/SpacesLauncherSettings.uss");
                    rootElement.styleSheets.Add(styleSheet);

                    var mainContainer = new VisualElement();
                    mainContainer.AddToClassList("main-container");
                    rootElement.Add(mainContainer);

                    var title = new Label { text = "Launcher Settings" };
                    title.AddToClassList("title");
                    mainContainer.Add(title);

                    var descriptionText = "By declaring the features used by your app, a <uses-feature> element is";
                    descriptionText += " added to the Android Manifest, which is used to inform any external entity";
                    descriptionText += " of the set of hardware and software features your application can support.";
                    descriptionText += "\n\n";
                    descriptionText += "By declaring that a feature is required by your app, you enable launchers";
                    descriptionText += " and app stores to present your app to only those devices that meet it's hardware";
                    descriptionText += " and software requirements.";
                    descriptionText += "\n\n";
                    descriptionText += "If a feature is declared as used, but is not marked as required, it is expected";
                    descriptionText += " that the application can function normally in the absence of that feature";
                    var description = new HelpBox { text = descriptionText };
                    mainContainer.Add(description);

                    var saveReminder = new HelpBox { text = "Make sure to save (Ctrl + S) after changing the below values", messageType = HelpBoxMessageType.Warning};
                    mainContainer.Add(saveReminder);

                    var propertiesContainer = new VisualElement();
                    propertiesContainer.AddToClassList("properties-container");
                    mainContainer.Add(propertiesContainer);

                    var topRowContainer = new VisualElement();
                    topRowContainer.AddToClassList("row-container");
                    propertiesContainer.Add(topRowContainer);

                    var featureNameLabel = new Label { text = "Feature" };
                    featureNameLabel.AddToClassList("grid-title");
                    featureNameLabel.AddToClassList("grid-cell");
                    topRowContainer.Add(featureNameLabel);

                    var useFeatureLabel = new Label { text = "Use Feature" };
                    useFeatureLabel.AddToClassList("grid-title");
                    useFeatureLabel.AddToClassList("grid-cell");
                    topRowContainer.Add(useFeatureLabel);

                    var requireFeatureLabel = new Label { text = "Require Feature" };
                    requireFeatureLabel.AddToClassList("grid-title");
                    requireFeatureLabel.AddToClassList("grid-cell");
                    topRowContainer.Add(requireFeatureLabel);

                    foreach (var featureName in SpacesLauncherSettings.FeatureNames)
                    {
                        createTogglePair(featureName);
                    }

                    void createTogglePair(string name)
                    {
                        var usePropertyName = "use" + name.Replace(" ", "");
                        var requirePropertyName = "require" + name.Replace(" ", "");

                        switch (name)
                        {
                            case "Hand Tracking":
#if QCHT_UNITY_CORE
                                var isHandTrackingEnabled = OpenXRSettings.ActiveBuildTargetInstance.GetFeature<QCHT.Interactions.Core.HandTrackingFeature>().enabled;
                                if (!isHandTrackingEnabled)
                                {
                                    settings.FindProperty(usePropertyName).boolValue = false;
                                    settings.ApplyModifiedPropertiesWithoutUndo();
                                    return;
                                }
#else
                                settings.FindProperty(usePropertyName).boolValue = false;
                                settings.ApplyModifiedPropertiesWithoutUndo();
                                return;
#endif
                                break;

                            case "Eye Tracking":
                                var isEyeTrackingEnabled = OpenXRSettings.ActiveBuildTargetInstance.GetFeature<EyeGazeInteraction>().enabled;
                                if (!isEyeTrackingEnabled)
                                {
                                    settings.FindProperty(usePropertyName).boolValue = false;
                                    settings.ApplyModifiedPropertiesWithoutUndo();
                                    return;
                                }

                                break;
                        }

                        var tooltips = GetTooltips(name);

                        var rowContainer = new VisualElement();
                        rowContainer.AddToClassList("row-container");
                        propertiesContainer.Add(rowContainer);

                        var featureLabel = new Label(name);
                        featureLabel.AddToClassList("grid-cell");
                        rowContainer.Add(featureLabel);

                        var useField = new Toggle();
                        useField.AddToClassList("grid-cell");
                        useField.value = settings.FindProperty(usePropertyName).boolValue;
                        useField.tooltip = tooltips.Item1;
                        rowContainer.Add(useField);

                        var requireField = new Toggle();
                        requireField.AddToClassList("grid-cell");
                        requireField.value = settings.FindProperty(requirePropertyName).boolValue;
                        requireField.SetEnabled(useField.value);
                        requireField.tooltip = tooltips.Item2;
                        rowContainer.Add(requireField);

                        useField.RegisterCallback<ChangeEvent<bool>>(evt =>
                        {
                            if (!evt.newValue)
                            {
                                requireField.value = false;
                            }

                            requireField.SetEnabled(evt.newValue);

                            // save scriptable object
                            settings.FindProperty(usePropertyName).boolValue = evt.newValue;
                            settings.ApplyModifiedPropertiesWithoutUndo();
                        });

                        requireField.RegisterCallback<ChangeEvent<bool>>(evt =>
                        {
                            // save scriptable object
                            settings.FindProperty(requirePropertyName).boolValue = evt.newValue;
                            settings.ApplyModifiedPropertiesWithoutUndo();
                        });
                    }
                },
                keywords = new HashSet<string>(new[] { "Launcher", "Require Features" })
            };

            return provider;
        }

        private static Tuple<string, string> GetTooltips(string featureName)
        {
            switch (featureName)
            {
                case "Hand Tracking":
                    return Tuple.Create("Declares the hand tracking feature is used by the application.",
                        "Indicates that the application can't function, or isn't designed to function, if the hand tracking feature is not supported on the device.");
                case "Eye Tracking":
                    return Tuple.Create("Declares the eye tracking feature is used by the application.",
                        "Indicates that the application can't function, or isn't designed to function, if the eye tracking feature is not supported on the device.");
                case "Passthrough":
                    return Tuple.Create("Declares the passthrough feature is used by the application.",
                        "Indicates that the application can't function, or isn't designed to function, if the passthrough feature is not supported.");
                case "Controllers":
                    return Tuple.Create("Declares the controllers are used by the application.",
                        "Indicates that the application can't function, or isn't designed to function, if the device doesn't have controllers.");
                case "Room Scale":
                    return Tuple.Create("Declares the room scale feature is used by the application.",
                        "Indicates that the application can't function, or isn't designed to function, if the user did not setup their environment to support room scale experiences.");
            }

            return Tuple.Create("", "");
        }
    }
}
