using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace Chromodoris
{
    internal class ParallellSrfCPDist
    {
        #region Fields

        private readonly Point3d[] _pts;
        private readonly Surface _srf;
        private readonly bool _testLocal;

        #endregion Fields

        #region Constructors

        internal ParallellSrfCPDist(List<Point3d> pts, Surface srf, bool testLocal)
        {
            _pts = pts.ToArray();
            _srf = srf;
            _testLocal = testLocal;
        }

        #endregion Constructors



        #region Methods

        internal List<double> ComputeMultiThreaded()
        {
            var dists = new double[_pts.Length];
            _ = Parallel.ForEach(Partitioner.Create(0, _pts.Length), (range, _) =>
              {
                      double u1 = 0;
                      double v1 = 0;
                  for (int i = range.Item1; i < range.Item2; i++)
                  {
                      Point3d srfPt;
                      if (_testLocal)
                      {
                          if (_srf.LocalClosestPoint(_pts[i], u1, v1, out double u, out double v))
                          {
                              srfPt = _srf.PointAt(u, v);
                              u1 = u;
                              v1 = v;
                          }
                          else
                          {
                              throw new Exception();
                          }
                      }
                      else
                      {
                          if (_srf.ClosestPoint(_pts[i], out double u, out double v))
                          {
                              srfPt = _srf.PointAt(u, v);
                              dists[i] = srfPt.DistanceTo(_pts[i]);
                          }
                          else
                          {
                              throw new Exception();
                          }

                      }
                  }


              });

            return dists.ToList();
        }

        #endregion Methods
    }
}
