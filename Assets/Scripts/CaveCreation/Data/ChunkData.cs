using System;
using UnityEngine;

namespace CaveCreation.Data
{
    [Serializable]
    public class ChunkData
    {
        public int Index;
        public VoxelData[] Voxels;

        public ChunkData(int index, VoxelData[] voxels)
        {
            Index = index;
            Voxels = voxels;
        }
    }
}