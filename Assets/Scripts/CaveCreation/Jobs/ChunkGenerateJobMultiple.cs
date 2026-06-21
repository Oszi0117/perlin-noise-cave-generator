using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Utils;

namespace CaveCreation.Jobs
{
    [BurstCompile]
    public struct ChunkGenerateJobMultiple : IJobParallelFor
    {
        //[NativeDisableParallelForRestriction] allows multiple jobs to write to the same native collection (gotta handle race conditions by yourself)
        [NativeDisableParallelForRestriction, WriteOnly]
        public NativeArray<float4> Result;

        [ReadOnly] public MultipleChunkGenerateData Data;

        public ChunkGenerateJobMultiple(NativeArray<float4> result, MultipleChunkGenerateData data)
        {
            Result = result;
            Data = data;
        }

        public void Execute(int jobIndex)
        {
            var chunkResult = new NativeList<float4>(Data.VoxelCountArray[jobIndex], Allocator.Temp);

            NoiseUtils.GetSdfVoxelValues(
                chunkResult,
                Data.BoundsMinArray[jobIndex],
                Data.BoundsMaxArray[jobIndex],
                Data.VoxelSize,
                Data.NoiseScale,
                Data.Seed,
                Data.Octaves,
                Data.Lacunarity,
                Data.Persistence,
                Data.IsoLevel,
                Data.RoomCenterArray,
                Data.RoomRadiusArray,
                Data.TunnelStartArray,
                Data.TunnelEndArray,
                Data.TunnelRadiusArray,
                Data.RoomIndexArray,
                Data.RoomIndexStartArray[jobIndex],
                Data.RoomIndexCountArray[jobIndex],
                Data.TunnelIndexArray,
                Data.TunnelIndexStartArray[jobIndex],
                Data.TunnelIndexCountArray[jobIndex],
                Data.SdfSurfaceThicknessInVoxels,
                Data.SdfSmoothUnionInVoxels,
                Data.SdfWallNoiseAmplitudeInVoxels,
                Data.SdfWallNoiseScaleInVoxels,
                Data.SdfWallLobeStrength,
                Data.SdfWallLobeScaleMultiplier,
                Data.SdfTunnelLobeStrength,
                Data.SdfTunnelLobeScaleMultiplier,
                Data.SdfInteriorNoiseStrength,
                Data.SdfInteriorNoiseCutoff,
                Data.SdfInteriorWallClearanceInVoxels,
                Data.SdfInteriorTunnelClearanceInVoxels,
                Data.SdfInteriorClearanceBlendInVoxels);

            var startIndex = Data.VoxelStartIndexArray[jobIndex];
            for (var i = 0; i < chunkResult.Length; i++)
                Result[startIndex + i] = chunkResult[i];

            chunkResult.Dispose();
        }
    }

