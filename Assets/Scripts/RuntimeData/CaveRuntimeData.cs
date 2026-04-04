using System.Collections.Generic;
using CaveCreation.Data;
using UnityEngine;

namespace RuntimeData
{
    public class CaveRuntimeData : RuntimeData<CaveRuntimeData>
    {
        public GameObject CaveParent;
        public List<ChunkData> Chunks;
        public List<GameObject> ChunkObjects;
    }
}
