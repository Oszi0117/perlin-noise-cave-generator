using UnityEngine;

namespace CaveManagement.ChunkGenerationData
{
    [CreateAssetMenu(fileName = "ChunkGenerationDataSO", menuName = "Chunk Generation Data")]
    public class ChunkGenerationDataSO : ScriptableObject
    {
         public Vector3 ChunkOrigin;
         public float VoxelSize = 1f;
         public Vector3 ChunkSize = new(50, 30, 50);
         [Range(0f, 1f)] public float Threshold = 0.55f;
         public Vector3 NoiseScale = new(0.08f, 0.08f, 0.08f);
         public int Seed = 2137;
         [Min(1)] public int Octaves = 4;
         [Min(1f)] public float Lacunarity = 2f;
         [Range(0f, 1f)] public float Persistence = 0.5f;
         public Vector3 Offset = Vector3.zero;
         public int MaxSpawnDestroyOperationsPerFrame = 100;
    }
}
