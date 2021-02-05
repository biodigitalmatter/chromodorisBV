/*
* This algorithm is based on Karsten Schmidt's 'toxiclibs' isosurfacer in Java
* https://bitbucket.org/postspectacular/toxiclibs
* Released under the Lesser GPL (LGPL 2.1)
*/

using System;
using System.Collections.Generic;

namespace Chromodoris
{
    public class VolumetricSpace
    {

        #region fields
        public int resX, resY, resZ;
        public int resX1, resY1, resZ1;

        public int sliceRes;

        public int numCells;

        private readonly float[,,] _isoData;

        public float[,,] IsoData => _isoData;
        #endregion

        #region constructors
        public VolumetricSpace(float[,,] isoData)
        {
            this.resX = isoData.GetLength(0);
            this.resY = isoData.GetLength(1);
            this.resZ = isoData.GetLength(2);
            resX1 = resX - 1;
            resY1 = resY - 1;
            resZ1 = resZ - 1;
            sliceRes = resX * resY;
            numCells = sliceRes * resZ;
            _isoData = isoData;
        }

        public VolumetricSpace(List<float> isoDataAsList, int resX, int resY, int resZ)
        {
            this.resX = resX;
            this.resY = resY;
            this.resZ = resZ;

            resX1 = resX - 1;
            resY1 = resY - 1;
            resZ1 = resZ - 1;
            sliceRes = resX * resY;
            numCells = sliceRes * resZ;
            _isoData = ArrayFromList(isoDataAsList);
        }
        #endregion

        #region methods
        private float[,,] ArrayFromList(List<float> isoDataAsList)
        {
            float[,,] array = new float[resX, resY, resZ];

            int listIdx = 0;

            for (int xIdx = 0; xIdx < resX; xIdx++)
            {
                for (int yIdx = 0; yIdx < resY; yIdx++)
                {
                    for (int zIdx = 0; zIdx < resZ; zIdx++)
                    {

                        array[xIdx, yIdx, zIdx] = isoDataAsList[listIdx];

                        listIdx++;

                    }

                }
            }

            return array;
        }

        /*
        private int clip(int val, int min, int max)
        {
            if (val < min)
            {
                return min;
            }

            if (val > max)
            {
                return max;
            }

            return val;
        }
        */

        public double getVoxelAt(int index)
        {
            int xVal = 0, yVal = 0, zVal = 0;

            if (index >= sliceRes)
            {
                zVal = (int)Math.Floor((double)index / sliceRes); // find the z row
                index = index - zVal * sliceRes;
            }

            if (index >= resX)
            {
                yVal = (int)Math.Floor((double)index / resX); // find the z row
                index = index - yVal * resX;
            }

            xVal = index;
            return IsoData[xVal, yVal, zVal];
        }

        public double getVoxelAt(int x, int y, int z)
        {
            return IsoData[x, y, z];
        }
        #endregion
    }
}
