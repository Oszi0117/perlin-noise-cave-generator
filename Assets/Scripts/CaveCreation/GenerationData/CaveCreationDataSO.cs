using UnityEngine;

namespace CaveCreation.GenerationData
{
    [CreateAssetMenu(fileName = "GenerationDataSO", menuName = "Generation Data")]
    public class CaveCreationDataSO : ScriptableObject
    {
        public Vector3Int GridSize = Vector3Int.one;
        public Vector3 CaveOrigin;
        public GameObject ChunkPrefab;
        public float VoxelSize = 1f;
        public Vector3 ChunkSize = new(50, 30, 50);
        public Vector3 NoiseScale = new(0.08f, 0.08f, 0.08f);
        public int Seed = 2137;
        [Min(1)] public int Octaves = 4;
        [Min(1f)] public float Lacunarity = 2f;
        [Range(0f, 1f)] public float Persistence = 0.5f;
        public float IsoLevel = 0.5f;

        public static CaveCreationDataSO CreateRandomizedInstance(CaveCreationDataSO source)
        {
            if (source == null)
            {
                Debug.LogError($"{nameof(CreateRandomizedInstance)} failed: source is null");
                return null;
            }

            var instance = Instantiate(source);

            instance.NoiseScale = new Vector3(
                Random.Range(0.001f, 0.1f),
                Random.Range(0.001f, 0.1f),
                Random.Range(0.001f, 0.1f)
            );
            instance.Seed = Random.Range(int.MinValue, int.MaxValue);
            instance.Octaves = Random.Range(1, 33);
            instance.Lacunarity = Random.Range(1f, 16f);
            instance.Persistence = Random.Range(0f, 1f);
            instance.IsoLevel = Random.Range(0.1f, 1f);

            return instance;
        }
    }
}