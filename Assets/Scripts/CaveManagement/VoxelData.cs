using System;
using Unity.Mathematics;

namespace CaveManagement
{
    [Serializable]
    public struct VoxelData
    {
        public float3 Position;

        public VoxelData(float3 position)
        {
            Position = position;
        }
    }
}