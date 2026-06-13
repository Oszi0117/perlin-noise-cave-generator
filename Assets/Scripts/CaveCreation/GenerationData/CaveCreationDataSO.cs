using UnityEngine;
using System;

namespace CaveCreation.GenerationData
{
    [CreateAssetMenu(fileName = "GenerationDataSO", menuName = "Generation Data")]
    public class CaveCreationDataSO : ScriptableObject
    {
        [HideInInspector]
        public Vector3Int GridSize = Vector3Int.one;
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

        [Header("Room Chunks")]
        [Tooltip("Small room chunks placed inside CaveSize. Example: 8 chunks sized 25-45.")]
        public RoomChunkTypeSettings SmallRoomChunks = new(8, new Vector2(25f, 45f));
        [Tooltip("Medium room chunks placed inside CaveSize. Example: 5 chunks sized 50-80.")]
        public RoomChunkTypeSettings MediumRoomChunks = new(5, new Vector2(50f, 80f));
        [Tooltip("Large room chunks placed inside CaveSize. Example: 2 chunks sized 90-140.")]
        public RoomChunkTypeSettings LargeRoomChunks = new(2, new Vector2(90f, 140f));
        [Tooltip("Random candidates tested for each room chunk. Example: 16 spreads chunks apart better than 1.")]
        [Min(1)] public int RoomChunkPlacementAttempts = 16;
        [Tooltip("Vertical scale applied to room chunk size. Example: 0.6-1.1 creates flatter or taller chunks.")]
        public Vector2 RoomChunkHeightScaleRange = new(0.6f, 1.1f);

        [Header("Closure")]
        [Tooltip("How many voxels open chunk sides extend outward for rounded caps. Example: 8 gives room for a visible sphere-like end.")]
        [Min(0f)] public float ClosureExtensionInVoxels = 8f;
        [Tooltip("How far closure influence blends inward from the boundary. Example: 6 makes a smoother transition into the cave.")]
        [Min(0f)] public float ClosureWallSmoothingDistanceInVoxels = 4.5f;
        [Tooltip("How far boundary detail noise samples are offset outward. Example: 1.5 keeps wall noise aligned with the cap direction.")]
        [Min(0f)] public float ClosureWallNoiseExtensionInVoxels = 1.5f;
        [Tooltip("Extra noise octaves used only on closure walls. Example: 0 is smoother, 2 is more detailed.")]
        [Min(0)] public int ClosureWallDetailOctavesOffset = 1;
        [Tooltip("How much closure walls use their extra detail noise. Example: 0.1 is smooth, 0.5 is rougher.")]
        [Range(0f, 1f)] public float ClosureWallNoiseBlend = 0.3f;
        [Tooltip("Overall strength of the closure wall override. Example: 0.7 is softer, 1.2 seals harder.")]
        [Range(0f, 2f)] public float ClosureWallStrength = 0.95f;
        [Tooltip("Thickness of the spherical closure blend band in voxels. Example: 12 creates a broader, smoother cap.")]
        [Min(0f)] public float ClosureBandInVoxels = 8f;
        [Tooltip("Minimum normalized cap thickness relative to cave size. Example: 0.08 prevents huge caves from getting razor-thin caps.")]
        [Range(0.001f, 1f)] public float ClosureMinNormalizedBand = 0.08f;
        [Tooltip("Maximum normalized cap thickness relative to cave size. Example: 0.35 prevents small caves from becoming all cap.")]
        [Range(0.001f, 1f)] public float ClosureMaxNormalizedBand = 0.35f;
        [Tooltip("How sharply the cap becomes solid near the outside. Example: 1 is soft, 2 is harder.")]
        [Min(0f)] public float ClosureHardening = 1.35f;
        [Tooltip("Scale of large rock bulges on closure walls in voxels. Example: 16 gives smoother broad lumps.")]
        [Min(0f)] public float ClosureRockLumpSizeInVoxels = 7f;
        [Tooltip("Scale of small stone detail on closure walls in voxels. Example: 10 is smoother, 3 is sharper.")]
        [Min(0f)] public float ClosureRockDetailSizeInVoxels = 2.75f;
        [Tooltip("How far rock noise pushes the spherical cap radius. Example: 0.2 is subtle, 0.8 is bumpy.")]
        [Range(0f, 2f)] public float ClosureRockRadiusAmplitude = 0.55f;
        [Tooltip("Extra density variation added around rock protrusions. Example: 0.02 is mild, 0.15 is craggy.")]
        [Range(0f, 1f)] public float ClosureRockDensityVariation = 0.08f;
        [Tooltip("Lowest density forced at the sealed outside edge. Example: 0.01 closes the mesh while keeping interpolation possible.")]
        [Range(0f, 1f)] public float ClosureHardSealValue = 0.01f;

