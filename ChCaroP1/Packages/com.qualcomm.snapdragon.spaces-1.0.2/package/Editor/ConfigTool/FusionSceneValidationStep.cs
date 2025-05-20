/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class FusionSceneValidationStep : EditorWindow, ISpacesEditorWindow
    {
        private const string SettingsURL = "https://docs.spaces.qualcomm.com/unity/setup/dual-render-fusion-setup-guide#configure-dual-render-fusion-settings";
        private const string ComponentsURL = "https://docs.spaces.qualcomm.com/unity/setup/dual-render-fusion-components";

        private void OnEnable()
        {
            CreateGUI();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            #region Title

            VisualElement titleLabel = new Label("Dual Render Fusion Scene Validation");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            #endregion

            #region Enable scene validation

            VisualElement text = new Label("1. Enable scene validation for the current scene.");
            text.AddToClassList("wizard-text");
            root.Add(text);

            var buttonRowContainer = new VisualElement();
            buttonRowContainer.AddToClassList("row-container");
            root.Add(buttonRowContainer);

            var validateSceneButton = new Button(EnableSceneValidation) { text = "Validate currently open scene" };
            validateSceneButton.AddToClassList("wizard-button");
            buttonRowContainer.Add(validateSceneButton);

            #endregion

            #region Open the Project Validation Window

            VisualElement text2 = new Label("2. Open the Project Validation Window.");
            text2.AddToClassList("wizard-text");
            root.Add(text2);

            var buttonRowContainer2 = new VisualElement();
            buttonRowContainer2.AddToClassList("row-container");
            root.Add(buttonRowContainer2);

            var projectValidationButton = new Button(OpenProjectValidation) { text = "Open Project Validation Window" };
            projectValidationButton.AddToClassList("wizard-button");
            buttonRowContainer2.Add(projectValidationButton);

            #endregion

            #region Apply fixes

            VisualElement text3 = new Label("3. Apply the fixes shown in the project validation window.");
            text3.AddToClassList("wizard-text");
            root.Add(text3);

            var helpBox = new HelpBox("This step is not performed automatically. Please make sure you have manually applied the fixes.", HelpBoxMessageType.Warning);
            helpBox.AddToClassList("help-box");
            root.Add(helpBox);

            #endregion

            #region Disable Scene Validation

            VisualElement text4 = new Label("4. Disable Scene Validation");
            text4.AddToClassList("wizard-text");
            root.Add(text4);

            var buttonRowContainer3 = new VisualElement();
            buttonRowContainer3.AddToClassList("row-container");
            root.Add(buttonRowContainer3);

            var disableSceneValidationButton = new Button(DisableSceneValidation) { text = "Disable Scene Validation" };
            disableSceneValidationButton.AddToClassList("wizard-button");
            buttonRowContainer3.Add(disableSceneValidationButton);

            var disableSceneValidationText = new HelpBox("Leaving the setting enabled can cause problems in automated build environments. ", HelpBoxMessageType.Error);
            disableSceneValidationText.AddToClassList("help-box");
            root.Add(disableSceneValidationText);

            #endregion

            #region Docs

            var docsText = new HelpBox("For more information, please refer to the following documentation pages", HelpBoxMessageType.Info);
            docsText.AddToClassList("help-box");
            root.Add(docsText);

            var docsButtonRowContainer = new VisualElement();
            docsButtonRowContainer.AddToClassList("row-container");
            root.Add(docsButtonRowContainer);

            var docsButton = new Button(OnDualRenderFusionSettingsButtonClick) { text = "<u>Configure Dual Render Fusion Settings</u>" };
            docsButton.AddToClassList("docs-button");
            docsButtonRowContainer.Add(docsButton);

            var componentsButton = new Button(OnComponentsButtonClick) { text = "<u>Dual Render Fusion Components</u>" };
            componentsButton.AddToClassList("docs-button");
            docsButtonRowContainer.Add(componentsButton);

            #endregion
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            nextButton.SetEnabled(true);
        }

        private void EnableSceneValidation()
        {
            OpenXRSettings.ActiveBuildTargetInstance.GetFeature<FusionFeature>().ValidateOpenScene = true;
            EditorUtility.RequestScriptReload();
        }

        private void DisableSceneValidation()
        {
            OpenXRSettings.ActiveBuildTargetInstance.GetFeature<FusionFeature>().ValidateOpenScene = false;
            EditorUtility.RequestScriptReload();
        }

        private void OpenProjectValidation()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");
        }

        private void OnDualRenderFusionSettingsButtonClick()
        {
            Application.OpenURL(SettingsURL);
        }

        private void OnComponentsButtonClick()
        {
            Application.OpenURL(ComponentsURL);
        }
    }
}
