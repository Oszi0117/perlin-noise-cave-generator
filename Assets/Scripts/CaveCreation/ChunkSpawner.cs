using System.Collections.Generic;
using CaveCreation.Data;
using CaveCreation.GenerationData;
using UnityEngine;
using Utils;

namespace CaveCreation
{
    public class ChunkSpawner
    {
        private CaveCreationDataSO _generateDataSO;
        private MeshFilter _meshFilter;
        private float _isoLevel => _generateDataSO.IsoLevel;
        private MarchingHelperData _helperData;

        public void Init(CaveCreationDataSO generateDataSO, MeshFilter meshFilter)
        {
            _generateDataSO = generateDataSO;
            _meshFilter = meshFilter;
            _helperData = new MarchingHelperData();
        }

        public void SpawnChunk(VoxelData[] data)
        {
            var field = MarchingCubesUtils.PrepareScalarField(
                data,
                _generateDataSO.VoxelSize,
                out _helperData.Width,
                out _helperData.Height,
                out _helperData.Depth,
                out _helperData.MinGridBounds);

            if (field.Length == 0)
                return;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int x = 0; x < _helperData.Width - 1; x++)
                for (int y = 0; y < _helperData.Height - 1; y++)
                    for (int z = 0; z < _helperData.Depth - 1; z++)
                        MarchCube(x, y, z, field, _generateDataSO.VoxelSize, vertices, triangles);
            
            UpdateMesh(vertices, triangles);
        }

        private void MarchCube(int x, int y, int z, float[] field, float cubeSize, List<Vector3> verts, List<int> tris)
        {
            var cubeIndex = 0;
            var v = new float[8];
            var p = new Vector3[8];

            for (int i = 0; i < 8; i++)
            {
                var ix = x + MarchingCubesUtils.Tables.Corners[i].x;
                var iy = y + MarchingCubesUtils.Tables.Corners[i].y;
                var iz = z + MarchingCubesUtils.Tables.Corners[i].z;

                v[i] = field[ix + iy * _helperData.Width + iz * _helperData.Width * _helperData.Height];

                p[i] = new Vector3(
                    (ix + _helperData.MinGridBounds.x) * cubeSize,
                    (iy + _helperData.MinGridBounds.y) * cubeSize,
                    (iz + _helperData.MinGridBounds.z) * cubeSize
                );

                if (v[i] < _isoLevel)
                    cubeIndex |= (1 << i);
            }

            var edgeFlags = MarchingCubesUtils.Tables.EdgeTable[cubeIndex];
            if (edgeFlags == 0 || edgeFlags == 255)
                return;

            Vector3[] edgePositions = new Vector3[12];
            if ((edgeFlags & 1) != 0)
                edgePositions[0] = MarchingCubesUtils.Interpolate(p[0], v[0], p[1], v[1], _isoLevel);
            if ((edgeFlags & 2) != 0)
                edgePositions[1] = MarchingCubesUtils.Interpolate(p[1], v[1], p[2], v[2], _isoLevel);
            if ((edgeFlags & 4) != 0)
                edgePositions[2] = MarchingCubesUtils.Interpolate(p[2], v[2], p[3], v[3], _isoLevel);
            if ((edgeFlags & 8) != 0)
                edgePositions[3] = MarchingCubesUtils.Interpolate(p[3], v[3], p[0], v[0], _isoLevel);
            if ((edgeFlags & 16) != 0)
                edgePositions[4] = MarchingCubesUtils.Interpolate(p[4], v[4], p[5], v[5], _isoLevel);
            if ((edgeFlags & 32) != 0)
                edgePositions[5] = MarchingCubesUtils.Interpolate(p[5], v[5], p[6], v[6], _isoLevel);
            if ((edgeFlags & 64) != 0)
                edgePositions[6] = MarchingCubesUtils.Interpolate(p[6], v[6], p[7], v[7], _isoLevel);
            if ((edgeFlags & 128) != 0)
                edgePositions[7] = MarchingCubesUtils.Interpolate(p[7], v[7], p[4], v[4], _isoLevel);
            if ((edgeFlags & 256) != 0)
                edgePositions[8] = MarchingCubesUtils.Interpolate(p[0], v[0], p[4], v[4], _isoLevel);
            if ((edgeFlags & 512) != 0)
                edgePositions[9] = MarchingCubesUtils.Interpolate(p[1], v[1], p[5], v[5], _isoLevel);
            if ((edgeFlags & 1024) != 0)
                edgePositions[10] = MarchingCubesUtils.Interpolate(p[2], v[2], p[6], v[6], _isoLevel);
            if ((edgeFlags & 2048) != 0)
                edgePositions[11] = MarchingCubesUtils.Interpolate(p[3], v[3], p[7], v[7], _isoLevel);

            for (int i = 0; MarchingCubesUtils.Tables.TrianglesTable[cubeIndex * 16 + i] != -1; i += 3)
            {
                var vCount = verts.Count;
                verts.Add(edgePositions[MarchingCubesUtils.Tables.TrianglesTable[cubeIndex * 16 + i]]);
                verts.Add(edgePositions[MarchingCubesUtils.Tables.TrianglesTable[cubeIndex * 16 + i + 1]]);
                verts.Add(edgePositions[MarchingCubesUtils.Tables.TrianglesTable[cubeIndex * 16 + i + 2]]);

                tris.Add(vCount);
                tris.Add(vCount + 1);
                tris.Add(vCount + 2);
            }
        }

        private void UpdateMesh(List<Vector3> verts, List<int> tris)
        {
            if (verts == null || verts.Count == 0)
                return;

            var mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _meshFilter.sharedMesh = mesh;
        }

        private struct MarchingHelperData
        {
            public int Width;
            public int Height;
            public int Depth;
            public Vector3Int MinGridBounds;
        }
    }
}