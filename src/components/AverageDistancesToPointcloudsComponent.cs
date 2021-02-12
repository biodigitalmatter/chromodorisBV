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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace Chromodoris
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
            InSamplePtsIdx = pManager.AddPointParameter(
                "Search points",
                "P",
                "Points to search from",
                GH_ParamAccess.list);

            InPtCloudsIdx = pManager.AddPointParameter(
                "Pointclouds",
                "C",
                "Pointclouds to search",
                GH_ParamAccess.tree);

            InNToAverageIdx = pManager.AddIntegerParameter(
                "Distances",
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
            OutAvgDistIdx = pManager.AddNumberParameter(
                "AvgDist",
                "D",
                "Average distance",
                GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var searchPts = new List<Point3d>();
            var nToAverage = 0;

            if (!DA.GetDataList(InSamplePtsIdx, searchPts))
                return;

            if (!DA.GetDataTree(InPtCloudsIdx, out GH_Structure<GH_Point> ptCloudDataTree))
                return;

            var ptCloudKDTrees = DataTreePtCloudToKDTree(ptCloudDataTree);

            if (ptCloudKDTrees.Length < 2)
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "Need more than one set of clouds to average results.");

            _ = DA.GetData(InNToAverageIdx, ref nToAverage);

            if (nToAverage < 0 || nToAverage > ptCloudKDTrees.Length)
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "Number of distances needs to more than 0 and less than the length of Pointclouds (C).");

            _ = DA.SetDataList(
                OutAvgDistIdx,
                GetAverageDistances(searchPts.ToArray(), ptCloudKDTrees, nToAverage));
        }

        private List<double> GetAverageDistances(
            Point3d[] searchPts,
            KDTreePtCloud[] ptClouds,
            int nToAveragePerDistance)
        {
            var averageDistances = new double[searchPts.Length];

            // Partitions are static and created before run, it's just a number
            // of ranges based on number of cores
            var partitioner = Partitioner.Create(0, searchPts.Length);

            _ = Parallel.ForEach(partitioner, (range, loopstate) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var allDists = new List<double>();

                    foreach (KDTreePtCloud tree in ptClouds)
                    {
                        allDists.Add(tree.GetClosestPtDistance(searchPts[i]));
                    }
                    allDists.Sort();

                    List<double> distsToAverage = allDists.GetRange(0, nToAveragePerDistance);
                    averageDistances[i] = distsToAverage.Average();
                }
            });

            return averageDistances.ToList();
        }

        private static KDTreePtCloud[] DataTreePtCloudToKDTree(GH_Structure<GH_Point> dataTree)
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
    }
}
