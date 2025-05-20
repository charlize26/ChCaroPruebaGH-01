/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine.UIElements;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    public interface ISpacesEditorWindow
    {
        public void Init(TargetPlatform targetPlatform, Button nextButton);
    }
}
