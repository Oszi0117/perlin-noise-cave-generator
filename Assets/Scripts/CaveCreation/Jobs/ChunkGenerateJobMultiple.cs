using CaveCreation.GenerationData;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
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

            NoiseUtils.GetVoxelValues(
                chunkResult,
                Data.BoundsMinArray[jobIndex],
                Data.BoundsMaxArray[jobIndex],
                Data.VoxelSizeArray[jobIndex],
                Data.NoiseScaleArray[jobIndex],
                Data.SeedArray[jobIndex],
                Data.OctavesArray[jobIndex],
                Data.LacunarityArray[jobIndex],
                Data.PersistenceArray[jobIndex]);
            
            NoiseUtils.ApplyOpenBoundaryWalls(
                chunkResult,
                Data.ClosureBoundsMinArray[jobIndex],
                Data.ClosureBoundsMaxArray[jobIndex],
                Data.VoxelSizeArray[jobIndex],
                Data.NoiseScaleArray[jobIndex],
                Data.SeedArray[jobIndex],
                Data.OctavesArray[jobIndex],
                Data.LacunarityArray[jobIndex],
                Data.PersistenceArray[jobIndex],
                Data.IsoLevel,
                Data.ClosureSettings,
                Data.OpenFaceMaskArray[jobIndex]);

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
        public readonly NativeArray<float3> ClosureBoundsMinArray;
        public readonly NativeArray<float3> ClosureBoundsMaxArray;
        public readonly NativeArray<float> VoxelSizeArray;
        public readonly NativeArray<float3> NoiseScaleArray;
        public readonly NativeArray<int> SeedArray;
        public readonly NativeArray<int> OctavesArray;
        public readonly NativeArray<float> LacunarityArray;
        public readonly NativeArray<float> PersistenceArray;
        public readonly NativeArray<byte> OpenFaceMaskArray;
        public readonly float IsoLevel;
        public readonly BoundaryClosureSettings ClosureSettings;
        public readonly NativeArray<int> VoxelStartIndexArray;
        public readonly NativeArray<int> VoxelCountArray;

        public MultipleChunkGenerateData(
            NativeArray<float3> boundsMinArray,
            NativeArray<float3> boundsMaxArray,
            NativeArray<float3> closureBoundsMinArray,
            NativeArray<float3> closureBoundsMaxArray,
            NativeArray<float> voxelSizeArray,
            NativeArray<float3> noiseScaleArray,
            NativeArray<int> seedArray,
            NativeArray<int> octavesArray,
            NativeArray<float> lacunarityArray,
            NativeArray<float> persistenceArray,
            NativeArray<byte> openFaceMaskArray,
            float isoLevel,
            BoundaryClosureSettings closureSettings,
            NativeArray<int> voxelStartIndexArray,
            NativeArray<int> voxelCountArray)
        {
            BoundsMinArray = boundsMinArray;
            BoundsMaxArray = boundsMaxArray;
            ClosureBoundsMinArray = closureBoundsMinArray;
            ClosureBoundsMaxArray = closureBoundsMaxArray;
            VoxelSizeArray = voxelSizeArray;
            NoiseScaleArray = noiseScaleArray;
            SeedArray = seedArray;
            OctavesArray = octavesArray;
            LacunarityArray = lacunarityArray;
            PersistenceArray = persistenceArray;
            OpenFaceMaskArray = openFaceMaskArray;
            IsoLevel = isoLevel;
            ClosureSettings = closureSettings;
            VoxelStartIndexArray = voxelStartIndexArray;
            VoxelCountArray = voxelCountArray;
        }
    }
}
