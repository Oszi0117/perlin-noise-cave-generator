using System;
using CaveCreation.GenerationData;
using Cysharp.Threading.Tasks;
using RuntimeData;
using UnityEngine;

namespace CaveCreation
{
    public class CaveManager : MonoBehaviour
    {
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private CaveCreationDataSO _caveCreationData;
        private readonly ChunkGenerator _chunkGenerator = new();
        private readonly ChunkSpawner _chunkSpawner = new();

        public void GenerateNewCave()
        {
            CreateCaveAsync().Forget();
        }

        public async UniTask CreateCaveAsync()
        {
            //TODO: generate multiple chunks at once
            _chunkGenerator.Init(generateDataSO: _caveCreationData);
            await _chunkGenerator.GenerateCave();
            _chunkSpawner.Init(_caveCreationData, _meshFilter);
            _chunkSpawner.SpawnChunk(CaveRuntimeData.Instance.Chunks[0].Voxels);
        }
    }
}
