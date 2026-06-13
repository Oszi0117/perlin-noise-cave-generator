using System.Diagnostics;
using CaveCreation.GenerationData;
using Cysharp.Threading.Tasks;
using RuntimeData;
using UnityEngine;
using Extensions;
using Debug = UnityEngine.Debug;

namespace CaveCreation
{
    public class CaveManager : MonoBehaviour
    {
        private readonly ChunkGenerator _chunkGenerator = new();
        private readonly ChunkSpawner _chunkSpawner = new();

        public async UniTask CreateCaveAsync(CaveCreationDataSO caveCreationData)
        {
            Cleanup();

            var caveObj = new GameObject
            {
                name =
                    $"[{caveCreationData.name}]_{Mathf.RoundToInt(caveCreationData.CaveSize.x)}x{Mathf.RoundToInt(caveCreationData.CaveSize.y)}x{Mathf.RoundToInt(caveCreationData.CaveSize.z)}",
                transform = { position = Vector3.zero, rotation = Quaternion.identity, localScale = Vector3.one }
            };
            CaveRuntimeData.Instance.CaveParent = caveObj;

            _chunkGenerator.Init(generateDataSO: caveCreationData);

            var stopwatch = Stopwatch.StartNew();
            await _chunkGenerator.GenerateCave();
            Debug.Log($"GenerateCave: {stopwatch.ElapsedMilliseconds}ms");
            
            _chunkSpawner.Init(caveCreationData);
            
            await _chunkSpawner.SpawnChunk(CaveRuntimeData.Instance.Chunks, caveObj.transform);
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

        private void OnDestroy()
        {
            _chunkSpawner.Dispose();
        }
    }
}
