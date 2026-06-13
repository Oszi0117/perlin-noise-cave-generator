using System.Runtime.CompilerServices;
using CaveCreation.GenerationData;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Utils
{
    [BurstCompile]
    public static class NoiseUtils
    {
        private const int PERMUTATION_TABLE_SIZE = 256;
        private const int PERMUTATION_TABLE_MASK = PERMUTATION_TABLE_SIZE - 1;
        private const int PERMUTATION_ARRAY_SIZE = PERMUTATION_TABLE_SIZE * 2;

        private const float NOISE_TO_UNIT_RANGE_SHIFT = 1f;
        private const float NOISE_TO_UNIT_RANGE_SCALE = 0.5f;

        private const int GRADIENT_HASH_MASK = 15;
        private const int GRADIENT_FIRST_COMPONENT_TOGGLE_THRESHOLD = 8;
        private const int GRADIENT_SECOND_COMPONENT_TOGGLE_THRESHOLD = 4;
        private const int GRADIENT_ALTERNATE_INDEX_1 = 12;
        private const int GRADIENT_ALTERNATE_INDEX_2 = 14;

        private const float ONE_UNIT_STEP = 1f;
        private const float QUINTIC_FADE_COEFFICIENT_FOR_T5 = 6f;
        private const float QUINTIC_FADE_COEFFICIENT_FOR_T4 = 15f;
        private const float QUINTIC_FADE_COEFFICIENT_FOR_T3 = 10f;

        public static void GetVoxelValues(
            NativeList<float4> result,
            float3 boundsMin,
            float3 boundsMax,
            float voxelSize,
            float3 noiseScale,
            int seed,
            int octaves = 4,
            float lacunarity = 2f,
            float persistence = 0.5f)
        {
            if (voxelSize <= 0f)
                voxelSize = 1f;

            if (octaves < 1)
                octaves = 1;

            var permutationArray = BuildPermutationTable(seed, Allocator.Temp);

            var size = boundsMax - boundsMin;

            var pointsCountX = math.max(1, (int)math.round(size.x / voxelSize)) + 1;
            var pointsCountY = math.max(1, (int)math.round(size.y / voxelSize)) + 1;
            var pointsCountZ = math.max(1, (int)math.round(size.z / voxelSize)) + 1;

            for (var yIndex = 0; yIndex < pointsCountY; yIndex++)
            {
                var yCoordinate = boundsMin.y + yIndex * voxelSize;

                for (var xIndex = 0; xIndex < pointsCountX; xIndex++)
                {
                    var xCoordinate = boundsMin.x + xIndex * voxelSize;

                    for (var zIndex = 0; zIndex < pointsCountZ; zIndex++)
                    {
                        var zCoordinate = boundsMin.z + zIndex * voxelSize;

                        var noiseValue = FractalBrownianMotion3D(
                            xCoordinate * noiseScale.x,
                            yCoordinate * noiseScale.y,
                            zCoordinate * noiseScale.z,
                            permutationArray, octaves, lacunarity, persistence);

                        result.Add(new float4(xCoordinate, yCoordinate, zCoordinate, noiseValue));
                    }
                }
            }

            permutationArray.Dispose();
        }

        public static float FractalBrownianMotion3D(
            float xCoordinate,
            float yCoordinate,
            float zCoordinate,
            NativeArray<int> permutationArray,
            int octaves,
            float lacunarity,
            float persistence)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var valueSum = 0f;
            var amplitudeSum = 0f;

            for (var octaveIndex = 0; octaveIndex < octaves; octaveIndex++)
            {
                var noiseSample = PerlinNoise3D(
                    xCoordinate * frequency, yCoordinate * frequency, zCoordinate * frequency, permutationArray);

                noiseSample = (noiseSample + NOISE_TO_UNIT_RANGE_SHIFT) * NOISE_TO_UNIT_RANGE_SCALE;
                valueSum += noiseSample * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return amplitudeSum > 0f ? valueSum / amplitudeSum : 0f;
        }

        public static float PerlinNoise3D(float xCoordinate, float yCoordinate, float zCoordinate,
            NativeArray<int> permutationArray)
        {
            var gridXIndex = FastFloor(xCoordinate) & PERMUTATION_TABLE_MASK;
            var gridYIndex = FastFloor(yCoordinate) & PERMUTATION_TABLE_MASK;
            var gridZIndex = FastFloor(zCoordinate) & PERMUTATION_TABLE_MASK;

            xCoordinate -= math.floor(xCoordinate);
            yCoordinate -= math.floor(yCoordinate);
            zCoordinate -= math.floor(zCoordinate);

            var fadeX = QuinticFade(xCoordinate);
            var fadeY = QuinticFade(yCoordinate);
            var fadeZ = QuinticFade(zCoordinate);

            var hashForXAndY = (permutationArray[gridXIndex] + gridYIndex) & PERMUTATION_TABLE_MASK;
            var hashForXPlusOneAndY = (permutationArray[(gridXIndex + 1) & PERMUTATION_TABLE_MASK] + gridYIndex) &
                                      PERMUTATION_TABLE_MASK;
            var hashForXYAndZ = (permutationArray[hashForXAndY] + gridZIndex) & PERMUTATION_TABLE_MASK;
            var hashForXYPlusOneAndZ = (permutationArray[(hashForXAndY + 1) & PERMUTATION_TABLE_MASK] + gridZIndex) &
                                       PERMUTATION_TABLE_MASK;
            var hashForXPlusOneYAndZ = (permutationArray[hashForXPlusOneAndY] + gridZIndex) & PERMUTATION_TABLE_MASK;
            var hashForXPlusOneYPlusOneAndZ =
                (permutationArray[(hashForXPlusOneAndY + 1) & PERMUTATION_TABLE_MASK] + gridZIndex) &
                PERMUTATION_TABLE_MASK;

            var gradientAt000 = GradientDotProduct(permutationArray[hashForXYAndZ],
                new float3(xCoordinate, yCoordinate, zCoordinate));
            var gradientAt100 = GradientDotProduct(permutationArray[hashForXPlusOneYAndZ],
                new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate, zCoordinate));
            var gradientAt010 = GradientDotProduct(permutationArray[hashForXYPlusOneAndZ],
                new float3(xCoordinate, yCoordinate - ONE_UNIT_STEP, zCoordinate));
            var gradientAt110 = GradientDotProduct(permutationArray[hashForXPlusOneYPlusOneAndZ],
                new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate - ONE_UNIT_STEP, zCoordinate));

            var gradientAt001 = GradientDotProduct(permutationArray[(hashForXYAndZ + 1) & PERMUTATION_TABLE_MASK],
                new float3(xCoordinate, yCoordinate, zCoordinate - ONE_UNIT_STEP));
            var gradientAt101 =
                GradientDotProduct(permutationArray[(hashForXPlusOneYAndZ + 1) & PERMUTATION_TABLE_MASK],
                    new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate, zCoordinate - ONE_UNIT_STEP));
            var gradientAt011 =
                GradientDotProduct(permutationArray[(hashForXYPlusOneAndZ + 1) & PERMUTATION_TABLE_MASK],
                    new float3(xCoordinate, yCoordinate - ONE_UNIT_STEP, zCoordinate - ONE_UNIT_STEP));
            var gradientAt111 =
                GradientDotProduct(permutationArray[(hashForXPlusOneYPlusOneAndZ + 1) & PERMUTATION_TABLE_MASK],
                    new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate - ONE_UNIT_STEP, zCoordinate - ONE_UNIT_STEP));

            var xInterpolationAtLowerYLowerZ = LinearInterpolate(fadeX, gradientAt000, gradientAt100);
            var xInterpolationAtUpperYLowerZ = LinearInterpolate(fadeX, gradientAt010, gradientAt110);
            var xInterpolationAtLowerYUpperZ = LinearInterpolate(fadeX, gradientAt001, gradientAt101);
            var xInterpolationAtUpperYUpperZ = LinearInterpolate(fadeX, gradientAt011, gradientAt111);
            var yInterpolationAtLowerZ =
                LinearInterpolate(fadeY, xInterpolationAtLowerYLowerZ, xInterpolationAtUpperYLowerZ);
            var yInterpolationAtUpperZ =
                LinearInterpolate(fadeY, xInterpolationAtLowerYUpperZ, xInterpolationAtUpperYUpperZ);

            var result = LinearInterpolate(fadeZ, yInterpolationAtLowerZ, yInterpolationAtUpperZ);
            return result;
        }

        private static NativeArray<int> BuildPermutationTable(int seed, Allocator allocator)
        {
            var permutationArray = new NativeArray<int>(PERMUTATION_ARRAY_SIZE, allocator);

            for (var index = 0; index < PERMUTATION_TABLE_SIZE; index++)
                permutationArray[index] = index;

            uint state = (uint)seed;
            for (var index = PERMUTATION_TABLE_SIZE - 1; index > 0; index--)
            {
                state = 1664525u * state + 1013904223u;
                var swapIndex = (int)(state % (uint)(index + 1));
                (permutationArray[index], permutationArray[swapIndex]) =
                    (permutationArray[swapIndex], permutationArray[index]);
            }

            for (var index = PERMUTATION_TABLE_SIZE; index < PERMUTATION_ARRAY_SIZE; index++)
                permutationArray[index] = permutationArray[index - PERMUTATION_TABLE_SIZE];

            return permutationArray;
        }
        
        public static void ApplyOpenBoundaryWalls(
            NativeList<float4> voxelValues,
            float3 closureBoundsMin,
            float3 closureBoundsMax,
            float voxelSize,
            float3 noiseScale,
            int seed,
            int octaves,
            float lacunarity,
            float persistence,
            float isoLevel,
            BoundaryClosureSettings closureSettings,
            byte openFaceMask)
        {
            if (voxelValues.Length == 0 || openFaceMask == 0)
                return;

            if (voxelSize <= 0f)
                voxelSize = 1f;

            if (octaves < 1)
                octaves = 1;

            var permutationArray = BuildPermutationTable(seed, Allocator.Temp);
            var wallBlendDistance = voxelSize * math.max(0f, closureSettings.WallSmoothingDistanceInVoxels);
            var extensionDistance = voxelSize * math.max(0f, closureSettings.WallNoiseExtensionInVoxels);
            var isoCeiling = math.max(0.02f, isoLevel - 0.02f);
            var hardSealValueFloor = math.max(0f, closureSettings.HardSealValue);
            var hardSealCeiling = math.max(hardSealValueFloor, isoLevel - 0.12f);
            var center = (closureBoundsMin + closureBoundsMax) * 0.5f;
            var halfExtents = math.max((closureBoundsMax - closureBoundsMin) * 0.5f, new float3(0.001f));
            var minNormalizedBand = math.max(0.001f, closureSettings.ClosureMinNormalizedBand);
            var maxNormalizedBand = math.max(minNormalizedBand, closureSettings.ClosureMaxNormalizedBand);
            var normalizedClosureBand = math.clamp(
                voxelSize * math.max(0.001f, closureSettings.ClosureBandInVoxels) /
                math.max(0.001f, math.cmin(halfExtents)),
                minNormalizedBand,
                maxNormalizedBand);
            var closureStartRadius = 1f - normalizedClosureBand;

            for (var i = 0; i < voxelValues.Length; i++)
            {
                var voxel = voxelValues[i];
                var position = voxel.xyz;
                var baseValue = voxel.w;
                var rockOffset = ComputeClosureRockOffset(position, voxelSize, permutationArray, closureSettings);
                var localClosureStartRadius = math.clamp(
                    closureStartRadius - rockOffset * normalizedClosureBand * math.max(0f, closureSettings.RockRadiusAmplitude),
                    0.05f,
                    0.98f);
                var sphericalClosureBlend = ComputeSphericalClosureInfluence(
                    position,
                    center,
                    halfExtents,
                    localClosureStartRadius);
                
                if (sphericalClosureBlend <= 0f)
                    continue;

                var minXInfluence = (openFaceMask & (1 << 0)) != 0
                    ? ComputeBoundaryInfluence(position.x - closureBoundsMin.x, wallBlendDistance)
                    : 0f;
                var maxXInfluence = (openFaceMask & (1 << 1)) != 0
                    ? ComputeBoundaryInfluence(closureBoundsMax.x - position.x, wallBlendDistance)
                    : 0f;
                var minYInfluence = (openFaceMask & (1 << 2)) != 0
                    ? ComputeBoundaryInfluence(position.y - closureBoundsMin.y, wallBlendDistance)
                    : 0f;
                var maxYInfluence = (openFaceMask & (1 << 3)) != 0
                    ? ComputeBoundaryInfluence(closureBoundsMax.y - position.y, wallBlendDistance)
                    : 0f;
                var minZInfluence = (openFaceMask & (1 << 4)) != 0
                    ? ComputeBoundaryInfluence(position.z - closureBoundsMin.z, wallBlendDistance)
                    : 0f;
                var maxZInfluence = (openFaceMask & (1 << 5)) != 0
                    ? ComputeBoundaryInfluence(closureBoundsMax.z - position.z, wallBlendDistance)
                    : 0f;

                var wallBlend = math.saturate(math.pow(sphericalClosureBlend, 0.82f) * math.max(0f, closureSettings.WallStrength));
                if (wallBlend <= 0f)
                    continue;

                var extensionOffset = new float3(
                    (maxXInfluence - minXInfluence) * extensionDistance,
                    (maxYInfluence - minYInfluence) * extensionDistance,
                    (maxZInfluence - minZInfluence) * extensionDistance);

                var extendedNoiseSample = FractalBrownianMotion3D(
                    (position.x + extensionOffset.x) * noiseScale.x,
                    (position.y + extensionOffset.y) * noiseScale.y,
                    (position.z + extensionOffset.z) * noiseScale.z,
                    permutationArray,
                    octaves + math.max(0, closureSettings.WallDetailOctavesOffset),
                    lacunarity,
                    persistence);

                var detailedWallValue = math.lerp(baseValue, extendedNoiseSample, math.saturate(closureSettings.WallNoiseBlend));
                detailedWallValue -= math.saturate(rockOffset) * math.max(0f, closureSettings.RockDensityVariation) * wallBlend;
                detailedWallValue = math.clamp(detailedWallValue * 0.45f + 0.1f, 0.02f, isoCeiling);

                var blendedValue = math.lerp(baseValue, detailedWallValue, wallBlend);
                var hardSealBlend = math.saturate(math.pow(sphericalClosureBlend, math.max(0.001f, closureSettings.ClosureHardening)));

                if (hardSealBlend > 0f)
                {
                    var hardSealValue = math.clamp(
                        hardSealValueFloor + extendedNoiseSample * 0.08f,
                        hardSealValueFloor,
                        hardSealCeiling);
                    blendedValue = math.lerp(blendedValue, hardSealValue, hardSealBlend);
                }

                voxelValues[i] = new float4(position, blendedValue);
            }

            permutationArray.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeBoundaryInfluence(float distanceToBoundary, float maxDistance)
        {
            if (distanceToBoundary <= 0f)
                return 1f;

            if (distanceToBoundary >= maxDistance)
                return 0f;

            var linear = 1f - distanceToBoundary / maxDistance;
            return linear * linear * (3f - 2f * linear);
        }

        private static float ComputeSphericalClosureInfluence(
            float3 position,
            float3 center,
            float3 halfExtents,
            float closureStartRadius)
        {
            var normalizedPosition = (position - center) / halfExtents;
            var radialDistance = math.length(normalizedPosition);
            if (radialDistance <= closureStartRadius)
                return 0f;

            var blend = math.saturate((radialDistance - closureStartRadius) / (1f - closureStartRadius));
            return blend * blend * (3f - 2f * blend);
        }

        private static float ComputeClosureRockOffset(
            float3 position,
            float voxelSize,
            NativeArray<int> permutationArray,
            BoundaryClosureSettings closureSettings)
        {
            var normalizedVoxelSize = math.max(voxelSize, 0.001f);
            var lumpFrequency = 1f / (normalizedVoxelSize * math.max(0.001f, closureSettings.RockLumpSizeInVoxels));
            var detailFrequency = 1f / (normalizedVoxelSize * math.max(0.001f, closureSettings.RockDetailSizeInVoxels));

            var lumpNoise = FractalBrownianMotion3D(
                position.x * lumpFrequency + 17.31f,
                position.y * lumpFrequency - 41.73f,
                position.z * lumpFrequency + 93.17f,
                permutationArray,
                3,
                2.05f,
                0.55f);

            var detailNoise = FractalBrownianMotion3D(
                position.x * detailFrequency - 113.11f,
                position.y * detailFrequency + 29.57f,
                position.z * detailFrequency - 7.83f,
                permutationArray,
                2,
                2.25f,
                0.45f);

            var signedLump = lumpNoise * 2f - 1f;
            var ridgedDetail = 1f - math.abs(detailNoise * 2f - 1f);
            var stonePeak = math.pow(math.saturate(ridgedDetail), 2.4f);

            return math.clamp(signedLump * 0.65f + stonePeak * 0.8f - 0.25f, -1f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastFloor(float value)
        {
            int i = (int)value;
            return value < i ? i - 1 : i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float QuinticFade(float value)
            => value * value * value *
               (value * (value * QUINTIC_FADE_COEFFICIENT_FOR_T5 - QUINTIC_FADE_COEFFICIENT_FOR_T4) +
                QUINTIC_FADE_COEFFICIENT_FOR_T3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LinearInterpolate(float interpolationFactor, float from, float to)
            => from + interpolationFactor * (to - from);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GradientDotProduct(int hash, float3 position)
        {
            var hashLowBits = hash & GRADIENT_HASH_MASK;
            var firstComponent = hashLowBits < GRADIENT_FIRST_COMPONENT_TOGGLE_THRESHOLD ? position.x : position.y;
            float secondComponent;
            if (hashLowBits < GRADIENT_SECOND_COMPONENT_TOGGLE_THRESHOLD)
                secondComponent = position.y;
            else
                secondComponent = hashLowBits is GRADIENT_ALTERNATE_INDEX_1 or GRADIENT_ALTERNATE_INDEX_2
                    ? position.x
                    : position.z;

            var firstContribution = (hashLowBits & 1) == 0 ? firstComponent : -firstComponent;
            var secondContribution = (hashLowBits & 2) == 0 ? secondComponent : -secondComponent;

            var dot = firstContribution + secondContribution;
            return dot;
        }
    }
}
