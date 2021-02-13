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
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace Chromodoris
{
    public class AverageDistancesToPointcloudsComponent : GH_TaskCapableComponent<AverageDistancesToPointcloudsComponent.SolveResults>
    {
        #region Fields

        private int _inSearchPtsIdx;
        private int _inPtCloudsIdx;
        private int _inNDistsToAvgIdx;
        private int _outAvgDistsIdx;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the AverageDistancesToPointclouds class.
        /// </summary>
        public AverageDistancesToPointcloudsComponent()
          : base(
                "AverageDistancesToPointclouds",
                "AvgDistCP",
                "Finds shortest distances to point in N number of point clouds.",
                "ChromodorisBV",
                "Extra")
        {
        }

        #endregion Constructors

        #region Properties
        // Test if this speeds up component
        public override bool IsPreviewCapable => false;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("D651E611-871B-436D-A987-3E401CB6B2AF");

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        #endregion Properties

        #region Methods

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            _inSearchPtsIdx = pManager.AddPointParameter(
                "Search points",
                "P",
                "Points to search from",
                GH_ParamAccess.item);

            _inPtCloudsIdx = pManager.AddPointParameter(
                "Pointclouds",
                "C",
                "Pointclouds to search",
                GH_ParamAccess.tree);

            _inNDistsToAvgIdx = pManager.AddIntegerParameter(
                "Number of distances",
                "ND",
                "Number of distances to average",
                GH_ParamAccess.item,
                2);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            _outAvgDistsIdx = pManager.AddNumberParameter(
                "AvgDist",
                "D",
                "Average distance",
                GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (InPreSolve)
            {
                // First pass; collect data and construct tasks
                Point3d searchPt = new Point3d();

                var nDistsToAvg = 0;
                _ = DA.GetData(_inNDistsToAvgIdx, ref nDistsToAvg);

                Task<SolveResults> tsk = null;
                if (DA.GetData(_inSearchPtsIdx, ref searchPt)
                    && DA.GetDataTree(_inPtCloudsIdx, out GH_Structure<GH_Point> ptCloudDataTree))
                {
                    var ptCloudKDTrees = PtCloudDataTreesToKDTrees(ptCloudDataTree);
                    tsk = Task.Run(() => ComputeAvgDist(searchPt, ptCloudKDTrees, nDistsToAvg), CancelToken);
                }
                TaskList.Add(tsk);

                return;
            }

            if (!GetSolveResults(DA, out SolveResults results))
            {
                // Compute right here, right now.
                // 1. Collect
                Point3d searchPt = new Point3d();
                if (!DA.GetData(_inSearchPtsIdx, ref searchPt)) { return; }

                var nDistsToAvg = 0;
                _ = DA.GetData(_inNDistsToAvgIdx, ref nDistsToAvg);

                if (!DA.GetDataTree(_inPtCloudsIdx, out GH_Structure<GH_Point> ptCloudDataTree)) { return; }
                var ptCloudKDTrees = PtCloudDataTreesToKDTrees(ptCloudDataTree);

                // 2. Compute
                results = ComputeAvgDist(searchPt, ptCloudKDTrees, nDistsToAvg);
            }

            // 3. Set
            if (results != null)
                _ = DA.SetData(_outAvgDistsIdx, results.Value);

        }

        static SolveResults ComputeAvgDist(
            Point3d searchPt,
            KDTreePtCloud[] ptClouds,
            int nToAveragePerDistance)
        {
            var result = new SolveResults();
            var allDists = new List<double>(ptClouds.Length);

            foreach (KDTreePtCloud tree in ptClouds)
            {
                allDists.Add(tree.GetClosestPtDistance(searchPt));
            }
            allDists.Sort();

            List<double> distsToAverage = allDists.GetRange(0, nToAveragePerDistance);
            result.Value = distsToAverage.Average();

            return result;
        }

        static KDTreePtCloud[] PtCloudDataTreesToKDTrees(GH_Structure<GH_Point> dataTree)
        {
            var kdTrees = new KDTreePtCloud[dataTree.PathCount];

            for (int i = 0; i < dataTree.PathCount; i++)
            {
                List<Point3d> pts = dataTree.Branches[i].Select(x => x.Value).ToList();
                kdTrees[i] = new KDTreePtCloud(pts);

            }

            return kdTrees;
        }

        #endregion Methods

        public class SolveResults
        {
            public double Value { get; set; }
        }

    }
}
