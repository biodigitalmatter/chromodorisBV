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
using System.Linq;

using Grasshopper.Kernel;

using Rhino.Geometry;

namespace Chromodoris
{
    public class IsosurfaceFromListComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the IsoMesh class.
        /// </summary>
        public IsosurfaceFromListComponent()
          : base("Build IsoSurface from list", "IsoSurfaceFromList",
              "Constructs a 3D isosurface from voxel data in a flat list and a box.",
              "ChromodorisBV", "Isosurface")
        {
        }

        private int InBIdx;
        private int InDIdx;
        private int InVIdx;
        private int InXIdx;
        private int InYIdx;
        private int InZIdx;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            InBIdx = pManager.AddBoxParameter("Box", "B", "The bounding box.", GH_ParamAccess.item);
            InDIdx = pManager.AddNumberParameter("Voxel Data", "D", "Voxelization data formatted as double[x,y,z].", GH_ParamAccess.list);
            InVIdx = pManager.AddNumberParameter("Sample Value", "V", "The value to sample at, ie. IsoValue", GH_ParamAccess.item);
            InXIdx = pManager.AddIntegerParameter("X resolution", "X", "X resolution of bounding box", GH_ParamAccess.item);
            InYIdx = pManager.AddIntegerParameter("Y resolution", "Y", "Y resolution of bounding box", GH_ParamAccess.item);
            InZIdx = pManager.AddIntegerParameter("Z resolution", "Z", "Z resolution of bounding box", GH_ParamAccess.item);
        }

        private int OutMIdx;

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            OutMIdx = pManager.AddMeshParameter("IsoSurface", "M", "The generated isosurface.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var box = new Box();
            double isovalue = 0;
            var voxelData = new List<double>();

            var resX = 0;
            var resY = 0;
            var resZ = 0;

            if (!DA.GetData(InBIdx, ref box))
            {
                return;
            }
            if (!DA.GetDataList(InDIdx, voxelData))
            {
                return;
            }

            if (!DA.GetData(InVIdx, ref isovalue))
            {
                return;
            }
            if (!DA.GetData(InXIdx, ref resX))
            {
                return;
            }
            if (!DA.GetData(InYIdx, ref resY))
            {
                return;
            }
            if (!DA.GetData(InZIdx, ref resZ))
            {
                return;
            }
            var floatVoxelData = voxelData.Select<double, float>(x => (float)x).ToList();

            var vs = new VolumetricSpace((List<float>)floatVoxelData, resX, resY, resZ);
            var isosurface = new HashIsoSurface(vs);
            var mesh = new Mesh();

            isosurface.computeSurfaceMesh(isovalue, ref mesh);
            TransformMesh(mesh, box, vs.IsoData);
            DA.SetData(OutMIdx, mesh);

            voxelData = null;
            vs = null;
            isosurface = null;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Chromodoris.Properties.Resources.Icon_Isosurface;
                // return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{845E601C-7FA3-476D-B4A6-8AF2331B40E8}"); }
        }

        public void TransformMesh(Rhino.Geometry.Mesh mesh, Box _box, float[,,] data)
        {


            int x = data.GetLength(0) - 1;
            int y = data.GetLength(1) - 1;
            int z = data.GetLength(2) - 1;


            var gridBox = new Box(Plane.WorldXY, new Interval(0, x), new Interval(0, y), new Interval(0, z));
            gridBox.RepositionBasePlane(gridBox.Center);

            Transform trans = Transform.PlaneToPlane(gridBox.Plane, _box.Plane);
            trans = trans * Transform.Scale(gridBox.Plane, _box.X.Length / gridBox.X.Length, _box.Y.Length / gridBox.Y.Length, _box.Z.Length / gridBox.Z.Length);

            mesh.Transform(trans);
            mesh.Faces.CullDegenerateFaces();
        }
    }
}
