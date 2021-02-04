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
    internal class VoxelSamplerDual
    {
        #region fields
        private readonly PointCloudVoxelData _ptCloudVoxel1;
        private readonly PointCloudVoxelData _ptCloudVoxel2;
        private readonly List<Point3d>[] _voxelPts;
        private readonly List<float>[] _voxelValues;
        private readonly bool _zyx;
        private readonly int _nVoxels;
        private readonly List<DimensionValues> _outputOrderedDimVals;
        private readonly List<VoxelData> _voxelList;
        #endregion fields

        #region constructors
        internal VoxelSamplerDual(List<Point3d> pointCloud1, List<Point3d> pointCloud2, Box box, int resX, int resY, int resZ, bool zyx = false)
        {
            _ptCloudVoxel1 = new PointCloudVoxelData(pointCloud1);
            _ptCloudVoxel2 = new PointCloudVoxelData(pointCloud2);

            BBox = box;
            // _bBox.RepositionBasePlane(box.Center);

            DimensionValues XVals;
            DimensionValues YVals;
            DimensionValues ZVals;

            XVals.NVoxels = resX;
            YVals.NVoxels = resY;
            ZVals.NVoxels = resZ;

            XVals.MinCoord = BBox.X.Min;
            YVals.MinCoord = BBox.Y.Min;
            ZVals.MinCoord = BBox.Z.Min;

            XVals.StepSize = (BBox.X.Max - BBox.X.Min) / (resX - 1);
            YVals.StepSize = (BBox.Y.Max - BBox.Y.Min) / (resY - 1);
            ZVals.StepSize = (BBox.Z.Max - BBox.Z.Min) / (resZ - 1);

            _nVoxels = resX * resY * resZ;

            _outputOrderedDimVals = new List<DimensionValues> { XVals, YVals, ZVals };
            if (zyx)
            {
                _outputOrderedDimVals.Reverse();
            }

            _zyx = zyx;

            _voxelValues = new List<float>[_nVoxels];
            _voxelPts = new List<Point3d>[_nVoxels];

        }
        #endregion constructors

        #region properties
        internal Box BBox { get; }
        internal List<Point3d> VoxelPtsList { get { return FlattenArrayOfList(_voxelPts); } }

        internal List<float> VoxelValuesList { get { return FlattenArrayOfList(_voxelValues); } }

        private int[] DimensionOrder { get { return (!_zyx) ? new int[] { 0, 1, 2 } : new int[] { 2, 1, 0 }; } }
        #endregion properties

        #region methods
        internal void ExecuteMultiThreaded()
        {
            var pLel = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            System.Threading.Tasks.Parallel.ForEach(Enumerable.Range(0, _nVoxels), pLel, i => SetVoxelValues(i));
        }

        private static List<T> FlattenArrayOfList<T>(IList<T>[] array)
        {
            List<T> newList = new List<T>();
            foreach (List<T> list in array)
            {
                newList.AddRange(list);
            }

            return newList;
        }

        private void SetVoxelValues(int voxelIdx)
        {
            var voxel = new Voxel(voxelIdx);

            var voxelPt = voxel.GetVoxelCenterPt(_outputOrderedDimVals, dimensionOrder: DimensionOrder);

            _voxelPts[voxelIdx].Add(voxelPt);

            float val = GetVoxelValue(voxelPt);
            _voxelValues[voxelIdx].Add(val);

        }

        private void AssignSection(int primaryDimIdx)
        {

            double getCoord(int idx, DimensionValues dimension)
            {
                return dimension.MinCoord + idx * dimension.StepSize; // + dimension.OffsetSize;
            }

            // Lists specific to slice to avoid race conditions
            _voxelValues[primaryDimIdx] = new List<float>();
            _voxelPts[primaryDimIdx] = new List<Point3d>();

            // Initialize once
            Point3d voxelPt;

            // Initialize with values so they can be overwritten
            var coord = new List<double> { 0, 0, 0 };

            coord[0] = getCoord(primaryDimIdx, _outputOrderedDimVals[0]);
            for (int secondaryDimIdx = 0; secondaryDimIdx < _outputOrderedDimVals[1].NVoxels; secondaryDimIdx++)
            {
                coord[1] = getCoord(secondaryDimIdx, _outputOrderedDimVals[1]);
                for (int tertiaryDimIdx = 0; tertiaryDimIdx < _outputOrderedDimVals[2].NVoxels; tertiaryDimIdx++)
                {
                    coord[2] = getCoord(tertiaryDimIdx, _outputOrderedDimVals[2]);

                    // Here the order has to be xyz
                    if (!_zyx)
                    {
                        voxelPt = new Point3d(coord[0], coord[1], coord[2]);
                    }
                    else
                    {
                        voxelPt = new Point3d(coord[2], coord[1], coord[0]);
                    }

                    _voxelPts[primaryDimIdx].Add(voxelPt);

                    float val = GetVoxelValue(voxelPt);
                    _voxelValues[primaryDimIdx].Add(val);

                }
            }
        }

        private float GetVoxelValue(Point3d voxelPt)
        {
            double voxelDist1 = _ptCloudVoxel1.GetClosestPtDistance(voxelPt);
            double voxelDist2 = _ptCloudVoxel2.GetClosestPtDistance(voxelPt);

            double sumDist = voxelDist1 + voxelDist2;
            double val = voxelDist1 / sumDist;

            return (float)val;
        }
        #endregion methods
    }

    public class PointCloudVoxelData
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

    public struct DimensionValues
    {
        public int NVoxels;
        public double MinCoord;
        public double StepSize;
    }

    public class Voxel
    {

        #region fields
        #endregion

        #region constructors
        public Voxel(int voxelIdx)
        {
            VoxelIdx = voxelIdx;
        }
        #endregion

        #region properties
        public int VoxelIdx { get; set; }
        #endregion

        #region methods

        public int[] GetVoxelSpaceCoord(int primaryRes, int secondaryRes)
        {
            var coord = new int[3];

            coord[0] = VoxelIdx / primaryRes;

            int mod = VoxelIdx % primaryRes;

            coord[1] = mod / secondaryRes;

            coord[2] = mod % secondaryRes;

            return coord;

        }

        public Point3d GetVoxelCenterPt(List<DimensionValues> dimVals, int[] dimensionOrder = null)
        {
            if (dimensionOrder is null)
            {
                dimensionOrder = new int[] { 0, 1, 2 };
            }

            int[] voxelSpaceCoord = GetVoxelSpaceCoord(dimVals[0].NVoxels, dimVals[1].NVoxels);

            var coord = new double[3];

            for (int i = 0; i < 3; i++)
            {
                coord[i] = dimVals[i].MinCoord + voxelSpaceCoord[i] * dimVals[i].StepSize;

            }

            return new Point3d(coord[dimensionOrder[0]], coord[dimensionOrder[1]], coord[dimensionOrder[2]]);
        }

        #endregion
    }

}
