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
        public List<float>[] SampledData;
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

            SampledData = new List<float>[_xRes];
            _voxelPts = new List<Point3d>[_xRes];
        }

        public List<Point3d> VoxelPts
        {
            get
            {
                List<Point3d> _newList = new List<Point3d>();
                foreach (List<Point3d> _list in _voxelPts) { _newList.AddRange(_list); }
                return _newList;
            }
        }

        public List<float> SampledValuesList
        {
            get
            {
                List<float> newList = new List<float>();
                foreach (List<float> list in SampledData) { newList.AddRange(list); }
                return newList;
            }
        }

        //public Box BBox => _bBox;

        //public float[,,] SampledData => _sampledData;

        //public List<Point3d> VoxelPts => _voxelPts;

        public void ExecuteMultiThreaded()
        {
            var pLel = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            System.Threading.Tasks.Parallel.ForEach(Enumerable.Range(0, _xRes), pLel, x => AssignSection(x));
        }

        private void AssignSection(int xIdx)
        {
            // List specific to xIdx slice to avoid race conditions
            SampledData[xIdx] = new List<float>();
            _voxelPts[xIdx] = new List<Point3d>();

            double xCoord = BBox.X.Min + xIdx * _xSpace + BBox.Center.X;
            for (int yIdx = 0; yIdx < _yRes; yIdx++)
            {
                double yCoord = BBox.Y.Min + yIdx * _ySpace + BBox.Center.Y;
                for (int zIdx = 0; zIdx < _zRes; zIdx++)
                {
                    double zCoord = BBox.Z.Min + zIdx * _zSpace + BBox.Center.Z;
                    int[] voxelRef = { xIdx, yIdx, zIdx };
                    var voxelPt = new Point3d(xCoord, yCoord, zCoord);
                    _voxelPts[xIdx].Add(voxelPt);  
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

            SampledData[voxelRef[0]].Add((float)val);
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
