/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class MRPassthroughStep : EditorWindow, ISpacesEditorWindow
    {
        private Button _nextButton;
        private Button _backgroundAlphaButton;

        private void OnEnable()
        {
            CreateGUI();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            // Title
            VisualElement titleLabel = new Label("Passthrough");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            VisualElement text = new Label("The session camera must have its Background alpha set to 0 in order to use and visualize Passthrough correctly on a Unity scene.");
            text.AddToClassList("wizard-text");
            root.Add(text);

            var buttonRowContainer = new VisualElement();
            buttonRowContainer.AddToClassList("row-container");
            root.Add(buttonRowContainer);

            _backgroundAlphaButton = new Button(SetCameraBackgroundAlphaToZero) { text = "Set Camera Background Alpha to 0" };
            _backgroundAlphaButton.SetEnabled(!IsCameraBackgroundAlphaZero());
            _backgroundAlphaButton.AddToClassList("wizard-button");
            buttonRowContainer.Add(_backgroundAlphaButton);
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            _nextButton = nextButton;
            nextButton.SetEnabled(false);
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var isBackgroundAlphaZero = IsCameraBackgroundAlphaZero();
            _backgroundAlphaButton.SetEnabled(!isBackgroundAlphaZero);
            _nextButton.SetEnabled(isBackgroundAlphaZero);
        }

        private void SetCameraBackgroundAlphaToZero()
        {
            var camera = FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("Camera not found");
                return;
            }
            var bgColor = camera.backgroundColor;
            camera.backgroundColor = new Color(bgColor.r, bgColor.g, bgColor.b, 0.0f);
            UpdateButtons();
        }

        private bool IsCameraBackgroundAlphaZero()
        {
            var bgColor = GetCameraBackgroundColor();
            return bgColor?.a.Equals(0.0f) ?? false;
        }

        private Color? GetCameraBackgroundColor()
        {
            var camera = FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                return null;
            }
            return camera.backgroundColor;
        }
    }
}
