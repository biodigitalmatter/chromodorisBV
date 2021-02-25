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
using System.Threading.Tasks;

using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace Chromodoris.IsoSurfacing
{
    internal struct DimensionValues
    {
        public int NVoxels;
        public double MinCoord;
        public double StepSize;
    }

    internal class VoxelSamplerDual
    {
        private readonly List<GHVoxelData>[] _ghVoxelData;
        private readonly List<DimensionValues> _outputOrderedDimVals;
        private readonly KDTreePtCloud[] _ptClouds;
        private readonly List<Point3d>[] _voxelPts;
        private readonly bool _zyx;

        internal VoxelSamplerDual(
            IEnumerable<Point3d> ptCloud1, IEnumerable<Point3d> ptCloud2, Box box,
            int resX, int resY, int resZ, bool zyx = false
        )
        {
            _ptClouds = new[]
                { new KDTreePtCloud(ptCloud1), new KDTreePtCloud(ptCloud2) };

            BBox = box;

            var xVals = new DimensionValues
            {
                NVoxels = resX,
                MinCoord = BBox.X.Min,
                StepSize = (BBox.X.Max - BBox.X.Min) / (resX - 1),
            };
            var yVals = new DimensionValues
            {
                NVoxels = resY,
                MinCoord = BBox.Y.Min,
                StepSize = (BBox.Y.Max - BBox.Y.Min) / (resY - 1),
            };
            var zVals = new DimensionValues
            {
                NVoxels = resZ,
                MinCoord = BBox.Z.Min,
                StepSize = (BBox.Z.Max - BBox.Z.Min) / (resZ - 1),
            };

            _outputOrderedDimVals = new List<DimensionValues> { xVals, yVals, zVals };
            if (zyx)
            {
                _outputOrderedDimVals.Reverse();
            }

            _zyx = zyx;

            _ghVoxelData = new List<GHVoxelData>[_outputOrderedDimVals[0].NVoxels];
            _voxelPts = new List<Point3d>[_outputOrderedDimVals[0].NVoxels];
        }

        internal Box BBox { get; }

        // Flat list
        internal IEnumerable<GHVoxelData> GHVoxelDataList =>
            _ghVoxelData.SelectMany(x => x);

        internal int NVoxels => _outputOrderedDimVals.Select(x => x.NVoxels)
            .Aggregate((a, x) => a * x);

        private Point3d OutputOrderedCoordsToPoint3d(IReadOnlyList<double> coords) =>
            !_zyx
                ? new Point3d(coords[0], coords[1], coords[2])
                : new Point3d(coords[2], coords[1], coords[0]);

        private Tuple<double, double, double> GetVoxelValues(Point3d voxelPt)
        {
            double voxelDist1 = _ptClouds[0].GetClosestPtDistance(voxelPt);
            double voxelDist2 = _ptClouds[1].GetClosestPtDistance(voxelPt);

            double sumDist = voxelDist1 + voxelDist2;
            double val = voxelDist1 / sumDist;

            return new Tuple<double, double, double>(val, voxelDist1, voxelDist2);
        }

        internal void ExecuteMultiThreaded()
        {
            _ = Parallel.ForEach(
                Partitioner.Create(0, _outputOrderedDimVals[0].NVoxels), (range, _) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        SetVoxelValuesForSlice(i);
                    }
                });
        }


        private void SetVoxelValuesForSlice(int primaryDimIdx)
        {
            double GetCoord(int idx, DimensionValues dim) =>
                dim.MinCoord + idx * dim.StepSize;

            // Lists specific to slice to avoid race conditions
            _ghVoxelData[primaryDimIdx] = new List<GHVoxelData>();
            _voxelPts[primaryDimIdx] = new List<Point3d>();

            // Initialize once

            var outputOrderedCoords = new double[3];

            outputOrderedCoords[0] = GetCoord(primaryDimIdx, _outputOrderedDimVals[0]);
            for (var secondaryDimIdx = 0;
                secondaryDimIdx < _outputOrderedDimVals[1].NVoxels;
                secondaryDimIdx++)
            {
                outputOrderedCoords[1] =
                    GetCoord(secondaryDimIdx, _outputOrderedDimVals[1]);
                for (var tertiaryDimIdx = 0;
                    tertiaryDimIdx < _outputOrderedDimVals[2].NVoxels;
                    tertiaryDimIdx++)
                {
                    outputOrderedCoords[2] =
                        GetCoord(tertiaryDimIdx, _outputOrderedDimVals[2]);

                    Point3d voxelPt = OutputOrderedCoordsToPoint3d(outputOrderedCoords);
                    Tuple<double, double, double> distData = GetVoxelValues(voxelPt);

                    _ghVoxelData[primaryDimIdx].Add(new GHVoxelData(voxelPt,
                        distData.Item1, distData.Item2, distData.Item3));
                }
            }
        }

        // Struct storing voxel values as GH_* types.
        // At the moment they are only used when setting component outputs, if they
        // need to be used outside of that context just change it to a struct with
        // doubles and either convert implicitly or using a computed property.
        internal readonly struct GHVoxelData
        {
            internal GHVoxelData(
                Point3d centerPt, double distFactor, double distPtCloud1,
                double distPtCloud2
            )
            {
                CenterPt = new GH_Point(centerPt);
                DistFactor = new GH_Number(distFactor);
                DistPtCloud1 = new GH_Number(distPtCloud1);
                DistPtCloud2 = new GH_Number(distPtCloud2);
            }

            internal GH_Point CenterPt { get; }
            internal GH_Number DistFactor { get; }
            internal GH_Number DistPtCloud1 { get; }
            internal GH_Number DistPtCloud2 { get; }
        }
    }
}
