/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    public class DualRenderFusionGameObjectHelper : MonoBehaviour
    {
        [MenuItem("GameObject/XR/Snapdragon Spaces/Dual Render Fusion/Dynamic OpenXR Loader", false, 5)]
        public static void AddDynamicOpenXRLoaderGameObjectToScene(MenuCommand mc)
        {
            DynamicOpenXRLoader oldDynamicOpenXRLoader = FindFirstObjectByType<DynamicOpenXRLoader>(FindObjectsInactive.Include);
            if (oldDynamicOpenXRLoader != null)
            {
                Debug.LogWarning($"There is a Dynamic OpenXR Loader component already present in the scene on the Game Object {oldDynamicOpenXRLoader.gameObject.name}. Skipping adding a new one.");
                return;
            }

            GameObject dynamicOpenXRLoaderGO = new GameObject("Dynamic OpenXR Loader");
            SpacesGlassStatus glassStatus = FindFirstObjectByType<SpacesGlassStatus>(FindObjectsInactive.Include);
            if (glassStatus != null)
            {
                Debug.LogWarning("Dynamic OpenXR Loader created a Spaces Glass Status component, but one already exists in the scene. Remove the Spaces Glass Status component you have in your scene on the Game Object: " + glassStatus.gameObject.name);
            }

            DynamicOpenXRLoader dynamicOpenXRLoader = dynamicOpenXRLoaderGO.AddComponent<DynamicOpenXRLoader>();
            GameObjectUtility.SetParentAndAlign(dynamicOpenXRLoaderGO, mc.context as GameObject);
            Undo.RegisterCreatedObjectUndo(dynamicOpenXRLoaderGO, "Create " + dynamicOpenXRLoaderGO.name);
            Selection.activeObject = dynamicOpenXRLoaderGO;
        }

        [MenuItem("GameObject/XR/Snapdragon Spaces/Dual Render Fusion/Spaces Glass Status", false, 10)]
        public static void AddSpacesGlassStatusGameObjectToScene(MenuCommand mc)
        {
            SpacesGlassStatus oldSpacesGlassStatus = FindFirstObjectByType<SpacesGlassStatus>(FindObjectsInactive.Include);
            if (oldSpacesGlassStatus != null)
            {
                Debug.LogWarning($"There is a Spaces Glass Status component already present in the scene on the Game Object {oldSpacesGlassStatus.gameObject.name}. Skipping adding a new one.");
                return;
            }

            GameObject spacesGlassStatusGO = new GameObject("Spaces Glass Status");
            SpacesGlassStatus glassStatus = spacesGlassStatusGO.AddComponent<SpacesGlassStatus>();
            GameObjectUtility.SetParentAndAlign(spacesGlassStatusGO, mc.context as GameObject);
            Undo.RegisterCreatedObjectUndo(spacesGlassStatusGO, "Create " + spacesGlassStatusGO.name);
            Selection.activeObject = spacesGlassStatusGO;
        }

        [MenuItem("GameObject/XR/Snapdragon Spaces/Dual Render Fusion/Host View", false, 10)]
        public static void AddSpacesHostViewGameObjectToScene(MenuCommand mc)
        {
            SpacesHostView oldSpacesHostView = FindFirstObjectByType<SpacesHostView>(FindObjectsInactive.Include);
            if (oldSpacesHostView != null)
            {
                Debug.LogWarning($"There is a Spaces Host View component already present in the scene on the Game Object {oldSpacesHostView.gameObject.name}. Skipping adding a new one.");
                return;
            }

            GameObject spacesHostViewGO = new GameObject("Spaces Host View");
            SpacesHostView spacesHostView = spacesHostViewGO.AddComponent<SpacesHostView>();
            GameObjectUtility.SetParentAndAlign(spacesHostViewGO, mc.context as GameObject);
            Undo.RegisterCreatedObjectUndo(spacesHostViewGO, "Create " + spacesHostViewGO.name);
            Selection.activeObject = spacesHostViewGO;
        }

        [MenuItem("GameObject/XR/Snapdragon Spaces/Spaces XR Simulator", false, 10)]
        public static void AddSpacesXRSimulator(MenuCommand mc)
        {
            SpacesXRSimulator oldSpacesXRSimulator = FindFirstObjectByType<SpacesXRSimulator>(FindObjectsInactive.Include);
            if (oldSpacesXRSimulator != null)
            {
                Debug.LogWarning($"There is a Spaces XR Simulator component already present in the scene on the Game Object {oldSpacesXRSimulator.gameObject.name}. Skipping adding a new one.");
                return;
            }

            GameObject spacesFusionSimulatorGO = new GameObject("Spaces XR Simulator");
            spacesFusionSimulatorGO.AddComponent<SpacesXRSimulator>();
            GameObjectUtility.SetParentAndAlign(spacesFusionSimulatorGO, mc.context as GameObject);
            Undo.RegisterCreatedObjectUndo(spacesFusionSimulatorGO, "Create " + spacesFusionSimulatorGO.name);
            Selection.activeObject = spacesFusionSimulatorGO;
        }

        [MenuItem("GameObject/XR/Snapdragon Spaces/Dual Render Fusion/Spaces Lifecycle Events", false, 10)]
        public static void AddLifecycleEvents(MenuCommand mc)
        {
            SpacesLifecycleEvents oldSpacesLifecycleEvents = FindFirstObjectByType<SpacesLifecycleEvents>(FindObjectsInactive.Include);
            if (oldSpacesLifecycleEvents != null)
            {
                Debug.LogWarning($"There is a Spaces Fusion Lifecycle Events component already present in the scene on the Game Object {oldSpacesLifecycleEvents.gameObject.name}. Skipping adding a new one.");
                return;
            }

            GameObject spacesLifecycleEventsGO = new GameObject("Spaces Lifecycle Events");
            spacesLifecycleEventsGO.AddComponent<SpacesLifecycleEvents>();
            GameObjectUtility.SetParentAndAlign(spacesLifecycleEventsGO, mc.context as GameObject);
            Undo.RegisterCreatedObjectUndo(spacesLifecycleEventsGO, "Create " + spacesLifecycleEventsGO.name);
            Selection.activeObject = spacesLifecycleEventsGO;
        }
    }
}
#endif
