/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SubsystemsImplementation.Extensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    /// An extension for <see cref="ARAnchorManager"/>. Manages and provides additional functionalities and features for spatial anchors.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARAnchorManager))]
    public class SpacesAnchorStore : MonoBehaviour
    {
        /// <summary>
        /// Describes the result of a <c>SaveAnchorWithResult</c> operation.
        /// </summary>
        /// <seealso cref="SaveAnchorWithResult(UnityEngine.XR.ARFoundation.ARAnchor,string,System.Action{SaveAnchorResult})"/>
        /// <seealso cref="SaveAnchorWithResult(UnityEngine.XR.ARFoundation.ARAnchor,System.Action{SaveAnchorResult})"/>
        public enum SaveAnchorResult
        {
            /// <summary>Anchor is pending saving. Cannot be normally seen, therefore use ARAnchor.<see cref="UnityEngine.XR.ARFoundation.ARAnchor.pending"/> instead.</summary>
            PENDING = 0,
            /// <summary>Saved successfully in local storage.</summary>
            SAVED = 1,
            /// <summary>Not saved in local storage due to a runtime error.</summary>
            FAILURE_RUNTIME_ERROR = -1,
            /// <summary>Not saved in local storage due to Spaces Anchor Store failing to load.</summary>
            FAILURE_STORE_NOT_LOADED = -2,
            /// <summary>Not saved in local storage due to insufficient quality of environment map.</summary>
            FAILURE_INSUFFICIENT_QUALITY = -3
        }

        private class SaveAnchorData
        {
            public readonly string AnchorName;
            public readonly ARAnchor Anchor;
            public readonly Action<SaveAnchorResult> OnSavedCallback;
            public SaveAnchorResult Result;

            public SaveAnchorData(string anchorName, ARAnchor anchor, Action<SaveAnchorResult> onSavedCallback)
            {
                AnchorName = anchorName;
                Anchor = anchor;
                OnSavedCallback = onSavedCallback;
                Result = SaveAnchorResult.PENDING;
            }
        }

        private class LoadAnchorData
        {
            public readonly string AnchorName;
            public readonly Action<bool> OnLoadedCallback;
            public ulong AnchorHandle;
            public ulong SpaceHandle;
            public bool Success;

            public LoadAnchorData(string anchorName, Action<bool> onLoadedCallback)
            {
                AnchorName = anchorName;
                AnchorHandle = 0;
                SpaceHandle = 0;
                OnLoadedCallback = onLoadedCallback;
                Success = false;
            }
        }

        private SpatialAnchorsFeature _feature;
        private SpatialAnchorsSubsystem _subsystem;
        private ulong _spatialAnchorStore;
        private bool _isStoreLoaded;

        private Thread _saveAnchorsWorker;
        private int _saveAnchorsWorkerRunning = 0; // false = 0; true = 1
        private readonly ConcurrentQueue<SaveAnchorData> _anchorsToSave = new();
        private readonly ConcurrentQueue<SaveAnchorData> _anchorsSaved = new();

        private Thread _loadAnchorsWorker;
        private int _loadAnchorsWorkerRunning = 0; // false = 0; true = 1
        private readonly ConcurrentQueue<LoadAnchorData> _anchorsToLoad = new();
        private readonly ConcurrentQueue<LoadAnchorData> _anchorsLoaded = new();

        private void OnEnable()
        {
            if (!_feature)
            {
                _feature = OpenXRSettings.Instance.GetFeature<SpatialAnchorsFeature>();
                if (!FeatureUseCheckUtility.IsFeatureUseable(_feature))
                {
#if !UNITY_EDITOR
                    Debug.LogWarning("Spatial Anchors Feature isn't available. Aborting SpacesSpatialAnchorStore initialization!");
#endif
                    return;
                }

                var subsystems = new List<SpatialAnchorsSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                if (subsystems.Count > 0)
                {
                    _subsystem = subsystems[0];
                }
                else
                {
                    Debug.LogError("Failed to get SpatialAnchorsSubsystem instance. Aborting SpacesSpatialAnchorStore initialization!");
                }
            }

            if (_feature)
            {
                _isStoreLoaded = _feature.TryCreateSpatialAnchorStoreConnection(out _spatialAnchorStore);

                _saveAnchorsWorker = new Thread(SaveAnchorsWorker) { Name = "SaveAnchorsWorker" };
                _loadAnchorsWorker = new Thread(LoadAnchorsWorker) { Name = "LoadAnchorsWorker" };
            }
        }

        private void OnDisable()
        {
            if (_feature)
            {
                if (_saveAnchorsWorker != null && _saveAnchorsWorker.IsAlive)
                {
                    _saveAnchorsWorker.Join();
                }

                if (_loadAnchorsWorker != null && _loadAnchorsWorker.IsAlive)
                {
                    _loadAnchorsWorker.Join();
                }

                _isStoreLoaded = !_feature.TryDestroySpatialAnchorStoreConnection(_spatialAnchorStore);
            }

            _feature = null;
        }

        private void Update()
        {
            // Update all saved anchors in the subsystem provider
            while (_anchorsSaved.TryDequeue(out SaveAnchorData savedAnchor))
            {
                if (savedAnchor.Result == SaveAnchorResult.SAVED)
                {
                    var provider = (SpatialAnchorsSubsystem.SpatialAnchorsProvider)_subsystem.GetProvider();
                    provider.UpdateAnchorSavedName(savedAnchor.Anchor.trackableId, savedAnchor.AnchorName);
                }

                savedAnchor.OnSavedCallback?.Invoke(savedAnchor.Result);
            }

            // Create GameObjects with the ARAnchor component for
            // each anchor that was loaded from the anchor store
            while (_anchorsLoaded.TryDequeue(out LoadAnchorData loadedAnchor))
            {
                if (loadedAnchor.Success)
                {
                    GameObject go = new GameObject
                    {
                        name = loadedAnchor.AnchorName,
                        transform =
                        {
                            position = Vector3.zero,
                            rotation = Quaternion.identity
                        }
                    };
                    go.SetActive(false);
                    var provider = (SpatialAnchorsSubsystem.SpatialAnchorsProvider)_subsystem.GetProvider();
                    provider.SetPersistentAnchorCandidate(new SpatialAnchor(TrackableId.invalidId, loadedAnchor.AnchorHandle, loadedAnchor.SpaceHandle, Pose.identity, loadedAnchor.AnchorName));
                    go.AddComponent<ARAnchor>();
                    go.SetActive(true);
                }

                loadedAnchor.OnLoadedCallback?.Invoke(loadedAnchor.Success);
            }
        }

        private void SaveAnchorsWorker()
        {
            SpacesThreadUtility.SetThreadHint(SpacesThreadType.SPACES_THREAD_TYPE_APPLICATION_WORKER);

            while (_anchorsToSave.TryDequeue(out SaveAnchorData anchorToSave))
            {
                // NOTE(TD): If the anchor to be saved is 'pending'
                // enqueue it back to the same queue in order to
                // try and save it whenever it becomes available.
                // This approach however keeps the thread alive until
                // this anchor has been dealt with.
                if (anchorToSave.Anchor.pending)
                {
                    _anchorsToSave.Enqueue(anchorToSave);
                    continue;
                }

                var provider = (SpatialAnchorsSubsystem.SpatialAnchorsProvider)_subsystem.GetProvider();
                ulong spatialAnchorHandle = provider.TryGetSpatialAnchorHandleFromTrackableId(anchorToSave.Anchor.trackableId);

                _feature.TryPersistSpatialAnchor(_spatialAnchorStore, spatialAnchorHandle, anchorToSave.AnchorName, out SaveAnchorResult result);
                anchorToSave.Result = result;
                _anchorsSaved.Enqueue(anchorToSave);
            }

            Interlocked.Exchange(ref _saveAnchorsWorkerRunning, 0);
        }

        private void LoadAnchorsWorker()
        {
            SpacesThreadUtility.SetThreadHint(SpacesThreadType.SPACES_THREAD_TYPE_APPLICATION_WORKER);

            // NOTE(TD): There is no mechanism to retry and load an anchor
            // if it has already failed to load once.
            while (_anchorsToLoad.TryDequeue(out LoadAnchorData anchorToLoad))
            {
                if (_feature.TryCreateSpatialAnchorFromPersistedNameMSFT(_spatialAnchorStore,
                        anchorToLoad.AnchorName, out ulong spatialAnchorHandle))
                {
                    ulong anchorSpaceHandle = _feature.TryCreateSpatialAnchorSpaceHandle(spatialAnchorHandle);
                    if (anchorSpaceHandle != 0)
                    {
                        anchorToLoad.AnchorHandle = spatialAnchorHandle;
                        anchorToLoad.SpaceHandle = anchorSpaceHandle;
                        anchorToLoad.Success = true;
                    }

                    _anchorsLoaded.Enqueue(anchorToLoad);
                }
            }

            Interlocked.Exchange(ref _loadAnchorsWorkerRunning, 0);
        }

        /// <summary>
        /// Save an <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/> to local storage by a given name. Can invoke a callback.
        /// </summary>
        /// <param name="anchor">AR Foundation anchor to save</param>
        /// <param name="anchorName">Name given to the anchor in storage</param>
        /// <param name="onSavedCallback">
        /// Invoked when the anchor has finished saving, with a boolean parameter:<br/>
        /// <c>true</c> = Saved successfully<br/>
        /// <c>false</c> = Failed saving
        /// </param>
        public void SaveAnchor(ARAnchor anchor, string anchorName, Action<bool> onSavedCallback = null)
        {
            if (_isStoreLoaded)
            {
                _anchorsToSave.Enqueue(new SaveAnchorData(anchorName, anchor, result => onSavedCallback?.Invoke(result == SaveAnchorResult.SAVED)));

                // Start the 'Save Anchors' thread if it's not already running
                if (Interlocked.CompareExchange(ref _saveAnchorsWorkerRunning, 1, 0) == 0)
                {
                    // The thread has finished working, but is still alive.
                    if (_saveAnchorsWorker.IsAlive)
                    {
                        _saveAnchorsWorker.Join();
                    }
                    _saveAnchorsWorker.Start();
                }
            }
            else
            {
                onSavedCallback?.Invoke(false);
            }
        }

        /// <summary>
        /// Save an <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/> to local storage by a generated hash. Can invoke a callback.
        /// </summary>
        /// <param name="anchor">AR Foundation anchor to save</param>
        /// <param name="onSavedCallback">
        /// Invoked when the anchor has finished saving, with a boolean parameter:<br/>
        /// <c>true</c> = Saved successfully<br/>
        /// <c>false</c> = Failed saving
        /// </param>
        public void SaveAnchor(ARAnchor anchor, Action<bool> onSavedCallback = null)
        {
            int hashCode = anchor.trackableId.GetHashCode();
            hashCode = (hashCode * 4999559) + DateTime.Now.GetHashCode();
            SaveAnchor(anchor, hashCode.ToString(), onSavedCallback);
        }

        /// <summary>
        /// Save an <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/> to local storage by a given name. Can invoke a callback.
        /// </summary>
        /// <param name="anchor">AR Foundation anchor to save</param>
        /// <param name="anchorName">Name given to the anchor in storage</param>
        /// <param name="onSavedCallback">
        /// Invoked when the anchor has finished saving, with a <see cref="SaveAnchorResult"/> parameter of value:
        /// <ul>
        /// <li><see cref="SaveAnchorResult.PENDING"/></li>
        /// <li><see cref="SaveAnchorResult.SAVED"/></li>
        /// <li><see cref="SaveAnchorResult.FAILURE_RUNTIME_ERROR"/></li>
        /// <li><see cref="SaveAnchorResult.FAILURE_STORE_NOT_LOADED"/></li>
        /// <li><see cref="SaveAnchorResult.FAILURE_INSUFFICIENT_QUALITY"/></li>
        /// </ul>
        /// </param>
        public void SaveAnchorWithResult(ARAnchor anchor, string anchorName, Action<SaveAnchorResult> onSavedCallback = null)
        {
            if (_isStoreLoaded)
            {
                _anchorsToSave.Enqueue(new SaveAnchorData(anchorName, anchor, onSavedCallback));

                // Start the 'Save Anchors' thread if it's not already running
                if (Interlocked.CompareExchange(ref _saveAnchorsWorkerRunning, 1, 0) == 0)
                {
                    // The thread has finished working, but is still alive.
                    if (_saveAnchorsWorker.IsAlive)
                    {
                        _saveAnchorsWorker.Join();
                    }
                    _saveAnchorsWorker.Start();
                }
            }
            else
            {
                onSavedCallback?.Invoke(SaveAnchorResult.FAILURE_STORE_NOT_LOADED);
            }

        }

        /// <summary>
        /// Save an <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/> to local storage by a generated hash. Can invoke a callback.
        /// </summary>
        /// <param name="anchor">AR Foundation anchor to save</param>
        /// <param name="onSavedCallback">
        /// Invoked when the anchor has finished saving, with a <see cref="SaveAnchorResult"/> parameter of value:
        /// <ul>
        /// <li><see cref="SaveAnchorResult.PENDING"/></li>
        /// <li><see cref="SaveAnchorResult.SAVED"/></li>
        /// <li><see cref="SaveAnchorResult.FAILURE_RUNTIME_ERROR"/></li>
        /// <li><see cref="SaveAnchorResult.FAILURE_STORE_NOT_LOADED"/></li>
        /// <li><see cref="SaveAnchorResult.FAILURE_INSUFFICIENT_QUALITY"/></li>
        /// </ul>
        /// </param>
        public void SaveAnchorWithResult(ARAnchor anchor, Action<SaveAnchorResult> onSavedCallback = null)
        {
            int hashCode = anchor.trackableId.GetHashCode();
            hashCode = (hashCode * 4999559) + DateTime.Now.GetHashCode();
            SaveAnchorWithResult(anchor, hashCode.ToString(), onSavedCallback);
        }

        /// <summary>
        /// Instantiate an <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/> from local storage. Can invoke a callback.
        /// The loading of the <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/> is not guaranteed.
        /// </summary>
        /// <param name="anchorName">Name of the saved anchor to load.</param>
        /// <param name="onLoadedCallback">A callback to execute when anchor loading is finished. Returns true if an anchor was successfully loaded, and false if it was not.</param>
        public void LoadSavedAnchor(string anchorName, Action<bool> onLoadedCallback = null)
        {
            if (!_isStoreLoaded)
            {
                onLoadedCallback?.Invoke(false);
                return;
            }

            if (anchorName == string.Empty)
            {
                Debug.LogWarning("Can't create an anchor with an empty name.");
                onLoadedCallback?.Invoke(false);
                return;
            }

            _anchorsToLoad.Enqueue(new LoadAnchorData(anchorName, onLoadedCallback));

            // Start the 'Load Anchors' thread if it's not already running
            if (Interlocked.CompareExchange(ref _loadAnchorsWorkerRunning, 1, 0) == 0)
            {
                // The thread has finished working, but is still alive.
                if (_loadAnchorsWorker.IsAlive)
                {
                    _loadAnchorsWorker.Join();
                }
                _loadAnchorsWorker.Start();
            }
        }

        /// <summary>
        /// Instantiate all <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/>s from local storage. Can invoke a callback.
        /// Loading of all <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/>s is not guaranteed.
        /// </summary>
        /// <param name="onLoadedCallback">A callback to execute when anchor loading is finished. Returns true if all anchors were successfully loaded, and false if they were not.</param>
        public void LoadAllSavedAnchors(Action<bool> onLoadedCallback = null)
        {
            if (!_isStoreLoaded)
            {
                return;
            }

            string[] anchorNames = GetSavedAnchorNames();
            foreach (var anchorName in anchorNames)
            {
                LoadSavedAnchor(anchorName, onLoadedCallback);
            }
        }

        /// <summary>
        /// Delete a saved anchor from the local storage.
        /// </summary>
        /// <param name="anchorName">Name of the anchor to delete.</param>
        public void DeleteSavedAnchor(string anchorName)
        {
            if (!_isStoreLoaded)
            {
                return;
            }

            if (anchorName == string.Empty)
            {
                Debug.LogError("Can't delete an anchor with an empty name.");
                return;
            }

            _feature.TryUnpersistSpatialAnchor(_spatialAnchorStore, anchorName);
        }

        /// <summary>
        /// Delete all saved anchors from the local storage.
        /// </summary>
        public void ClearStore()
        {
            if (_isStoreLoaded && _feature.TryClearSpatialAnchorStoreMSFT(_spatialAnchorStore))
            {
                var provider = (SpatialAnchorsSubsystem.SpatialAnchorsProvider)_subsystem.GetProvider();
                provider.ClearAllAnchorSavedNames();
            }
        }

        /// <summary>
        /// Query the local storage for all saved anchor names.
        /// </summary>
        /// <returns>Unordered string array with all saved anchor names.</returns>
        public string[] GetSavedAnchorNames()
        {
            if (!_isStoreLoaded)
            {
                return Array.Empty<string>();
            }

            _feature.TryEnumeratePersistedSpatialAnchorNames(_spatialAnchorStore, out string[] namesList);
            return namesList;
        }

        /// <summary>
        /// Query the local storage for the name of a saved anchor from an <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/>.
        /// </summary>
        /// <param name="anchor">The saved <see cref="UnityEngine.XR.ARFoundation.ARAnchor"/> to search for.</param>
        /// <returns>The name of the saved anchor or an empty string if no anchor exists.</returns>
        public string GetSavedAnchorNameFromARAnchor(ARAnchor anchor)
        {
            if (!_isStoreLoaded)
            {
                return string.Empty;
            }

            var provider = (SpatialAnchorsSubsystem.SpatialAnchorsProvider)_subsystem.GetProvider();
            return provider.TryGetSavedNameFromTrackableId(anchor.trackableId);
        }
    }
}
