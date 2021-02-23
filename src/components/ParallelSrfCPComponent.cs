using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

using Grasshopper.Kernel;

using Rhino.Geometry;

namespace Chromodoris.Components
{
    public class ParallelSrfCPComponent : GH_Component
    {
        private int _inSamplePtsIdx;
        private int _inSrfIdx;
        private int _outDistIdx;

        /// <summary>
        ///     Initializes a new instance of the AverageDistancesToPointclouds class.
        /// </summary>
        public ParallelSrfCPComponent() : base("Parallel Srf Closest Point Distance",
            "ParSrfCPDist",
            "Find the distance to the closest point on surface, in parallel",
            "ChromodorisBV", "Extra")
        {
        }

        /// <summary>
        ///     Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid =>
            new Guid("35EF6DCE-F540-40EC-99BB-6C32A75BCE89");

        // Test if this speeds up component
        public override bool IsPreviewCapable => false;

        /// <summary>
        ///     Provides an Icon for the component.
        /// </summary>
        //You can add image files to your project resources and access them like this:
        // return Resources.IconForThisComponent;
        protected override Bitmap Icon => null;

        /// <summary>
        ///     Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            _inSamplePtsIdx = pManager.AddPointParameter("Points", "P", "Sample point",
                GH_ParamAccess.list);

            _inSrfIdx = pManager.AddSurfaceParameter("Surface", "S", "Base surface.",
                GH_ParamAccess.item);
        }

        /// <summary>
        ///     Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            _outDistIdx = pManager.AddNumberParameter("Distance", "D",
                "Distance between sample point and closest point on surface.",
                GH_ParamAccess.list);
        }

        /// <summary>
        ///     This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            Surface srf = null;
            var pts = new List<Point3d>();

            try
            {
                if (!da.GetDataList(_inSamplePtsIdx, pts))
                {
                    return;
                }
            }
            catch (OutOfMemoryException)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Too many points to access, out of memory error.");
            }

            if (!da.GetData(_inSrfIdx, ref srf))
            {
                return;
            }

            var solveData = new SolveData(pts, srf);

            _ = da.SetDataList(_outDistIdx, GetDistances(solveData));
        }

        private IEnumerable<double> GetDistances(SolveData solveData)
        {
            var dists = new double[solveData.Pts.Count];

            // Static parts
            OrderablePartitioner<Tuple<int, int>> partitioner =
                Partitioner.Create(0, solveData.Pts.Count);

            _ = Parallel.ForEach(partitioner,
                (range, loopState) => ComputeRange(dists, solveData, range, loopState));

            return dists.ToList();
        }

        private static void ComputeRange(
            in double[] resultList, SolveData solveData, Tuple<int, int> range,
            ParallelLoopState loopState
        )
        {
            for (int i = range.Item1; i < range.Item2; i++)
            {
                if (!solveData.Srf.ClosestPoint(solveData.Pts[i], out double u,
                    out double v))
                {
                    loopState.Break();
                    throw new Exception("No point found on surface.");
                }

                resultList[i] =
                    solveData.Pts[i].DistanceTo(solveData.Srf.PointAt(u, v));
            }
        }

        // TODO: This should be tested more to see if there's any impact.
        protected override void AfterSolveInstance() => GC.Collect();

        private readonly struct SolveData
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
