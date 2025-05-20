/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;
using Object = UnityEngine.Object;
using Unity.XR.CoreUtils;

namespace Qualcomm.Snapdragon.Spaces
{
    public static class OriginLocationUtility
    {
        public static XROrigin FindXROrigin(bool includeInactive = false)
        {
            return Object.FindFirstObjectByType<XROrigin>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        }

        public static Camera GetOriginCamera(bool includeInactive = false)
        {
            return FindXROrigin(includeInactive)?.Camera;
        }

        public static Transform GetOriginTransform(bool includeInactive = false)
        {
            return FindXROrigin(includeInactive)?.transform;
        }
    }
}
