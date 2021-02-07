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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KDTree;
using Rhino.Geometry;

namespace Chromodoris
{
    internal struct DimensionValues
    {
        #region Fields

        public int NVoxels;
        public double MinCoord;
        public double StepSize;

        #endregion Fields
    }

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

    internal class VoxelSamplerDual
    {
        #region Fields

        private readonly KDTreePtCloud[] _ptClouds;
        private readonly List<Point3d>[] _voxelPts;
        private readonly List<float>[] _voxelValues;
        private readonly bool _zyx;
        private readonly List<DimensionValues> _outputOrderedDimVals;

        #endregion Fields

        #region Constructors

        internal VoxelSamplerDual(List<Point3d> pointCloud1, List<Point3d> pointCloud2, Box box, int resX, int resY, int resZ, bool zyx = false)
        {
            _ptClouds = new KDTreePtCloud[2] { new KDTreePtCloud(pointCloud1), new KDTreePtCloud(pointCloud2) };

            BBox = box;

            DimensionValues XVals, YVals, ZVals;

            XVals.NVoxels = resX;
            YVals.NVoxels = resY;
            ZVals.NVoxels = resZ;

            XVals.MinCoord = BBox.X.Min;
            YVals.MinCoord = BBox.Y.Min;
            ZVals.MinCoord = BBox.Z.Min;

            XVals.StepSize = (BBox.X.Max - BBox.X.Min) / (resX - 1);
            YVals.StepSize = (BBox.Y.Max - BBox.Y.Min) / (resY - 1);
            ZVals.StepSize = (BBox.Z.Max - BBox.Z.Min) / (resZ - 1);

            _outputOrderedDimVals = new List<DimensionValues> { XVals, YVals, ZVals };
            if (zyx)
            {
                _outputOrderedDimVals.Reverse();
            }

            _zyx = zyx;

            _voxelValues = new List<float>[_outputOrderedDimVals[0].NVoxels];
            _voxelPts = new List<Point3d>[_outputOrderedDimVals[0].NVoxels];
        }

        #endregion Constructors



        #region Properties

        internal Box BBox { get; }
        internal List<Point3d> VoxelPtsList { get => FlattenArrayOfList(_voxelPts); }

        internal List<float> VoxelValuesList { get => FlattenArrayOfList(_voxelValues); }

        #endregion Properties



        #region Methods

        internal void ExecuteMultiThreaded()
        {
            _ = Parallel.ForEach(Partitioner.Create(0, _outputOrderedDimVals[0].NVoxels), (range, _) =>
              {
                  for (int i = range.Item1; i < range.Item2; i++)
                      SetVoxelValuesForSlice(i);
              });
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

        private void SetVoxelValuesForSlice(int primaryDimIdx)
        {
            double getCoord(int idx, DimensionValues dimension)
            {
                return dimension.MinCoord + idx * dimension.StepSize;
            }

            // Lists specific to slice to avoid race conditions
            _voxelValues[primaryDimIdx] = new List<float>();
            _voxelPts[primaryDimIdx] = new List<Point3d>();

            // Initialize once
            Point3d voxelPt;

            var outputOrderedCoords = new double[3];

            outputOrderedCoords[0] = getCoord(primaryDimIdx, _outputOrderedDimVals[0]);
            for (int secondaryDimIdx = 0; secondaryDimIdx < _outputOrderedDimVals[1].NVoxels; secondaryDimIdx++)
            {
                outputOrderedCoords[1] = getCoord(secondaryDimIdx, _outputOrderedDimVals[1]);
                for (int tertiaryDimIdx = 0; tertiaryDimIdx < _outputOrderedDimVals[2].NVoxels; tertiaryDimIdx++)
                {
                    outputOrderedCoords[2] = getCoord(tertiaryDimIdx, _outputOrderedDimVals[2]);

                    voxelPt = OutputOrderedCoordsToPoint3d(outputOrderedCoords);

                    _voxelPts[primaryDimIdx].Add(voxelPt);
                    _voxelValues[primaryDimIdx].Add(GetVoxelValue(voxelPt));
                }
            }
        }

        private Point3d OutputOrderedCoordsToPoint3d(double[] inputCoords)
        {
            return (!_zyx) ? new Point3d(inputCoords[0], inputCoords[1], inputCoords[2]) : new Point3d(inputCoords[2], inputCoords[1], inputCoords[0]);
        }

        private float GetVoxelValue(Point3d voxelPt)
        {
            double voxelDist1 = _ptClouds[0].GetClosestPtDistance(voxelPt);
            double voxelDist2 = _ptClouds[1].GetClosestPtDistance(voxelPt);

            double sumDist = voxelDist1 + voxelDist2;
            double val = voxelDist1 / sumDist;

            return (float)val;
        }

        #endregion Methods
    }
}
