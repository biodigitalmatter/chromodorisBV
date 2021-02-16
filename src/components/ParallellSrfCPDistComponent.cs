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
        private int _inSamplePtsIdx;
        private int _inSrfIdx;
        private int _outDistIdx;

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
            Surface srf = null;
            var pts = new List<Point3d>();

            if (!DA.GetDataList(_inSamplePtsIdx, pts))
            {
                return;
            }

            if (!DA.GetData(_inSrfIdx, ref srf))
            {
                return;
            }

            SolveData solveData = new SolveData(pts, srf);

            _ = DA.SetDataList(_outDistIdx, GetDistances(solveData));
        }

        private List<double> GetDistances(SolveData solveData)
        {
            var dists = new double[solveData.Pts.Count];

            // Load balance = True, based on the assumption that this operation's
            // execution time varies.
            // var partitioner = Partitioner.Create(Enumerable.Range(0, pts.Length).ToList(), true);

            // Larger ranges to use be able top update thread specific u1 and v1.
            var partitioner = Partitioner.Create(0, solveData.Pts.Count);

            _ = Parallel.ForEach(partitioner, (range, loopstate) => ComputeRange(dists, solveData, range, loopstate));

            return dists.ToList();
        }

        private static void ComputeRange(in double[] resultList, SolveData solveData, Tuple<int, int> range, ParallelLoopState loopstate)
        {
            for (int i = range.Item1; i < range.Item2; i++)
            {
                if (!solveData.Srf.ClosestPoint(solveData.Pts[i], out double u, out double v))
                {
                    // AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Closest point not found.");
                    loopstate.Break();
                }
                resultList[i] = solveData.Pts[i].DistanceTo(solveData.Srf.PointAt(u, v));
            }
            // GC.Collect(2, GCCollectionMode.Optimized);
        }

        protected override void AfterSolveInstance() => GC.Collect();

        public readonly struct SolveData
        {
            public List<Point3d> Pts { get; }
            public Surface Srf { get; }
            public SolveData(in List<Point3d> pts, in Surface srf)
            {
                Pts = pts;
                Srf = srf;
            }
        }
    }
}
