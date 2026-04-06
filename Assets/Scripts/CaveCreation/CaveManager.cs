using CaveCreation.Data;
using CaveCreation.GenerationData;
using Cysharp.Threading.Tasks;
using RuntimeData;
using UnityEngine;
using System.Runtime.InteropServices;
using Extensions;

namespace CaveCreation
{
    public class CaveManager : MonoBehaviour
    {
        [SerializeField] private CaveCreationDataSO _caveCreationData;
        private readonly ChunkGenerator _chunkGenerator = new();
        private readonly ChunkSpawner _chunkSpawner = new();

        public void GenerateFromPreset()
        {
            CreateCaveAsync(_caveCreationData).Forget();
        }

        public void GenerateRandom()
        {
            var rnd = CaveCreationDataSO.CreateRandomizedInstance(_caveCreationData);
            CreateCaveAsync(rnd).Forget();
        }

        public async UniTask CreateCaveAsync(CaveCreationDataSO caveCreationData)
        {
            Cleanup();

            var caveObj = new GameObject
            {
                name =
                    $"[{caveCreationData.name}]_{caveCreationData.GridSize.x}x{caveCreationData.GridSize.y}x{caveCreationData.GridSize.z}",
                transform = { position = Vector3.zero, rotation = Quaternion.identity, localScale = Vector3.one }
            };
            CaveRuntimeData.Instance.CaveParent = caveObj;

            _chunkGenerator.Init(generateDataSO: caveCreationData);

            await _chunkGenerator.GenerateCave();

            _chunkSpawner.Init(caveCreationData);

            foreach (var chunk in CaveRuntimeData.Instance.Chunks)
                _chunkSpawner.SpawnChunk(chunk.Voxels, caveObj.transform);
        }

        private void Cleanup()
        {
            if (CaveRuntimeData.Instance.ChunkObjects == null ||
                CaveRuntimeData.Instance.ChunkObjects.Count == 0) return;

            foreach (var oldChunk in CaveRuntimeData.Instance.ChunkObjects)
                oldChunk.UniversalDestroy();
            
            CaveRuntimeData.Instance.CaveParent.UniversalDestroy();

            CaveRuntimeData.Instance.CaveParent = null;
            CaveRuntimeData.Instance.ChunkObjects = null;
        }
    }
}