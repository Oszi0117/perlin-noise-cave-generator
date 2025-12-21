using System;
using UnityEngine;

namespace CaveManagement
{
    [Serializable]
    public struct MeshData
    {
        public int ChunkIndex;
        public Vector3[] Vertices;
        public int[] Triangles;
        public Vector3[] Normals;

        public MeshData(int chunkIndex, Vector3[] vertices, int[] triangles, Vector3[] normals)
        {
            ChunkIndex = chunkIndex;
            Vertices = vertices;
            Triangles = triangles;
            Normals = normals;
        }
    }
}