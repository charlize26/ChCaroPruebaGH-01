/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    internal class SpacesLauncherSettings : ScriptableObject
    {
        private const string LauncherSettingsParentDirectory = "Assets/Editor";
        private const string LauncherSettingsPath = LauncherSettingsParentDirectory + "/SpacesLauncherSettings.asset";

        public static List<string> FeatureNames = new()
        {
            "Hand Tracking",
            "Eye Tracking",
            "Passthrough",
            "Controllers",
            "Room Scale"
        };

        private static SpacesLauncherSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SpacesLauncherSettings>(LauncherSettingsPath);
            if (settings != null)
            {
                return settings;
            }

            settings = CreateInstance<SpacesLauncherSettings>();
            if (!Directory.Exists(LauncherSettingsParentDirectory))
            {
                Directory.CreateDirectory(LauncherSettingsParentDirectory);
            }
            AssetDatabase.CreateAsset(settings, LauncherSettingsPath);
            AssetDatabase.SaveAssets();

            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

#pragma warning disable 0414 // CS0414: The field is assigned but its value is never used
        [SerializeField] private bool useHandTracking;

        [SerializeField] private bool requireHandTracking;

        [SerializeField] private bool useEyeTracking;

        [SerializeField] private bool requireEyeTracking;

        [SerializeField] private bool usePassthrough;

        [SerializeField] private bool requirePassthrough;

        [SerializeField] private bool useControllers;

        [SerializeField] private bool requireControllers;

        [SerializeField] private bool useRoomScale;

        [SerializeField] private bool requireRoomScale;
#pragma warning restore 0414
    }
}
