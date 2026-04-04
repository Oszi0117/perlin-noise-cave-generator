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

            var boundsMinArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var boundsMaxArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var voxelSizeArray = new NativeArray<float>(chunkCount, Allocator.Persistent);
            var noiseScaleArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var seedArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var octavesArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var lacunarityArray = new NativeArray<float>(chunkCount, Allocator.Persistent);
            var persistenceArray = new NativeArray<float>(chunkCount, Allocator.Persistent);

            for (var i = 0; i < chunkCount; i++)
            {
                var halfSize = chunkSize * 0.5f;
                boundsMinArray[i] = chunkOrigins[i] - halfSize;
                boundsMaxArray[i] = chunkOrigins[i] + halfSize;
                voxelSizeArray[i] = _generateDataSO.VoxelSize;
                noiseScaleArray[i] = _generateDataSO.NoiseScale;
                seedArray[i] = _generateDataSO.Seed;
                octavesArray[i] = _generateDataSO.Octaves;
                lacunarityArray[i] = _generateDataSO.Lacunarity;
                persistenceArray[i] = _generateDataSO.Persistence;
            }

            var voxelsPerChunk = CalculateVoxelCount(chunkSize, _generateDataSO.VoxelSize);
            var voxelsNative = new NativeArray<float4>(chunkCount * voxelsPerChunk, Allocator.Persistent);

            var data = new MultipleChunkGenerateData(
                boundsMinArray,
                boundsMaxArray,
                voxelSizeArray,
                noiseScaleArray,
                seedArray,
                octavesArray,
                lacunarityArray,
                persistenceArray,
                voxelsPerChunk);

            var job = new ChunkGenerateJobMultiple(voxelsNative, data);
            var handle = job.Schedule(chunkCount, 1);

            await UniTask.WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            var chunks = new List<ChunkData>(chunkCount);
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var chunkVoxelsManaged = new VoxelData[voxelsPerChunk];
                var startIndex = chunkIndex * voxelsPerChunk;

                for (var voxelIndex = 0; voxelIndex < voxelsPerChunk; voxelIndex++)
                {
                    var p = voxelsNative[startIndex + voxelIndex];
                    chunkVoxelsManaged[voxelIndex] = new VoxelData(p.xyz, p.w);
                }

                chunks.Add(new ChunkData(index: chunkIndex, voxels: chunkVoxelsManaged));
            }

            voxelsNative.Dispose();
            boundsMinArray.Dispose();
            boundsMaxArray.Dispose();
            voxelSizeArray.Dispose();
            noiseScaleArray.Dispose();
            seedArray.Dispose();
            octavesArray.Dispose();
            lacunarityArray.Dispose();
            persistenceArray.Dispose();

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