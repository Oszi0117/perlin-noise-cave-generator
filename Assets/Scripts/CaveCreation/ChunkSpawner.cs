using CaveCreation.GenerationData;
using Cysharp.Threading.Tasks;
using Extensions;
using RuntimeData;
using UnityEngine;

namespace CaveCreation
{
    public class ChunkSpawner
    {
        private static readonly int Color = Shader.PropertyToID("_BaseColor");

        private async UniTask ClearPreviousChunk(CaveCreationDataSO caveCreationDataSO)
        {
            var runtimeData = CaveRuntimeData.Instance;
            if (runtimeData.VoxelObjects == null)
                return;

            var iterator = 0;
            foreach (var voxelObj in runtimeData.VoxelObjects)
            {
                if (iterator >= caveCreationDataSO.MaxSpawnDestroyOperationsPerFrame)
                {
                    iterator = 0;
                    await UniTask.DelayFrame(1);
                }
                
                voxelObj.UniversalDestroy();
                iterator++;
            }
        }
        
        public async UniTask SpawnChunks(CaveCreationDataSO caveCreationDataSO, Transform parent)
        {
            await ClearPreviousChunk(caveCreationDataSO);
            var runtimeData = CaveRuntimeData.Instance;

            runtimeData.VoxelObjects = new();
            runtimeData.ChunkObjects = new();
            
            foreach (var chunk in runtimeData.Chunks)
            {
                var chunkObj = new GameObject($"Chunk_{chunk.Index}");
                chunkObj.transform.SetParent(parent);
                runtimeData.ChunkObjects.Add(chunkObj);
                var voxelIndex = 0;
                foreach (var voxel in chunk.Voxels)
                {
                    if (voxelIndex >= caveCreationDataSO.MaxSpawnDestroyOperationsPerFrame)
                    {
                        voxelIndex = 0;
                        await UniTask.DelayFrame(1);
                    }
                    
                    var voxelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    voxelObj.name = $"Voxel_{voxelIndex}";
                    voxelObj.transform.SetParent(chunkObj.transform, false);
                    voxelObj.transform.position = voxel.Position;
                    var meshRenderer = voxelObj.GetComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = caveCreationDataSO.VoxelMaterial;
                    var matPropBlock = new MaterialPropertyBlock();
                    meshRenderer.GetPropertyBlock(matPropBlock);
                    matPropBlock.SetColor(Color, UnityEngine.Color.white * voxel.Value);
                    meshRenderer.SetPropertyBlock(matPropBlock);
                    voxelIndex++;
                    runtimeData.VoxelObjects.Add(voxelObj);
                }
            }
        }
    }
}