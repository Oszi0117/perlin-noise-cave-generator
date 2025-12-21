using System;
using Unity.Mathematics;

namespace CaveManagement
{
    [Serializable]
    public struct ChunkData
    {
        public int Index;
        public VoxelData[] Voxels;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public float VoxelSize;
        public int3 GridSize;

        public ChunkData(int index, VoxelData[] voxels, float3 boundsMin, float3 boundsMax, float voxelSize, int3 gridSize)
        {
            Index = index;
            Voxels = voxels;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            VoxelSize = voxelSize;
            GridSize = gridSize;
        }
    }
}