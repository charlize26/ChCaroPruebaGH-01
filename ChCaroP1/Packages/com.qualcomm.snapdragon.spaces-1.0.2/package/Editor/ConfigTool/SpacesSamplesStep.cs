/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class SpacesSamplesStep : EditorWindow, ISpacesEditorWindow
    {
        private const string SpacesPackageName = "Snapdragon Spaces";
        private const string FusionSampleName = "Fusion Samples";
        private PackageInfo _spacesPackage;
        private TargetPlatform _targetPlatform;

        private void OnEnable()
        {
            CreateGUI();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            // Title
            VisualElement titleLabel = new Label("Import Snapdragon Spaces Samples");
            titleLabel.AddToClassList("title-label");
            root.Add(titleLabel);

            // Samples List Container
            var samplesListContainer = new VisualElement { name = "samplesListContainer" };
            root.Add(samplesListContainer);

            PopulateSamples();
        }

        public void Init(TargetPlatform targetPlatform, Button nextButton)
        {
            nextButton.SetEnabled(true);
            _targetPlatform = targetPlatform;
            PopulateSamples();
        }

        private void PopulateSamples()
        {
            var root = rootVisualElement;
            var samplesListContainer = root.Q("samplesListContainer");
            samplesListContainer.Clear();
            foreach (var sample in LoadSpacesSamples())
            {
                if (sample.displayName == FusionSampleName && _targetPlatform == TargetPlatform.MixedReality)
                {
                    continue;
                }

                var sampleName = new Label(sample.displayName);
                sampleName.AddToClassList("sample-name");
                samplesListContainer.Add(sampleName);

                var sampleDescription = new Label(sample.description);
                sampleDescription.AddToClassList("sample-description");
                samplesListContainer.Add(sampleDescription);

                var buttonRowContainer = new VisualElement();
                buttonRowContainer.AddToClassList("row-container");
                samplesListContainer.Add(buttonRowContainer);

                var importButton = new Button(OnImportButtonClicked) { text = sample.isImported ? $"Reimport {sample.displayName}" : $"Import {sample.displayName}" };
                if (sample.displayName == FusionSampleName && !SpacesSetup.IsFusionFeatureEnabled())
                {
                    importButton.tooltip = "Dual Render Fusion is not enabled";
                    importButton.SetEnabled(false);
                }

                importButton.AddToClassList("wizard-button");
                buttonRowContainer.Add(importButton);

                // ============================
                continue;

                void OnImportButtonClicked()
                {
                    var sourcePath = $"Packages/com.qualcomm.snapdragon.spaces/Samples~/{sample.displayName}";
                    if (!Directory.Exists(sourcePath))
                    {
                        Debug.LogError($"Could not find Sample: {sample.displayName}");
                        return;
                    }

                    var parentDirectory = $"{Application.dataPath}/Samples/{_spacesPackage.displayName}/{_spacesPackage.version}/";
                    if (!Directory.Exists(parentDirectory))
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }

                    var destinationPath = parentDirectory + sample.displayName;
                    if (Directory.Exists(destinationPath))
                    {
                        Directory.Delete(destinationPath, true);
                    }

                    FileUtil.CopyFileOrDirectory(sourcePath, destinationPath);
                    AssetDatabase.Refresh();
                }
            }
        }

        private List<Sample> LoadSpacesSamples()
        {
            _spacesPackage = PackageInfo.GetAllRegisteredPackages().Where(Package => Package.displayName == SpacesPackageName).ToList().First();
            if (_spacesPackage == null)
            {
                return null;
            }

            return Sample.FindByPackage(_spacesPackage.name, _spacesPackage.version).ToList();
        }
    }
}
