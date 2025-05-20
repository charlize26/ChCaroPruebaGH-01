/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct XrSystemGraphicsProperties
	{
		private uint _maxSwapchainImageHeight;
		private uint _maxSwapchainImageWidth;
		private uint _maxLayerCount;

		public uint MaxSwapchainImageHeight => _maxSwapchainImageHeight;
		public uint MaxSwapchainImageWidth => _maxSwapchainImageWidth;
		public uint MaxLayerCount => _maxLayerCount;
	}
}
