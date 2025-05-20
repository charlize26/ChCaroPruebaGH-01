/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.UIElements;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class SpacesPluginStep : EditorWindow, ISpacesEditorWindow
    {
        private Button _androidPlatformButton;
        private Button _nextButton;
        private Button _spacesExperimentalFeaturesButton;
        private Button _spacesFeaturesButton;

        private void OnEnable()
        {
            CreateGUI();
        }

        public void CreateGUI()
        {
            var rootElement = rootVisualElement;

            var root = new VisualElement();
            root.AddToClassList("page-container");
            rootElement.Add(root);

            #region Title

            VisualElement titleLabel = new Label("Enable Snapdragon Spaces OpenXR Plug-in");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            #endregion

            #region Switch Build Target to Android

            VisualElement text1 = new Label("1. Switch Build Target to Android");
            text1.AddToClassList("wizard-text");
            root.Add(text1);

            var button1RowContainer = new VisualElement();
            button1RowContainer.AddToClassList("row-container");
            root.Add(button1RowContainer);

            _androidPlatformButton = new Button(SetBuildTargetToAndroid) { text = "Switch Platform" };
            _androidPlatformButton.SetEnabled(!IsBuildTargetSetToAndroid());
            _androidPlatformButton.AddToClassList("wizard-button");
            button1RowContainer.Add(_androidPlatformButton);

            #endregion

            #region Open XR Plug-in Management

            VisualElement text2 = new Label("2. Open XR Plug-in Management under Project Settings");
            text2.AddToClassList("wizard-text");
            root.Add(text2);

            var button2RowContainer = new VisualElement();
            button2RowContainer.AddToClassList("row-container");
            root.Add(button2RowContainer);

            var button2 = new Button(OpenPluginManagementSettings) { text = "Open XR Plug-in Management" };
            button2.AddToClassList("wizard-button");
            button2RowContainer.Add(button2);

            #endregion

            #region Enable the OpenXR plug-in

            VisualElement text3 = new Label("3. Switch to the Android tab, and enable the OpenXR plug-in");
            text3.AddToClassList("wizard-text");
            root.Add(text3);

            var helpBox = new HelpBox("This step is not performed automatically. Please make sure you have manually enabled the OpenXR plug-in", HelpBoxMessageType.Warning);
            helpBox.AddToClassList("help-box");
            root.Add(helpBox);

            #endregion

            #region Enable Snapdragon Spaces feature group

            VisualElement text4 = new Label("4. Under the OpenXR plug-in, enable the Snapdragon Spaces feature group(s)");
            text4.AddToClassList("wizard-text");
            root.Add(text4);

            var button3RowContainer = new VisualElement();
            button3RowContainer.AddToClassList("row-container");
            root.Add(button3RowContainer);

            _spacesFeaturesButton = new Button { text = "Enable Snapdragon Spaces Feature Group" };
            _spacesFeaturesButton.SetEnabled(!AreSpacesFeaturesEnabled());
            _spacesFeaturesButton.AddToClassList("wizard-button");
            button3RowContainer.Add(_spacesFeaturesButton);

            var button4RowContainer = new VisualElement();
            button4RowContainer.AddToClassList("row-container");
            root.Add(button4RowContainer);

            _spacesExperimentalFeaturesButton = new Button { text = "Enable Snapdragon Spaces Experimental Feature Group" };

            _spacesExperimentalFeaturesButton.SetEnabled(!OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, "com.qualcomm.snapdragon.spaces.experimental").isEnabled);
            _spacesExperimentalFeaturesButton.AddToClassList("wizard-button");
            button4RowContainer.Add(_spacesExperimentalFeaturesButton);

            _spacesFeaturesButton.clicked += () =>
            {
                EnableSpacesFeatures();
                UpdateButtons();
            };
            _spacesExperimentalFeaturesButton.clicked += () =>
            {
                EnableSpacesExperimentalFeatures();
                UpdateButtons();
            };

            #endregion
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            _nextButton = nextButton;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _androidPlatformButton.SetEnabled(!IsBuildTargetSetToAndroid());
            _spacesFeaturesButton.SetEnabled(!AreSpacesFeaturesEnabled());
            _spacesExperimentalFeaturesButton.SetEnabled(!AreSpacesExperimentalFeaturesEnabled());
            _nextButton?.SetEnabled(IsBuildTargetSetToAndroid() && (AreSpacesFeaturesEnabled() || AreSpacesExperimentalFeaturesEnabled()));
        }

        private static bool IsBuildTargetSetToAndroid()
        {
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        }

        private static void SetBuildTargetToAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        private static bool AreSpacesFeaturesEnabled()
        {
            return OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, "com.qualcomm.snapdragon.spaces").isEnabled;
        }

        private static void EnableSpacesFeatures()
        {
            OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, "com.qualcomm.snapdragon.spaces").isEnabled = true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(BuildTargetGroup.Android);
        }

        private static bool AreSpacesExperimentalFeaturesEnabled()
        {
            return OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, "com.qualcomm.snapdragon.spaces.experimental").isEnabled;
        }

        private static void EnableSpacesExperimentalFeatures()
        {
            OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, "com.qualcomm.snapdragon.spaces.experimental").isEnabled = true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(BuildTargetGroup.Android);
        }

        private static void OpenPluginManagementSettings()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }
    }
}
