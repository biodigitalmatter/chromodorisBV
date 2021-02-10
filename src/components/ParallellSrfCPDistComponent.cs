using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Chromodoris
{
    public class ParallellSrfCPDistComponent : GH_Component
    {
        #region Fields

        private int _inSamplePtsIdx;
        private int _inSrfIdx;
        private int _outDistIdx;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the AverageDistancesToPointclouds class.
        /// </summary>
        public ParallellSrfCPDistComponent()
          : base("Parallell Srf Closest Point Distance", "ParSrfCPDist",
                 "Find the distance to the closest point on surface, in parallell", "ChromodorisBV", "Extra")
        {
        }

        #endregion Constructors



        #region Properties

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("35EF6DCE-F540-40EC-99BB-6C32A75BCE89");
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        #endregion Properties



        #region Methods

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            _inSamplePtsIdx = pManager.AddPointParameter("Points", "P", "Sample point", GH_ParamAccess.list);
            _inSrfIdx = pManager.AddSurfaceParameter("Surface", "S", "Base surface.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            _outDistIdx = pManager.AddNumberParameter("Distance", "D", "Distance between sample point and closest point on surface.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var pts = new List<Point3d>();
            Surface srf = null;

            if (!DA.GetDataList(_inSamplePtsIdx, pts))
            {
                return;
            }

            if (!DA.GetData(_inSrfIdx, ref srf))
            {
                return;
            }


            var parSrfDistCls = new ParallellSrfCPDist(pts, srf, false);

            DA.SetDataList(_outDistIdx, parSrfDistCls.ComputeMultiThreaded());
        }

        #endregion Methods
    }
}
