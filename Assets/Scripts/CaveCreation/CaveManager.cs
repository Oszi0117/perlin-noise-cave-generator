using CaveCreation.GenerationData;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace CaveCreation
{
    public class CaveManager : MonoBehaviour
    {
        [FormerlySerializedAs("_chunkGenerationData")] [SerializeField] private CaveCreationDataSO _caveCreationData;
        private readonly ChunkGenerator _chunkGenerator = new();
        private readonly ChunkSpawner _chunkSpawner = new();

        public void GenerateNewCave()
        {
            CreateCaveAsync().Forget();
        }

        public async UniTask CreateCaveAsync()
        {
            _chunkGenerator.Init(generateDataSO: _caveCreationData);
            await _chunkGenerator.GenerateCave();
            await _chunkSpawner.SpawnChunks(_caveCreationData, transform);
        }
    }
}
