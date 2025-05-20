/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    /// Represents a Spaces Marker detected or tracked in the physical environment.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpacesARMarker : ARTrackable<XRTrackedMarker, SpacesARMarker>
    {
        /// <summary>
        /// Whether the marker data has been decoded and is available.
        /// </summary>
        public bool IsMarkerDataAvailable { get; private set; }

        /// <summary>
        /// The decoded marker data. Will return an empty string if <see cref="IsMarkerDataAvailable"/> is <c>false</c>.
        /// </summary>
        public string Data => IsMarkerDataAvailable ? _data : string.Empty;

        /// <summary>
        /// The 2D size of the marker.
        /// </summary>
        public Vector2 Size => IsMarkerDataAvailable ? _size : new Vector2();

        internal void TryGetMarkerData(XRQrCodeTrackingSubsystem subsystem)
        {
            IsMarkerDataAvailable = subsystem.TryGetMarkerData(sessionRelativeData.trackableId, out _data);
        }

        internal void TryGetMarkerSize(XRQrCodeTrackingSubsystem subsystem)
        {
            subsystem.TryGetMarkerSize(sessionRelativeData.trackableId, out _size);
        }

        private string _data;
        private Vector2 _size;
    }
}
