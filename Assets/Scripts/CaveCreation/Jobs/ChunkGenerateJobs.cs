using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Utils;

namespace CaveCreation.Jobs
{
    [BurstCompile]
    public struct ChunkGenerateJobs : IJobParallelFor
    {
        [WriteOnly] public NativeArray<NativeList<float4>> Result;
        [ReadOnly] public MultipleChunkGenerateData Data;

        public ChunkGenerateJobs(NativeArray<NativeList<float4>> result, MultipleChunkGenerateData data)
        {
            Result = result;
            Data = data;
        }

        public void Execute(int jobIndex)
        {
            NoiseUtils.GetVoxelValues(
                Result[jobIndex],
                Data.BoundsMinArray[jobIndex],
                Data.BoundsMaxArray[jobIndex],
                Data.VoxelSizeArray[jobIndex],
                Data.NoiseScaleArray[jobIndex],
                Data.SeedArray[jobIndex],
                Data.OctavesArray[jobIndex],
                Data.LacunarityArray[jobIndex],
                Data.PersistenceArray[jobIndex]);
        }
    }

    [BurstCompile]
    public struct ChunkGenerateJobSingle : IJob
    {
        public NativeList<float4> Result;
        [ReadOnly] public SingleChunkGenerateData Data;

        public ChunkGenerateJobSingle(SingleChunkGenerateData data)
        {
            Result = new NativeList<float4>(Allocator.Persistent);
            Data = data;
        }

        public void Execute()
        {
            NoiseUtils.GetVoxelValues(
                Result,
                Data.BoundsMin,
                Data.BoundsMax,
                Data.VoxelSize,
                Data.NoiseScale,
                Data.Seed,
                Data.Octaves,
                Data.Lacunarity,
                Data.Persistence);
        }
    }

    public readonly struct SingleChunkGenerateData
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

        public SingleChunkGenerateData(
            float3 boundsMin,
            float3 boundsMax,
            float voxelSize,
            float threshold,
            float3 noiseScale,
            int seed,
            int octaves,
            float lacunarity,
            float persistence)
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
        }
    }

    public readonly struct MultipleChunkGenerateData
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

        public MultipleChunkGenerateData(
            NativeArray<float3> boundsMinArray,
            NativeArray<float3> boundsMaxArray,
            NativeArray<float> voxelSizeArray,
            NativeArray<float> thresholdArray,
            NativeArray<float3> noiseScaleArray,
            NativeArray<int> seedArray,
            NativeArray<int> octavesArray,
            NativeArray<float> lacunarityArray,
            NativeArray<float> persistenceArray)
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
        }
    }
}