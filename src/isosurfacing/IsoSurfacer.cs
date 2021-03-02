/*
* This algorithm is based on Karsten Schmidt's 'toxiclibs' isosurfacer in Java
* https://bitbucket.org/postspectacular/toxiclibs
* Released under the Lesser GPL (LGPL 2.1)
*/


using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

namespace Chromodoris.IsoSurfacing
{
    internal class IsoSurfacer
    {
        private readonly Box _bBox;
        private readonly float[,,] _isoData;
        private readonly double _isoValue;
        private short[] _cellIndexCache, _prevCellIndexCache;
        private Dictionary<int, int> _edgeVertices; // int2 is face index

        public IsoSurfacer(float[,,] isoData, double isoValue, Box bBox)
        {
            _isoData = isoData;
            _isoValue = isoValue;
            _bBox = bBox;

            _cellIndexCache = new short[SliceRes];
            _prevCellIndexCache = new short[SliceRes];

            ResetEdgeVerticesDict();
        }

        private int XRes => _isoData.GetLength(0);
        private int YRes => _isoData.GetLength(1);
        private int ZRes => _isoData.GetLength(2);
        private int SliceRes => XRes * YRes;

        private Box GridBox =>
            new Box(Plane.WorldXY, new Interval(0, XRes - 1), new Interval(0, YRes - 1),
                new Interval(0, ZRes - 1));

        private double GetVoxelAt(int index)
        {
            var yVal = 0;
            var zVal = 0;

            if (index >= SliceRes)
            {
                zVal = (int)Math.Floor((double)index / SliceRes); // find the z row
                index -= zVal * SliceRes;
            }

            if (index >= XRes)
            {
                yVal = (int)Math.Floor((double)index / XRes); // find the z row
                index -= yVal * XRes;
            }

            int xVal = index;
            return _isoData[xVal, yVal, zVal];
        }

        internal Mesh GenerateSurfaceMesh()
        {
            var mesh = new Mesh();

            double offsetZ = 0;
            for (var z = 0; z < ZRes - 1; z++)
            {
                int sliceOffset = SliceRes * z;
                double offsetY = 0;
                for (var y = 0; y < YRes - 1; y++)
                {
                    double offsetX = 0;
                    int sliceIndex = XRes * y;
                    int offset = sliceIndex + sliceOffset;
                    for (var x = 0; x < XRes - 1; x++)
                    {
                        int cellIndex = GetCellIndex(x, y, z);
                        _cellIndexCache[sliceIndex + x] = (short)cellIndex;
                        if (cellIndex > 0 && cellIndex < 255)
                        {
                            int edgeFlags =
                                MarchingCubesIndex.edgesToCompute[cellIndex];
                            if (edgeFlags > 0 && edgeFlags < 255)
                            {
                                int edgeOffsetIndex = offset * 3;
                                double offsetData = GetVoxelAt(offset);
                                double isoDiff = _isoValue - offsetData;
                                if ((edgeFlags & 1) > 0)
                                {
                                    double t = isoDiff /
                                               (GetVoxelAt(offset + 1) - offsetData);
                                    _edgeVertices[edgeOffsetIndex] =
                                        mesh.Vertices.Add(offsetX + t, y, z);
                                }

                                if ((edgeFlags & 2) > 0)
                                {
                                    double t = isoDiff / (GetVoxelAt(offset + XRes) -
                                        offsetData);
                                    _edgeVertices[edgeOffsetIndex + 1] =
                                        mesh.Vertices.Add(x, offsetY + t, z);
                                }

                                if ((edgeFlags & 4) > 0)
                                {
                                    double t = isoDiff /
                                               (GetVoxelAt(offset + SliceRes) -
                                                offsetData);
                                    _edgeVertices[edgeOffsetIndex + 2] =
                                        mesh.Vertices.Add(x, y, offsetZ + t);
                                }
                            }
                        }

                        offsetX++;
                        offset++;
                    }

                    offsetY++;
                }

                if (z > 0)
                {
                    CreateFacesForSlice(mesh, z - 1);
                }

                short[] tmp = _prevCellIndexCache;
                _prevCellIndexCache = _cellIndexCache;
                _cellIndexCache = tmp;
                offsetZ++;
            }

            CreateFacesForSlice(mesh, ZRes - 2);

            mesh.Faces.CullDegenerateFaces();
            mesh.Transform(BoxToBox(GridBox, _bBox));

            return mesh;
        }

