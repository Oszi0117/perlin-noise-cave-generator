using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Utils;

namespace CaveCreation.Jobs
{
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
        public readonly float3 NoiseScale;
        public readonly int Seed;
        public readonly int Octaves;
        public readonly float Lacunarity;
        public readonly float Persistence;

        public SingleChunkGenerateData(
            float3 boundsMin,
            float3 boundsMax,
            float voxelSize,
            float3 noiseScale,
            int seed,
            int octaves,
            float lacunarity,
            float persistence)
        {
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            VoxelSize = voxelSize;
            NoiseScale = noiseScale;
            Seed = seed;
            Octaves = octaves;
            Lacunarity = lacunarity;
            Persistence = persistence;
        }
    }
}