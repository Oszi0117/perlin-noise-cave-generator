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
            var chunkResult = new NativeList<float4>(Data.VoxelsPerChunk, Allocator.Temp);

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

            var startIndex = jobIndex * Data.VoxelsPerChunk;
            for (var i = 0; i < chunkResult.Length; i++)
                Result[startIndex + i] = chunkResult[i];

            chunkResult.Dispose();
        }
    }

    public readonly struct MultipleChunkGenerateData
    {
        public readonly NativeArray<float3> BoundsMinArray;
        public readonly NativeArray<float3> BoundsMaxArray;
        public readonly NativeArray<float> VoxelSizeArray;
        public readonly NativeArray<float3> NoiseScaleArray;
        public readonly NativeArray<int> SeedArray;
        public readonly NativeArray<int> OctavesArray;
        public readonly NativeArray<float> LacunarityArray;
        public readonly NativeArray<float> PersistenceArray;
        public readonly int VoxelsPerChunk;

        public MultipleChunkGenerateData(
            NativeArray<float3> boundsMinArray,
            NativeArray<float3> boundsMaxArray,
            NativeArray<float> voxelSizeArray,
            NativeArray<float3> noiseScaleArray,
            NativeArray<int> seedArray,
            NativeArray<int> octavesArray,
            NativeArray<float> lacunarityArray,
            NativeArray<float> persistenceArray,
            int voxelsPerChunk)
        {
            BoundsMinArray = boundsMinArray;
            BoundsMaxArray = boundsMaxArray;
            VoxelSizeArray = voxelSizeArray;
            NoiseScaleArray = noiseScaleArray;
            SeedArray = seedArray;
            OctavesArray = octavesArray;
            LacunarityArray = lacunarityArray;
            PersistenceArray = persistenceArray;
            VoxelsPerChunk = voxelsPerChunk;
        }
    }
}