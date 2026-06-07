using System.Collections.Generic;
using CaveCreation.Data;
using CaveCreation.GenerationData;
using CaveCreation.Jobs;
using Cysharp.Threading.Tasks;
using RuntimeData;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Utils;
using Object = UnityEngine.Object;

namespace CaveCreation
{
    //TODO: try adding vertices welding
    public class ChunkSpawner
    {
        private CaveCreationDataSO _generateDataSO;
        private float _isoLevel => _generateDataSO.IsoLevel;
        private NativeArray<int3> _corners;
        private NativeArray<int> _edgeTable;
        private NativeArray<int> _trianglesTable;

        public void Init(CaveCreationDataSO generateDataSO)
        {
            _generateDataSO = generateDataSO;
            CaveRuntimeData.Instance.ChunkObjects = new List<GameObject>();
            if (!_corners.IsCreated)
                _corners = new NativeArray<int3>(MarchingCubesUtils.Tables.Corners, Allocator.Persistent);

            if (!_edgeTable.IsCreated)
                _edgeTable = new NativeArray<int>(MarchingCubesUtils.Tables.EdgeTable, Allocator.Persistent);

            if (!_trianglesTable.IsCreated)
                _trianglesTable = new NativeArray<int>(MarchingCubesUtils.Tables.TrianglesTable, Allocator.Persistent);
        }

        public async UniTask SpawnChunk(IReadOnlyList<ChunkData> chunks, Transform parent)
        {
            if (chunks == null || chunks.Count == 0)
                return;
            
            var chunkCount = chunks.Count;
            var totalVoxelCount = 0;
            var totalMaxVertexCount = 0;
            var voxelStartIndexPerChunk = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var voxelCountPerChunk = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var vertexStartIndexPerChunk = new NativeArray<int>(chunkCount, Allocator.Persistent);

            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var voxelCount = chunks[chunkIndex].Voxels?.Length ?? 0;
                voxelStartIndexPerChunk[chunkIndex] = totalVoxelCount;
                voxelCountPerChunk[chunkIndex] = voxelCount;
                totalVoxelCount += voxelCount;

                vertexStartIndexPerChunk[chunkIndex] = totalMaxVertexCount;
                totalMaxVertexCount += CalculateMaxVertices(chunks[chunkIndex].Voxels, _generateDataSO.VoxelSize);
            }

            if (totalVoxelCount == 0 || totalMaxVertexCount == 0)
            {
                voxelStartIndexPerChunk.Dispose();
                voxelCountPerChunk.Dispose();
                vertexStartIndexPerChunk.Dispose();
                return;
            }
            
            var flattenedVoxels = new NativeArray<float4>(totalVoxelCount, Allocator.Persistent);
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var chunkVoxels = chunks[chunkIndex].Voxels;
                if (chunkVoxels == null || chunkVoxels.Length == 0)
                    continue;

                var chunkStart = voxelStartIndexPerChunk[chunkIndex];
                for (var voxelIndex = 0; voxelIndex < chunkVoxels.Length; voxelIndex++)
                {
                    var voxel = chunkVoxels[voxelIndex];
                    flattenedVoxels[chunkStart + voxelIndex] = new float4(voxel.Position, voxel.Value);
                }
            }

            var vertices = new NativeArray<float3>(totalMaxVertexCount, Allocator.Persistent);
            var triangles = new NativeArray<int>(totalMaxVertexCount, Allocator.Persistent);
            var vertexCounts = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var triangleCounts = new NativeArray<int>(chunkCount, Allocator.Persistent);

            var job = new ChunkSpawnJobMultiple
            {
                Voxels = flattenedVoxels,
                Corners = _corners,
                EdgeTable = _edgeTable,
                TrianglesTable = _trianglesTable,
                VoxelStartIndexPerChunk = voxelStartIndexPerChunk,
                VoxelCountPerChunk = voxelCountPerChunk,
                VertexStartIndexPerChunk = vertexStartIndexPerChunk,
                Vertices = vertices,
                Triangles = triangles,
                VertexCountPerChunk = vertexCounts,
                TriangleCountPerChunk = triangleCounts,
                VoxelSize = _generateDataSO.VoxelSize,
                IsoLevel = _isoLevel
            };
            
            var handle = job.Schedule(chunkCount, 1);
            await UniTask.WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var vertexCount = vertexCounts[chunkIndex];
                var triangleCount = triangleCounts[chunkIndex];
                if (vertexCount == 0 || triangleCount == 0)
                    continue;

                var chunkStart = vertexStartIndexPerChunk[chunkIndex];
                var chunkVertices = new List<Vector3>(vertexCount);
                var chunkTriangles = new List<int>(triangleCount);

                for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
                    chunkVertices.Add(vertices[chunkStart + vertexIndex]);

                for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                    chunkTriangles.Add(triangles[chunkStart + triangleIndex]);

                var mesh = UpdateMesh(chunkVertices, chunkTriangles);
                if (mesh == null)
                    continue;

                var chunkObj = Object.Instantiate(_generateDataSO.ChunkPrefab, parent);
                chunkObj.GetComponent<MeshFilter>().mesh = mesh;
                CaveRuntimeData.Instance.ChunkObjects.Add(chunkObj);
            }
            
            flattenedVoxels.Dispose();
            vertices.Dispose();
            triangles.Dispose();
            vertexCounts.Dispose();
            triangleCounts.Dispose();
            voxelStartIndexPerChunk.Dispose();
            voxelCountPerChunk.Dispose();
            vertexStartIndexPerChunk.Dispose();
        }

        private static Mesh UpdateMesh(List<Vector3> verts, List<int> tris)
        {
            if (verts.Count == 0)
                return null;

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
        
        private static int CalculateMaxVertices(VoxelData[] voxels, float voxelSize)
        {
            if (voxels == null || voxels.Length == 0)
                return 0;

            var normalizedVoxelSize = voxelSize <= 0f ? 1f : voxelSize;
            var min = voxels[0].Position;
            var max = min;

            for (var i = 1; i < voxels.Length; i++)
            {
                var position = voxels[i].Position;
                min = math.min(min, position);
                max = math.max(max, position);
            }

            var pointsCountX = math.max(1, (int)math.round((max.x - min.x) / normalizedVoxelSize)) + 1;
            var pointsCountY = math.max(1, (int)math.round((max.y - min.y) / normalizedVoxelSize)) + 1;
            var pointsCountZ = math.max(1, (int)math.round((max.z - min.z) / normalizedVoxelSize)) + 1;

            return (pointsCountX - 1) * (pointsCountY - 1) * (pointsCountZ - 1) * 15;
        }

        public void Dispose()
        {
            if (_corners.IsCreated)
                _corners.Dispose();
            if (_edgeTable.IsCreated)
                _edgeTable.Dispose();
            if (_trianglesTable.IsCreated)
                _trianglesTable.Dispose();
        }
    }
}
