using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Utils;

namespace CaveManagement.Jobs
{
    public struct MarchingCubesJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Occupancy;
        public int3 GridSize;
        public float3 BoundsMin;
        public float VoxelSize;
        public float IsoLevel;

        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;

        public void Execute()
        {
            var cubeCountX = math.max(0, GridSize.x - 1);
            var cubeCountY = math.max(0, GridSize.y - 1);
            var cubeCountZ = math.max(0, GridSize.z - 1);

            for (var z = 0; z < cubeCountZ; z++)
            for (var y = 0; y < cubeCountY; y++)
            for (var x = 0; x < cubeCountX; x++)
            {
                var cubeIndex = 0;
                var cubeCorners = new FixedList128Bytes<float3> { Length = 8 };
                var cubeValues = new FixedList64Bytes<float> { Length = 8 };

                for (var corner = 0; corner < 8; corner++)
                {
                    var offset = MarchingCubesUtils.CornerOffsets[corner];
                    var cornerIndex = MarchingCubesUtils.Index(x + offset.x, y + offset.y, z + offset.z, GridSize);
                    cubeValues[corner] = Occupancy[cornerIndex];
                    cubeCorners[corner] =
                        BoundsMin + (new float3(x + offset.x, y + offset.y, z + offset.z) + 0.5f) * VoxelSize;

                    if (cubeValues[corner] > IsoLevel)
                        cubeIndex |= 1 << corner;
                }

                var edgeFlags = MarchingCubesUtils.EdgeTable[cubeIndex];
                if (edgeFlags == 0)
                    continue;

                var vertexList = new NativeArray<float3>(length: 12, Allocator.Temp);

                for (var edge = 0; edge < 12; edge++)
                {
                    if ((edgeFlags & (1 << edge)) == 0) continue;

                    var connection = MarchingCubesUtils.EdgeConnection[edge];
                    var a = connection.x;
                    var b = connection.y;
                    vertexList[edge] = MarchingCubesUtils.VertexInterp(IsoLevel, cubeCorners[a], cubeCorners[b],
                        cubeValues[a], cubeValues[b]);
                }

                for (var triangle = 0; MarchingCubesUtils.TriangleTable[cubeIndex, triangle] != -1; triangle += 3)
                {
                    for (var localVertex = 0; localVertex < 3; localVertex++)
                    {
                        var vertexIndex = MarchingCubesUtils.TriangleTable[cubeIndex, triangle + localVertex];
                        Triangles.Add(Vertices.Length);
                        Vertices.Add(vertexList[vertexIndex]);
                    }
                }
            }
        }
    }
}