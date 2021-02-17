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
using System.Threading.Tasks;

using Rhino.Geometry;

namespace Chromodoris
{
    internal struct DimensionValues
    {
        public int NVoxels;
        public double MinCoord;
        public double StepSize;
    }

    internal class VoxelSamplerDual
    {
        private readonly List<DimensionValues> _outputOrderedDimVals;
        private readonly KDTreePtCloud[] _ptClouds;
        private readonly List<Point3d>[] _voxelPts;
        private readonly List<VoxelValues>[] _voxelValues;
        private readonly bool _zyx;

        internal VoxelSamplerDual(List<Point3d> pointCloud1, List<Point3d> pointCloud2, Box box, int resX, int resY,
            int resZ, bool zyx = false)
        {
            _ptClouds = new[] { new KDTreePtCloud(pointCloud1), new KDTreePtCloud(pointCloud2) };

            BBox = box;

            DimensionValues xVals, yVals, zVals;

            xVals.NVoxels = resX;
            yVals.NVoxels = resY;
            zVals.NVoxels = resZ;

            xVals.MinCoord = BBox.X.Min;
            yVals.MinCoord = BBox.Y.Min;
            zVals.MinCoord = BBox.Z.Min;

            xVals.StepSize = (BBox.X.Max - BBox.X.Min) / (resX - 1);
            yVals.StepSize = (BBox.Y.Max - BBox.Y.Min) / (resY - 1);
            zVals.StepSize = (BBox.Z.Max - BBox.Z.Min) / (resZ - 1);

            _outputOrderedDimVals = new List<DimensionValues> { xVals, yVals, zVals };
            if (zyx) _outputOrderedDimVals.Reverse();

            _zyx = zyx;

            _voxelValues = new List<VoxelValues>[_outputOrderedDimVals[0].NVoxels];
            _voxelPts = new List<Point3d>[_outputOrderedDimVals[0].NVoxels];
        }

        internal Box BBox { get; }
        internal List<Point3d> VoxelPtsList => FlattenArrayOfList(_voxelPts);

        internal List<VoxelValues> VoxelValuesList => FlattenArrayOfList(_voxelValues);

        private Point3d OutputOrderedCoordsToPoint3d(IReadOnlyList<double> coords)
        {
            return !_zyx ? new Point3d(coords[0], coords[1], coords[2]) : new Point3d(coords[2], coords[1], coords[0]);
        }

        private VoxelValues GetVoxelValues(Point3d voxelPt)
        {
            double voxelDist1 = _ptClouds[0].GetClosestPtDistance(voxelPt);
            double voxelDist2 = _ptClouds[1].GetClosestPtDistance(voxelPt);

            double sumDist = voxelDist1 + voxelDist2;
            double val = voxelDist1 / sumDist;

            return new VoxelValues((float)val, voxelDist1, voxelDist2);
        }

        internal void ExecuteMultiThreaded()
        {
            _ = Parallel.ForEach(Partitioner.Create(0, _outputOrderedDimVals[0].NVoxels), (range, _) =>
            {
                for (int i = range.Item1; i < range.Item2; i++) SetVoxelValuesForSlice(i);
            });
        }

        private static List<T> FlattenArrayOfList<T>(IEnumerable<List<T>> array)
        {
            var newList = new List<T>();
            foreach (List<T> list in array) newList.AddRange(list);

            return newList;
        }

        private void SetVoxelValuesForSlice(int primaryDimIdx)
        {
            double GetCoord(int idx, DimensionValues dim)
            {
                return dim.MinCoord + idx * dim.StepSize;
            }

            // Lists specific to slice to avoid race conditions
            _voxelValues[primaryDimIdx] = new List<VoxelValues>();
            _voxelPts[primaryDimIdx] = new List<Point3d>();

            // Initialize once

            var outputOrderedCoords = new double[3];

            outputOrderedCoords[0] = GetCoord(primaryDimIdx, _outputOrderedDimVals[0]);
            for (var secondaryDimIdx = 0; secondaryDimIdx < _outputOrderedDimVals[1].NVoxels; secondaryDimIdx++)
            {
                outputOrderedCoords[1] = GetCoord(secondaryDimIdx, _outputOrderedDimVals[1]);
                for (var tertiaryDimIdx = 0; tertiaryDimIdx < _outputOrderedDimVals[2].NVoxels; tertiaryDimIdx++)
                {
                    outputOrderedCoords[2] = GetCoord(tertiaryDimIdx, _outputOrderedDimVals[2]);

                    Point3d voxelPt = OutputOrderedCoordsToPoint3d(outputOrderedCoords);

                    _voxelPts[primaryDimIdx].Add(voxelPt);
                    _voxelValues[primaryDimIdx].Add(GetVoxelValues(voxelPt));
                }
            }
        }

        internal readonly struct VoxelValues
        {
            internal VoxelValues(float distFactor, double distPtCloud1, double distPtCloud2)
            {
                DistFactor = distFactor;
                DistPtCloud1 = distPtCloud1;
                DistPtCloud2 = distPtCloud2;
            }

            internal float DistFactor { get; }
            internal double DistPtCloud1 { get; }
            internal double DistPtCloud2 { get; }
        }
    }
}
