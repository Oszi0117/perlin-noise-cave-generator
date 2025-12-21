using System;
using System.Collections.Generic;
using CaveManagement.ChunkGenerationData;
using CaveManagement.Jobs;
using Cysharp.Threading.Tasks;
using RuntimeData;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CaveManagement
{
    [Serializable]
    public class ChunkGenerator
    {
        private ChunkGenerationDataSO _generateDataSO;

        public void Init(ChunkGenerationDataSO generateDataSO)
        {
            _generateDataSO = generateDataSO;
        }

        public async UniTask GenerateCave()
        {
            var generationBounds = new Bounds(_generateDataSO.ChunkOrigin, _generateDataSO.ChunkSize);
            var boundsMin = new float3(generationBounds.min.x, generationBounds.min.y, generationBounds.min.z);
            var boundsMax = new float3(generationBounds.max.x, generationBounds.max.y, generationBounds.max.z);
            
            var data = new SingleChunkGenerateData(
                boundsMin: boundsMin,
                boundsMax: boundsMax,
                voxelSize: _generateDataSO.VoxelSize,
                threshold: _generateDataSO.Threshold,
                noiseScale: _generateDataSO.NoiseScale,
                seed: _generateDataSO.Seed,
                octaves: _generateDataSO.Octaves,
                lacunarity: _generateDataSO.Lacunarity,
                persistence: _generateDataSO.Persistence,
                offset: _generateDataSO.Offset
            );
            var job = new ChunkGenerateJobSingle(data);
            var handle = job.Schedule();
            
            await UniTask.WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            var voxelsNative = job.Result;
            var count = voxelsNative.Length;
            var voxelsManaged = new VoxelData[count];
            
            for (int i = 0; i < count; i++)
            {
                var p = voxelsNative[i];
                voxelsManaged[i] = new VoxelData(p);
            }
            voxelsNative.Dispose();
            
            var chunkSize = boundsMax - boundsMin;
            var cellCountX = math.max(1, (int)math.ceil(chunkSize.x / _generateDataSO.VoxelSize));
            var cellCountY = math.max(1, (int)math.ceil(chunkSize.y / _generateDataSO.VoxelSize));
            var cellCountZ = math.max(1, (int)math.ceil(chunkSize.z / _generateDataSO.VoxelSize));
            var gridSize = new int3(cellCountX, cellCountY, cellCountZ);
            
            var occupancy = new NativeArray<byte>(cellCountX * cellCountY * cellCountZ, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            for (var i = 0; i < voxelsManaged.Length; i++)
            {
                var index = GridIndex(voxelsManaged[i].Position, boundsMin, _generateDataSO.VoxelSize, gridSize);
                if (index >= 0 && index < occupancy.Length)
                    occupancy[index] = 1;
            }

            var marchingJob = new MarchingCubesJob
            {
                Occupancy = occupancy,
                GridSize = gridSize,
                BoundsMin = boundsMin,
                VoxelSize = _generateDataSO.VoxelSize,
                IsoLevel = 0.5f,
                Vertices = new NativeList<float3>(Allocator.TempJob),
                Triangles = new NativeList<int>(Allocator.TempJob)
            };

            var marchingHandle = marchingJob.Schedule();
            await UniTask.WaitUntil(() => marchingHandle.IsCompleted);
            marchingHandle.Complete();

            var meshData = await UniTask.RunOnThreadPool(() =>
            {
                var rawVertices = marchingJob.Vertices.ToArray(Allocator.TempJob);
                var rawTriangles = marchingJob.Triangles.ToArray(Allocator.TempJob);
                try
                {
                    return WeldMesh(0, rawVertices, rawTriangles, _generateDataSO.VoxelSize, boundsMin);
                }
                finally
                {
                    rawVertices.Dispose();
                    rawTriangles.Dispose();
                }
            });

            marchingJob.Vertices.Dispose();
            marchingJob.Triangles.Dispose();
            occupancy.Dispose();

            var chunks = new List<ChunkData>
            {
                new(
                    index: 0,
                    voxels: voxelsManaged,
                    boundsMin: boundsMin,
                    boundsMax: boundsMax,
                    voxelSize: _generateDataSO.VoxelSize,
                    gridSize: gridSize)
            };

            CaveRuntimeData.Instance.Chunks = chunks;
            CaveRuntimeData.Instance.Meshes = new List<MeshData> { meshData };
        }
        
        private static int GridIndex(float3 position, float3 boundsMin, float voxelSize, int3 gridSize)
        {
            var relative = (position - boundsMin - new float3(voxelSize * 0.5f)) / voxelSize;
            var x = (int)math.round(relative.x);
            var y = (int)math.round(relative.y);
            var z = (int)math.round(relative.z);

            if (x < 0 || y < 0 || z < 0 || x >= gridSize.x || y >= gridSize.y || z >= gridSize.z)
                return -1;

            return (z * gridSize.y + y) * gridSize.x + x;
        }

        private static MeshData WeldMesh(int chunkIndex, NativeArray<float3> vertices, NativeArray<int> triangles, float voxelSize, float3 boundsMin)
        {
            var weldedVertices = new List<Vector3>();
            var weldedNormals = new List<Vector3>();
            var weldedIndices = new List<int>(triangles.Length);
            var vertexLookup = new Dictionary<int3, int>(vertices.Length, new Int3Comparer());
            var rawToWelded = new int[vertices.Length];
            var quantizationScale = math.max(voxelSize * 0.01f, 1e-4f);

            for (var i = 0; i < vertices.Length; i++)
            {
                var key = Quantize(vertices[i], boundsMin, quantizationScale);
                if (!vertexLookup.TryGetValue(key, out var index))
                {
                    index = weldedVertices.Count;
                    vertexLookup[key] = index;
                    weldedVertices.Add(vertices[i]);
                    weldedNormals.Add(Vector3.zero);
                }

                rawToWelded[i] = index;
            }

            for (var i = 0; i < triangles.Length; i++)
            {
                var weldedIndex = rawToWelded[triangles[i]];
                weldedIndices.Add(weldedIndex);
            }

            for (var i = 0; i < weldedIndices.Count; i += 3)
            {
                var a = weldedIndices[i];
                var b = weldedIndices[i + 1];
                var c = weldedIndices[i + 2];

                var normal = Vector3.Cross(
                    weldedVertices[b] - weldedVertices[a],
                    weldedVertices[c] - weldedVertices[a]);

                weldedNormals[a] += normal;
                weldedNormals[b] += normal;
                weldedNormals[c] += normal;
            }

            for (var i = 0; i < weldedNormals.Count; i++)
            {
                weldedNormals[i] = weldedNormals[i].normalized;
            }

            return new MeshData(chunkIndex, weldedVertices.ToArray(), weldedIndices.ToArray(), weldedNormals.ToArray());
        }

        private static int3 Quantize(float3 position, float3 boundsMin, float quantizationScale)
        {
            var relative = (position - boundsMin) / quantizationScale;
            return new int3(
                (int)math.round(relative.x),
                (int)math.round(relative.y),
                (int)math.round(relative.z));
        }

        private sealed class Int3Comparer : IEqualityComparer<int3>
        {
            public bool Equals(int3 x, int3 y)
            {
                return x.x == y.x && x.y == y.y && x.z == y.z;
            }

            public int GetHashCode(int3 obj)
            {
                unchecked
                {
                    var hash = obj.x;
                    hash = (hash * 397) ^ obj.y;
                    hash = (hash * 397) ^ obj.z;
                    return hash;
                }
            }
        }
    }
}