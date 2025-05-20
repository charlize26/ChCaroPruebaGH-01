/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    /// Enum that represents the image tracking mode type.
    /// </summary>
    public enum SpacesImageTrackingMode
    {
        /// <summary>
        /// Dynamic mode updates the position of tracked images frequently, and works on moving and static targets. If the tracked image cannot be found, no location or pose is reported.
        /// </summary>
        DYNAMIC = 0,

        /// <summary>
        /// Static mode is useful for tracking images that are known to be static. Images tracked in this mode are fixed in position when first detected, and never updated. This leads to less power consumption and greater performance.
        /// </summary>
        STATIC = 1,

        /// <summary>
        /// Adaptive mode will periodically update the position of static images if they have moved slightly. This finds a balance between power consumption and accuracy for static images.
        /// </summary>
        ADAPTIVE = 2,

        /// <summary>
        /// Invalid tracking mode.
        /// </summary>
        INVALID = 0x7FFFFFFF
    }
}
