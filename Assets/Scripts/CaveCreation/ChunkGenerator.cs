using System;
using System.Collections.Generic;
using CaveCreation.Data;
using CaveCreation.GenerationData;
using CaveCreation.Jobs;
using Cysharp.Threading.Tasks;
using RuntimeData;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CaveCreation
{
    [Serializable]
    public class ChunkGenerator
    {
        private CaveCreationDataSO _generateDataSO;

        public void Init(CaveCreationDataSO generateDataSO)
        {
            _generateDataSO = generateDataSO;
        }

        public async UniTask GenerateCave()
        {
            var chunkSize = new float3(_generateDataSO.ChunkSize.x, _generateDataSO.ChunkSize.y,
                _generateDataSO.ChunkSize.z);
            var normalizedVoxelSize = _generateDataSO.VoxelSize <= 0f ? 1f : _generateDataSO.VoxelSize;
            var closureExtension = normalizedVoxelSize * Mathf.Max(0f, _generateDataSO.ClosureExtensionInVoxels);
            var chunkBounds = BuildRoomChunkBounds(
                chunkSize,
                normalizedVoxelSize);

            if (chunkBounds.Count == 0)
            {
                CaveRuntimeData.Instance.Chunks = new List<ChunkData>();
                return;
            }

            var chunkCount = chunkBounds.Count;

            var boundsMinArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var boundsMaxArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var voxelSizeArray = new NativeArray<float>(chunkCount, Allocator.Persistent);
            var noiseScaleArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var seedArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var octavesArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var lacunarityArray = new NativeArray<float>(chunkCount, Allocator.Persistent);
            var persistenceArray = new NativeArray<float>(chunkCount, Allocator.Persistent);
            var closureBoundsMinArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var closureBoundsMaxArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var openFaceMaskArray = new NativeArray<byte>(chunkCount, Allocator.Persistent);
            var voxelStartIndexArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var voxelCountArray = new NativeArray<int>(chunkCount, Allocator.Persistent);

            var totalVoxelCount = 0;
            var closureExtensionOffset = new float3(closureExtension);

            for (var i = 0; i < chunkCount; i++)
            {
                const byte openFaceMask = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5);
                var chunk = chunkBounds[i];
                var boundsMin = chunk.Min;
                var boundsMax = chunk.Max;

                var extendedBoundsMin = boundsMin - closureExtensionOffset;
                var extendedBoundsMax = boundsMax + closureExtensionOffset;

                boundsMinArray[i] = extendedBoundsMin;
                boundsMaxArray[i] = extendedBoundsMax;
                closureBoundsMinArray[i] = extendedBoundsMin;
                closureBoundsMaxArray[i] = extendedBoundsMax;
                voxelSizeArray[i] = normalizedVoxelSize;
                noiseScaleArray[i] = _generateDataSO.NoiseScale;
                seedArray[i] = _generateDataSO.Seed;
                octavesArray[i] = _generateDataSO.Octaves;
                lacunarityArray[i] = _generateDataSO.Lacunarity;
                persistenceArray[i] = _generateDataSO.Persistence;
                openFaceMaskArray[i] = openFaceMask;

                var voxelCount = CalculateVoxelCount(extendedBoundsMax - extendedBoundsMin, normalizedVoxelSize);
                voxelStartIndexArray[i] = totalVoxelCount;
                voxelCountArray[i] = voxelCount;
                totalVoxelCount += voxelCount;
            }

            var voxelsNative = new NativeArray<float4>(totalVoxelCount, Allocator.Persistent);

            var data = new MultipleChunkGenerateData(
                boundsMinArray,
                boundsMaxArray,
                closureBoundsMinArray,
                closureBoundsMaxArray,
                voxelSizeArray,
                noiseScaleArray,
                seedArray,
                octavesArray,
                lacunarityArray,
                persistenceArray,
                openFaceMaskArray,
                _generateDataSO.IsoLevel,
                _generateDataSO.GetClosureSettings(),
                voxelStartIndexArray,
                voxelCountArray);

            var job = new ChunkGenerateJobMultiple(voxelsNative, data);
            var handle = job.Schedule(chunkCount, 1);

            await UniTask.WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            var chunks = new List<ChunkData>(chunkCount);
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var voxelCount = voxelCountArray[chunkIndex];
                var chunkVoxelsManaged = new VoxelData[voxelCount];
                var startIndex = voxelStartIndexArray[chunkIndex];

                for (var voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                {
                    var p = voxelsNative[startIndex + voxelIndex];
                    chunkVoxelsManaged[voxelIndex] = new VoxelData(p.xyz, p.w);
                }

                chunks.Add(new ChunkData(index: chunkIndex, voxels: chunkVoxelsManaged));
            }

            voxelsNative.Dispose();
            boundsMinArray.Dispose();
            boundsMaxArray.Dispose();
            closureBoundsMinArray.Dispose();
            closureBoundsMaxArray.Dispose();
            voxelSizeArray.Dispose();
            noiseScaleArray.Dispose();
            seedArray.Dispose();
            octavesArray.Dispose();
            lacunarityArray.Dispose();
            persistenceArray.Dispose();
            openFaceMaskArray.Dispose();
            voxelStartIndexArray.Dispose();
            voxelCountArray.Dispose();

            CaveRuntimeData.Instance.Chunks = chunks;
        }

        private static int CalculateVoxelCount(float3 chunkSize, float voxelSize)
        {
            var normalizedVoxelSize = voxelSize <= 0f ? 1f : voxelSize;

            var pointsCountX = math.max(1, (int)math.round(chunkSize.x / normalizedVoxelSize)) + 1;
            var pointsCountY = math.max(1, (int)math.round(chunkSize.y / normalizedVoxelSize)) + 1;
            var pointsCountZ = math.max(1, (int)math.round(chunkSize.z / normalizedVoxelSize)) + 1;

            return pointsCountX * pointsCountY * pointsCountZ;
        }

        private List<ChunkBounds> BuildRoomChunkBounds(
            float3 fallbackChunkSize,
            float voxelSize)
        {
            var caveSize = GetSafeCaveSize(fallbackChunkSize);
            var caveBoundsMin = new float3(_generateDataSO.CaveOrigin) - caveSize * 0.5f;
            var caveBoundsMax = caveBoundsMin + caveSize;

            var totalRoomChunkCount =
                Mathf.Max(0, _generateDataSO.SmallRoomChunks.Amount) +
                Mathf.Max(0, _generateDataSO.MediumRoomChunks.Amount) +
                Mathf.Max(0, _generateDataSO.LargeRoomChunks.Amount);
            var chunks = new List<ChunkBounds>(totalRoomChunkCount);
            var random = new Unity.Mathematics.Random((uint)(_generateDataSO.Seed == 0 ? 1 : _generateDataSO.Seed));

            AddRoomChunks(_generateDataSO.LargeRoomChunks, caveBoundsMin, caveBoundsMax, voxelSize, ref random, chunks);
            AddRoomChunks(_generateDataSO.MediumRoomChunks, caveBoundsMin, caveBoundsMax, voxelSize, ref random, chunks);
            AddRoomChunks(_generateDataSO.SmallRoomChunks, caveBoundsMin, caveBoundsMax, voxelSize, ref random, chunks);

            return chunks;
        }

        private float3 GetSafeCaveSize(float3 fallbackChunkSize)
        {
            var caveSize = new float3(_generateDataSO.CaveSize.x, _generateDataSO.CaveSize.y, _generateDataSO.CaveSize.z);
            if (caveSize.x > 0f && caveSize.y > 0f && caveSize.z > 0f)
                return caveSize;

            var legacyGridSize = new float3(
                Mathf.Max(1, _generateDataSO.GridSize.x),
                Mathf.Max(1, _generateDataSO.GridSize.y),
                Mathf.Max(1, _generateDataSO.GridSize.z));
            return fallbackChunkSize * legacyGridSize;
        }

        private void AddRoomChunks(
            RoomChunkTypeSettings settings,
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            float voxelSize,
            ref Unity.Mathematics.Random random,
            List<ChunkBounds> chunks)
        {
            var amount = Mathf.Max(0, settings.Amount);
            if (amount == 0)
                return;

            var sizeMin = Mathf.Max(voxelSize * 2f, Mathf.Min(settings.SizeRange.x, settings.SizeRange.y));
            var sizeMax = Mathf.Max(sizeMin, Mathf.Max(settings.SizeRange.x, settings.SizeRange.y));
            var heightScaleMin = Mathf.Max(0.1f, Mathf.Min(_generateDataSO.RoomChunkHeightScaleRange.x,
                _generateDataSO.RoomChunkHeightScaleRange.y));
            var heightScaleMax = Mathf.Max(heightScaleMin, Mathf.Max(_generateDataSO.RoomChunkHeightScaleRange.x,
                _generateDataSO.RoomChunkHeightScaleRange.y));
            var attempts = Mathf.Max(1, _generateDataSO.RoomChunkPlacementAttempts);
            var caveSize = caveBoundsMax - caveBoundsMin;

            for (var roomIndex = 0; roomIndex < amount; roomIndex++)
            {
                var bestChunk = default(ChunkBounds);
                var bestScore = float.NegativeInfinity;

                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    var baseSize = random.NextFloat(sizeMin, sizeMax);
                    var size = new float3(
                        baseSize * random.NextFloat(0.85f, 1.2f),
                        baseSize * random.NextFloat(heightScaleMin, heightScaleMax),
                        baseSize * random.NextFloat(0.85f, 1.2f));
                    size = math.min(size, caveSize);

                    var halfSize = size * 0.5f;
                    var center = RandomPointInsideBounds(caveBoundsMin + halfSize, caveBoundsMax - halfSize, ref random);
                    var candidate = new ChunkBounds(center - halfSize, center + halfSize);
                    var score = ScoreChunkPlacement(candidate, chunks);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestChunk = candidate;
                    }
                }

                chunks.Add(bestChunk);
            }
        }

        private static float ScoreChunkPlacement(ChunkBounds candidate, IReadOnlyList<ChunkBounds> chunks)
        {
            if (chunks.Count == 0)
                return 0f;

            var candidateCenter = candidate.Center;
            var candidateRadius = math.length(candidate.Size) * 0.5f;
            var score = float.PositiveInfinity;

            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var radius = math.length(chunk.Size) * 0.5f;
                score = math.min(score, math.distance(candidateCenter, chunk.Center) - candidateRadius - radius);
            }

            return score;
        }

        private static float3 RandomPointInsideBounds(
            float3 min,
            float3 max,
            ref Unity.Mathematics.Random random)
        {
            var center = (min + max) * 0.5f;
            return new float3(
                min.x <= max.x ? random.NextFloat(min.x, max.x) : center.x,
                min.y <= max.y ? random.NextFloat(min.y, max.y) : center.y,
                min.z <= max.z ? random.NextFloat(min.z, max.z) : center.z);
        }

        private readonly struct ChunkBounds
        {
            public readonly float3 Min;
            public readonly float3 Max;

            public float3 Center => (Min + Max) * 0.5f;
            public float3 Size => Max - Min;

            public ChunkBounds(float3 min, float3 max)
            {
                Min = min;
                Max = max;
            }
        }
    }
}
