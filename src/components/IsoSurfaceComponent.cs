/*
 *      ___  _  _  ____   __   _  _   __  ____   __  ____  __  ____
 *     / __)/ )( \(  _ \ /  \ ( \/ ) /  \(    \ /  \(  _ \(  )/ ___)
 *    ( (__ ) __ ( )   /(  O )/ \/ \(  O )) D ((  O ))   / )( \___ \
 *     \___)\_)(_/(__\_) \__/ \_)(_/ \__/(____/ \__/(__\_)(__)(____/
 *
 *    Copyright Cameron Newnham 2015-2016
 *
 *    This program is free software: you can redistribute it and/or modify
 *    it under the terms of the GNU General Public License as published by
 *    the Free Software Foundation, either version 3 of the License, or
 *    (at your option) any later version.
 *
 *    This program is distributed in the hope that it will be useful,
 *    but WITHOUT ANY WARRANTY; without even the implied warranty of
 *    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *    GNU General Public License for more details.
 *
 *    You should have received a copy of the GNU General Public License
 *    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Chromodoris.IsoSurfacing;
using Chromodoris.Properties;

using Grasshopper.Kernel;

using Rhino.Geometry;

namespace Chromodoris.Components
{
    // ReSharper disable once UnusedType.Global
    public class IsosurfaceComponent : GH_Component
    {
        private int _inBIdx;
        private int _inDIdx;
        private int _inVIdx;
        private int _outMIdx;

        /// <summary>
        ///     Initializes a new instance of the IsoMesh class.
        /// </summary>
        public IsosurfaceComponent() : base("Build IsoSurface", "IsoSurface",
            "Constructs a 3D isosurface from voxel data (float[x,y,z]) and a box.",
            "ChromodorisBV", "Isosurface")
        {
        }

        /// <summary>
        ///     Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon =>
            Resources.Icon_Isosurface;

        /// <summary>
        ///     Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid =>
            new Guid("{8726c6b0-f222-4fd9-9882-dd0cd0067988}");

        /// <summary>
        ///     Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            _inBIdx = pManager.AddBoxParameter("Box", "B", "The bounding box.",
                GH_ParamAccess.item);
            _inDIdx = pManager.AddGenericParameter("Voxel Data", "D",
                "Voxelization data formatted as double[x,y,z].", GH_ParamAccess.item);
            _inVIdx = pManager.AddNumberParameter("Sample Value", "V",
                "The value to sample at, ie. IsoValue", GH_ParamAccess.item);
        }

        /// <summary>
        ///     Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            _outMIdx = pManager.AddMeshParameter("IsoSurface", "M",
                "The generated isosurface.", GH_ParamAccess.item);
        }

        /// <summary>
        ///     This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            var box = new Box();
            var isoValue = 0.0;
            float[,,] voxelData = null;

            var requiredDataGotten = new List<bool>
            {
                da.GetData(_inBIdx, ref box),
                da.GetData(_inDIdx, ref voxelData),
                da.GetData(_inVIdx, ref isoValue),
            };

            // Check if any of the required parameters where not given
            if (requiredDataGotten.Any(x => x is false))
            {
                return;
            }

            var isoSurfacer = new IsoSurfacer(voxelData, isoValue, box);

            da.SetData(_outMIdx, isoSurfacer.GenerateSurfaceMesh());
        }
    }
}
