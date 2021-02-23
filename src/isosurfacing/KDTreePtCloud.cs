using System.Collections.Generic;
using System.Linq;

using KDSharp.KDTree;

using Rhino.Geometry;

namespace Chromodoris.IsoSurfacing
{
    internal class KDTreePtCloud
    {
        internal KDTreePtCloud(IEnumerable<Point3d> inPts)
        {
            Pts = new List<Point3d>(inPts);
            Tree = new KDTree<int>(3);

            for (var i = 0; i < Pts.Count; i++)
            {
                Tree.AddPoint(Point3dToDoubleArray(Pts[i]), i);
            }

            Tree.TrimExcess();
        }

        // Could be made public if needed
        private KDTree<int> Tree { get; }
        private List<Point3d> Pts { get; }

        internal double GetClosestPtDistance(Point3d pt)
        {
            double[] voxelPos = { pt.X, pt.Y, pt.Z };
            int nborIdx = Tree.NearestNeighbors(voxelPos, 1).First();
            Point3d foundPt = Pts[nborIdx];
            return pt.DistanceTo(foundPt);
        }

        private static double[] Point3dToDoubleArray(Point3d pt)
        {
            return new[] { pt.X, pt.Y, pt.Z };
        }
    }
}
