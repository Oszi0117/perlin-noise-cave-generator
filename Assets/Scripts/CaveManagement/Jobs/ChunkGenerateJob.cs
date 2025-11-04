using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Utils;

namespace CaveManagement.Jobs
{
    [BurstCompile]
    public struct ChunkGenerateJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<NativeList<float3>> Result;
        [ReadOnly] public ChunkGenerateData Data;

        public ChunkGenerateJob(NativeArray<NativeList<float3>> result, ChunkGenerateData data)
        {
            Result = result;
            Data = data;
        }

        public void Execute(int jobIndex)
        {
            NoiseUtils.GetVoxelCenters(
                Result[jobIndex],
                Data.BoundsMinArray[jobIndex],
                Data.BoundsMaxArray[jobIndex],
                Data.VoxelSizeArray[jobIndex],
                Data.ThresholdArray[jobIndex],
                Data.NoiseScaleArray[jobIndex],
                Data.SeedArray[jobIndex],
                Data.OctavesArray[jobIndex],
                Data.LacunarityArray[jobIndex],
                Data.PersistenceArray[jobIndex],
                Data.OffsetArray[jobIndex]);
        }
    }

    [BurstCompile]
    public struct ChunkGenerateJobSingle : IJob
    {
        public NativeList<float3> Result;
        [ReadOnly] public ChunkGenerateSingleData Data;

        public ChunkGenerateJobSingle(ChunkGenerateSingleData data)
        {
            Result = new NativeList<float3>(Allocator.Persistent);
            Data = data;
        }

        public void Execute()
        {
            NoiseUtils.GetVoxelCenters(
                Result,
                Data.BoundsMin,
                Data.BoundsMax,
                Data.VoxelSize,
                Data.Threshold,
                Data.NoiseScale,
                Data.Seed,
                Data.Octaves,
                Data.Lacunarity,
                Data.Persistence,
                Data.Offset);
        }
    }

    public readonly struct ChunkGenerateSingleData
    {
        public readonly float3 BoundsMin;
        public readonly float3 BoundsMax;
        public readonly float VoxelSize;
        public readonly float Threshold;
        public readonly float3 NoiseScale;
        public readonly int Seed;
        public readonly int Octaves;
        public readonly float Lacunarity;
        public readonly float Persistence;
        public readonly float3 Offset;

        public ChunkGenerateSingleData(
            float3 boundsMin,
            float3 boundsMax,
            float voxelSize,
            float threshold,
            float3 noiseScale,
            int seed,
            int octaves,
            float lacunarity,
            float persistence,
            float3 offset)
        {
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            VoxelSize = voxelSize;
            Threshold = threshold;
            NoiseScale = noiseScale;
            Seed = seed;
            Octaves = octaves;
            Lacunarity = lacunarity;
            Persistence = persistence;
            Offset = offset;
        }
    }

    public readonly struct ChunkGenerateData
    {
        public readonly NativeArray<float3> BoundsMinArray;
        public readonly NativeArray<float3> BoundsMaxArray;
        public readonly NativeArray<float> VoxelSizeArray;
        public readonly NativeArray<float> ThresholdArray;
        public readonly NativeArray<float3> NoiseScaleArray;
        public readonly NativeArray<int> SeedArray;
        public readonly NativeArray<int> OctavesArray;
        public readonly NativeArray<float> LacunarityArray;
        public readonly NativeArray<float> PersistenceArray;
        public readonly NativeArray<float3> OffsetArray;

        public ChunkGenerateData(
            NativeArray<float3> boundsMinArray,
            NativeArray<float3> boundsMaxArray,
            NativeArray<float> voxelSizeArray,
            NativeArray<float> thresholdArray,
            NativeArray<float3> noiseScaleArray,
            NativeArray<int> seedArray,
            NativeArray<int> octavesArray,
            NativeArray<float> lacunarityArray,
            NativeArray<float> persistenceArray,
            NativeArray<float3> offsetArray)
        {
            BoundsMinArray = boundsMinArray;
            BoundsMaxArray = boundsMaxArray;
            VoxelSizeArray = voxelSizeArray;
            ThresholdArray = thresholdArray;
            NoiseScaleArray = noiseScaleArray;
            SeedArray = seedArray;
            OctavesArray = octavesArray;
            LacunarityArray = lacunarityArray;
            PersistenceArray = persistenceArray;
            OffsetArray = offsetArray;
        }
    }
}