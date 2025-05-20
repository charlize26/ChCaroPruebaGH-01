/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class DualRenderFusionStep : EditorWindow, ISpacesEditorWindow
    {
        private const string DocsURL = "https://docs.spaces.qualcomm.com/unity/setup/dual-render-fusion-setup-guide";
        private const string MigrationGuideURL = "https://docs.spaces.qualcomm.com/unity/setup/dual-render-fusion-migration-guide";
        private Button _nextButton;

        private void OnEnable()
        {
            CreateGUI();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            #region Title

            var titleLabel = new Label("Dual Render Fusion Setup");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            var buttonRowContainer = new VisualElement();
            buttonRowContainer.AddToClassList("row-container");
            root.Add(buttonRowContainer);

            var setupFusionButton = new Button(OnSetupFusionButtonClick) { text = "Configure project for Dual Render Fusion" };
            setupFusionButton.AddToClassList("wizard-button");
            buttonRowContainer.Add(setupFusionButton);

            var stepsContainer = new VisualElement();
            root.Add(stepsContainer);

            var text = new Label("Enabling Dual Render Fusion support will make the following changes to the project settings:");
            text.AddToClassList("wizard-text");
            stepsContainer.Add(text);

            #endregion

            #region Android Settings

            var step1 = new Label("Under <i>Project Settings > XR Plug-In Management > (Android settings tab)</i>");
            step1.AddToClassList("wizard-text");
            stepsContainer.Add(step1);

            var step1a = new Label("  \u2022 <b>Initialize XR on Startup</b> will be <b>disabled</b>");
            step1a.AddToClassList("wizard-text");
            stepsContainer.Add(step1a);

            #endregion

            #region Base Runtime feature settings

            var step2 = new Label("Under <i>Project Settings > XR Plug-In Management > OpenXR > Base Runtime feature settings (gear icon)</i>");
            step2.AddToClassList("wizard-text");
            stepsContainer.Add(step2);

            var step2a = new Label("  \u2022 <b>Launch App On Viewer</b> will be <b>disabled</b>");
            step2a.AddToClassList("wizard-text");
            stepsContainer.Add(step2a);

            var step2b = new Label("  \u2022 <b>Launch Controller On Host</b> will be <b>disabled</b>");
            step2b.AddToClassList("wizard-text");
            stepsContainer.Add(step2b);

            var step2c = new Label("  \u2022 <b>Export Headless</b> will be <b>disabled</b>");
            step2c.AddToClassList("wizard-text");
            stepsContainer.Add(step2c);

            #endregion

            #region Player Settings

            var step3 = new Label("Under <i>Project Settings > Player > Other Settings > Configuration</i>");
            step3.AddToClassList("wizard-text");
            stepsContainer.Add(step3);

            var step3a = new Label("  \u2022 <b>Active Input Handling</b> will be set to <b>both</b>");
            step3a.AddToClassList("wizard-text");
            stepsContainer.Add(step3a);

            #endregion

            #region Docs Help Box

            var docsContainer = new VisualElement();
            docsContainer.AddToClassList("docs-container");
            stepsContainer.Add(docsContainer);

            var docsText = new HelpBox("For more information, refer to the Dual Render Fusion Setup Guide on the documentation website", HelpBoxMessageType.Info);
            docsText.AddToClassList("help-box");
            docsContainer.Add(docsText);

            var migrationText = new HelpBox("If an old version of Dual Render Fusion package was previously installed, please follow the migration guide", HelpBoxMessageType.Warning);
            migrationText.AddToClassList("help-box");
            docsContainer.Add(migrationText);

            var docsButtonRowContainer = new VisualElement();
            docsButtonRowContainer.AddToClassList("row-container");
            docsContainer.Add(docsButtonRowContainer);

            var docsButton = new Button(OnDocumentationButtonClick) { text = "<u>Open Dual Render Fusion Setup Guide</u>" };
            docsButton.AddToClassList("docs-button");
            docsButtonRowContainer.Add(docsButton);

            var migrationButton = new Button(OnMigrationButtonClick) { text = "<u>Open Dual Render Fusion Migration Guide</u>" };
            migrationButton.AddToClassList("docs-button");
            docsButtonRowContainer.Add(migrationButton);

            #endregion

            // =============================================
            return;

            void OnSetupFusionButtonClick()
            {
                FusionProjectSettingsHelper.ConfigureFusionProject();
                _nextButton.SetEnabled(true);
            }

            void OnDocumentationButtonClick()
            {
                Application.OpenURL(DocsURL);
            }

            void OnMigrationButtonClick()
            {
                Application.OpenURL(MigrationGuideURL);
            }
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            _nextButton = nextButton;
            nextButton.SetEnabled(false);
        }
    }
}
