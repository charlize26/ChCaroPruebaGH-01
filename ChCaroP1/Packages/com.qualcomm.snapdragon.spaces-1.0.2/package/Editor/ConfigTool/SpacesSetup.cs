/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    [InitializeOnLoad]
    public static class UnityConfigurationTool
    {
        private static readonly string CONFIG_TOOL_PRESENTED_KEY = Application.dataPath + "Config_Tool_Presented";

        static UnityConfigurationTool()
        {
            EditorApplication.update += OnInit;
        }

        private static bool ConfigToolHasBeenPresented
        {
            get => EditorPrefs.GetBool(CONFIG_TOOL_PRESENTED_KEY, false);
            set => EditorPrefs.SetBool(CONFIG_TOOL_PRESENTED_KEY, value);
        }

        private static void OnInit()
        {
            EditorApplication.update -= OnInit;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !ConfigToolHasBeenPresented)
            {
                SpacesSetup.ShowSpacesSetupWindow();
                ConfigToolHasBeenPresented = true;
            }
        }
    }

    public enum TargetPlatform
    {
        AugmentedReality,
        MixedReality
    }

    public class SpacesSetup : EditorWindow
    {
        private const String ARDocumentationURl = "https://docs.spaces.qualcomm.com/unity/setup/setup-guide";
        private const String MRDocumentationURl = "https://docs.spaces.qualcomm.com/unity/setup/setup-guide";
        private const String ARPlatformText = "Augmented Reality Project Setup";
        private const String XRPlatformText = "Mixed Reality Project Setup";

        private static readonly string TARGET_PLATFORM_KEY = Application.dataPath + "Target_Platform";

        private static List<ISpacesEditorWindow> _arSteps = new();
        private static List<ISpacesEditorWindow> _mrSteps = new();
        private static List<ISpacesEditorWindow> _steps = new();
        private static Button _nextButton;

        private int _currentStepIndex;
        private bool _docked;
        private VisualElement _mainContainer;
        private Image _platformIconImage;
        private Label _platformLabel;
        private Button _previousButton;
        private ScrollView _scrollView;
        private Sprite _spacesLogo;
        private Sprite _spacesWordmark;
        private Label _stepIndicator;
        private TargetPlatform _targetPlatform;
        private Sprite _arIcon;
        private Sprite _xrIcon;
        private readonly Color _lightModeIconTint = new Color(0.1f, 0.1f, 0.1f);
        private readonly Color _darkModeIconTint = Color.white;

        private static int MaxSteps => _steps.Count;
        private ISpacesEditorWindow CurrentStep => _steps[_currentStepIndex];
        private string NextButtonText => LastStepActive ? "Done" : "Next";
        private bool LastStepActive => _currentStepIndex == MaxSteps - 1;

        private static TargetPlatform LastSelectedTargetPlatform
        {
            get
            {
                var targetPlatformString = EditorPrefs.GetString(TARGET_PLATFORM_KEY, TargetPlatform.AugmentedReality.ToString());
                if (Enum.TryParse(targetPlatformString, true, out TargetPlatform lastSelectedTargetPlatform))
                {
                    return lastSelectedTargetPlatform;
                }

                return TargetPlatform.AugmentedReality;
            }

            set => EditorPrefs.SetString(TARGET_PLATFORM_KEY, value.ToString());
        }

        [MenuItem("Window/XR/Snapdragon Spaces/Configuration Tool")]
        public static void ShowSpacesSetupWindow()
        {
            var window = GetWindow<SpacesSetup>();
            window.titleContent = new GUIContent("Snapdragon Spaces");
            window.minSize = new Vector2(950, 800);
            window.maxSize = new Vector2(950, 800);
        }

        private void Update()
        {
            if (_docked == docked || _scrollView == null)
            {
                return;
            }

            _scrollView.horizontalScrollerVisibility = docked ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            _scrollView.verticalScrollerVisibility = docked ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            _docked = docked;
        }

        private void OnEnable()
        {
            _arSteps = new List<ISpacesEditorWindow>
            {
                CreateInstance<SpacesPluginStep>(),
                CreateInstance<ProjectSettingsStep>(),
                CreateInstance<SpacesFeaturesStep>(),
                CreateInstance<SpacesSamplesStep>(),
                CreateInstance<ARFoundationSceneSetupStep>()
            };
            AddFusionStepsConditionally();

            _mrSteps = new List<ISpacesEditorWindow>
            {
                CreateInstance<SpacesPluginStep>(),
                CreateInstance<ProjectSettingsStep>(),
                CreateInstance<SpacesFeaturesStep>(),
                CreateInstance<SpacesSamplesStep>(),
                CreateInstance<ARFoundationSceneSetupStep>(),
                CreateInstance<MRPassthroughStep>()
            };

            _targetPlatform = LastSelectedTargetPlatform;
            _steps = _targetPlatform == TargetPlatform.AugmentedReality ? _arSteps : _mrSteps;
        }

        private void OnDisable()
        {
            DestroyChildWindows();
        }

        private void OnDestroy()
        {
            DestroyChildWindows();
        }

        public void CreateGUI()
        {
            _spacesLogo = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.qualcomm.snapdragon.spaces/Editor/Resources/spacesLogo.png");
            _spacesWordmark = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.qualcomm.snapdragon.spaces/Editor/Resources/spacesWordmark.png");
            _arIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.qualcomm.snapdragon.spaces/Editor/Resources/arICon.png");
            _xrIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.qualcomm.snapdragon.spaces/Editor/Resources/xrIcon.png");

            // Root Window
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.qualcomm.snapdragon.spaces/Editor/ConfigTool/SpacesSetup.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            _scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scrollView.AddToClassList("scroll-box");
            rootVisualElement.Add(_scrollView);

            var rootContainer = new VisualElement();
            rootContainer.AddToClassList("root-container");
            _scrollView.Add(rootContainer);

            // Spaces Logo Container
            var logoContainer = new VisualElement();
            logoContainer.AddToClassList("logo-container");
            rootContainer.Add(logoContainer);

            var spacerLeft = new VisualElement();
            spacerLeft.AddToClassList("spacer");
            logoContainer.Add(spacerLeft);

            // Spaces Logo
            var spacesLogoImage = new Image { sprite = _spacesLogo };
            logoContainer.Add(spacesLogoImage);

            // Spaces Wordmark
            var spacesWordmark = new Image { sprite = _spacesWordmark };
            spacesWordmark.tintColor = EditorGUIUtility.isProSkin ? _darkModeIconTint : _lightModeIconTint;
            logoContainer.Add(spacesWordmark);

            var spacerRight = new VisualElement();
            spacerRight.AddToClassList("spacer");
            logoContainer.Add(spacerRight);

            // Platform Row Container
            var platformRowContainer = new VisualElement();
            platformRowContainer.AddToClassList("platform-row-container");
            rootContainer.Add(platformRowContainer);

            // Platform Label Container
            var platformLabelContainer = new VisualElement();
            platformLabelContainer.AddToClassList("platform-label-container");

            // Platform Icon
            _platformIconImage = new Image { sprite = _arIcon };
            _platformIconImage.scaleMode = ScaleMode.StretchToFill;
            _platformIconImage.tintColor = EditorGUIUtility.isProSkin ? _darkModeIconTint : _lightModeIconTint;
            _platformIconImage.AddToClassList("platform-icon");
            platformLabelContainer.Add(_platformIconImage);

            // Platform Label
            _platformLabel = new Label(_targetPlatform == TargetPlatform.AugmentedReality ? ARPlatformText : XRPlatformText);
            _platformLabel.AddToClassList("platform-label");
            platformLabelContainer.Add(_platformLabel);

            // Documentation Button
            var docsButton = new Button(OnDocumentationButtonClick) { text = "<u>Open Documentation</u>" };
            docsButton.AddToClassList("docs-button");
            platformLabelContainer.Add(docsButton);

            // Platform Picker
            var platformPicker = new EnumField("Select Target Platform", _targetPlatform);
            platformPicker.AddToClassList("platform-picker");
            platformRowContainer.Add(platformPicker);
            platformRowContainer.Add(platformLabelContainer);
            platformPicker.RegisterCallback<ChangeEvent<Enum>>(evt =>
            {
                OnPlatformChanged(evt.newValue);
            });

            // Main Container
            _mainContainer = new VisualElement();
            _mainContainer.AddToClassList("main-container");
            _mainContainer.AddToClassList(EditorGUIUtility.isProSkin ? "main-container-dark" : "main-container-light");
            rootContainer.Add(_mainContainer);

            // Bottom Row Container
            var bottomRowContainer = new VisualElement();
            bottomRowContainer.AddToClassList("row-container");
            rootContainer.Add(bottomRowContainer);

            // Add Skip button
            var skipButton = new Button(OnSetupLaterButtonClick) { text = "Setup Later" };
            skipButton.AddToClassList("wizard-button");
            skipButton.SetEnabled(true);
            skipButton.tooltip = "You can open this window again at anytime by going to Window > XR > Snapdragon Spaces > Configuration Tool";
            bottomRowContainer.Add(skipButton);

            // Bottom Row Spacer
            var bottomSpacer = new VisualElement();
            bottomSpacer.AddToClassList("spacer");
            bottomRowContainer.Add(bottomSpacer);

            // Add Previous button
            _previousButton = new Button(OnPreviousButtonClick) { text = "Previous" };
            _previousButton.AddToClassList("wizard-button");
            _previousButton.SetEnabled(_currentStepIndex > 0);
            bottomRowContainer.Add(_previousButton);

            // Step Indicator
            _stepIndicator = new Label($"{_currentStepIndex + 1} of {MaxSteps}");
            _stepIndicator.AddToClassList("step-indicator");
            bottomRowContainer.Add(_stepIndicator);

            // Add Next button
            _nextButton = new Button(OnNextButtonClick) { text = NextButtonText };
            _nextButton.AddToClassList("wizard-button");
            bottomRowContainer.Add(_nextButton);

            // Fill Main Container
            PopulateMainContainer();

            // =============================================
            return;

            void OnDocumentationButtonClick()
            {
                var url = _targetPlatform == TargetPlatform.AugmentedReality ? ARDocumentationURl : MRDocumentationURl;
                Application.OpenURL(url);
            }

            void OnSetupLaterButtonClick()
            {
                Debug.LogFormat("<color=orange>[Snapdragon Spaces Configuration Tool] To configure your project for Snapdragon Spaces, open the configuration tool by going to \"Window > XR > Snapdragon Spaces > Configuration Tool\" in the menu bar.</color>");
                Close();
            }

            void OnPreviousButtonClick()
            {
                SetStep(_currentStepIndex - 1);
            }

            void OnNextButtonClick()
            {
                if (CurrentStep is SpacesFeaturesStep &&
                    _targetPlatform == TargetPlatform.AugmentedReality)
                {
                    if (IsFusionFeatureEnabled())
                    {
                        AddFusionStepsConditionally();
                    }
                    else
                    {
                        RemoveFusionSteps();
                    }
                }

                if (LastStepActive)
                {
                    GetWindow<SpacesSetup>().Close();
                    return;
                }

                SetStep(_currentStepIndex + 1);
            }
        }

        public static bool IsFusionFeatureEnabled()
        {
            var activeBuildTargetInstance = OpenXRSettings.ActiveBuildTargetInstance;
            if (activeBuildTargetInstance == null)
            {
                return false;
            }

            var fusionFeature = activeBuildTargetInstance.GetFeature<FusionFeature>();
            return fusionFeature != null && fusionFeature.enabled;
        }

        private void AddFusionStepsConditionally()
        {
            if (!IsFusionFeatureEnabled())
            {
                return;
            }

            // Fusion Project Settings Step
            var indexOfFusionStep = GetIndexOfStep<DualRenderFusionStep>(_arSteps);
            if (indexOfFusionStep < 0)
            {
                var indexOfFeaturesStep = GetIndexOfStep<SpacesFeaturesStep>(_arSteps);
                if (indexOfFeaturesStep < 0)
                {
                    Debug.LogError("Spaces Features Step not found");
                }
                else
                {
                    _arSteps.Insert(indexOfFeaturesStep + 1, CreateInstance<DualRenderFusionStep>());
                }
            }

            // Fusion Scene Validation Step
            var indexOfFusionSceneValidationStep = GetIndexOfStep<FusionSceneValidationStep>(_arSteps);
            if (indexOfFusionSceneValidationStep < 0)
            {
                var indexOfARFoundationStep = GetIndexOfStep<ARFoundationSceneSetupStep>(_arSteps);
                if (indexOfARFoundationStep < 0)
                {
                    Debug.LogError("AR Foundation Step not found");
                }
                else
                {
                    _arSteps.Insert(indexOfARFoundationStep + 1, CreateInstance<FusionSceneValidationStep>());
                }
            }
        }

        private void RemoveFusionSteps()
        {
            // Fusion Project Settings Step
            var indexOfFusionStep = GetIndexOfStep<DualRenderFusionStep>(_arSteps);
            if (indexOfFusionStep > 0)
            {
                _arSteps.RemoveAt(indexOfFusionStep);
            }

            // Fusion Scene Validation Step
            var indexOfFusionSceneValidationStep = GetIndexOfStep<FusionSceneValidationStep>(_arSteps);
            if (indexOfFusionSceneValidationStep > 0)
            {
                _arSteps.RemoveAt(indexOfFusionSceneValidationStep);
            }
        }

        private int GetIndexOfStep<T>(List<ISpacesEditorWindow> targetList = null)
        {
            targetList ??= _targetPlatform == TargetPlatform.AugmentedReality ? _arSteps : _mrSteps;

            return targetList.FindIndex(Window => Window is T);
        }

        private void DestroyChildWindows()
        {
            if (_arSteps != null)
            {
                foreach (var step in _arSteps)
                {
                    DestroyImmediate(step as EditorWindow);
                }
            }

            if (_mrSteps != null)
            {
                foreach (var step in _mrSteps)
                {
                    DestroyImmediate(step as EditorWindow);
                }
            }

            if (_steps != null)
            {
                foreach (var step in _steps)
                {
                    DestroyImmediate(step as EditorWindow);
                }
            }
        }

        private void OnPlatformChanged(Enum newPlatform)
        {
            LastSelectedTargetPlatform = (TargetPlatform)newPlatform;
            switch (newPlatform)
            {
                case TargetPlatform.AugmentedReality:
                    _platformLabel.text = ARPlatformText;
                    _platformIconImage.sprite = _arIcon;
                    _targetPlatform = TargetPlatform.AugmentedReality;
                    _steps = _arSteps;
                    break;
                case TargetPlatform.MixedReality:
                    _platformLabel.text = XRPlatformText;
                    _platformIconImage.sprite = _xrIcon;
                    _targetPlatform = TargetPlatform.MixedReality;
                    _steps = _mrSteps;
                    break;
            }

            SetStep(0);
        }

        private void SetStep(int step)
        {
            if (step < 0 || step > MaxSteps - 1)
            {
                return;
            }

            _currentStepIndex = step;
            PopulateMainContainer();
            UpdateStepSection();
        }

        private void UpdateStepSection()
        {
            _previousButton.SetEnabled(_currentStepIndex > 0);
            _nextButton.text = NextButtonText;
            _stepIndicator.text = $"{_currentStepIndex + 1} of {MaxSteps}";
        }

        private void PopulateMainContainer()
        {
            _mainContainer.Clear();
            CurrentStep.Init(_targetPlatform, _nextButton);
            _mainContainer.Add((CurrentStep as EditorWindow)?.rootVisualElement);
        }
    }
}
