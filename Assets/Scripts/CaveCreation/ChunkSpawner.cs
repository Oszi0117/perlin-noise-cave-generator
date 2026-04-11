using System;
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
            var voxelsPerChunk = chunks[0].Voxels?.Length ?? 0;
            if (voxelsPerChunk == 0)
                return;
            
            var flattenedVoxels = new NativeArray<float4>(chunkCount * voxelsPerChunk, Allocator.Persistent);
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var chunkVoxels = chunks[chunkIndex].Voxels;
                if (chunkVoxels == null || chunkVoxels.Length != voxelsPerChunk)
                    continue;

                var chunkStart = chunkIndex * voxelsPerChunk;
                for (var voxelIndex = 0; voxelIndex < voxelsPerChunk; voxelIndex++)
                {
                    var voxel = chunkVoxels[voxelIndex];
                    flattenedVoxels[chunkStart + voxelIndex] = new float4(voxel.Position, voxel.Value);
                }
            }

            var pointsCountX = math.max(1, (int)math.round(_generateDataSO.ChunkSize.x / _generateDataSO.VoxelSize)) + 1;
            var pointsCountY = math.max(1, (int)math.round(_generateDataSO.ChunkSize.y / _generateDataSO.VoxelSize)) + 1;
            var pointsCountZ = math.max(1, (int)math.round(_generateDataSO.ChunkSize.z / _generateDataSO.VoxelSize)) + 1;
            var maxVerticesPerChunk = (pointsCountX - 1) * (pointsCountY - 1) * (pointsCountZ - 1) * 15;
            var vertices = new NativeArray<float3>(chunkCount * maxVerticesPerChunk, Allocator.Persistent);
            var triangles = new NativeArray<int>(chunkCount * maxVerticesPerChunk, Allocator.Persistent);
            var vertexCounts = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var triangleCounts = new NativeArray<int>(chunkCount, Allocator.Persistent);

            var job = new ChunkSpawnJobMultiple
            {
                Voxels = flattenedVoxels,
                Corners = _corners,
                EdgeTable = _edgeTable,
                TrianglesTable = _trianglesTable,
                Vertices = vertices,
                Triangles = triangles,
                VertexCountPerChunk = vertexCounts,
                TriangleCountPerChunk = triangleCounts,
                VoxelsPerChunk = voxelsPerChunk,
                MaxVerticesPerChunk = maxVerticesPerChunk,
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

                var chunkStart = chunkIndex * maxVerticesPerChunk;
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