using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Chromodoris
{
    internal class AverageDistanceToPointclouds
    {
        #region Fields

        private readonly List<Point3d> _largePtCloud;
        private readonly KDTreePtCloud[] _otherPointClouds;
        private readonly int _nToAveragePerDistance;

        #endregion Fields

        #region Constructors

        internal AverageDistanceToPointclouds(List<Point3d> largePtCloud, GH_Structure<GH_Point> otherPointCloudsTree, int nToAveragePerDistance)
        {
            _largePtCloud = largePtCloud;
            _otherPointClouds = new KDTreePtCloud[otherPointCloudsTree.PathCount];

            for (int i = 0; i < otherPointCloudsTree.PathCount; i++)
            {
                var gh_pts = (List<GH_Point>)otherPointCloudsTree.get_Branch(i);
                var pts = gh_pts.ConvertAll<Point3d>(x => x.Value);
                _otherPointClouds[i] = new KDTreePtCloud(pts);
            }

            _nToAveragePerDistance = nToAveragePerDistance;
        }

        #endregion Constructors



        #region Methods

        internal List<double> ComputeMultiThreaded()
        {
            var averageDistances = new double[_largePtCloud.Count];
            _ = Parallel.ForEach(Partitioner.Create(0, _largePtCloud.Count), (range, _) =>
              {
                  for (int i = range.Item1; i < range.Item2; i++)
                  {
                      Point3d pt = _largePtCloud[i];

                      var allDists = new List<double>();

                      foreach (KDTreePtCloud tree in _otherPointClouds)
                      {
                          allDists.Add(tree.GetClosestPtDistance(pt));
                      }
                      allDists.Sort();

                      List<double> distsToAverage = allDists.GetRange(0, _nToAveragePerDistance);
                      averageDistances[i] = distsToAverage.Average();
                  }
              });

            return averageDistances.ToList();
        }

        #endregion Methods
    }
}
