/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
    internal sealed partial class OlderRuntimeCompatibilityFeature
    {
        private const string Library = "libOlderRuntimeCompatibility";

        [DllImport(Library, EntryPoint = "orcInitializeCompatibilitySystem")]
        private static extern ORCResult Internal_orcInitializeCompatibilitySystem(IntPtr setupInfo);

        [DllImport(Library, EntryPoint = "orcHookCompatibilitySystem")]
        private static extern ORCResult Internal_orcHookCompatibilitySystem(IntPtr hookSetupInfo, IntPtr hookGetInstanceProcAddr);

        [DllImport(Library, EntryPoint = "orcShutdownCompatibilitySystem")]
        private static extern ORCResult Internal_orcShutdownCompatibilitySystem();
    }
}
