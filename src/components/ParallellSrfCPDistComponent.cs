using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Grasshopper.Kernel;

using Rhino.Geometry;

namespace Chromodoris
{
    public class ParallellSrfCPDistComponent : GH_TaskCapableComponent<ParallellSrfCPDistComponent.SolveResult>
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
                GH_ParamAccess.item);

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
                GH_ParamAccess.item);
        }

        private delegate void Del();

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Del errorNoPtsDelegate = () => AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No closest point found.");

            if (InPreSolve)
            {
                // First pass; collect data and construct tasks

                SolveData solveData = GetInputs(DA);

                Task<SolveResult> tsk = null;
                {
                    tsk = Task.Run(() => ComputeDistance(solveData, errorNoPtsDelegate), CancelToken);
                }
                TaskList.Add(tsk);

                return;
            }

            if (!GetSolveResults(DA, out SolveResult results))
            {
                // Compute right here, right now.
                // 1. Collect
                SolveData solveData = GetInputs(DA);

                // 2. Compute
                results = ComputeDistance(solveData, errorNoPtsDelegate);
            }

            // 3. Set
            if (results != null)
            {
                _ = DA.SetData(_outDistIdx, results.Value);
            }
        }

        private SolveData GetInputs(IGH_DataAccess DA)
        {
            Point3d pt = new Point3d();
            Surface srf = null;
            if (!DA.GetData(_inSamplePtsIdx, ref pt))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input P failed to collect data.");
            }
            if (!DA.GetData(_inSrfIdx, ref srf))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input S failed to collect data.");
            }

            return new SolveData(pt, srf);
        }

        //private List<double> GetDistances(SolveData solveData)
        //{
        //    var dists = new double[solveData.Pts.Count];

        //    // Load balance = True, based on the assumption that this operation's
        //    // execution time varies.
        //    // var partitioner = Partitioner.Create(Enumerable.Range(0, pts.Length).ToList(), true);

        //    // Larger ranges to use be able top update thread specific u1 and v1.
        //    var partitioner = Partitioner.Create(0, solveData.Pts.Count);

        //    _ = Parallel.ForEach(partitioner, (range, loopstate) => ComputeRange(dists, solveData, range, loopstate));

        //    return dists.ToList();
        //}

        //private static void ComputeRange(in double[] resultList, SolveData solveData, Tuple<int, int> range, ParallelLoopState loopstate)
        //{
        //    for (int i = range.Item1; i < range.Item2; i++)
        //    {
        //        if (!solveData.Srf.ClosestPoint(solveData.Pts[i], out double u, out double v))
        //        {
        //            // AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Closest point not found.");
        //            loopstate.Break();
        //        }
        //        resultList[i] = solveData.Pts[i].DistanceTo(solveData.Srf.PointAt(u, v));
        //    }
        //    // GC.Collect(2, GCCollectionMode.Optimized);
        //}

        //protected override void AfterSolveInstance() => GC.Collect();
        private static SolveResult ComputeDistance(SolveData solveData, in Del errorNoPtsDelegate)
        {
            if (solveData.Srf.ClosestPoint(solveData.Pt, out double u, out double v))
            {
                return new SolveResult(solveData.Pt.DistanceTo(solveData.Srf.PointAt(u, v)));
            }

            // Raise if no pt found
            errorNoPtsDelegate();
            return null;
        }

        private readonly struct SolveData
        {
            public Point3d Pt { get; }
            public Surface Srf { get; }
            public SolveData(Point3d pt, in Surface srf)
            {
                Pt = pt;
                Srf = srf;
            }
        }
        public class SolveResult
        {
            public double Value { get; }
            public SolveResult(double value)
            {
                Value = value;
            }
        }
    }
}
