using System.Collections.Generic;
using System.Linq;

using KDSharp.KDTree;

using Rhino.Geometry;

namespace Chromodoris
{
    internal class KDTreePtCloud
    {
        private readonly List<Point3d> _pts;
        private readonly KDTree<int> _tree;


        internal KDTreePtCloud(IEnumerable<Point3d> inPts)
        {
            _pts = new List<Point3d>(inPts);
            _tree = new KDTree<int>(3);

            for (var i = 0; i < _pts.Count; i++)
            {
                double[] pos = { _pts[i].X, _pts[i].Y, _pts[i].Z };
                _tree.AddPoint(pos, i);
            }
        }


        internal int Count => _pts.Count;


        internal double GetClosestPtDistance(Point3d voxelPt)
        {
            double[] voxelPos = { voxelPt.X, voxelPt.Y, voxelPt.Z };
            int nborIdx = _tree.NearestNeighbors(voxelPos, 1).First();
            Point3d pt = _pts[nborIdx];
            return pt.DistanceTo(voxelPt);
        }
    }
}
