/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

using System.Collections.Generic;

namespace Qualcomm.Snapdragon.Spaces
{
    internal partial class CompositionLayersFeature
    {
        /*
         * Proper handling of the public static lifecycle methods in SpacesComposition.cs ListenFor... and StopListeningFor
         * requires that some additional information about the target of the invocation is saved for later.
         * Ensures that calls in the style of .OnLayerEvent += callback.Invoke; and .OnLayerEvent -= callback.Invoke; work as expected.
         */
        private static readonly Dictionary<object, CompositionLayerCreatedEventHandler> _createdEventHandlers = new ();
        private static readonly Dictionary<object, CompositionLayerDestroyedEventHandler> _destroyedEventHandlers = new ();

        public delegate void CompositionLayerCreatedEventHandler(uint layerId);
        private CompositionLayerCreatedEventHandler _onCompositionLayerCreated;
        public event CompositionLayerCreatedEventHandler OnCompositionLayerCreated
        {
            // Override the add and remove calls to the event to ensure that the calls in the style += callback.Invoke and -= callback.Invoke are correctly processed.
            add
            {
                if (_createdEventHandlers.TryGetValue(value.Target, out var handler))
                {
                    _onCompositionLayerCreated -= handler;
                }

                var newHandler = new CompositionLayerCreatedEventHandler(value);
                _createdEventHandlers[value.Target] = newHandler;
                _onCompositionLayerCreated += newHandler;
            }
            remove
            {
                if (_createdEventHandlers.Remove(value.Target, out var handler))
                {
                    _onCompositionLayerCreated -= handler;
                }
            }
        }

        public delegate void CompositionLayerDestroyedEventHandler(uint layerId);
        private CompositionLayerDestroyedEventHandler _onCompositionLayerDestroyed;
        public event CompositionLayerDestroyedEventHandler OnCompositionLayerDestroyed
        {
            // Override the add and remove calls to the event to ensure that the calls in the style += callback.Invoke and -= callback.Invoke are correctly processed.
            add
            {
                if (_destroyedEventHandlers.TryGetValue(value.Target, out var handler))
                {
                    _onCompositionLayerDestroyed -= handler;
                }

                var newHandler = new CompositionLayerDestroyedEventHandler(value);
                _destroyedEventHandlers[value.Target] = newHandler;
                _onCompositionLayerDestroyed += newHandler;
            }
            remove
            {
                if (_destroyedEventHandlers.Remove(value.Target, out var handler))
                {
                    _onCompositionLayerDestroyed -= handler;
                }
            }
        }
    }
}
