using Cysharp.Threading.Tasks;
using UnityEngine;
using CaveManagement.ChunkGenerationData;

namespace CaveManagement
{
    public class CaveManager : MonoBehaviour
    {
        [SerializeField] private ChunkGenerationDataSO _chunkGenerationData;
        private readonly ChunkGenerator _chunkGenerator = new();
        private readonly ChunkSpawner _chunkSpawner = new();

        public void GenerateNewCave()
        {
            CreateCaveAsync().Forget();
        }

        public async UniTask CreateCaveAsync()
        {
            _chunkGenerator.Init(generateDataSO: _chunkGenerationData);
            await _chunkGenerator.GenerateCave();
            _chunkSpawner.SpawnChunks(_chunkGenerationData, transform);
        }
    }
}
