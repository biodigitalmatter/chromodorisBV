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
using KDTree;
using Rhino.Geometry;

namespace Chromodoris
{
    class VoxelSamplerDual
    {

        public List<double> debugList = new List<double>();

        public Box BBox;
        private double xSpace;
        private double ySpace;
        private double zSpace;

        private PointCloudVoxelData ptCloudVoxel1;
        private PointCloudVoxelData ptCloudVoxel2;

        private readonly int _xRes;
        private readonly int _yRes;
        private readonly int _zRes;

        private List<double> _values;
        private double _range;
        private double _rangeSq;
        private bool _bulge = false;
        private bool _linear;

        private Transform xfm;

        private Transform xfmToGrid;
        private Transform xfmFromGrid;
        // private Transform scaleInv;
        private readonly bool useXfm = false;

        public VoxelSamplerDual(List<Point3d> pointCloud1, List<Point3d> pointCloud2, List<double> values, double cellSize, double range, bool bulge, bool linear)
        {
            ptCloudVoxel1 = new PointCloudVoxelData(pointCloud1, _xRes, _yRes, _zRes);
            ptCloudVoxel2 = new PointCloudVoxelData(pointCloud2, _xRes, _yRes, _zRes);

            _values = values;
            _range = range;
            _rangeSq = _range * _range;
            _bulge = bulge;
            _linear = linear;
            CreateEnvironment(cellSize, out BBox, out _xRes, out _yRes, out _zRes);
        }

        public VoxelSamplerDual(List<Point3d> pointCloud1, List<Point3d> pointCloud2, List<double> values, double cellSize, Box box, double range, bool bulge, bool linear)
        {
            ptCloudVoxel1 = new PointCloudVoxelData(pointCloud1, _xRes, _yRes, _zRes);
            ptCloudVoxel2 = new PointCloudVoxelData(pointCloud2, _xRes, _yRes, _zRes);

            _values = values;
            _range = range;
            _rangeSq = _range * _range;
            _bulge = bulge;
            _linear = linear;

            CreateEnvironment(cellSize, box, out BBox, out _xRes, out _yRes, out _zRes);

            if (BBox.Plane.ZAxis != Vector3d.ZAxis || BBox.Plane.YAxis != Vector3d.YAxis || BBox.Plane.XAxis != Vector3d.XAxis)
            {
                xfm = GetBoxTransform(BBox, _xRes, _yRes, _zRes);

                useXfm = true;
            }

        }

        public VoxelSamplerDual(List<Point3d> pointCloud1, List<Point3d> pointCloud2, List<double> values, Box box, int resX, int resY, int resZ, double range, bool bulge, bool linear)
        {
            _values = values;
            _range = range;
            _rangeSq = _range * _range;
            _bulge = bulge;
            _linear = linear;
            BBox = box;
            BBox.RepositionBasePlane(box.Center);
            _xRes = resX;
            _yRes = resY;
            _zRes = resZ;

            ptCloudVoxel1 = new PointCloudVoxelData(pointCloud1, _xRes, _yRes, _zRes);
            ptCloudVoxel2 = new PointCloudVoxelData(pointCloud2, _xRes, _yRes, _zRes);

            if (BBox.Plane.ZAxis != Vector3d.ZAxis || BBox.Plane.YAxis != Vector3d.YAxis || BBox.Plane.XAxis != Vector3d.XAxis)
            {
                xfm = GetBoxTransform(BBox, _xRes, _yRes, _zRes);
                useXfm = true;
            }
        }

        public float[,,] SampledData1
        {
            get
            {
                return ptCloudVoxel1.SampledData;
            }
        }

        public float[,,] SampledData2
        {
            get
            {
                return ptCloudVoxel2.SampledData;
            }
        }

        private List<Point3d> GetMergedPtClouds()
        {

            var mergedCloud = new List<Point3d>(ptCloudVoxel1.Pts);
            mergedCloud.AddRange(ptCloudVoxel2.Pts);
            return mergedCloud;

        }

        public Transform GetBoxTransform(Box box, int x, int y, int z)
        {
            Box gridBox = new Box(Plane.WorldXY, new Interval(0, x), new Interval(0, y), new Interval(0, z));
            gridBox.RepositionBasePlane(gridBox.Center);

            var trans = Transform.PlaneToPlane(gridBox.Plane, box.Plane);
            trans = trans * Transform.Scale(gridBox.Plane, box.X.Length / gridBox.X.Length, box.Y.Length / gridBox.Y.Length, box.Z.Length / gridBox.Z.Length);

            return trans;

        }

        public void Init()
        {
            xSpace = (BBox.X.Max - BBox.X.Min) / (_xRes - 1);
            ySpace = (BBox.Y.Max - BBox.Y.Min) / (_yRes - 1);
            zSpace = (BBox.Z.Max - BBox.Z.Min) / (_zRes - 1);

            // make transform from full box to scaled box
            // _box is the big box
            var _gridbox = new Box(Plane.WorldXY, new Interval(0, _xRes - 1), new Interval(0, _yRes - 1), new Interval(0, _zRes - 1));
            _gridbox.RepositionBasePlane(_gridbox.Center);
            xfmToGrid = BoxToBoxTransform(BBox, _gridbox);
            xfmFromGrid = BoxToBoxTransform(_gridbox, BBox);

        }

