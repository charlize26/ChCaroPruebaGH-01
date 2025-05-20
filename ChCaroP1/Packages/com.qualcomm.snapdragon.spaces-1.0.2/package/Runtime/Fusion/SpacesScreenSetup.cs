/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    [MovedFrom(false, null, null, "FusionScreenSetup")]
    public class SpacesScreenSetup : MonoBehaviour
    {
        enum OrientationType
        {
            None,
            Portrait,
            Landscape
        }

        [SerializeField]
        OrientationType ForcedOrientation = OrientationType.None;

        void Awake()
        {
            if (OpenXRSettings.Instance.GetFeature<BaseRuntimeFeature>()?.PreventSleepMode ?? false)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            // If fusion is not enabled the screen orientation should not be changed (to Portrait).
            // Doing so on an Aio device causes the headset display orientation to be changed which causes it to render incorrectly.
            if (OpenXRSettings.Instance.GetFeature<FusionFeature>()?.enabled ?? false)
            {
                switch (ForcedOrientation)
                {
                    case OrientationType.Portrait:
                        Screen.orientation = ScreenOrientation.Portrait;
                        break;

                    case OrientationType.Landscape:
                        Screen.orientation = ScreenOrientation.LandscapeLeft;
                        break;
                }
            }
        }
    }
}
