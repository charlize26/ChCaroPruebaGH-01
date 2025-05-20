/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class ProjectSettingsStep : EditorWindow, ISpacesEditorWindow
    {
        private static readonly List<SpacesSetting> _spacesXRSettings = new();
        private static readonly List<SpacesSetting> _spacesARSettings = new();
        private List<SpacesSetting> _activeSpacesSettings;
        private Button _resetButton;
        private ListView _settingsListView;
        private TargetPlatform _targetPlatform;

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

            // Title
            var titleLabel = new Label("Project Settings");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            var settingsListContainer = new VisualElement { name = "settingsListContainer" };
            root.Add(settingsListContainer);

            PopulateSettingsListView();

            var buttonsContainer = new VisualElement();
            buttonsContainer.AddToClassList("buttons-container");
            root.Add(buttonsContainer);

            // Add Refresh button
            var refreshButton = new Button(OnRefreshButtonClick) { text = "Refresh" };
            refreshButton.AddToClassList("wizard-button");
            refreshButton.SetEnabled(true);
            refreshButton.tooltip = "Refresh the project settings recommendations list to get an updated version";
            buttonsContainer.Add(refreshButton);

            // Add Reset button
            _resetButton = new Button(OnResetButtonClick) { text = "Reset" };
            _resetButton.AddToClassList("wizard-button");
            _resetButton.tooltip = "Reset changes made to the settings list above";
            _resetButton.SetEnabled(false);
            buttonsContainer.Add(_resetButton);

            // Add Apply button
            var applyButton = new Button(OnApplyButtonClick) { text = "Apply" };
            applyButton.AddToClassList("wizard-button");
            applyButton.tooltip = "Apply the settings selected above to the project settings";
            buttonsContainer.Add(applyButton);
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            nextButton.SetEnabled(true);
            _targetPlatform = targetPlatform;
            switch (_targetPlatform)
            {
                case TargetPlatform.AugmentedReality:
                    _activeSpacesSettings = _spacesARSettings;
                    break;
                case TargetPlatform.MixedReality:
                    _activeSpacesSettings = _spacesXRSettings;
                    break;
            }

            PopulateSettingsListView();
        }

        private void PopulateSettingsListView()
        {
            var root = rootVisualElement;
            var parentView = root.Q("settingsListContainer");
            if (_settingsListView != null)
            {
                parentView.Remove(_settingsListView);
                _settingsListView.Clear();
            }

            _settingsListView = CreateSettingsList();
            parentView.Add(_settingsListView);
        }

        private void OnRefreshButtonClick()
        {
            PopulateSettingsListView();
        }

        private void OnApplyButtonClick()
        {
            foreach (var setting in _activeSpacesSettings.Where(setting => setting.isSelected && setting.failsCheck()))
            {
                setting.performFix();
            }

            PopulateSettingsListView();
        }

        private void OnResetButtonClick()
        {
            foreach (var setting in _activeSpacesSettings.Where(setting => !setting.isSelected))
            {
                setting.isSelected = true;
            }

            PopulateSettingsListView();
            UpdateResetButton();
        }

        private void UpdateResetButton()
        {
            var anySettingDisabled = _activeSpacesSettings.Any(setting => !setting.isSelected);
            _resetButton.SetEnabled(anySettingDisabled);
        }

        private ListView CreateSettingsList()
        {
            PopulateSettings(_spacesARSettings, TargetPlatform.AugmentedReality);
            PopulateSettings(_spacesXRSettings, TargetPlatform.MixedReality);

            // Settings List
            var newListView = new ListView();
            newListView.AddToClassList("settings-list");
            newListView.makeItem = (Func<Toggle>)MakeItem;
            newListView.bindItem = BindItem;
            switch (_targetPlatform)
            {
                case TargetPlatform.AugmentedReality:
                    newListView.itemsSource = _spacesARSettings;
                    break;
                case TargetPlatform.MixedReality:
                    newListView.itemsSource = _spacesXRSettings;
                    break;
            }

            return newListView;

            // ========================================

            Toggle MakeItem()
            {
                var toggle = new Toggle();
                toggle.AddToClassList("settings-toggle");
                return toggle;
            }

            void BindItem(VisualElement element, int index)
            {
                var spacesSetting = _activeSpacesSettings[index];
                if (element is not Toggle toggle)
                {
                    return;
                }

                toggle.text = spacesSetting.title;
                toggle.tooltip = spacesSetting.tooltipMessage;
                toggle.value = spacesSetting.isSelected;
                toggle.SetEnabled(spacesSetting.failsCheck());
                toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
                {
                    spacesSetting.isSelected = evt.newValue;
                    UpdateResetButton();
                });
            }
        }

        private static void PopulateSettings(List<SpacesSetting> spacesSettings, TargetPlatform targetPlatform)
        {
            spacesSettings.Clear();
            switch (targetPlatform)
            {
                case TargetPlatform.AugmentedReality:
                    spacesSettings.Add(SpacesSetting.ARControllerProfile);
                    break;
                case TargetPlatform.MixedReality:
                    spacesSettings.Add(SpacesSetting.XRControllerProfile);
                    break;
            }

            spacesSettings.Add(SpacesSetting.GraphicsAPI);
            spacesSettings.Add(SpacesSetting.MinimumAndroidSDKVersion);
            spacesSettings.Add(SpacesSetting.ScriptingBackend);
            spacesSettings.Add(SpacesSetting.Orientation);
            spacesSettings.Add(SpacesSetting.MetaQuestForceRemoveInternet);
            spacesSettings.Add(SpacesSetting.SpacesDefineSymbol);
#if UNITY_2022_2_OR_NEWER
            spacesSettings.Add(SpacesSetting.TargetAndroidSDKVersion);
#endif
        }
    }
}