        private void CreateFacesForSlice(in Mesh mesh, int z)
        {
            var face = new int[16];
            int sliceOffset = SliceRes * z;
            for (var y = 0; y < YRes - 1; y++)
            {
                int offset = XRes * y;
                for (var x = 0; x < XRes - 1; x++)
                {
                    int cellIndex = _prevCellIndexCache[offset];
                    if (cellIndex > 0 && cellIndex < 255)
                    {
                        var n = 0;
                        int edgeIndex;
                        int[] cellTriangles =
                            MarchingCubesIndex.cellTriangles[cellIndex];
                        while ((edgeIndex = cellTriangles[n]) != -1)
                        {
                            int[] edgeOffsetInfo =
                                MarchingCubesIndex.edgeOffsets[edgeIndex];
                            face[n] =
                                (x + edgeOffsetInfo[0] +
                                 XRes * (y + edgeOffsetInfo[1]) +
                                 SliceRes * (z + edgeOffsetInfo[2])) * 3 +
                                edgeOffsetInfo[3];
                            n++;
                        }

                        for (var i = 0; i < n; i += 3)
                        {
                            try
                            {
                                int va = _edgeVertices[face[i + 1]];
                                int vb = _edgeVertices[face[i + 2]];
                                int vc = _edgeVertices[face[i]];
                                mesh.Faces.AddFace(vc, vb, va);
                            }
                            catch (KeyNotFoundException)
                            {
                            }
                        }
                    }

                    offset++;
                }
            }

            int minIndex = sliceOffset * 3;


            List<int> toRemove = (
                from entry in _edgeVertices
                where entry.Key < minIndex
                select entry.Key).ToList();

            foreach (int dat in toRemove)
            {
                _edgeVertices.Remove(dat);
            }
        }

        private int GetCellIndex(int x, int y, int z)
        {
            var cellIndex = 0;
            int idx = x + y * XRes + z * SliceRes;
            if (GetVoxelAt(idx) < _isoValue)
            {
                cellIndex |= 0x01;
            }

            if (GetVoxelAt(idx + SliceRes) < _isoValue)
            {
                cellIndex |= 0x08;
            }

            if (GetVoxelAt(idx + XRes) < _isoValue)
            {
                cellIndex |= 0x10;
            }

            if (GetVoxelAt(idx + XRes + SliceRes) < _isoValue)
            {
                cellIndex |= 0x80;
            }

            idx++;
            if (GetVoxelAt(idx) < _isoValue)
            {
                cellIndex |= 0x02;
            }

            if (GetVoxelAt(idx + SliceRes) < _isoValue)
            {
                cellIndex |= 0x04;
            }

            if (GetVoxelAt(idx + XRes) < _isoValue)
            {
                cellIndex |= 0x20;
            }

            if (GetVoxelAt(idx + XRes + SliceRes) < _isoValue)
            {
                cellIndex |= 0x40;
            }

            return cellIndex;
        }

        /**
         * Resets mesh vertices to default positions and clears face index. Needs to
         * be called in between successive calls to
         * {@link #computeSurfaceMesh(Mesh, double)}.
         */
        private void ResetEdgeVerticesDict()
        {
            _edgeVertices = new Dictionary<int, int>(XRes * YRes * 10);
        }

        private static Transform BoxToBox(in Box source, in Box target)
        {
            var sourceCenterPlane = new Plane(source.Center, source.Plane.XAxis,
                source.Plane.YAxis);
            var targetCenterPlane = new Plane(target.Center, target.Plane.XAxis,
                target.Plane.YAxis);

            Transform planeToPlane =
                Transform.PlaneToPlane(sourceCenterPlane, targetCenterPlane);
            Transform scale = Transform.Scale(sourceCenterPlane,
                target.X.Length / source.X.Length, target.Y.Length / source.Y.Length,
                target.Z.Length / source.Z.Length);

            return planeToPlane * scale;
        }
    }
}
