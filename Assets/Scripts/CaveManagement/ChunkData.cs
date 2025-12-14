using System;

namespace CaveManagement
{
    [Serializable]
    public struct ChunkData
    {
        public  int Index;
        public  VoxelData[] Voxels;

        public ChunkData(int index, VoxelData[] voxels)
        {
            Index = index;
            Voxels = voxels;
        }
    }
}