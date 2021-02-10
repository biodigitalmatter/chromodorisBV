using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Chromodoris.components
{
    public class AverageDistancesToPointcloudsComponent : GH_Component
    {
        #region Fields

        private int InSamplePtsIdx;
        private int InPtCloudsIdx;
        private int InNToAverageIdx;
        private int OutAvgDistIdx;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the AverageDistancesToPointclouds class.
        /// </summary>
        public AverageDistancesToPointcloudsComponent()
          : base("AverageDistancesToPointclouds", "AvgDistCP",
                 "Finds shortest distances to point in N number of point clouds.", "ChromodorisBV", "Extra")
        {
        }

        #endregion Constructors



        #region Properties

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("D651E611-871B-436D-A987-3E401CB6B2AF");

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
            InSamplePtsIdx = pManager.AddPointParameter("Search points", "P", "Points to search from", GH_ParamAccess.list);
            InPtCloudsIdx = pManager.AddPointParameter("Pointclouds", "C", "Pointclouds to search", GH_ParamAccess.tree);
            InNToAverageIdx = pManager.AddIntegerParameter("Distances", "ND", "Number of distances to average", GH_ParamAccess.item, 2);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            OutAvgDistIdx = pManager.AddNumberParameter("AvgDist", "D", "Average distance", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var searchPts = new List<Point3d>();
            var nToAverage = 0;

            if (!DA.GetDataTree(InPtCloudsIdx, out GH_Structure<GH_Point> ptCloudTree))
            {
                return;
            }

            if (ptCloudTree.PathCount < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Need more than one set of clouds to average results.");
            }

            if (!DA.GetDataList(InSamplePtsIdx, searchPts))
            {
                return;
            }

            DA.GetData(InNToAverageIdx, ref nToAverage);

            var averageClass = new AverageDistanceToPointclouds(searchPts, ptCloudTree, nToAverage);

            DA.SetDataList(OutAvgDistIdx, averageClass.ComputeMultiThreaded());
        }

        #endregion Methods
    }
}
