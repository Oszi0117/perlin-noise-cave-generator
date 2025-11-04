using System;
using System.Collections.Generic;
using CaveManagement.Jobs;
using Cysharp.Threading.Tasks;
using RuntimeData;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CaveManagement
{
    [Serializable]
    public class CaveGenerator
    {
        [SerializeField] private Vector3 _chunkOrigin;
        [SerializeField] private float _voxelSize = 1f;
        [SerializeField] private Vector3 _chunkSize = new(50, 30, 50);
        [SerializeField, Range(0f, 1f)] private float _threshold = 0.55f;
        [SerializeField] private Vector3 _noiseScale = new(0.08f, 0.08f, 0.08f);
        [SerializeField] private int _seed = 1337;
        [SerializeField, Min(1)] private int _octaves = 4;
        [SerializeField, Min(1f)] private float _lacunarity = 2f;
        [SerializeField, Range(0f, 1f)] private float _persistence = 0.5f;
        [SerializeField] private Vector3 _offset = Vector3.zero;
        
        private List<GameObject> _spawnedCubes = new();
        
        public void PickRoomPositions()
        {
            //ChunkCenterPickerUtils.GetChunkCenters()
        }

        public async UniTask GenerateCave()
        {
            var generationBounds = new Bounds(_chunkOrigin, _chunkSize);
            var data = new ChunkGenerateSingleData(
                boundsMin: new float3(generationBounds.min.x, generationBounds.min.y, generationBounds.min.z),
                boundsMax: new float3(generationBounds.max.x, generationBounds.max.y, generationBounds.max.z),
                voxelSize: _voxelSize,
                threshold: _threshold,
                noiseScale: _noiseScale,
                seed: _seed,
                octaves: _octaves,
                lacunarity: _lacunarity,
                persistence: _persistence,
                offset: _offset
            );
            var job = new ChunkGenerateJobSingle(data);
            var handle = job.Schedule();
            
            await UniTask.WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            ClearCave();

            var voxels = job.Result;
            
            CaveData.Instance.VoxelCenters = voxels;
            
            foreach (var voxelCenter in voxels)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = voxelCenter;
                _spawnedCubes.Add(go);
            }
        }

        public void ClearCave()
        {
            foreach (var spawnedCube in _spawnedCubes)
            {
                Object.Destroy(spawnedCube);
            }
            _spawnedCubes.Clear();
            CaveData.Instance.VoxelCenters = new NativeList<float3>();
        }
    }
}