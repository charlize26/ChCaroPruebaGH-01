/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEditor;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    [CustomEditor(typeof(CameraAccessFeature))]
    public class CameraAccessFeatureEditor : UnityEditor.Editor
    {
        private SerializedProperty _directAccessConversion;
        private SerializedProperty _cpuFrameCacheSize;
        private SerializedProperty _highPriorityAsyncConversion;
        private SerializedProperty _cacheFrameBeforeAsyncConversion;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // Because the checkbox is directly appended to the label, a manual spacing is added to the default label width.
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth + 100;
            EditorGUILayout.PropertyField(_directAccessConversion);
            EditorGUILayout.PropertyField(_cpuFrameCacheSize);
            EditorGUILayout.PropertyField(_highPriorityAsyncConversion);
            EditorGUILayout.PropertyField(_cacheFrameBeforeAsyncConversion);
            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _directAccessConversion = serializedObject.FindProperty("DirectMemoryAccessConversion");
            _cpuFrameCacheSize = serializedObject.FindProperty("CpuFrameCacheSize");
            _highPriorityAsyncConversion = serializedObject.FindProperty("HighPriorityAsyncConversion");
            _cacheFrameBeforeAsyncConversion = serializedObject.FindProperty("CacheFrameBeforeAsyncConversion");
        }
    }
}