        public BoundaryClosureSettings GetClosureSettings()
        {
            return new BoundaryClosureSettings(
                ClosureWallDetailOctavesOffset,
                ClosureWallSmoothingDistanceInVoxels,
                ClosureWallNoiseExtensionInVoxels,
                ClosureWallNoiseBlend,
                ClosureWallStrength,
                ClosureBandInVoxels,
                ClosureMinNormalizedBand,
                ClosureMaxNormalizedBand,
                ClosureHardening,
                ClosureRockLumpSizeInVoxels,
                ClosureRockDetailSizeInVoxels,
                ClosureRockRadiusAmplitude,
                ClosureRockDensityVariation,
                ClosureHardSealValue);
        }

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
    
    public readonly struct BoundaryClosureSettings
    {
        // Extra closure-only noise octaves. Example: 0 keeps caps smooth, 2 adds fine detail.
        public readonly int WallDetailOctavesOffset;
        // Inward boundary blend distance in voxels. Example: 6 softens the cap-to-cave transition.
        public readonly float WallSmoothingDistanceInVoxels;
        // Outward offset for sampling boundary wall noise. Example: 1.5 follows the open-face direction.
        public readonly float WallNoiseExtensionInVoxels;
        // Blend amount between base cave noise and closure detail noise. Example: 0.1 is smooth.
        public readonly float WallNoiseBlend;
        // Multiplier for how strongly closure density overrides base noise. Example: 1 seals firmly.
        public readonly float WallStrength;
        // Spherical cap transition thickness in voxels. Example: 12 gives a broad rounded cap.
        public readonly float ClosureBandInVoxels;
        // Lower clamp for normalized cap thickness. Example: 0.08 avoids thin caps on large caves.
        public readonly float ClosureMinNormalizedBand;
        // Upper clamp for normalized cap thickness. Example: 0.35 avoids over-thick caps on small caves.
        public readonly float ClosureMaxNormalizedBand;
        // Exponent controlling how quickly the cap becomes solid. Example: 2 creates a harder shell.
        public readonly float ClosureHardening;
        // World-noise scale for large rock bulges in voxels. Example: 16 creates smoother lumps.
        public readonly float RockLumpSizeInVoxels;
        // World-noise scale for small stone detail in voxels. Example: 3 creates sharper texture.
        public readonly float RockDetailSizeInVoxels;
        // Strength of radius displacement from rock noise. Example: 0.2 is subtle, 0.8 is rough.
        public readonly float RockRadiusAmplitude;
        // Extra density variation on rock protrusions. Example: 0.02 is mild, 0.15 is craggy.
        public readonly float RockDensityVariation;
        // Minimum outside-edge density used to force closure. Example: 0.01 closes without crushing detail.
        public readonly float HardSealValue;

        public BoundaryClosureSettings(
            int wallDetailOctavesOffset,
            float wallSmoothingDistanceInVoxels,
            float wallNoiseExtensionInVoxels,
            float wallNoiseBlend,
            float wallStrength,
            float closureBandInVoxels,
            float closureMinNormalizedBand,
            float closureMaxNormalizedBand,
            float closureHardening,
            float rockLumpSizeInVoxels,
            float rockDetailSizeInVoxels,
            float rockRadiusAmplitude,
            float rockDensityVariation,
            float hardSealValue)
        {
            WallDetailOctavesOffset = wallDetailOctavesOffset;
            WallSmoothingDistanceInVoxels = wallSmoothingDistanceInVoxels;
            WallNoiseExtensionInVoxels = wallNoiseExtensionInVoxels;
            WallNoiseBlend = wallNoiseBlend;
            WallStrength = wallStrength;
            ClosureBandInVoxels = closureBandInVoxels;
            ClosureMinNormalizedBand = closureMinNormalizedBand;
            ClosureMaxNormalizedBand = closureMaxNormalizedBand;
            ClosureHardening = closureHardening;
            RockLumpSizeInVoxels = rockLumpSizeInVoxels;
            RockDetailSizeInVoxels = rockDetailSizeInVoxels;
            RockRadiusAmplitude = rockRadiusAmplitude;
            RockDensityVariation = rockDensityVariation;
            HardSealValue = hardSealValue;
        }
    }
}
