using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Chromodoris.IsoSurfacing;
using Chromodoris.Properties;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Chromodoris.components
{
    public class CloseVoxelGridFromListComponent : GH_Component
    {
        private int _inNXIdx;
        private int _inNYIdx;
        private int _inNZIdx;
        private int _inVIdx;
        private int _outVcIdx;

        /// <summary>
        ///     Initializes a new instance of the CloseVoxelGridFromList class.
        /// </summary>
        public CloseVoxelGridFromListComponent() : base("CloseVoxelGridFromList",
            "CloseFromList", "Closes a voxel grid.", "ChromodorisBV", "Isosurface")
        {
        }

        /// <summary>
        ///     Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon =>
            Resources.Icon_Close_Voxel_Grid;

        /// <summary>
        ///     Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid =>
            new Guid("8a9ad93a-b827-4f3e-82cf-6603c83eb80a");

        /// <summary>
        ///     Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            _inVIdx = pManager.AddNumberParameter("Values", "V",
                "Distances values for voxels.", GH_ParamAccess.list);
            _inNXIdx = pManager.AddIntegerParameter("Number X", "NX",
                "Number of voxels along X axis", GH_ParamAccess.item);
            _inNYIdx = pManager.AddIntegerParameter("Number Y", "NY",
                "Number of voxels along X axis", GH_ParamAccess.item);
            _inNZIdx = pManager.AddIntegerParameter("Number Z", "NZ",
                "Number of voxels along X axis", GH_ParamAccess.item);
        }

        /// <summary>
        ///     Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            _outVcIdx = pManager.AddNumberParameter("Closed grid.", "Vc",
                "Distance values of closed voxel grid.", GH_ParamAccess.list);
        }

        /// <summary>
        ///     This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The da object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            var values = new List<double>();
            var nX = 0;
            var nY = 0;
            var nZ = 0;

            var requiredDataGotten = new List<bool>
            {
                da.GetDataList(_inVIdx, values),
                da.GetData(_inNXIdx, ref nX),
                da.GetData(_inNYIdx, ref nY),
                da.GetData(_inNZIdx, ref nZ),
            };

            if (requiredDataGotten.Any(x => x is false))
            {
                return;
            }

            if (values.Count != nX * nY * nZ)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Grid resolution doesn't match length of values.");
                return;
            }

            var voxelGrid = new SparseVoxelGrid<VoxelData>(values, nX, nY, nZ);

            voxelGrid.Close();

            _ = da.SetDataList(_outVcIdx,
                voxelGrid.Select(v => new GH_Number(v.Value)));
        }
    }
}
