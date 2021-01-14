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
              "Chromodoris", "Isosurface")
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
        int InRIdx;
        int InDIdx;
        int InLIdx;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            InP1Idx = pManager.AddPointParameter("Pointcloud 1", "P1", "First cloud to sample.", GH_ParamAccess.list);
            InP2Idx = pManager.AddPointParameter("Pointcloud 2", "P2", "Second cloud to sample.", GH_ParamAccess.list);
            InBIdx = pManager.AddBoxParameter("Box", "B", "The box representing the voxel grid.", GH_ParamAccess.item);
            InXIdx = pManager.AddIntegerParameter("X Resolution", "X", "The number of grid cells in the X-direction.", GH_ParamAccess.item);
            InYIdx = pManager.AddIntegerParameter("Y Resolution", "Y", "The number of grid cells in the Y-direction.", GH_ParamAccess.item);
            InZIdx = pManager.AddIntegerParameter("Z Resolution", "Z", "The number of grid cells in the Z-direction.", GH_ParamAccess.item);
            InRIdx = pManager.AddNumberParameter("Effective Range", "R", "The maximum search range for voxel sampling.", GH_ParamAccess.item);
            InDIdx = pManager.AddBooleanParameter("Density Sampling", "D", "Toggle point density affecting the point values", GH_ParamAccess.item, false);
            InLIdx = pManager.AddBooleanParameter("Linear Sampling", "L", "Toggle falloff from exponential to linear", GH_ParamAccess.item, true);
            pManager[InLIdx].Optional = true;
            pManager[InDIdx].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>

        int OutBIdx;
        int OutD1Idx;
        int OutD2Idx;
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            OutBIdx = pManager.AddBoxParameter("Box", "B", "The generated box representing voxel grid.", GH_ParamAccess.item);
            OutD1Idx = pManager.AddGenericParameter("Voxel Data 1", "D1", "Voxel data as float[x,y,z]", GH_ParamAccess.item);
            OutD2Idx = pManager.AddGenericParameter("Voxel Data 2", "D2", "Voxel data as float[x,y,z]", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> pointCloud1 = new List<Point3d>();
            List<Point3d> pointCloud2 = new List<Point3d>();
            List<double> charges = new List<double>();
            int xr = 0;
            int yr = 0;
            int zr = 0;
            Box box = new Box();
            double range = 0;
            bool bulge = false;
            bool linear = true;

            if (!DA.GetDataList(InP1Idx, pointCloud1))
            {
                return;
            }

            if (!DA.GetDataList(InP2Idx, pointCloud2))
            {
                return;
            }

            // DA.GetDataList("Charges", charges); // Optional
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

            if (!DA.GetData(InRIdx, ref range))
            {
                return;
            }

            DA.GetData(InDIdx, ref bulge); // Optional
            DA.GetData(InLIdx, ref linear); // Optional

            if (range <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Range must be larger than 0.");
                return;
            }

            if (charges.Count != 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Charges not implemented yet.");
            }

            var sampler = new VoxelSamplerDual(pointCloud1, pointCloud2, charges, box, xr, yr, zr, range, bulge, linear);
            sampler.Init();
            sampler.executeMultiThreaded();

            _ = DA.SetData(OutBIdx, sampler.BBox);
            _ = DA.SetData(OutD1Idx, sampler.SampledData1);
            _ = DA.SetData(OutD2Idx, sampler.SampledData2);
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
