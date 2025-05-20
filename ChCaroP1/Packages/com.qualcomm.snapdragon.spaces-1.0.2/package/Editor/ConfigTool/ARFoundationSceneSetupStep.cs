/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class ARFoundationSceneSetupStep : EditorWindow, ISpacesEditorWindow
    {
        private const string MainCameraTag = "MainCamera";

        private Button _addARSessionButton;
        private Button _addXROriginButton;
        private Button _disableARCameraBackgroundButton;
        private Button _disableARCameraManagerButton;
        private Button _nextButton;
        private Button _removeMainCameraButton;
        private TargetPlatform _targetPlatform;

        private void OnEnable()
        {
            CreateGUI();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            #region Title

            VisualElement titleLabel = new Label("AR Foundation Scene Setup");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            #endregion

            #region Remove Main Camera

            VisualElement text1 = new Label("1. Remove the Main Camera from the scene");
            text1.AddToClassList("wizard-text");
            root.Add(text1);

            var button1RowContainer = new VisualElement();
            button1RowContainer.AddToClassList("row-container");
            root.Add(button1RowContainer);

            _removeMainCameraButton = new Button(RemoveNonARFoundationMainCameras) { text = "Remove Main Camera" };
            _removeMainCameraButton.SetEnabled(IsNonARFoundationMainCameraPresentInScene());
            _removeMainCameraButton.AddToClassList("wizard-button");
            button1RowContainer.Add(_removeMainCameraButton);

            #endregion

            #region Add AR Session

            VisualElement text2 = new Label("2. Add AR Session to the scene");
            text2.AddToClassList("wizard-text");
            root.Add(text2);

            var button2RowContainer = new VisualElement();
            button2RowContainer.AddToClassList("row-container");
            root.Add(button2RowContainer);

            _addARSessionButton = new Button(AddARSession) { text = "Add AR Session" };
            _addARSessionButton.SetEnabled(!IsARSessionPresentInScene());
            _addARSessionButton.AddToClassList("wizard-button");
            button2RowContainer.Add(_addARSessionButton);

            #endregion

            #region Add XR Origin and Main Camera

            VisualElement text3 = new Label("3. Add XR Origin and Main Camera to the scene");
            text3.AddToClassList("wizard-text");
            root.Add(text3);

            var button3RowContainer = new VisualElement();
            button3RowContainer.AddToClassList("row-container");
            root.Add(button3RowContainer);

            _addXROriginButton = new Button(AddXROrigin) { text = "Add XR Origin and Main Camera" };
            _addXROriginButton.SetEnabled(!IsXROriginPresentInScene());
            _addXROriginButton.AddToClassList("wizard-button");
            button3RowContainer.Add(_addXROriginButton);

            #endregion

            #region Disable AR Camera Manager

            VisualElement text4 = new Label("4. Disable AR Camera Manager component from the Main Camera");
            text4.AddToClassList("wizard-text");
            root.Add(text4);

            var button4RowContainer = new VisualElement();
            button4RowContainer.AddToClassList("row-container");
            root.Add(button4RowContainer);

            _disableARCameraManagerButton = new Button(DisableARCameraManager) { text = "Disable AR Camera Manager" };
            _disableARCameraManagerButton.SetEnabled(IsARCameraManagerEnabled());
            _disableARCameraManagerButton.AddToClassList("wizard-button");
            button4RowContainer.Add(_disableARCameraManagerButton);

            var helpBox = new HelpBox("To avoid camera lifecycle related issues, disable the AR Camera Manager component in the Main Camera GameObject, unless is needed for using the Camera Frame Access Feature.", HelpBoxMessageType.Error);
            helpBox.AddToClassList("help-box");
            root.Add(helpBox);

            #endregion

            #region Disable AR Camera Background

            VisualElement text5 = new Label("5. Disable AR Camera Background component from the Main Camera");
            text5.AddToClassList("wizard-text");
            root.Add(text5);

            var button5RowContainer = new VisualElement();
            button5RowContainer.AddToClassList("row-container");
            root.Add(button5RowContainer);

            _disableARCameraBackgroundButton = new Button(DisableARCameraBackground) { text = "Disable AR Camera Background" };
            _disableARCameraBackgroundButton.SetEnabled(IsARCameraBackgroundEnabled());
            _disableARCameraBackgroundButton.AddToClassList("wizard-button");
            button5RowContainer.Add(_disableARCameraBackgroundButton);

            #endregion
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            _targetPlatform = targetPlatform;
            _nextButton = nextButton;
            UpdateButtons();
        }

        private void RemoveNonARFoundationMainCameras()
        {
            var mainCameraGameObjects = GameObject.FindGameObjectsWithTag(MainCameraTag).ToList();
            foreach (var mainCamera in mainCameraGameObjects)
            {
                var isARFoundationCamera = mainCamera.GetComponentInParent<XROrigin>() != null;
                if (!isARFoundationCamera)
                {
                    DestroyImmediate(mainCamera);
                }
            }

            UpdateButtons();
        }

        private void AddARSession()
        {
            EditorApplication.ExecuteMenuItem("GameObject/XR/AR Session");
            UpdateButtons();
        }

        private void AddXROrigin()
        {
            switch (_targetPlatform)
            {
                case TargetPlatform.AugmentedReality:
                    EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (AR)");
                    break;
                case TargetPlatform.MixedReality:
                    EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
                    break;
            }

            UpdateButtons();
        }

        private void DisableARCameraManager()
        {
            var mainCamera = GameObject.Find("Main Camera");
            if (mainCamera == null)
            {
                Debug.LogWarning("Main Camera not found");
                return;
            }

            var component = mainCamera.GetComponent<ARCameraManager>();
            if (component == null)
            {
                Debug.LogWarning("AR Camera Manager not found on Main Camera");
                return;
            }

            component.enabled = false;

            UpdateButtons();
        }

        private void DisableARCameraBackground()
        {
            var mainCamera = GameObject.Find("Main Camera");
            if (mainCamera == null)
            {
                Debug.LogWarning("Main Camera not found");
                return;
            }

            var component = mainCamera.GetComponent<ARCameraBackground>();
            if (component == null)
            {
                Debug.LogWarning("AR Camera Background not found on Main Camera");
                return;
            }

            component.enabled = false;

            UpdateButtons();
        }

        private static bool IsNonARFoundationMainCameraPresentInScene()
        {
            var cameraObjects = GameObject.FindGameObjectsWithTag(MainCameraTag).ToList();
            foreach (var mainCamera in cameraObjects)
            {
                var isARFoundationCamera = mainCamera.GetComponentInParent<XROrigin>() != null;
                if(!isARFoundationCamera)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsARSessionPresentInScene()
        {
            return FindAnyObjectByType<ARSession>() != null;
        }

        private static bool IsXROriginPresentInScene()
        {
            return FindAnyObjectByType<XROrigin>() != null;
        }

        private static bool IsARCameraManagerEnabled()
        {
            return FindAnyObjectByType<ARCameraManager>()?.enabled ?? false;
        }

        private static bool IsARCameraBackgroundEnabled()
        {
            return FindAnyObjectByType<ARCameraBackground>()?.enabled ?? false;
        }

        private static bool ARFoundationSceneChecksPass()
        {
            return
                IsARSessionPresentInScene() &&
                IsXROriginPresentInScene() &&
                !IsARCameraManagerEnabled() &&
                !IsARCameraBackgroundEnabled();
        }

        private void UpdateButtons()
        {
            _removeMainCameraButton.SetEnabled(IsNonARFoundationMainCameraPresentInScene());
            _addARSessionButton.SetEnabled(!IsARSessionPresentInScene());
            _addXROriginButton.SetEnabled(!IsXROriginPresentInScene());
            _disableARCameraManagerButton.SetEnabled(IsARCameraManagerEnabled());
            _disableARCameraBackgroundButton.SetEnabled(IsARCameraBackgroundEnabled());

            _nextButton.SetEnabled(ARFoundationSceneChecksPass());
        }
    }
}
