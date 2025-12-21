using CaveManagement.ChunkGenerationData;
using RuntimeData;
using UnityEngine;

namespace CaveManagement
{
    public class ChunkSpawner
    {
        public void SpawnChunks(ChunkGenerationDataSO generateDataSO, Transform parent)
        {
            var runtimeData = CaveRuntimeData.Instance;
            if (runtimeData.Meshes == null)
                return;

            foreach (var meshData in runtimeData.Meshes)
            {
                var chunkObject = new GameObject($"Chunk_{meshData.ChunkIndex}");
                chunkObject.transform.SetParent(parent, false);

                var meshFilter = chunkObject.AddComponent<MeshFilter>();
                var meshRenderer = chunkObject.AddComponent<MeshRenderer>();

                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(meshData.Vertices);
                mesh.SetTriangles(meshData.Triangles, 0);
                mesh.SetNormals(meshData.Normals);
                mesh.RecalculateBounds();

                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = generateDataSO.VoxelMaterial;
            }
        }
    }
}