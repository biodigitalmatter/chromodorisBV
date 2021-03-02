using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Rhino.Geometry;

// ReSharper disable UnusedMember.Global

namespace Chromodoris.IsoSurfacing
{
    public class SparseVoxelGrid<T> : IReadOnlyList<T> where T : VoxelData, new()
    {
        private readonly T[,,] _grid;

        private SparseVoxelGrid(Box bBox) => BBox = bBox;

        public SparseVoxelGrid(Box bBox, int xRes, int yRes, int zRes) : this(bBox) =>
            _grid = new T[xRes, yRes, zRes];

        public SparseVoxelGrid(Box bBox, T[,,] voxelGridArray) : this(bBox) =>
            _grid = (T[,,])voxelGridArray.Clone();

        public SparseVoxelGrid(
            Box bBox, IEnumerable<T> flatVoxelDataList, int xRes, int yRes, int zRes
        ) : this(bBox) =>
            _grid = UnflattenList3D(flatVoxelDataList, xRes, yRes, zRes);

        public SparseVoxelGrid(
            Box bBox, IEnumerable<double> flatVoxelValueList, int xRes, int yRes,
            int zRes
        ) : this(bBox, flatVoxelValueList.Select(d => (T)d), xRes, yRes, zRes)
        {
        }

        /// <summary>
        ///     Create instance without specifying bounding box. Unit box is used to get voxel coordinates.
        /// </summary>
        /// <param name="flatVoxelValueList"></param>
        /// <param name="xRes"></param>
        /// <param name="yRes"></param>
        /// <param name="zRes"></param>
        public SparseVoxelGrid(
            IEnumerable<double> flatVoxelValueList, int xRes, int yRes, int zRes
        ) : this(CreateUnitBox(xRes, yRes, zRes), flatVoxelValueList, xRes, yRes, zRes)
        {
        }

        public Box BBox { get; }

        private double XMinCoord => BBox.X.Min;
        private double YMinCoord => BBox.Y.Min;
        private double ZMinCoord => BBox.Z.Min;
        private double XStepSize => (BBox.X.Max - BBox.X.Min) / (XRes - 1);
        private double YStepSize => (BBox.Y.Max - BBox.Y.Min) / (YRes - 1);
        private double ZStepSize => (BBox.Z.Max - BBox.Z.Min) / (ZRes - 1);

        private int XRes => _grid.GetLength(0);
        private int YRes => _grid.GetLength(1);
        private int ZRes => _grid.GetLength(2);

        public IEnumerable<Point3d> VoxelPtsList =>
            Enumerable.Range(0, Count).Select(GetPt);

        public T this[int x, int y, int z] => GetValue(x, y, z);
        public T this[int[] indices] => GetValue(indices);

        public IEnumerable<T> DataOrderedByZYX => OrderedByZYX(GetValue);
        public IEnumerable<Point3d> PtsOrderedByZYX => OrderedByZYX(GetPt);

        /// <summary>
        ///     Gets element at list index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index] => GetValue(index);

        public int Count => _grid.Length;

        public IEnumerator<T> GetEnumerator() => _grid.OfType<T>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static Box CreateUnitBox(int xRes, int yRes, int zRes) =>
            new Box(Plane.WorldXY, new Interval(0, xRes - 1), new Interval(0, yRes - 1),
                new Interval(0, zRes - 1));

        private int[] GetGridCoord(int index) => GetGridCoord(index, YRes, ZRes);

        private static int[] GetGridCoord(int index, int yRes, int zRes)
        {
            var coord = new int[3];

            coord[0] = index / (yRes * zRes);
            int tmp = index % (yRes * zRes);

            coord[1] = tmp / zRes;
            coord[2] = tmp % zRes;

            return coord;
        }

        public Point3d GetPt(int listIndex)
        {
            int[] coord = GetGridCoord(listIndex);

            return GetPt(coord);
        }

        private Point3d GetPt(IReadOnlyList<int> arrayIndices)
        {
            double x = XMinCoord + arrayIndices[0] * XStepSize;
            double y = YMinCoord + arrayIndices[1] * YStepSize;
            double z = ZMinCoord + arrayIndices[2] * ZStepSize;

            return new Point3d(x, y, z);
        }

        private static T1[,,] UnflattenList3D<T1>(
            IEnumerable<T1> list, int nDim1, int nDim2, int nDim3
        )
        {
            var array = new T1[nDim1, nDim2, nDim3];

            var idx = 0;
            foreach (T1 value in list)
            {
                int[] gridCoord = GetGridCoord(idx, nDim2, nDim3);
                array.SetValue(value, gridCoord);

                idx++;
            }

            return array;
        }

        private static bool IsVoxelOnBoundary(int idx, int xRes, int yRes, int zRes)
        {
            int[] gridCoord = GetGridCoord(idx, yRes, zRes);
            return gridCoord.Any(j => j == 0) || gridCoord[0] == xRes - 1 ||
                   gridCoord[1] == yRes - 1 || gridCoord[2] == zRes - 1;
        }

        private bool IsVoxelOnBoundary(int idx) =>
            IsVoxelOnBoundary(idx, XRes, YRes, ZRes);

        public void Close()
        {
            // Load balancing seems to outweigh the cost of initializing the small function body
            // for large grids.
            _ = Parallel.ForEach(Partitioner.Create(Enumerable.Range(0, Count)),
                (idx, _) =>
                {
                    if (IsVoxelOnBoundary(idx))
                    {
                        this[idx].Value = 1.0;
                    }
                });
        }

        private IEnumerable<T1> OrderedByZYX<T1>(Func<int[], T1> getFunc)
        {
            return
                from z in Enumerable.Range(0, ZRes)
                from y in Enumerable.Range(0, YRes)
                from x in Enumerable.Range(0, XRes)
                select getFunc(new[] { x, y, z });
        }

        // ReSharper disable MemberCanBePrivate.Global
        public void SetValue(T value, int x, int y, int z)
        {
            _grid.SetValue(value, x, y, z);
        }

        public void SetValue(T value, int[] indices) =>
            SetValue(value, indices[0], indices[1], indices[2]);

        public void SetValue(T value, int listIdx) =>
            SetValue(value, GetGridCoord(listIdx));

        public T GetValue(int x, int y, int z) =>
            (T)_grid.GetValue(x, y, z);

        public T GetValue(int[] indices) =>
            GetValue(indices[0], indices[1], indices[2]);

        public T GetValue(int listIdx) =>
            GetValue(GetGridCoord(listIdx));
        // ReSharper restore MemberCanBePrivate.Global
    }

    public class VoxelData
    {
        public VoxelData()
        {
        }

        protected VoxelData(double value) => Value = value;

        public double Value { get; set; } = double.NaN;

        public static explicit operator VoxelData(double d) => new VoxelData(d);
    }
}
