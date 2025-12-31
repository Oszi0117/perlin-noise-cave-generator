using System;
using System.Collections.Generic;
using CaveCreation.Data;
using CaveCreation.GenerationData;
using CaveCreation.Jobs;
using Cysharp.Threading.Tasks;
using RuntimeData;
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
            var generationBounds = new Bounds(_generateDataSO.ChunkOrigin, _generateDataSO.ChunkSize);
            var boundsMin = new float3(generationBounds.min.x, generationBounds.min.y, generationBounds.min.z);
            var boundsMax = new float3(generationBounds.max.x, generationBounds.max.y, generationBounds.max.z);
            
            var data = new SingleChunkGenerateData(
                boundsMin: boundsMin,
                boundsMax: boundsMax,
                voxelSize: _generateDataSO.VoxelSize,
                noiseScale: _generateDataSO.NoiseScale,
                seed: _generateDataSO.Seed,
                octaves: _generateDataSO.Octaves,
                lacunarity: _generateDataSO.Lacunarity,
                persistence: _generateDataSO.Persistence
            );
            var job = new ChunkGenerateJobSingle(data);
            var handle = job.Schedule();
            
            await UniTask.WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            var voxelsNative = job.Result;
            var count = voxelsNative.Length;
            var voxelsManaged = new VoxelData[count];
            
            for (int i = 0; i < count; i++)
            {
                var p = voxelsNative[i];
                voxelsManaged[i] = new VoxelData(p.xyz, p.w);
            }

            voxelsNative.Dispose();

            var chunks = new List<ChunkData>
            {
                new(index: 0, voxels: voxelsManaged)
            };

            CaveRuntimeData.Instance.Chunks = chunks;
        }
    }
}