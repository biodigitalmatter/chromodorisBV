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

using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace Chromodoris.IsoSurfacing
{
    internal class VoxelSamplerDual
    {
        private readonly KDTreePtCloud[] _ptClouds;
        private readonly SparseVoxelGrid<DualSamplerVoxelData> _voxelGrid;
        private readonly bool _zyx;

        internal VoxelSamplerDual(
            IEnumerable<Point3d> ptCloud1, IEnumerable<Point3d> ptCloud2, Box box,
            int resX, int resY, int resZ, bool zyx = false
        )
        {
            _ptClouds = new[]
                { new KDTreePtCloud(ptCloud1), new KDTreePtCloud(ptCloud2) };

            _voxelGrid =
                new SparseVoxelGrid<DualSamplerVoxelData>(box, resX, resY, resZ);
            _zyx = zyx;
        }

        internal Box BBox => _voxelGrid.BBox;

        private int NVoxels => _voxelGrid.Count;

        private void SetVoxelValues(int idx)
        {
            Point3d centerPt = _voxelGrid.GetPt(idx);
            double distPtCloud1 = _ptClouds[0].GetClosestPtDistance(centerPt);
            double distPtCloud2 = _ptClouds[1].GetClosestPtDistance(centerPt);

            double sumDist = distPtCloud1 + distPtCloud2;
            double distFactor = distPtCloud1 / sumDist;

            _voxelGrid.SetValue(
                new DualSamplerVoxelData(distFactor, distPtCloud1, distPtCloud2), idx);
        }

        internal SamplerResults Sample()
        {
            _ = Parallel.ForEach(Partitioner.Create(0, NVoxels), (range, _) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    SetVoxelValues(i);
                }
            });

            var results = new SamplerResults();

            IEnumerable<Point3d> ptEnumerable =
                !_zyx ? _voxelGrid.VoxelPtsList : _voxelGrid.PtsOrderedByZYX;

            results.CenterPts =
                from pt in ptEnumerable
                select new GH_Point(pt);

            IEnumerable<DualSamplerVoxelData> dataEnumerable =
                !_zyx ? _voxelGrid : _voxelGrid.DataOrderedByZYX;
            // To avoid multiple enumerations
            List<DualSamplerVoxelData> dataList = dataEnumerable.ToList();

            results.DistFactors =
                from data in dataList
                select new GH_Number(data.Value);

            results.DistsPtCloud1 =
                from data in dataList
                select new GH_Number(data.DistPtCloud1);

            results.DistsPtCloud2 =
                from data in dataList
                select new GH_Number(data.DistPtCloud2);

            return results;
        }
    }

    internal class SamplerResults
    {
        public IEnumerable<GH_Point> CenterPts { get; set; }
        public IEnumerable<GH_Number> DistFactors { get; set; }
        public IEnumerable<GH_Number> DistsPtCloud1 { get; set; }
        public IEnumerable<GH_Number> DistsPtCloud2 { get; set; }
    }

    public class DualSamplerVoxelData : VoxelData
    {
        public DualSamplerVoxelData()
        {
            DistPtCloud1 = double.NaN;
            DistPtCloud1 = double.NaN;
        }

        public DualSamplerVoxelData(
            double value, double distPtCloud1, double distPtCloud2
        ) : base(value)
        {
            DistPtCloud1 = distPtCloud1;
            DistPtCloud2 = distPtCloud2;
        }

        public double DistPtCloud1 { get; }
        public double DistPtCloud2 { get; }
    }
}
