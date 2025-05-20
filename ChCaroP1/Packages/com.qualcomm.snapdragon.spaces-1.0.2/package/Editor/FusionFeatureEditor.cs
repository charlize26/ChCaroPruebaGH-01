/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    [CustomEditor(typeof(FusionFeature))]
    internal class FusionFeatureEditor : UnityEditor.Editor
    {
        private float _fixedSpaceWidth = 6f;
        private float _fixedToggleWidth = 15f;
        private SerializedProperty _validateOpenScene;
        private SerializedProperty _ignoreOpenxrVersion;

        private void OnEnable()
        {
            _validateOpenScene = serializedObject.FindProperty("ValidateOpenScene");
            _ignoreOpenxrVersion = serializedObject.FindProperty("IgnoreOpenXRVersion");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // Because the checkbox is directly appended to the label, a manual spacing is added to the default label width.
            var labelWidth = EditorGUIUtility.labelWidth;
            var maxElementWidth = Mathf.Max(
                EditorStyles.label.CalcSize(new GUIContent(_validateOpenScene.displayName)).x,
                EditorStyles.label.CalcSize(new GUIContent(_ignoreOpenxrVersion.displayName)).x
            );
            EditorGUIUtility.labelWidth = maxElementWidth + _fixedSpaceWidth + _fixedToggleWidth;

            bool oldValidationValue = _validateOpenScene.boolValue;
            bool oldIgnoreOpenxrVersion = _ignoreOpenxrVersion.boolValue;
            EditorGUILayout.PropertyField(_validateOpenScene);
            EditorGUILayout.PropertyField(_ignoreOpenxrVersion);

            // If the validate open scene checkbox is toggled, the feature validators are applied but do not visually appear in the Project Validation tab.
            // The recommendation in our documentation up to this point has been to find the FusionFeature.FeatureValidators script and right-click -> Reimport
            // If the checkbox has been toggled, do this programmatically instead.
            if (oldValidationValue != _validateOpenScene.boolValue || oldIgnoreOpenxrVersion != _ignoreOpenxrVersion.boolValue)
            {
                var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                var sourcefiles = assemblies
                    .SelectMany(assembly => assembly.sourceFiles)
                    .Where(file => !string.IsNullOrEmpty(file) && file.Contains("FusionFeature.FeatureValidators"));

                foreach (var file in sourcefiles)
                {
                    AssetDatabase.ImportAsset(file);
                }
            }
            EditorGUILayout.Space();

            // Reset the original Editor label width in order to avoid broken UI.
            EditorGUIUtility.labelWidth = labelWidth;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
