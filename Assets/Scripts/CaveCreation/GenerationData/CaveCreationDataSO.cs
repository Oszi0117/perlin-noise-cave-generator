using UnityEngine;
using System;

namespace CaveCreation.GenerationData
{
    [CreateAssetMenu(fileName = "GenerationDataSO", menuName = "Generation Data")]
    public class CaveCreationDataSO : ScriptableObject
    {
        public Vector3 CaveOrigin;
        [Tooltip("World-space bounds of the full cave system. Example: 300x100x600 creates a long cave volume.")]
        [Min(1f)] public Vector3 CaveSize = new(300, 100, 600);
        public GameObject ChunkPrefab;
        public float VoxelSize = 1f;
        public Vector3 ChunkSize = new(50, 30, 50);
        public Vector3 NoiseScale = new(0.08f, 0.08f, 0.08f);
        public int Seed = 2137;
        [Min(1)] public int Octaves = 4;
        [Min(1f)] public float Lacunarity = 2f;
        [Range(0f, 1f)] public float Persistence = 0.5f;
        public float IsoLevel = 0.5f;

        [Header("Global SDF")]
        [Tooltip("Width of the sampled SDF transition around cave walls in voxels. Example: 2 keeps the surface crisp.")]
        [Min(0.1f)] public float SdfSurfaceThicknessInVoxels = 2f;
        [Tooltip("Smooth union radius used when SDF rooms/tunnels overlap. Example: 6 blends intersections softly.")]
        [Min(0f)] public float SdfSmoothUnionInVoxels = 6f;
        [Tooltip("How much world-space noise pushes cave walls in voxels. Example: 12 creates visible rocky bulges.")]
        [Min(0f)] public float SdfWallNoiseAmplitudeInVoxels = 12f;
        [Tooltip("Scale of broad wall noise in voxels. Example: 10 creates medium rock waves, 20 is smoother.")]
        [Min(0.1f)] public float SdfWallNoiseScaleInVoxels = 10f;
        [Tooltip("Strength of broad room wall lobes that create recesses and tabs. Example: 0.8 is subtle, 1.6 is chunky.")]
        [Range(0f, 3f)] public float SdfWallLobeStrength = 1.35f;
        [Tooltip("Scale multiplier for broad room wall lobes. Example: 3 makes larger recesses than the base wall scale.")]
        [Min(0.1f)] public float SdfWallLobeScaleMultiplier = 3f;
        [Tooltip("Strength of tunnel wall lobes. Example: 0.7 adds rough tunnels without pinching them too much.")]
        [Range(0f, 3f)] public float SdfTunnelLobeStrength = 0.8f;
        [Tooltip("Scale multiplier for broad tunnel wall lobes. Example: 2.5 makes long uneven tunnel walls.")]
        [Min(0.1f)] public float SdfTunnelLobeScaleMultiplier = 2.5f;

        [Header("Interior Noise Structures")]
        [Tooltip("How strongly Perlin/fBm noise creates solid structures inside rooms. Example: 1 restores visible internal rocks, 0 disables them.")]
        [Range(0f, 1f)] public float SdfInteriorNoiseStrength = 1f;
        [Tooltip("Noise cutoff for internal solid structures. Example: 0.42 is moderate, 0.55 creates more rocks.")]
        [Range(0f, 1f)] public float SdfInteriorNoiseCutoff = 0.42f;
        [Tooltip("Distance kept clear from outer room/tunnel walls before internal structures can appear. Example: 8 protects cave walls.")]
        [Min(0f)] public float SdfInteriorWallClearanceInVoxels = 8f;
        [Tooltip("Extra keep-clear radius around tunnel centerlines beyond TunnelRadius. Example: 8 prevents noise from blocking passages.")]
        [Min(0f)] public float SdfInteriorTunnelClearanceInVoxels = 8f;
        [Tooltip("Blend distance used when internal structures fade in near protected walls/tunnels. Example: 4 avoids sharp cutoffs.")]
        [Min(0.1f)] public float SdfInteriorClearanceBlendInVoxels = 4f;

        [Header("Room Features")]
        [Tooltip("Small SDF room features placed inside CaveSize. Example: 8 rooms sized 25-45.")]
        public RoomChunkTypeSettings SmallRoomChunks = new(8, new Vector2(25f, 45f));
        [Tooltip("Medium SDF room features placed inside CaveSize. Example: 5 rooms sized 50-80.")]
        public RoomChunkTypeSettings MediumRoomChunks = new(5, new Vector2(50f, 80f));
        [Tooltip("Large SDF room features placed inside CaveSize. Example: 2 rooms sized 90-140.")]
        public RoomChunkTypeSettings LargeRoomChunks = new(2, new Vector2(90f, 140f));
        [Tooltip("Random candidates tested for each room feature. Example: 16 spreads rooms apart better than 1.")]
        [Min(1)] public int RoomChunkPlacementAttempts = 16;
        [Tooltip("Vertical scale applied to room feature size. Example: 0.6-1.1 creates flatter or taller rooms.")]
        public Vector2 RoomChunkHeightScaleRange = new(0.6f, 1.1f);

        [Header("Optional SDF Tunnels")]
        [Tooltip("Connect placed room features with SDF capsule tunnels. Example: enabled creates one cave system.")]
        public bool ConnectRoomsWithSdfTunnels = true;
        [Tooltip("Tunnel radius in world units. Example: 16 creates roughly 32-unit-wide passages.")]
        [Min(0.1f)] public float TunnelRadius = 16f;
        [Tooltip("Maximum length of one curved tunnel SDF segment. Example: 70 creates smoother curves.")]
        [Min(1f)] public float TunnelSegmentLength = 70f;
        [Tooltip("Maximum sideways curve offset in world units. Example: 35 bends tunnels without huge detours.")]
        [Min(0f)] public float TunnelCurveAmplitude = 35f;
        [Tooltip("Distance between generated tunnel bend points. Example: 80 creates more bends than 160.")]
        [Min(1f)] public float TunnelCurvePointSpacing = 80f;

        public static CaveCreationDataSO CreateRandomizedInstance()
        {
            var instance = CreateInstance<CaveCreationDataSO>();

            instance.ChunkPrefab = Resources.Load<GameObject>("ChunkPrefab");
            instance.NoiseScale = new Vector3(
                UnityEngine.Random.Range(0.001f, 0.1f),
                UnityEngine.Random.Range(0.001f, 0.1f),
                UnityEngine.Random.Range(0.001f, 0.1f)
            );
            instance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            instance.Octaves = UnityEngine.Random.Range(1, 33);
            instance.Lacunarity = UnityEngine.Random.Range(1f, 16f);
            instance.Persistence = UnityEngine.Random.Range(0f, 1f);
            instance.IsoLevel = UnityEngine.Random.Range(0.1f, 1f);
            instance.SmallRoomChunks.Amount = UnityEngine.Random.Range(4, 10);
            instance.MediumRoomChunks.Amount = UnityEngine.Random.Range(2, 7);
            instance.LargeRoomChunks.Amount = UnityEngine.Random.Range(1, 4);

            return instance;
        }
    }

    [Serializable]
    public struct RoomChunkTypeSettings
    {
        [Min(0)] public int Amount;
        public Vector2 SizeRange;

        public RoomChunkTypeSettings(int amount, Vector2 sizeRange)
        {
            Amount = amount;
            SizeRange = sizeRange;
        }
    }
}
