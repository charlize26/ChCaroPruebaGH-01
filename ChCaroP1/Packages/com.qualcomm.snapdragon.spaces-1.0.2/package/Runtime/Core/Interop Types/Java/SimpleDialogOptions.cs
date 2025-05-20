/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;

namespace Qualcomm.Snapdragon.Spaces
{
    /**
     * Collection of strings which can be used to customise or localise a basic (android) dialog.
     */
    [Serializable]
    public class SimpleDialogOptions
    {
        public string Title;
        public string Message;
        public string PositiveButtonText;
        public string NegativeButtonText;
    }
}
