using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace RuntimeData
{
    public class CaveData : RuntimeData<CaveData>
    {
        public NativeList<float3> VoxelCenters;
    }
}
