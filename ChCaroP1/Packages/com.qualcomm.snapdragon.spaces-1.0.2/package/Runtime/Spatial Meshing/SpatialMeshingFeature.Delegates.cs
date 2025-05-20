/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
    internal sealed partial class SpatialMeshingFeature
    {
        private const string Library = "libMeshingProvider";

        [DllImport(Library, EntryPoint = "GetInterceptedInstanceProcAddr")]
        private static extern IntPtr Internal_GetInterceptedInstanceProcAddr(IntPtr xrGetInstanceProcAddr);

        [DllImport(Library, EntryPoint = "RegisterMeshingLifecycleProvider")]
        private static extern void Internal_RegisterMeshingLifecycleProvider();

        [DllImport(Library, EntryPoint = "SetInstanceHandle")]
        private static extern void Internal_SetInstanceHandle(ulong instance);

        [DllImport(Library, EntryPoint = "SetSessionHandle")]
        private static extern void Internal_SetSessionHandle(ulong session);

        [DllImport(Library, EntryPoint = "SetSpaceHandle")]
        private static extern void Internal_SetSpaceHandle(ulong space);
    }
}
