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
    public class VoxelSampleDual : GH_Component
    {
        private int _inBIdx;
        private int _inP1Idx;
        private int _inP2Idx;
        private int _inXIdx;
        private int _inYIdx;
        private int _inZIdx;
        private int _inZYXIdx;
        private int _outBIdx;
        private int _outD1Idx;
        private int _outD2Idx;
        private int _outFIdx;
        private int _outPIdx;


        /// <summary>
        ///     Initializes a new instance of the VoxelSampleDual class.
        /// </summary>
        public VoxelSampleDual() : base("Sample Voxels (Dual)", "VoxelSample(D)",
            "Construct and sample a voxel grid from two point cloud affecting each other.",
            "ChromodorisBV", "Isosurface")
        {
        }


        /// <summary>
        ///     Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid =>
            new Guid("cb4e6b5c-6b2a-4a4e-9ab1-6c16ae102760");

        /// <summary>
        ///     Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon => Resources.Icons_Isosurface_Custom;

        public override bool IsPreviewCapable => false;


        /// <summary>
        ///     Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            _inP1Idx = pManager.AddPointParameter("Pointcloud 1", "P1",
                "First cloud to sample.", GH_ParamAccess.list);

            _inP2Idx = pManager.AddPointParameter("Pointcloud 2", "P2",
                "Second cloud to sample.", GH_ParamAccess.list);

            _inBIdx = pManager.AddBoxParameter("Box", "B",
                "The box representing the voxel grid.", GH_ParamAccess.item);

            _inXIdx = pManager.AddIntegerParameter("X Resolution", "X",
                "The number of grid cells in the X-direction.", GH_ParamAccess.item);

            _inYIdx = pManager.AddIntegerParameter("Y Resolution", "Y",
                "The number of grid cells in the Y-direction.", GH_ParamAccess.item);

            _inZIdx = pManager.AddIntegerParameter("Z Resolution", "Z",
                "The number of grid cells in the Z-direction.", GH_ParamAccess.item);

            _inZYXIdx = pManager.AddBooleanParameter("ZYX output", "ZYX",
                "Order output first by Z dimension, then Y and last X. Defaults to True.",
                GH_ParamAccess.item, false);
        }

        /// <summary>
        ///     Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            _outBIdx = pManager.AddBoxParameter("Box", "B",
                "The generated box representing voxel grid.", GH_ParamAccess.item);

            _outFIdx = pManager.AddGenericParameter("Distance factor", "F",
                "Distance to closest point in first pointcloud compared to closest point in second.",
                GH_ParamAccess.list);

            _outD1Idx = pManager.AddGenericParameter("Distance to CP in first PtCloud",
                "D1", "Absolute distance to closest point in first pointcloud.",
                GH_ParamAccess.list);

            _outD2Idx = pManager.AddGenericParameter("Distance to CP in 2nd PtCloud",
                "D2", "Absolute distance to closest point in second pointcloud.",
                GH_ParamAccess.list);

            _outPIdx = pManager.AddPointParameter("Voxel center points", "P",
                "Voxel center points.", GH_ParamAccess.list);
        }

        /// <summary>
        ///     This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            var ptCloud1 = new List<Point3d>();
            var ptCloud2 = new List<Point3d>();
            var xr = 0;
            var yr = 0;
            var zr = 0;
            var box = new Box();
            var zyx = true;

            var requiredDataGotten = new List<bool>
            {
                da.GetDataList(_inP1Idx, ptCloud1),
                da.GetDataList(_inP2Idx, ptCloud2),
                da.GetData(_inBIdx, ref box),
                da.GetData(_inXIdx, ref xr),
                da.GetData(_inYIdx, ref yr),
                da.GetData(_inZIdx, ref zr),
            };

            // Check if any of the required parameters where not given
            if (requiredDataGotten.Any(x => x is false))
            {
                return;
            }

            _ = da.GetData(_inZYXIdx, ref zyx);

            var sampler =
                new VoxelSamplerDual(ptCloud1, ptCloud2, box, xr, yr, zr, zyx);
            sampler.ExecuteMultiThreaded();

            int voxelDataCount = sampler.NVoxels;

            var distFactors = new List<GH_Number>(voxelDataCount);
            var distsPtCloud1 = new List<GH_Number>(voxelDataCount);
            var distsPtCloud2 = new List<GH_Number>(voxelDataCount);
            var centerPts = new List<GH_Point>(voxelDataCount);

            foreach (VoxelSamplerDual.GHVoxelData voxelData in sampler.GHVoxelDataList)
            {
                distFactors.Add(voxelData.DistFactor);
                distsPtCloud1.Add(voxelData.DistPtCloud1);
                distsPtCloud2.Add(voxelData.DistPtCloud2);
                centerPts.Add(voxelData.CenterPt);
            }

            _ = da.SetData(_outBIdx, sampler.BBox);
            _ = da.SetDataList(_outFIdx, distFactors);
            _ = da.SetDataList(_outD1Idx, distsPtCloud1);
            _ = da.SetDataList(_outD2Idx, distsPtCloud2);
            _ = da.SetDataList(_outPIdx, centerPts);
        }
    }
}
