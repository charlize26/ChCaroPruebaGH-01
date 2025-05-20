/*
 * Copyright (c) Qualcomm Technologies, Inc. and/or its subsidiaries.
 * All rights reserved.
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 */

namespace Qualcomm.Snapdragon.Spaces
{
    /// <summary>
    /// Defines the type of the composition layer.
    /// </summary>
    public enum SpacesCompositionLayerType
    {
        /// <summary>
        /// A texture will be projected onto a single-sided quadrilateral.
        /// </summary>
        Quad = 1,
        /// <summary>
        /// A cube-map will be projected onto the interior faces of a cube.
        /// </summary>
        Cube = 2,
        /// <summary>
        /// A texture will be projected onto the interior surface of a cylindrical section. Like a curved TV.
        /// </summary>
        Cylinder = 3,
        /// <summary>
        /// An equirectangular texture will be projected onto the interior surface of a sphere.
        /// </summary>
        SphericalEquirect = 4,
        //Passthrough = 5
    }
}
