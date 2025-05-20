/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct XrSystemTrackingProperties
	{
		private bool _orientationTracking;
		private bool _positionTracking;

		public bool OrientationTracking => _orientationTracking;
		public bool PositionTracking => _positionTracking;
	}
}
