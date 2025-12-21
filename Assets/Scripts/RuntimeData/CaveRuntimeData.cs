using System.Collections.Generic;
using CaveManagement;

namespace RuntimeData
{
    public class CaveRuntimeData : RuntimeData<CaveRuntimeData>
    {
        public List<ChunkData> Chunks;
        public List<MeshData> Meshes;
    }
}
