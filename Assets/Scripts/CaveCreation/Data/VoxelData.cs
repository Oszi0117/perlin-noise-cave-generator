using System;
using Unity.Mathematics;

namespace CaveCreation.Data
{
    [Serializable]
    public struct VoxelData
    {
        public float3 Position;
        public float Value;

        public VoxelData(float3 position, float value)
        {
            Position = position;
            Value = value;
        }
    }
}