using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using RuntimeData;
using UnityEngine;
using Utils;
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
            ClearCave();

            var generationBounds = new Bounds(_chunkOrigin, _chunkSize);
            
            var voxels = await UniTask.RunOnThreadPool(GenerateTask);
            

            CaveData.Instance.VoxelCenters = voxels;

            foreach (var voxelCenter in voxels)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = voxelCenter;
                _spawnedCubes.Add(go);
            }

            return;
            
            UniTask<Vector3[]> GenerateTask()
            {
                var voxelCenters = NoiseUtils.GetVoxelCenters(
                    generationBounds,
                    _voxelSize,
                    _threshold,
                    _noiseScale,
                    _seed,
                    _octaves,
                    _lacunarity,
                    _persistence,
                    _offset
                );

                return UniTask.FromResult(voxelCenters);
            }
        }

        public void ClearCave()
        {
            foreach (var spawnedCube in _spawnedCubes)
            {
                Object.Destroy(spawnedCube);
            }
            _spawnedCubes.Clear();
            CaveData.Instance.VoxelCenters = null;
        }
    }
}