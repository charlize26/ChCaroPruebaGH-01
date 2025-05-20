/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SubsystemsImplementation.Extensions;
using UnityEngine.XR.ARSubsystems;

namespace Qualcomm.Snapdragon.Spaces
{
    internal class SpacesMutableRuntimeReferenceImageLibrary : MutableRuntimeReferenceImageLibrary
    {
        private struct AddImageJob : IJob
        {
            public void Execute() { }
        }

        internal SpacesMutableRuntimeReferenceImageLibrary(XRReferenceImageLibrary serializedLibrary, SpacesReferenceImageTrackingModes trackingModes)
        {
            if (serializedLibrary == null)
            {
                return;
            }

            if (trackingModes.Count != serializedLibrary.count)
            {
                Debug.LogWarning("Number of tracking modes defined does not match the number of images in the reference library!");
            }

            foreach (XRReferenceImage referenceImage in serializedLibrary)
            {
                if (ValidateXRReferenceImage(referenceImage, trackingModes))
                {
                    _activeImages.Add(referenceImage);
                }
            }
        }

        protected override XRReferenceImage GetReferenceImageAt(int index)
        {
            return _activeImages[index];
        }

        public override int count => _activeImages.Count;

        protected override JobHandle ScheduleAddImageJobImpl(
            NativeSlice<byte> imageBytes,
            Vector2Int sizeInPixels,
            TextureFormat format,
            XRReferenceImage referenceImage,
            JobHandle inputDeps)
        {
            if (ValidateXRReferenceImage(referenceImage, TrackingModes))
            {
                _provider ??= GetProvider();

                _activeImages.Add(referenceImage);
                _provider?.SyncInternalImageTracker();
            }
            else
            {
                Debug.LogWarning($"XRReferenceImage {referenceImage.name} is skipped.");
            }

            var job = new AddImageJob();
            return job.Schedule();
        }

        protected override TextureFormat GetSupportedTextureFormatAtImpl(int index)
        {
            return _supportedTextureFormats[index];
        }

        private readonly List<TextureFormat> _supportedTextureFormats = new() { TextureFormat.RGB24 };
        public override int supportedTextureFormatCount => _supportedTextureFormats.Count;

        private bool ValidateXRReferenceImage(XRReferenceImage referenceImage, SpacesReferenceImageTrackingModes trackingModes)
        {
            if (referenceImage.texture == null)
            {
                Debug.LogWarning("XRReferenceImage '" + referenceImage.name + "' has no valid texture set.");
                return false;
            }

            if (referenceImage.texture.format != TextureFormat.RGB24)
            {
                Debug.LogWarning("XRReferenceImage '" + referenceImage.name + "' has an invalid texture format (" + referenceImage.texture.format + "). Image targets must be set to RGB24 bit format.");
                return false;
            }

            if (!referenceImage.specifySize || referenceImage.size == Vector2.zero)
            {
                Debug.LogWarning("XRReferenceImage '" + referenceImage.name + "' does not have a specified physical size.");
                return false;
            }

            if (!trackingModes.ReferenceImageNames.Contains(referenceImage.name))
            {
                Debug.LogWarning($"No tracking mode defined for {referenceImage.name}. Using Dynamic by default.");
                TrackingModes.AddTrackingModeForReferenceImage(referenceImage.name, XrImageTargetTrackingModeQCOM.XR_IMAGE_TARGET_TRACKING_MODE_DYNAMIC_QCOM);
            }
            else
            {
                TrackingModes.AddTrackingModeForReferenceImage(referenceImage.name, trackingModes.GetTrackingModeForReferenceImage(referenceImage.name));
            }

            return true;
        }

        private ImageTrackingSubsystem.ImageTrackingProvider GetProvider()
        {
            var subsystems = new List<ImageTrackingSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
            {
                var ITsubsystem = subsystems[0];
                return (ImageTrackingSubsystem.ImageTrackingProvider)ITsubsystem.GetProvider();
            }

            return null;
        }

        private readonly List<XRReferenceImage> _activeImages = new();
        internal SpacesReferenceImageTrackingModes TrackingModes { get; } = new();

        private ImageTrackingSubsystem.ImageTrackingProvider _provider;
    }
}
