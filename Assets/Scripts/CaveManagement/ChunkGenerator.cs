using System;
using System.Collections.Generic;
using CaveManagement.ChunkGenerationData;
using CaveManagement.Jobs;
using Cysharp.Threading.Tasks;
using RuntimeData;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CaveManagement
{
    [Serializable]
    public class ChunkGenerator
    {
        private ChunkGenerationDataSO _generateDataSO;

        public void Init(ChunkGenerationDataSO generateDataSO)
        {
            _generateDataSO = generateDataSO;
        }

        public async UniTask GenerateCave()
        {
            var generationBounds = new Bounds(_generateDataSO.ChunkOrigin, _generateDataSO.ChunkSize);
            var data = new SingleChunkGenerateData(
                boundsMin: new float3(generationBounds.min.x, generationBounds.min.y, generationBounds.min.z),
                boundsMax: new float3(generationBounds.max.x, generationBounds.max.y, generationBounds.max.z),
                voxelSize: _generateDataSO.VoxelSize,
                threshold: _generateDataSO.Threshold,
                noiseScale: _generateDataSO.NoiseScale,
                seed: _generateDataSO.Seed,
                octaves: _generateDataSO.Octaves,
                lacunarity: _generateDataSO.Lacunarity,
                persistence: _generateDataSO.Persistence,
                offset: _generateDataSO.Offset
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
                voxelsManaged[i] = new VoxelData(p);
            }
            voxelsNative.Dispose();

            var chunks = new List<ChunkData>
            {
                new (index: 0, voxels: voxelsManaged)
            };

            CaveRuntimeData.Instance.Chunks = chunks;
        }
    }
}