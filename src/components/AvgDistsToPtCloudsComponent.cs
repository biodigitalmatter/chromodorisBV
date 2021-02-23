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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

using Chromodoris.IsoSurfacing;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace Chromodoris.Components
{
    public class AvgDistsToPtCloudsComponent : GH_TaskCapableComponent<
        AvgDistsToPtCloudsComponent.SolveResults>
    {
        private int _inNDistsToAvgIdx;
        private int _inPtCloudsIdx;
        private int _inSearchPtsIdx;
        private int _outAvgDistsIdx;

        /// <summary>
        ///     Initializes a new instance of the AverageDistancesToPointclouds class.
        /// </summary>
        public AvgDistsToPtCloudsComponent() : base("AverageDistancesToPointclouds",
            "AvgDistToCP",
            "Finds shortest distances to point in N number of point clouds.",
            "ChromodorisBV", "Extra")
        {
        }

        // Test if this speeds up component
        public override bool IsPreviewCapable => false;

        /// <summary>
        ///     Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid =>
            new Guid("D651E611-871B-436D-A987-3E401CB6B2AF");

        /// <summary>
        ///     Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        ///     Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            _inSearchPtsIdx = pManager.AddPointParameter("Search points", "P",
                "Points to search from", GH_ParamAccess.item);

            _inPtCloudsIdx = pManager.AddPointParameter("Pointclouds", "C",
                "Pointclouds to search", GH_ParamAccess.tree);

            _inNDistsToAvgIdx = pManager.AddIntegerParameter("Number of distances",
                "ND", "Number of distances to average", GH_ParamAccess.item, 2);
        }

        /// <summary>
        ///     Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            _outAvgDistsIdx = pManager.AddNumberParameter("AvgDist", "D",
                "Average distance", GH_ParamAccess.item);
        }

        /// <summary>
        ///     This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            if (InPreSolve)
            {
                // First pass; collect data and construct tasks
                var searchPt = new Point3d();

                var nDistsToAvg = 0;
                _ = da.GetData(_inNDistsToAvgIdx, ref nDistsToAvg);

                Task<SolveResults> tsk = null;
                if (da.GetData(_inSearchPtsIdx, ref searchPt) &&
                    da.GetDataTree(_inPtCloudsIdx,
                        out GH_Structure<GH_Point> ptCloudDataTree))
                {
                    KDTreePtCloud[] ptCloudKdTrees =
                        PtCloudDataTreesToKdTrees(ptCloudDataTree);
                    tsk = Task.Run(
                        () => ComputeAvgDist(searchPt, ptCloudKdTrees, nDistsToAvg),
                        CancelToken);
                }

                TaskList.Add(tsk);

                return;
            }

            if (!GetSolveResults(da, out SolveResults results))
            {
                // Compute right here, right now.
                // 1. Collect
                var searchPt = new Point3d();

                if (!da.GetData(_inSearchPtsIdx, ref searchPt))
                {
                    return;
                }

                if (!da.GetDataTree(_inPtCloudsIdx,
                    out GH_Structure<GH_Point> ptCloudDataTree))
                {
                    return;
                }

                var nDistsToAvg = 0;
                _ = da.GetData(_inNDistsToAvgIdx, ref nDistsToAvg);

                KDTreePtCloud[] ptCloudKdTrees =
                    PtCloudDataTreesToKdTrees(ptCloudDataTree);

                // 2. Compute
                results = ComputeAvgDist(searchPt, ptCloudKdTrees, nDistsToAvg);
            }

            // 3. Set
            if (results != null)
            {
                _ = da.SetData(_outAvgDistsIdx, results.Value);
            }
        }

        private static SolveResults ComputeAvgDist(
            Point3d searchPt, IEnumerable<KDTreePtCloud> ptClouds,
            int nToAveragePerDistance
        )
        {
            // Don't make this PLINQ since this method is already running in parallel
            List<double> allDists = ptClouds
                .Select(tree => tree.GetClosestPtDistance(searchPt)).ToList();

            allDists.Sort();

            List<double> distsToAverage = allDists.GetRange(0, nToAveragePerDistance);

            return new SolveResults(distsToAverage.Average());
        }

        private static KDTreePtCloud[] PtCloudDataTreesToKdTrees(
            GH_Structure<GH_Point> dataTree
        )
        {
            var kdTrees = new KDTreePtCloud[dataTree.PathCount];

            for (var i = 0; i < dataTree.PathCount; i++)
            {
                List<Point3d> pts = dataTree.Branches[i].ConvertAll(x => x.Value);
                kdTrees[i] = new KDTreePtCloud(pts);
            }

            return kdTrees;
        }

        public class SolveResults
        {
            public SolveResults(double value) => Value = value;

            public double Value { get; }
        }
    }
}
