/*
 *      ___  _  _  ____   __   _  _   __  ____   __  ____  __  ____
 *     / __)/ )( \(  _ \ /  \ ( \/ ) /  \(    \ /  \(  _ \(  )/ ___)
 *    ( (__ ) __ ( )   /(  O )/ \/ \(  O )) D ((  O ))   / )( \___ \
 *     \___)\_)(_/(__\_) \__/ \_)(_/ \__/(____/ \__/(__\_)(__)(____/BV
 *
 *    ChromodorisBV is built on Chromodoris
 *    (https://bitbucket.org/camnewnham/chromodoris) by Cameron Newnham,
 *    copyright 2015-2016. ChromodorisBV is copyright Anton Tetov Johansson
 *    2020.
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
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace Chromodoris.Components
{
    // ReSharper disable once UnusedType.Global
    public class IsosurfaceFromListComponent : GH_Component
    {
        private int _inBIdx;
        private int _inIIdx;
        private int _inNXIdx;
        private int _inNYIdx;
        private int _inNZIdx;
        private int _inVIdx;

        private int _outMIdx;

        /// <summary>
        ///     Initializes a new instance of the IsoMesh class.
        /// </summary>
        public IsosurfaceFromListComponent() : base("Build isosurface from list",
            "IsoSurfaceFromList",
            "Constructs a 3D isosurface from voxel data in a flat list and a box.",
            "ChromodorisBV", "Isosurface")
        {
        }

        /// <summary>
        ///     Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon => Resources.Icon_Isosurface;

        // return null;
        /// <summary>
        ///     Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid =>
            new Guid("{845E601C-7FA3-476D-B4A6-8AF2331B40E8}");

        /// <summary>
        ///     Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            _inVIdx = pManager.AddNumberParameter("Values", "V",
                "Distance values in a flat, XYZ ordered list.", GH_ParamAccess.list);
            _inBIdx = pManager.AddBoxParameter("BBox", "B", "Bounding box.",
                GH_ParamAccess.item);
            _inNXIdx = pManager.AddIntegerParameter("Number X", "NX",
                "Number of voxels along X axis", GH_ParamAccess.item);
            _inNYIdx = pManager.AddIntegerParameter("Number Y", "NY",
                "Number of voxels along X axis", GH_ParamAccess.item);
            _inNZIdx = pManager.AddIntegerParameter("Number Z", "NZ",
                "Number of voxels along X axis", GH_ParamAccess.item);
            _inIIdx = pManager.AddNumberParameter("Isovalue", "I",
                "Value for the isosurface", GH_ParamAccess.item);
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
            var voxelData = new List<GH_Number>();

            var xRes = 0;
            var yRes = 0;
            var zRes = 0;

            var requiredDataGotten = new List<bool>
            {
                da.GetDataList(_inVIdx, voxelData),
                da.GetData(_inBIdx, ref box),
                da.GetData(_inNXIdx, ref xRes),
                da.GetData(_inNYIdx, ref yRes),
                da.GetData(_inNZIdx, ref zRes),
                da.GetData(_inIIdx, ref isoValue),
            };

            // Check if any of the required parameters where not given
            if (requiredDataGotten.Any(x => x is false))
            {
                return;
            }

            float[,,] isoData = UnflattenListTo3D(
                voxelData.ConvertAll(GH_NumberToFloatConverter()), xRes, yRes, zRes);

            var isoSurfacer = new IsoSurfacer(isoData, isoValue, box);

            da.SetData(_outMIdx, isoSurfacer.GenerateSurfaceMesh());
        }

        private static Converter<GH_Number, float> GH_NumberToFloatConverter()
        {
            return x => (float)x.Value;
        }

        private static T[,,] UnflattenListTo3D<T>(
            IList<T> list, int xRes, int yRes, int zRes
        )
        {
            var array = new T[xRes, yRes, zRes];
            var listIdx = 0;

            for (var x = 0; x < xRes; x++)
            {
                for (var y = 0; y < yRes; y++)
                {
                    for (var z = 0; z < zRes; z++)
                    {
                        array[x, y, z] = list[listIdx];

                        listIdx++;
                    }
                }
            }

            return array;
        }
    }
}
