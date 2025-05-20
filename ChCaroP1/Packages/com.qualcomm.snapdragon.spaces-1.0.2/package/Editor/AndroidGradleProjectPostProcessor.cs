/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public class AndroidGradleProjectPostProcessor : IPostGenerateGradleAndroidProject
    {
        public class XmlAttributeContainer
        {
            public string Name;
            public string Value;
            public string Prefix;

            public XmlAttributeContainer(string name, string value)
            {
                Name = name;
                Value = value;
                Prefix = "android";
            }

            public XmlAttributeContainer(string name, string value, string prefix)
            {
                Name = name;
                Value = value;
                Prefix = prefix;
            }
        }

        public int callbackOrder => 0;

        public XmlNamespaceManager _namespaceManager;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings == null)
            {
                return;
            }

            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var manifest = ReadXmlDocument(manifestPath);
            if (manifest == null)
            {
                return;
            }
            _namespaceManager = new XmlNamespaceManager(manifest.NameTable);
            _namespaceManager.AddNamespace("android", "http://schemas.android.com/apk/res/android");
            _namespaceManager.AddNamespace("tools", "http://schemas.android.com/tools");

            var isOpenXRLoaderActive = settings.Manager.activeLoaders?.Any(loader => loader.GetType() == typeof(OpenXRLoader));
            var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var baseRuntimeFeature = openXRSettings?.GetFeature<BaseRuntimeFeature>();
            var fusionFeature = openXRSettings?.GetFeature<FusionFeature>();
            var cameraFeature = openXRSettings?.GetFeature<CameraAccessFeature>();
            if (isOpenXRLoaderActive == true && baseRuntimeFeature != null && baseRuntimeFeature.enabled)
            {
                ModifyArchiveInBuildGradle(true, path, "SpacesServicesHelper");
                ModifyArchiveInBuildGradle(true, path, "libopenxr_loader");
                SetUsesFeatures(manifest, true);

                CheckMinApiVersion(openXRSettings, manifest);
                if (fusionFeature != null && fusionFeature.enabled)
                {
                    ModifyArchiveInBuildGradle(true, path, "SpacesActivities");
                    ModifyGradleProjectForHostController(true, path);
                    ModifySpacesLauncherLaunchCategory(manifest, false, "com.qualcomm.snapdragon.spaces.splashscreen.SplashScreenActivity");
#if UNITY_2023_1_OR_NEWER
                    switch (PlayerSettings.Android.applicationEntry)
                    {
                        case AndroidApplicationEntry.Activity:
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(true, path, "UnityPlayerActivity.java");
                            break;
                        case AndroidApplicationEntry.GameActivity:
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(true, path, "UnityPlayerGameActivity.java");
                            break;
                        default:
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(true, path, "UnityPlayerActivity.java");
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(true, path, "UnityPlayerGameActivity.java");
                            break;
                    }
#else
                    ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                    ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                    ModifyPlayerActivityWindowFocusChangeBehaviour(true, path, "UnityPlayerActivity.java");
#endif

                    SetMetaData(manifest, false, "SKIP_LAUNCH_ON_VIEWER");
                    SetMetaData(manifest, false, "SKIP_PERMISSION_CHECKS");
                    SetMetaData(manifest, false, "LAUNCH_CONTROLLER_ON_HOST");
                    SetMetaData(manifest, baseRuntimeFeature.ShowSplashScreenOnHost, "SHOW_SPLASH_SCREEN_ON_HOST", "true");
                }
                else if (!baseRuntimeFeature.LaunchAppOnViewer && !baseRuntimeFeature.LaunchControllerOnHost)
                {
                    ModifyArchiveInBuildGradle(false, path, "SpacesActivities");
                    ModifyGradleProjectForHostController(false, path);
#if UNITY_2023_1_OR_NEWER
                    switch (PlayerSettings.Android.applicationEntry)
                    {
                        case AndroidApplicationEntry.Activity:
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerActivity.java");
                            break;
                        case AndroidApplicationEntry.GameActivity:
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerGameActivity.java");
                            break;
                        default:
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerActivity.java");
                            ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerGameActivity.java");
                            break;
                    }
#else
                    ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                    ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                    ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerActivity.java");
#endif
                    SetMetaData(manifest, false, "SKIP_LAUNCH_ON_VIEWER");
                    SetMetaData(manifest, false, "SKIP_PERMISSION_CHECKS");
                    SetMetaData(manifest, false, "LAUNCH_CONTROLLER_ON_HOST");
                    SetMetaData(manifest, baseRuntimeFeature.ShowSplashScreenOnHost, "SHOW_SPLASH_SCREEN_ON_HOST", "true");
                }
                else
                {
                    ModifyArchiveInBuildGradle(true, path, "SpacesActivities");
#if UNITY_2023_1_OR_NEWER
                    switch (PlayerSettings.Android.applicationEntry)
                    {
                        case AndroidApplicationEntry.Activity:
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(baseRuntimeFeature.LaunchAppOnViewer, path, "UnityPlayerActivity.java");
                            break;
                        case AndroidApplicationEntry.GameActivity:
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(baseRuntimeFeature.LaunchAppOnViewer, path, "UnityPlayerGameActivity.java");
                            break;
                        default:
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(baseRuntimeFeature.LaunchAppOnViewer, path, "UnityPlayerActivity.java");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                            ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                            ModifyPlayerActivityWindowFocusChangeBehaviour(baseRuntimeFeature.LaunchAppOnViewer, path, "UnityPlayerGameActivity.java");
                            break;
                    }
#else
                    ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                    ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                    ModifyPlayerActivityWindowFocusChangeBehaviour(baseRuntimeFeature.LaunchAppOnViewer, path, "UnityPlayerActivity.java");
#endif
                    SetMetaData(manifest,baseRuntimeFeature.ShowLaunchMessageOnHost, "SHOW_LAUNCH_MESSAGE_ON_HOST", "true");
                    SetMetaData(manifest, baseRuntimeFeature.ShowSplashScreenOnHost, "SHOW_SPLASH_SCREEN_ON_HOST", "true");
                    SetMetaData(manifest, !baseRuntimeFeature.LaunchAppOnViewer, "SKIP_LAUNCH_ON_VIEWER", "true");
                    SetMetaData(manifest, baseRuntimeFeature.LaunchControllerOnHost, "LAUNCH_CONTROLLER_ON_HOST", "true");
                    ModifySpacesLauncherLaunchCategory(manifest, !baseRuntimeFeature.ExportHeadless);
                    /* Determine if the controller archive should be replaced or overwritten by a possible same-name archive from the Assets folder. */
                    var projectArchives = new DirectoryInfo(Application.dataPath).GetFiles("*.aar");
                    var addController = !baseRuntimeFeature.UseCustomController || (baseRuntimeFeature.UseCustomController && projectArchives.Any(fileInfo => fileInfo.Name == "SpacesActivities.aar"));
                    ModifyArchiveInBuildGradle(baseRuntimeFeature.LaunchControllerOnHost && addController, path, "SpacesActivities");
                    ModifyGradleProjectForHostController(baseRuntimeFeature.LaunchControllerOnHost, path);
                    SetMetaData(manifest, baseRuntimeFeature.SkipPermissionChecks, "SKIP_PERMISSION_CHECKS", "true");
                }

                if (cameraFeature != null && cameraFeature.enabled)
                {
                    AddPermission(manifest, "android.permission.CAMERA");
                }
            }
            else
            {
                /* Roll back every change that was made if the Base Runtime Feature is not enabled. */
                ModifyArchiveInBuildGradle(false, path, "SpacesServicesHelper");
                ModifyArchiveInBuildGradle(false, path, "SpacesActivities");
                ModifyArchiveInBuildGradle(false, path, "libopenxr_loader");
                ModifyGradleProjectForHostController(false, path);
#if UNITY_2023_1_OR_NEWER
                switch (PlayerSettings.Android.applicationEntry)
                {
                    case AndroidApplicationEntry.Activity:
                        ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                        ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                        ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerActivity.java");
                        break;
                    case AndroidApplicationEntry.GameActivity:
                        ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                        ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                        ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerGameActivity.java");
                        break;
                    default:
                        ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                        ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                        ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerActivity.java");
                        ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerGameActivity", "android.intent.category.LAUNCHER");
                        ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerGameActivity", "com.qualcomm.qti.intent.category.SPACES");
                        ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerGameActivity.java");
                        break;
                }
#else
                    ModifyCategoryForActivity(manifest, true, "com.unity3d.player.UnityPlayerActivity", "android.intent.category.LAUNCHER");
                    ModifyCategoryForActivity(manifest, false, "com.unity3d.player.UnityPlayerActivity", "com.qualcomm.qti.intent.category.SPACES");
                    ModifyPlayerActivityWindowFocusChangeBehaviour(false, path, "UnityPlayerActivity.java");
#endif

                SetMetaData(manifest, false, "SHOW_LAUNCH_MESSAGE_ON_HOST");
                SetMetaData(manifest, false, "SHOW_SPLASH_SCREEN_ON_HOST");
                SetMetaData(manifest, false, "SKIP_LAUNCH_ON_VIEWER");
                SetMetaData(manifest, false, "SKIP_PERMISSION_CHECKS");
				SetMetaData(manifest, false, "LAUNCH_CONTROLLER_ON_HOST");
                SetUsesFeatures(manifest, false);
            }


            WriteXmlDocument(manifestPath, manifest);
        }

        private void CheckMinApiVersion(OpenXRSettings OpenXRSettings, XmlDocument manifest)
        {
            var newMinApiLevel = OpenXRSettings.GetFeature<SpacesOpenXRFeature>().MinApiLevel;
            AddMinApiLevel(manifest,  newMinApiLevel.ToString());

            var spacesFeaturesList = new List<SpacesOpenXRFeature>();
            OpenXRSettings?.GetFeatures(spacesFeaturesList);
            foreach (var feature in spacesFeaturesList!.Where(feature => feature.enabled).Where(feature => IsThisVersionBiggerThanTheLast(newMinApiLevel, feature.MinApiLevel)))
            {
                Debug.Log($"The feature {feature.name} needs a newer version ({feature.MinApiLevel}) than the current minApi level ({newMinApiLevel}).");
                newMinApiLevel = feature.MinApiLevel;
                AddMinApiLevel(manifest, newMinApiLevel.ToString());
            }
        }

        private void AddMinApiLevel(XmlDocument manifest, string newMinApiLevel)
        {
            SetMetaData(manifest,true, "minApiLevel", newMinApiLevel);
        }

        private bool IsThisVersionBiggerThanTheLast(Version previousVersion, Version newVersion)
        {
            if (previousVersion.CompareTo(newVersion) < 0)
            {
                return true;
            }

            return false;
        }

        private void ModifyArchiveInBuildGradle(bool add, string path, string archiveName)
        {
            var gradlePropertiesPath = Path.Combine(path, "build.gradle");
            var lines = new List<string>(File.ReadAllLines(gradlePropertiesPath));
            var regex = "(?:[^A-Za-z])(" + archiveName + ")(?:[^A-Za-z],)";
            if (add)
            {
                if (!lines.Any(line => Regex.Match(line, regex).Success))
                {
                    int index = GetIndexForMatch(ref lines,
                        new[]
                        {
                            "dependencies",
                            "{"
                        });
                    ModifyBuildScript(true, "    implementation(name: '" + archiveName + "', ext:'aar')", ref lines, index + 1);
                }
            }
            else
            {
                lines.RemoveAll(line => Regex.Match(line, regex).Success);
            }

            File.WriteAllText(gradlePropertiesPath, String.Join(Environment.NewLine, lines));
        }

        private void ModifyCategoryForActivity(XmlDocument manifest, bool add, string activity, string category)
        {
            var intentSelector = $"/manifest/application/activity[@android:name='{activity}']/intent-filter";
            var categorySelector = $"{intentSelector}/category[@android:name='{category}']";
            var categoryNode = manifest.SelectSingleNode(categorySelector, _namespaceManager);

            if (add && categoryNode == null)
            {
                var intentNode = manifest.SelectSingleNode(intentSelector, _namespaceManager);
                if (intentNode == null)
                {
                    return;
                }

                AppendXmlNode(intentNode, "category", new XmlAttributeContainer("name", category));
            }

            if (!add && categoryNode != null && categoryNode.ParentNode != null)
            {
                categoryNode.ParentNode.RemoveChild(categoryNode);
            }
        }

        private void ModifySpacesLauncherLaunchCategory(XmlDocument manifest, bool add, string activityNameFull = "com.qualcomm.snapdragon.spaces.customlauncher.SpacesLauncher")
        {
            RemoveXmlNode(manifest, "/manifest/application/activity[@android:name='"+activityNameFull+"']");

            if (!add)
            {
                var applicationNode = manifest.SelectSingleNode("/manifest/application", _namespaceManager);
                var activityNode = AppendXmlNode(applicationNode, "activity", new XmlAttributeContainer("name", activityNameFull), new XmlAttributeContainer("exported", "true"));
                AppendXmlNode(activityNode, "intent-filter", new XmlAttributeContainer("node", "removeAll", "tools"));
            }
        }

        private void ModifyGradleProjectForHostController(bool applyPatch, string path)
        {
            var gradlePropertiesPath = Path.Combine(path, "..", "gradle.properties");
            var lines = new List<string>(File.ReadAllLines(gradlePropertiesPath));
            ModifyBuildScript(true, "android.useAndroidX=true", ref lines);
            ModifyBuildScript(applyPatch, "android.enableJetifier=true", ref lines);
            File.WriteAllText(gradlePropertiesPath, String.Join(Environment.NewLine, lines));
            var launcherBuildGradlePath = Path.Combine(path, "..", "launcher", "build.gradle");
            lines = new List<string>(File.ReadAllLines(launcherBuildGradlePath));
            int index = GetIndexForMatch(ref lines,
                new[]
                {
                    "implementation",
                    "project",
                    "unityLibrary"
                });
            ModifyBuildScript(applyPatch, "    implementation 'com.android.support.constraint:constraint-layout:1.1.3'", ref lines, index + 1);
            ModifyBuildScript(applyPatch, "    implementation 'com.google.android.material:material:1.3.0'", ref lines, index + 1);
            ModifyBuildScript(applyPatch, "    implementation 'androidx.navigation:navigation-fragment-ktx:2.4.2'", ref lines, index + 1);
            ModifyBuildScript(applyPatch, "    implementation 'androidx.navigation:navigation-ui-ktx:2.4.2'", ref lines, index + 1);
            ModifyBuildScript(applyPatch, "    implementation 'androidx.lifecycle:lifecycle-livedata-ktx:2.4.1'", ref lines, index + 1);
            ModifyBuildScript(applyPatch, "    implementation 'androidx.lifecycle:lifecycle-viewmodel-ktx:2.4.1'", ref lines, index + 1);
            ModifyBuildScript(applyPatch, "    implementation 'androidx.databinding:viewbinding:7.2.1'", ref lines, index + 1);
            index = GetIndexForMatch(ref lines,
                new[]
                {
                    "apply",
                    "plugin",
                    "com.android.application"
                });
            ModifyBuildScript(applyPatch, "apply plugin: 'kotlin-android'", ref lines, index + 1);
            index = GetIndexForMatch(ref lines,
                new[]
                {
                    "android",
                    "{"
                });
            ModifyBuildScript(applyPatch, "    buildFeatures { viewBinding true }", ref lines, index + 1);
            File.WriteAllText(launcherBuildGradlePath, String.Join(Environment.NewLine, lines));
            var mainBuildGradlePath = Path.Combine(path, "..", "build.gradle");
            lines = new List<string>(File.ReadAllLines(mainBuildGradlePath));
#if UNITY_2022_2_OR_NEWER
            index = GetIndexForMatch(ref lines,
                new[]
                {
                    "id",
                    "android",
                    "library",
                    "version"
                });
            ModifyBuildScript(applyPatch, "    id 'org.jetbrains.kotlin.jvm' version '1.6.10' apply false", ref lines, index - 1);
#else
            index = GetIndexForMatch(ref lines,
                new[]
                {
                    "classpath",
                    "android",
                    "build",
                    "gradle"
                });
            ModifyBuildScript(applyPatch, "            classpath 'org.jetbrains.kotlin:kotlin-gradle-plugin:1.6.10'", ref lines, index + 1);
#endif
            File.WriteAllText(mainBuildGradlePath, String.Join(Environment.NewLine, lines));
        }

        private void ModifyPlayerActivityWindowFocusChangeBehaviour(bool add, string path, string activityName)
        {
            /* Because Spaces applications launch on glasses (secondary display) we can ignore focus lost events which allow for
             * keyevents to still be processed by unity for remote OpenXR controllers to function. */

            var unityPlayerActivityPath = Path.Combine(path, "..", "unityLibrary", "src", "main", "java", "com", "unity3d", "player", activityName);
            var lines = new List<string>(File.ReadAllLines(unityPlayerActivityPath));
            int index = GetIndexForMatch(ref lines,
                new[]
                {
                    "super",
                    "onWindowFocusChanged",
                    "hasFocus"
                });
            ModifyBuildScript(add, "        if (!hasFocus) return;", ref lines, index);
            File.WriteAllText(unityPlayerActivityPath, String.Join(Environment.NewLine, lines));
        }

        private void AddPermission(XmlDocument manifest, string name)
        {
            RemoveXmlNode(manifest, "//uses-permission[@android:name='" + name + "']");
            AppendXmlNode(manifest.DocumentElement, "uses-permission", new XmlAttributeContainer("name", name));
        }

        private void SetMetaData(XmlDocument manifest, bool add, string key, string data = "")
        {
            key = "com.qualcomm.snapdragon.spaces." + key;
            RemoveXmlNode(manifest, "/manifest/application/meta-data[@android:name='" + key + "']");

            if (add && data != "")
            {
                var applicationNode = manifest.SelectSingleNode("/manifest/application", _namespaceManager);
                AppendXmlNode(applicationNode, "meta-data", new XmlAttributeContainer("name", key), new XmlAttributeContainer("value", data));
            }
        }

        private void SetUsesFeatures(XmlDocument manifest, bool add)
        {
            var featureNames = SpacesLauncherSettings.FeatureNames;
            var settings = SpacesLauncherSettings.GetSerializedSettings();

            foreach (var featureName in featureNames)
            {
                var usePropertyName = "use" + featureName.Replace(" ", "");
                var isFeatureUsed = settings.FindProperty(usePropertyName).boolValue;
                var requirePropertyName = "require" + featureName.Replace(" ", "");
                var isFeatureRequired = settings.FindProperty(requirePropertyName).boolValue;
                var key = "snapdragon.spaces." + featureName.ToLower().Replace(" ", "_");

                RemoveXmlNode(manifest, "/manifest/uses-feature[@android:name='" + key + "']");

                if (add && isFeatureUsed)
                {
                    AppendXmlNode(manifest.DocumentElement, "uses-feature", new XmlAttributeContainer("name", key), new XmlAttributeContainer("required", isFeatureRequired ? "true" : "false"));
                }
            }
        }

        private XmlDocument ReadXmlDocument(string path)
        {
            var xmlDocument = new XmlDocument();
            using (var reader = new XmlTextReader(path))
            {
                reader.Read();
                xmlDocument.Load(reader);
            }

            return xmlDocument;
        }

        private void WriteXmlDocument(string path, XmlDocument xmlDocument)
        {
            using (var writer = new XmlTextWriter(path, new UTF8Encoding(false)))
            {
                writer.Formatting = Formatting.Indented;
                xmlDocument.Save(writer);
            }
        }

        private void ModifyBuildScript(bool add, string newLine, ref List<string> lines, int index = -1)
        {
            if (add)
            {
                if (!lines.Any(line => line.Replace(" ", "").Contains(newLine.Replace(" ", ""))))
                {
                    lines.Insert(index == -1 ? lines.Count : index, newLine);
                }
            }
            else
            {
                lines.RemoveAll(line => line.Replace(" ", "").Contains(newLine.Replace(" ", "")));
            }
        }

        private int GetIndexForMatch(ref List<string> lines, string[] matchPhrases)
        {
            try
            {
                var index = lines.FindIndex(line => matchPhrases.All(line.Contains));
                return index;
            }
            catch
            {
                return -1;
            }
        }

        private XmlNode CreateXmlNode(XmlDocument xmlDocument, string name, params XmlAttributeContainer[] attributes)
        {
            var node = xmlDocument.CreateNode(XmlNodeType.Element, name, _namespaceManager.DefaultNamespace);
            foreach (var attribute in attributes)
            {
                var xmlAttribute = xmlDocument.CreateAttribute(attribute.Prefix, attribute.Name, _namespaceManager.LookupNamespace(attribute.Prefix));
                xmlAttribute.Value = attribute.Value;
                node.Attributes!.Append(xmlAttribute);
            }

            return node;
        }

        private XmlNode AppendXmlNode(XmlNode parent, string name, params XmlAttributeContainer[] attributes)
        {
            return parent.AppendChild(
                CreateXmlNode(parent.OwnerDocument, name, attributes)
            );
        }

        private XmlNode RemoveXmlNode(XmlDocument xmlDocument, string xpath)
        {
            var node = xmlDocument.SelectSingleNode(xpath, _namespaceManager);
            if (node != null)
            {
                return node.ParentNode!.RemoveChild(node);
            }
            return null;
        }
    }
}
