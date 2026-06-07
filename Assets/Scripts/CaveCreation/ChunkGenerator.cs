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
            var chunkOrigins = BuildChunkOrigins();
            if (chunkOrigins == null || chunkOrigins.Count == 0)
            {
                CaveRuntimeData.Instance.Chunks = new List<ChunkData>();
                return;
            }

            var chunkCount = chunkOrigins.Count;
            var chunkSize = new float3(_generateDataSO.ChunkSize.x, _generateDataSO.ChunkSize.y,
                _generateDataSO.ChunkSize.z);
            var normalizedVoxelSize = _generateDataSO.VoxelSize <= 0f ? 1f : _generateDataSO.VoxelSize;
            var closureExtension = normalizedVoxelSize * Mathf.Max(0f, _generateDataSO.ClosureExtensionInVoxels);

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

            var safeGridX = Mathf.Max(1, _generateDataSO.GridSize.x);
            var safeGridY = Mathf.Max(1, _generateDataSO.GridSize.y);
            var safeGridZ = Mathf.Max(1, _generateDataSO.GridSize.z);
            var closureBoundsMin = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var closureBoundsMax = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            var totalVoxelCount = 0;

            for (var i = 0; i < chunkCount; i++)
            {
                var halfSize = chunkSize * 0.5f;
                var boundsMin = chunkOrigins[i] - halfSize;
                var boundsMax = chunkOrigins[i] + halfSize;

                var x = i % safeGridX;
                var y = (i / safeGridX) % safeGridY;
                var z = i / (safeGridX * safeGridY);

                byte openFaceMask = 0;
                if (x == 0)
                    openFaceMask |= 1 << 0;
                if (x == safeGridX - 1)
                    openFaceMask |= 1 << 1;
                if (y == 0)
                    openFaceMask |= 1 << 2;
                if (y == safeGridY - 1)
                    openFaceMask |= 1 << 3;
                if (z == 0)
                    openFaceMask |= 1 << 4;
                if (z == safeGridZ - 1)
                    openFaceMask |= 1 << 5;

                var extendedBoundsMin = boundsMin;
                var extendedBoundsMax = boundsMax;
                if ((openFaceMask & (1 << 0)) != 0)
                    extendedBoundsMin.x -= closureExtension;
                if ((openFaceMask & (1 << 1)) != 0)
                    extendedBoundsMax.x += closureExtension;
                if ((openFaceMask & (1 << 2)) != 0)
                    extendedBoundsMin.y -= closureExtension;
                if ((openFaceMask & (1 << 3)) != 0)
                    extendedBoundsMax.y += closureExtension;
                if ((openFaceMask & (1 << 4)) != 0)
                    extendedBoundsMin.z -= closureExtension;
                if ((openFaceMask & (1 << 5)) != 0)
                    extendedBoundsMax.z += closureExtension;

                boundsMinArray[i] = extendedBoundsMin;
                boundsMaxArray[i] = extendedBoundsMax;
                closureBoundsMin = math.min(closureBoundsMin, extendedBoundsMin);
                closureBoundsMax = math.max(closureBoundsMax, extendedBoundsMax);
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

            for (var i = 0; i < chunkCount; i++)
            {
                closureBoundsMinArray[i] = closureBoundsMin;
                closureBoundsMaxArray[i] = closureBoundsMax;
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

        private List<float3> BuildChunkOrigins()
        {
            var chunkOrigins = new List<float3>();
            var safeGridX = Mathf.Max(1, _generateDataSO.GridSize.x);
            var safeGridY = Mathf.Max(1, _generateDataSO.GridSize.y);
            var safeGridZ = Mathf.Max(1, _generateDataSO.GridSize.z);
            var chunkSize = _generateDataSO.ChunkSize;
            var baseOrigin = _generateDataSO.CaveOrigin;

            for (var z = 0; z < safeGridZ; z++)
            for (var y = 0; y < safeGridY; y++)
            for (var x = 0; x < safeGridX; x++)
                chunkOrigins.Add(new float3(
                    baseOrigin.x + x * chunkSize.x,
                    baseOrigin.y + y * chunkSize.y,
                    baseOrigin.z + z * chunkSize.z));

            return chunkOrigins;
        }
    }
}