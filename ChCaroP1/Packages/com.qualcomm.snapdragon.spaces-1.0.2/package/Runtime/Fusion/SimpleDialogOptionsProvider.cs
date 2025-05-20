/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    /**
     * An abstract component which can be added to a gameobject to provide dialog options to be consumed elsewhere.
     * Implement a subclass of this to control what is displayed by these dialogs (e.g. for localisation).
     */
    public abstract class SimpleDialogOptionsProvider : MonoBehaviour
    {
        public abstract SimpleDialogOptions GetDialogOptions();
    }
}
