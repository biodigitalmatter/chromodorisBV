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

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Chromodoris
{
    public class VoxelSampleDual : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the VoxelSampleDual class.
        /// </summary>
        public VoxelSampleDual()
          : base("Sample Voxels (Dual)", "VoxelSample(D)",
              "Construct and sample a voxel grid from two point cloud affecting eachover.",
              "ChromodorisBV", "Isosurface")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>

        int InP1Idx;
        int InP2Idx;
        int InBIdx;
        int InXIdx;
        int InYIdx;
        int InZIdx;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            InP1Idx = pManager.AddPointParameter("Pointcloud 1", "P1", "First cloud to sample.", GH_ParamAccess.list);
            InP2Idx = pManager.AddPointParameter("Pointcloud 2", "P2", "Second cloud to sample.", GH_ParamAccess.list);
            InBIdx = pManager.AddBoxParameter("Box", "B", "The box representing the voxel grid.", GH_ParamAccess.item);
            InXIdx = pManager.AddIntegerParameter("X Resolution", "X", "The number of grid cells in the X-direction.", GH_ParamAccess.item);
            InYIdx = pManager.AddIntegerParameter("Y Resolution", "Y", "The number of grid cells in the Y-direction.", GH_ParamAccess.item);
            InZIdx = pManager.AddIntegerParameter("Z Resolution", "Z", "The number of grid cells in the Z-direction.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>

        int OutBIdx;
        int OutDIdx;
        int OutPIdx;
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            OutBIdx = pManager.AddBoxParameter("Box", "B", "The generated box representing voxel grid.", GH_ParamAccess.item);
            OutDIdx = pManager.AddGenericParameter("Voxel Data 1", "D", "Voxel data stored in an array.", GH_ParamAccess.list);
            OutPIdx = pManager.AddPointParameter("Voxel center points", "P", "Voxel center points.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var pointCloud1 = new List<Point3d>();
            var pointCloud2 = new List<Point3d>();
            var xr = 0;
            var yr = 0;
            var zr = 0;
            var box = new Box();

            if (!DA.GetDataList(InP1Idx, pointCloud1))
            {
                return;
            }

            if (!DA.GetDataList(InP2Idx, pointCloud2))
            {
                return;
            }

            if (!DA.GetData(InBIdx, ref box))
            {
                return;
            }

            if (!DA.GetData(InXIdx, ref xr))
            {
                return;
            }

            if (!DA.GetData(InYIdx, ref yr))
            {
                return;
            }

            if (!DA.GetData(InZIdx, ref zr))
            {
                return;
            }

            var sampler = new VoxelSamplerDual(pointCloud1, pointCloud2, box, xr, yr, zr);
            sampler.ExecuteMultiThreaded();

            _ = DA.SetData(OutBIdx, sampler.BBox);
            _ = DA.SetDataList(OutDIdx, sampler.VoxelValuesList);
            _ = DA.SetDataList(OutPIdx, sampler.VoxelPtsList);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.Icons_Isosurface_Custom;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("cb4e6b5c-6b2a-4a4e-9ab1-6c16ae102760");
    }
}