        public Transform BoxToBoxTransform(Box source, Box target)
        {
            var trans = Transform.PlaneToPlane(source.Plane, target.Plane);
            trans = trans * Transform.Scale(source.Plane, target.X.Length / source.X.Length, target.Y.Length / source.Y.Length, target.Z.Length / source.Z.Length);
            return trans;
        }

        public Box getBox()
        {
            return BBox;
        }


        public void executeMultiThreaded()
        {
            var pLel = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            System.Threading.Tasks.Parallel.ForEach(Enumerable.Range(0, _zRes), pLel, z => AssignSection(z));
        }
        private void AssignSection(int z)
        {
                double zCoord = BBox.Z.Min + z * zSpace + BBox.Center.Z;
                for (int y = 0; y < _yRes; y++)
                {
                    double yCoord = BBox.Y.Min + y * ySpace + BBox.Center.Y;
                    for (int x = 0; x < _xRes; x++)
                    {
                        double xCoord = BBox.X.Min + x * xSpace + BBox.Center.X;
                        int[] voxelRef = { x, y, z };
                        var voxelPt = new Point3d(xCoord, yCoord, zCoord);
                        AssignVoxelValues(voxelRef, voxelPt);
                    }
                }
        }


        private void AssignVoxelValues(int[] voxelRef, Point3d voxelPt)
        {
            double voxelDist1 = ptCloudVoxel1.GetClosestPtDistance(voxelPt);
            double voxelDist2 = ptCloudVoxel2.GetClosestPtDistance(voxelPt);

            double sumDist = voxelDist1 + voxelDist2;
            double val1 = voxelDist1 / sumDist;
            double val2 = 1 - val1;

            ptCloudVoxel1.SampledData.SetValue((float)val1, voxelRef);
            ptCloudVoxel2.SampledData.SetValue((float)val2, voxelRef);

        }

        private void CreateEnvironment(double cellSize, out Box box, out int xDim, out int yDim, out int zDim)
        {

            box = new Box(Plane.WorldXY, GetMergedPtClouds());
            box.Inflate(_range);
            box.RepositionBasePlane(box.Center);

            xDim = (int)Math.Floor((double)box.X.Length / (double)cellSize);
            yDim = (int)Math.Floor((double)box.Y.Length / (double)cellSize);
            zDim = (int)Math.Floor((double)box.Z.Length / (double)cellSize);

            double xLen = xDim * cellSize;

            box.X = new Interval(-(xDim * cellSize) / 2, (xDim * cellSize) / 2);
            box.Y = new Interval(-(yDim * cellSize) / 2, (yDim * cellSize) / 2);
            box.Z = new Interval(-(zDim * cellSize) / 2, (zDim * cellSize) / 2);
        }

        private void CreateEnvironment(double cellSize, Box boxIn, out Box box, out int xDim, out int yDim, out int zDim)
        {
            box = boxIn;
            box.RepositionBasePlane(box.Center);

            xDim = (int)Math.Floor((double)box.X.Length / (double)cellSize);
            yDim = (int)Math.Floor((double)box.Y.Length / (double)cellSize);
            zDim = (int)Math.Floor((double)box.Z.Length / (double)cellSize);

            box.X = new Interval(-(xDim * cellSize) / 2, (xDim * cellSize) / 2);
            box.Y = new Interval(-(yDim * cellSize) / 2, (yDim * cellSize) / 2);
            box.Z = new Interval(-(zDim * cellSize) / 2, (zDim * cellSize) / 2);
        }


        class PointCloudVoxelData
        {
            private readonly List<Point3d> pts;
            public float[,,] SampledData { get => sampledData; set => sampledData = value; }

            public List<Point3d> Pts => pts;

            public KDTree<int> Tree => _tree;

            private readonly KDTree<int> _tree;

            private float[,,] sampledData;

            public PointCloudVoxelData(List<Point3d> inPts, int xRes, int yRes, int zRes)
            {
                pts = inPts;
                SampledData = new float[xRes, yRes, zRes];
                _tree = new KDTree<int>(3);

                for (int i = 0; i < pts.Count; i++)
                {
                    double[] pos = { pts[i].X, pts[i].Y, pts[i].Z };
                    _tree.AddPoint(pos, i);

                }
            }

            public double GetClosestPtDistance(Point3d voxelPt, int maxCount = 1)
            {
                double[] voxelPos = { voxelPt.X, voxelPt.Y, voxelPt.Z };
                NearestNeighbour<int> nbors = Tree.NearestNeighbors(voxelPos, maxCount);
                int idx = nbors.First();
                Point3d pt = Pts[idx];
                return pt.DistanceTo(voxelPt);
            }

        }
    }

}
