using System.Collections.Generic;
using System.Linq;

using KDSharp.KDTree;

using Rhino.Geometry;

namespace Chromodoris
{
    internal class KDTreePtCloud
    {
        #region Fields

        private readonly List<Point3d> _pts;
        private readonly KDTree<int> _tree;

        #endregion Fields

        #region Constructors

        internal KDTreePtCloud(List<Point3d> inPts)
        {
            _pts = new List<Point3d>(inPts);
            _tree = new KDTree<int>(3);

            for (int i = 0; i < _pts.Count; i++)
            {
                double[] pos = { _pts[i].X, _pts[i].Y, _pts[i].Z };
                _tree.AddPoint(pos, i);
            }
        }

        #endregion Constructors



        #region Properties

        internal int Count { get => _pts.Count; }

        #endregion Properties



        #region Methods

        internal double GetClosestPtDistance(Point3d voxelPt)
        {
            double[] voxelPos = { voxelPt.X, voxelPt.Y, voxelPt.Z };
            int nborIdx = _tree.NearestNeighbors(voxelPos, 1).First();
            Point3d pt = _pts[nborIdx];
            return pt.DistanceTo(voxelPt);
        }

        #endregion Methods
    }
}
