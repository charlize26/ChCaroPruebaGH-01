/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
    internal sealed partial class GPUCameraAccess
    {
        private const string Library = "libCameraProvider";

        #region Camera Provider Library bindings
        [DllImport(Library, EntryPoint = "GetRenderEventFuncPtr")]
        private static extern IntPtr Internal_GetRenderEventFuncPtr();
        [DllImport(Library, EntryPoint = "LockMutex")]
        private static extern IntPtr Internal_LockMutex();
        [DllImport(Library, EntryPoint = "UnlockMutex")]
        private static extern IntPtr Internal_UnlockMutex();
        #endregion
    }
}
