using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace CaveCreation.Jobs
{
    [BurstCompile]
    public struct ChunkSpawnJobMultiple : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> Voxels;
        [ReadOnly] public NativeArray<int3> Corners;
        [ReadOnly] public NativeArray<int> EdgeTable;
        [ReadOnly] public NativeArray<int> TrianglesTable;
        [ReadOnly] public NativeArray<int> VoxelStartIndexPerChunk;
        [ReadOnly] public NativeArray<int> VoxelCountPerChunk;
        [ReadOnly] public NativeArray<int> VertexStartIndexPerChunk;

        [NativeDisableParallelForRestriction] public NativeArray<float3> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<int> Triangles;

        [WriteOnly] public NativeArray<int> VertexCountPerChunk;
        [WriteOnly] public NativeArray<int> TriangleCountPerChunk;

        public float VoxelSize;
        public float IsoLevel;

        public void Execute(int chunkIndex)
        {
            var chunkStart = VoxelStartIndexPerChunk[chunkIndex];
            var voxelsPerChunk = VoxelCountPerChunk[chunkIndex];
            if (voxelsPerChunk <= 0)
            {
                VertexCountPerChunk[chunkIndex] = 0;
                TriangleCountPerChunk[chunkIndex] = 0;
                return;
            }

            var min = Voxels[chunkStart].xyz;
            var max = min;
            for (var i = 1; i < voxelsPerChunk; i++)
            {
                var voxelPosition = Voxels[chunkStart + i].xyz;
                min = math.min(min, voxelPosition);
                max = math.max(max, voxelPosition);
            }

            var minGridBounds = new int3(
                (int)math.round(min.x / VoxelSize),
                (int)math.round(min.y / VoxelSize),
                (int)math.round(min.z / VoxelSize));

            var width = (int)math.round((max.x - min.x) / VoxelSize) + 1;
            var height = (int)math.round((max.y - min.y) / VoxelSize) + 1;
            var depth = (int)math.round((max.z - min.z) / VoxelSize) + 1;

            if (width <= 1 || height <= 1 || depth <= 1)
            {
                VertexCountPerChunk[chunkIndex] = 0;
                TriangleCountPerChunk[chunkIndex] = 0;
                return;
            }

            var field = new NativeArray<float>(width * height * depth, Allocator.Temp);
            for (var i = 0; i < voxelsPerChunk; i++)
            {
                var voxel = Voxels[chunkStart + i];
                var lx = (int)math.round(voxel.x / VoxelSize) - minGridBounds.x;
                var ly = (int)math.round(voxel.y / VoxelSize) - minGridBounds.y;
                var lz = (int)math.round(voxel.z / VoxelSize) - minGridBounds.z;

                if (lx < 0 || lx >= width || ly < 0 || ly >= height || lz < 0 || lz >= depth)
                    continue;

                var fieldIndex = lx + ly * width + lz * width * height;
                field[fieldIndex] = voxel.w;
            }

            var chunkVertexBase = VertexStartIndexPerChunk[chunkIndex];
            var localVertexCount = 0;
            var localTriangleCount = 0;

            for (var x = 0; x < width - 1; x++)
            for (var y = 0; y < height - 1; y++)
            for (var z = 0; z < depth - 1; z++)
                MarchCube(x, y, z, field, width, height, minGridBounds, chunkVertexBase, ref localVertexCount, ref localTriangleCount);

            VertexCountPerChunk[chunkIndex] = localVertexCount;
            TriangleCountPerChunk[chunkIndex] = localTriangleCount;
            field.Dispose();
        }

        private void MarchCube(int x, int y, int z, NativeArray<float> field, int width, int height, int3 minGridBounds,
            int chunkVertexBase, ref int localVertexCount, ref int localTriangleCount)
        {
            var cubeIndex = 0;
            var cubeValuesA = new float4();
            var cubeValuesB = new float4();
            var cornerPositionsA = new float3x4();
            var cornerPositionsB = new float3x4();

            for (var i = 0; i < 8; i++)
            {
                var corner = Corners[i];
                var ix = x + corner.x;
                var iy = y + corner.y;
                var iz = z + corner.z;

                var value = field[ix + iy * width + iz * width * height];
                SetCubeValue(ref cubeValuesA, ref cubeValuesB, i, value);
                SetCornerPosition(ref cornerPositionsA, ref cornerPositionsB, i, new float3(
                    (ix + minGridBounds.x) * VoxelSize,
                    (iy + minGridBounds.y) * VoxelSize,
                    (iz + minGridBounds.z) * VoxelSize));

                if (value < IsoLevel)
                    cubeIndex |= 1 << i;
            }

            var edgeFlags = EdgeTable[cubeIndex];
            if (edgeFlags == 0 || edgeFlags == 255)
                return;

            var edgePositionsA = new float3x4();
            var edgePositionsB = new float3x4();
            var edgePositionsC = new float3x4();
            if ((edgeFlags & 1) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 0, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 0), GetCubeValue(cubeValuesA, cubeValuesB, 0),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 1),
                    GetCubeValue(cubeValuesA, cubeValuesB, 1)));
            if ((edgeFlags & 2) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 1, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 1), GetCubeValue(cubeValuesA, cubeValuesB, 1),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 2),
                    GetCubeValue(cubeValuesA, cubeValuesB, 2)));
            if ((edgeFlags & 4) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 2, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 2), GetCubeValue(cubeValuesA, cubeValuesB, 2),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 3),
                    GetCubeValue(cubeValuesA, cubeValuesB, 3)));
            if ((edgeFlags & 8) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 3, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 3), GetCubeValue(cubeValuesA, cubeValuesB, 3),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 0),
                    GetCubeValue(cubeValuesA, cubeValuesB, 0)));
            if ((edgeFlags & 16) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 4, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 4), GetCubeValue(cubeValuesA, cubeValuesB, 4),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 5),
                    GetCubeValue(cubeValuesA, cubeValuesB, 5)));
            if ((edgeFlags & 32) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 5, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 5), GetCubeValue(cubeValuesA, cubeValuesB, 5),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 6),
                    GetCubeValue(cubeValuesA, cubeValuesB, 6)));
            if ((edgeFlags & 64) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 6, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 6), GetCubeValue(cubeValuesA, cubeValuesB, 6),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 7),
                    GetCubeValue(cubeValuesA, cubeValuesB, 7)));
            if ((edgeFlags & 128) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 7, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 7), GetCubeValue(cubeValuesA, cubeValuesB, 7),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 4),
                    GetCubeValue(cubeValuesA, cubeValuesB, 4)));
            if ((edgeFlags & 256) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 8, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 0), GetCubeValue(cubeValuesA, cubeValuesB, 0),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 4),
                    GetCubeValue(cubeValuesA, cubeValuesB, 4)));
            if ((edgeFlags & 512) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 9, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 1), GetCubeValue(cubeValuesA, cubeValuesB, 1),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 5),
                    GetCubeValue(cubeValuesA, cubeValuesB, 5)));
            if ((edgeFlags & 1024) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 10, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 2), GetCubeValue(cubeValuesA, cubeValuesB, 2),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 6),
                    GetCubeValue(cubeValuesA, cubeValuesB, 6)));
            if ((edgeFlags & 2048) != 0)
                SetEdgePosition(ref edgePositionsA, ref edgePositionsB, ref edgePositionsC, 11, Interpolate(
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 3), GetCubeValue(cubeValuesA, cubeValuesB, 3),
                    GetCornerPosition(cornerPositionsA, cornerPositionsB, 7),
                    GetCubeValue(cubeValuesA, cubeValuesB, 7)));

            for (var i = 0; TrianglesTable[cubeIndex * 16 + i] != -1; i += 3)
            {
                var vertexStart = localVertexCount;
                Vertices[chunkVertexBase + vertexStart] = GetEdgePosition(edgePositionsA, edgePositionsB,
                    edgePositionsC,
                    TrianglesTable[cubeIndex * 16 + i]);
                Vertices[chunkVertexBase + vertexStart + 1] = GetEdgePosition(edgePositionsA, edgePositionsB,
                    edgePositionsC, TrianglesTable[cubeIndex * 16 + i + 1]);
                Vertices[chunkVertexBase + vertexStart + 2] = GetEdgePosition(edgePositionsA, edgePositionsB,
                    edgePositionsC, TrianglesTable[cubeIndex * 16 + i + 2]);

                Triangles[chunkVertexBase + localTriangleCount] = vertexStart;
                Triangles[chunkVertexBase + localTriangleCount + 1] = vertexStart + 1;
                Triangles[chunkVertexBase + localTriangleCount + 2] = vertexStart + 2;

                localVertexCount += 3;
                localTriangleCount += 3;
            }
        }

        private float3 Interpolate(float3 p1, float v1, float3 p2, float v2)
        {
            if (math.abs(v1 - v2) < float.Epsilon)
                return (p1 + p2) * 0.5f;

            var t = (IsoLevel - v1) / (v2 - v1);
            return p1 + t * (p2 - p1);
        }

        private static void SetCubeValue(ref float4 first, ref float4 second, int index, float value)
        {
            if (index < 4)
                first[index] = value;
            else
                second[index - 4] = value;
        }

        private static float GetCubeValue(float4 first, float4 second, int index)
            => index < 4 ? first[index] : second[index - 4];

        private static void SetCornerPosition(ref float3x4 first, ref float3x4 second, int index, float3 value)
        {
            if (index < 4)
                first[index] = value;
            else
                second[index - 4] = value;
        }

        private static float3 GetCornerPosition(float3x4 first, float3x4 second, int index)
            => index < 4 ? first[index] : second[index - 4];

        private static void SetEdgePosition(ref float3x4 first, ref float3x4 second, ref float3x4 third, int index, float3 value)
        {
            if (index < 4)
                first[index] = value;
            else if (index < 8)
                second[index - 4] = value;
            else
                third[index - 8] = value;
        }

        private static float3 GetEdgePosition(float3x4 first, float3x4 second, float3x4 third, int index)
        {
            if (index < 4)
                return first[index];
            
            return index < 8 ? second[index - 4] : third[index - 8];
        }
    }
}