    public readonly struct MultipleChunkGenerateData
    {
        public readonly NativeArray<float3> BoundsMinArray;
        public readonly NativeArray<float3> BoundsMaxArray;
        public readonly float VoxelSize;
        public readonly float3 NoiseScale;
        public readonly int Seed;
        public readonly int Octaves;
        public readonly float Lacunarity;
        public readonly float Persistence;
        public readonly float IsoLevel;
        public readonly NativeArray<float3> RoomCenterArray;
        public readonly NativeArray<float3> RoomRadiusArray;
        public readonly NativeArray<float3> TunnelStartArray;
        public readonly NativeArray<float3> TunnelEndArray;
        public readonly NativeArray<float> TunnelRadiusArray;
        public readonly NativeArray<int> RoomIndexArray;
        public readonly NativeArray<int> RoomIndexStartArray;
        public readonly NativeArray<int> RoomIndexCountArray;
        public readonly NativeArray<int> TunnelIndexArray;
        public readonly NativeArray<int> TunnelIndexStartArray;
        public readonly NativeArray<int> TunnelIndexCountArray;
        public readonly float SdfSurfaceThicknessInVoxels;
        public readonly float SdfSmoothUnionInVoxels;
        public readonly float SdfWallNoiseAmplitudeInVoxels;
        public readonly float SdfWallNoiseScaleInVoxels;
        public readonly float SdfWallLobeStrength;
        public readonly float SdfWallLobeScaleMultiplier;
        public readonly float SdfTunnelLobeStrength;
        public readonly float SdfTunnelLobeScaleMultiplier;
        public readonly float SdfInteriorNoiseStrength;
        public readonly float SdfInteriorNoiseCutoff;
        public readonly float SdfInteriorWallClearanceInVoxels;
        public readonly float SdfInteriorTunnelClearanceInVoxels;
        public readonly float SdfInteriorClearanceBlendInVoxels;
        public readonly NativeArray<int> VoxelStartIndexArray;
        public readonly NativeArray<int> VoxelCountArray;

        public MultipleChunkGenerateData(
            NativeArray<float3> boundsMinArray,
            NativeArray<float3> boundsMaxArray,
            float voxelSize,
            float3 noiseScale,
            int seed,
            int octaves,
            float lacunarity,
            float persistence,
            float isoLevel,
            NativeArray<float3> roomCenterArray,
            NativeArray<float3> roomRadiusArray,
            NativeArray<float3> tunnelStartArray,
            NativeArray<float3> tunnelEndArray,
            NativeArray<float> tunnelRadiusArray,
            NativeArray<int> roomIndexArray,
            NativeArray<int> roomIndexStartArray,
            NativeArray<int> roomIndexCountArray,
            NativeArray<int> tunnelIndexArray,
            NativeArray<int> tunnelIndexStartArray,
            NativeArray<int> tunnelIndexCountArray,
            float sdfSurfaceThicknessInVoxels,
            float sdfSmoothUnionInVoxels,
            float sdfWallNoiseAmplitudeInVoxels,
            float sdfWallNoiseScaleInVoxels,
            float sdfWallLobeStrength,
            float sdfWallLobeScaleMultiplier,
            float sdfTunnelLobeStrength,
            float sdfTunnelLobeScaleMultiplier,
            float sdfInteriorNoiseStrength,
            float sdfInteriorNoiseCutoff,
            float sdfInteriorWallClearanceInVoxels,
            float sdfInteriorTunnelClearanceInVoxels,
            float sdfInteriorClearanceBlendInVoxels,
            NativeArray<int> voxelStartIndexArray,
            NativeArray<int> voxelCountArray)
        {
            BoundsMinArray = boundsMinArray;
            BoundsMaxArray = boundsMaxArray;
            VoxelSize = voxelSize;
            NoiseScale = noiseScale;
            Seed = seed;
            Octaves = octaves;
            Lacunarity = lacunarity;
            Persistence = persistence;
            IsoLevel = isoLevel;
            RoomCenterArray = roomCenterArray;
            RoomRadiusArray = roomRadiusArray;
            TunnelStartArray = tunnelStartArray;
            TunnelEndArray = tunnelEndArray;
            TunnelRadiusArray = tunnelRadiusArray;
            RoomIndexArray = roomIndexArray;
            RoomIndexStartArray = roomIndexStartArray;
            RoomIndexCountArray = roomIndexCountArray;
            TunnelIndexArray = tunnelIndexArray;
            TunnelIndexStartArray = tunnelIndexStartArray;
            TunnelIndexCountArray = tunnelIndexCountArray;
            SdfSurfaceThicknessInVoxels = sdfSurfaceThicknessInVoxels;
            SdfSmoothUnionInVoxels = sdfSmoothUnionInVoxels;
            SdfWallNoiseAmplitudeInVoxels = sdfWallNoiseAmplitudeInVoxels;
            SdfWallNoiseScaleInVoxels = sdfWallNoiseScaleInVoxels;
            SdfWallLobeStrength = sdfWallLobeStrength;
            SdfWallLobeScaleMultiplier = sdfWallLobeScaleMultiplier;
            SdfTunnelLobeStrength = sdfTunnelLobeStrength;
            SdfTunnelLobeScaleMultiplier = sdfTunnelLobeScaleMultiplier;
            SdfInteriorNoiseStrength = sdfInteriorNoiseStrength;
            SdfInteriorNoiseCutoff = sdfInteriorNoiseCutoff;
            SdfInteriorWallClearanceInVoxels = sdfInteriorWallClearanceInVoxels;
            SdfInteriorTunnelClearanceInVoxels = sdfInteriorTunnelClearanceInVoxels;
            SdfInteriorClearanceBlendInVoxels = sdfInteriorClearanceBlendInVoxels;
            VoxelStartIndexArray = voxelStartIndexArray;
            VoxelCountArray = voxelCountArray;
        }
    }
}
