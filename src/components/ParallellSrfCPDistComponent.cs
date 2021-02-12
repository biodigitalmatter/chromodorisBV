using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Grasshopper.Kernel;

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
          : base(
                "Parallell Srf Closest Point Distance",
                "ParSrfCPDist",
                "Find the distance to the closest point on surface, in parallell",
                "ChromodorisBV",
                "Extra")
        {
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("35EF6DCE-F540-40EC-99BB-6C32A75BCE89");

        // Test if this speeds up component
        public override bool IsPreviewCapable => false;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        //You can add image files to your project resources and access them like this:
        // return Resources.IconForThisComponent;
        protected override System.Drawing.Bitmap Icon => null;

        #endregion Properties

        #region Methods
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            _inSamplePtsIdx = pManager.AddPointParameter(
                "Points",
                "P",
                "Sample point",
                GH_ParamAccess.list);

            _inSrfIdx = pManager.AddSurfaceParameter(
                "Surface",
                "S",
                "Base surface.",
                GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            _outDistIdx = pManager.AddNumberParameter(
                "Distance",
                "D",
                "Distance between sample point and closest point on surface.",
                GH_ParamAccess.list);
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
                return;

            var pts_array = pts.ToArray();

            if (!DA.GetData(_inSrfIdx, ref srf))
                return;

            _ = DA.SetDataList(_outDistIdx, GetDistances(pts_array, srf));
        }

        internal List<double> GetDistances(Point3d[] pts, Surface srf)
        {
            var dists = new double[pts.Length];

            // Load balance = True, based on the assumption that this operation's
            // execution time varies.
            // var partitioner = Partitioner.Create(Enumerable.Range(0, pts.Length).ToList(), true);

            // Larger ranges to use be able top update thread specific u1 and v1.
            var partitioner = Partitioner.Create(0, pts.Length);

            _ = Parallel.ForEach(partitioner, (range, loopstate) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    if (!srf.ClosestPoint(pts[i], out double u, out double v))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Closest point not found.");
                        loopstate.Break();
                    }
                    dists[i] = pts[i].DistanceTo(srf.PointAt(u, v));
                }
            });

            return dists.ToList();
        }

        #endregion Methods
    }
}
