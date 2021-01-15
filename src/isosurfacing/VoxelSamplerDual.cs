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
using KDTree;
using Rhino.Geometry;

namespace Chromodoris
{
    internal class VoxelSamplerDual
    {
        public List<Point3d>[] _voxelPts;
        public Box BBox;
        public float[,,] SampledData;
        private readonly PointCloudVoxelData _ptCloudVoxel1;
        private readonly PointCloudVoxelData _ptCloudVoxel2;
        private readonly int _xRes;
        private readonly double _xSpace;
        private readonly int _yRes;
        private readonly double _ySpace;
        private readonly int _zRes;
        private readonly double _zSpace;

        public VoxelSamplerDual(List<Point3d> pointCloud1, List<Point3d> pointCloud2, Box box, int resX, int resY, int resZ)
        {
            _ptCloudVoxel1 = new PointCloudVoxelData(pointCloud1);
            _ptCloudVoxel2 = new PointCloudVoxelData(pointCloud2);

            BBox = box;
            BBox.RepositionBasePlane(box.Center);

            _xRes = resX;
            _yRes = resY;
            _zRes = resZ;

            _xSpace = (BBox.X.Max - BBox.X.Min) / (_xRes - 1);
            _ySpace = (BBox.Y.Max - BBox.Y.Min) / (_yRes - 1);
            _zSpace = (BBox.Z.Max - BBox.Z.Min) / (_zRes - 1);

            SampledData = new float[_xRes, _yRes, _zRes];
            _voxelPts = new List<Point3d>[_zRes];
        }

        public List<Point3d> VoxelPts
        {
            get
            {
                var _newList = new List<Point3d>();
                foreach (List<Point3d> _list in _voxelPts) { _newList.AddRange(_list); }
                return _newList;
            }
        }

        //public Box BBox => _bBox;

        //public float[,,] SampledData => _sampledData;

        //public List<Point3d> VoxelPts => _voxelPts;

        public void ExecuteMultiThreaded()
        {
            var pLel = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            System.Threading.Tasks.Parallel.ForEach(Enumerable.Range(0, _zRes), pLel, z => AssignSection(z));
        }

        private void AssignSection(int z)
        {
            _voxelPts[z] = new List<Point3d>();
            double zCoord = BBox.Z.Min + z * _zSpace + BBox.Center.Z;
            for (int x = 0; x < _xRes; x++)
            {
                double xCoord = BBox.X.Min + x * _xSpace + BBox.Center.X;
                for (int y = 0; y < _yRes; y++)
                {
                    double yCoord = BBox.Y.Min + y * _ySpace + BBox.Center.Y;
                    int[] voxelRef = { x, y, z };
                    var voxelPt = new Point3d(xCoord, yCoord, zCoord);
                    _voxelPts[z].Add(voxelPt);
                    AssignVoxelValues(voxelRef, voxelPt);
                }
            }
        }

        private void AssignVoxelValues(int[] voxelRef, Point3d voxelPt)
        {
            double voxelDist1 = _ptCloudVoxel1.GetClosestPtDistance(voxelPt);
            double voxelDist2 = _ptCloudVoxel2.GetClosestPtDistance(voxelPt);

            double sumDist = voxelDist1 + voxelDist2;
            double val = voxelDist1 / sumDist;

            SampledData.SetValue((float)val, voxelRef);
        }

        private class PointCloudVoxelData
        {
            public List<Point3d> Pts;
            private readonly KDTree<int> _tree;

            public PointCloudVoxelData(List<Point3d> inPts)
            {
                Pts = new List<Point3d>(inPts);
                _tree = new KDTree<int>(3);

                for (int i = 0; i < Pts.Count; i++)
                {
                    double[] pos = { Pts[i].X, Pts[i].Y, Pts[i].Z };
                    _tree.AddPoint(pos, i);
                }
            }

            public double GetClosestPtDistance(Point3d voxelPt, int maxCount = 1)
            {
                double[] voxelPos = { voxelPt.X, voxelPt.Y, voxelPt.Z };
                NearestNeighbour<int> nbors = _tree.NearestNeighbors(voxelPos, maxCount);
                int idx = nbors.First();
                Point3d pt = Pts[idx];
                return pt.DistanceTo(voxelPt);
            }
        }
    }
}
