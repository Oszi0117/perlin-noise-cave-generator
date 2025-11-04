using System.Runtime.CompilerServices;
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

        private const float VOXEL_CENTER_OFFSET = 0.5f;
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

        public static void GetVoxelCenters(
            NativeList<float3> result,
            float3 boundsMin,
            float3 boundsMax,
            float voxelSize,
            float threshold,
            float3 noiseScale,
            int seed,
            int octaves = 4,
            float lacunarity = 2f,
            float persistence = 0.5f,
            float3 offset = default)
        {
            if (voxelSize <= 0f)
                return;

            if (octaves < 1)
                octaves = 1;

            var permutationArray = BuildPermutationTable(seed, Allocator.Temp);

            var size = boundsMax - boundsMin;

            var cellCountX = math.max(1, (int)math.ceil(size.x / voxelSize));
            var cellCountY = math.max(1, (int)math.ceil(size.y / voxelSize));
            var cellCountZ = math.max(1, (int)math.ceil(size.z / voxelSize));

            for (var yIndex = 0; yIndex < cellCountY; yIndex++)
            {
                var yCoordinate = boundsMin.y + (yIndex + VOXEL_CENTER_OFFSET) * voxelSize;
                if (yCoordinate > boundsMax.y) continue;

                for (var xIndex = 0; xIndex < cellCountX; xIndex++)
                {
                    var xCoordinate = boundsMin.x + (xIndex + VOXEL_CENTER_OFFSET) * voxelSize;
                    if (xCoordinate > boundsMax.x) continue;

                    for (var zIndex = 0; zIndex < cellCountZ; zIndex++)
                    {
                        var zCoordinate = boundsMin.z + (zIndex + VOXEL_CENTER_OFFSET) * voxelSize;
                        if (zCoordinate > boundsMax.z) continue;

                        var noiseValue = FractalBrownianMotion3D(
                            (xCoordinate + offset.x) * noiseScale.x,
                            (yCoordinate + offset.y) * noiseScale.y,
                            (zCoordinate + offset.z) * noiseScale.z,
                            permutationArray, octaves, lacunarity, persistence);

                        if (noiseValue >= threshold) result.Add(new float3(xCoordinate, yCoordinate, zCoordinate));
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

        public static float PerlinNoise3D(float xCoordinate, float yCoordinate, float zCoordinate, NativeArray<int> permutationArray)
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
            var hashForXPlusOneAndY = (permutationArray[(gridXIndex + 1) & PERMUTATION_TABLE_MASK] + gridYIndex) & PERMUTATION_TABLE_MASK;
            var hashForXYAndZ = (permutationArray[hashForXAndY] + gridZIndex) & PERMUTATION_TABLE_MASK;
            var hashForXYPlusOneAndZ = (permutationArray[(hashForXAndY + 1) & PERMUTATION_TABLE_MASK] + gridZIndex) & PERMUTATION_TABLE_MASK;
            var hashForXPlusOneYAndZ = (permutationArray[hashForXPlusOneAndY] + gridZIndex) & PERMUTATION_TABLE_MASK;
            var hashForXPlusOneYPlusOneAndZ = (permutationArray[(hashForXPlusOneAndY + 1) & PERMUTATION_TABLE_MASK] + gridZIndex) & PERMUTATION_TABLE_MASK;

            var gradientAt000 = GradientDotProduct(permutationArray[hashForXYAndZ], new float3(xCoordinate, yCoordinate, zCoordinate));
            var gradientAt100 = GradientDotProduct(permutationArray[hashForXPlusOneYAndZ], new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate, zCoordinate));
            var gradientAt010 = GradientDotProduct(permutationArray[hashForXYPlusOneAndZ], new float3(xCoordinate, yCoordinate - ONE_UNIT_STEP, zCoordinate));
            var gradientAt110 = GradientDotProduct(permutationArray[hashForXPlusOneYPlusOneAndZ], new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate - ONE_UNIT_STEP, zCoordinate));
        
            var gradientAt001 = GradientDotProduct(permutationArray[(hashForXYAndZ + 1) & PERMUTATION_TABLE_MASK], new float3(xCoordinate, yCoordinate, zCoordinate - ONE_UNIT_STEP));
            var gradientAt101 = GradientDotProduct(permutationArray[(hashForXPlusOneYAndZ + 1) & PERMUTATION_TABLE_MASK], new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate, zCoordinate - ONE_UNIT_STEP));
            var gradientAt011 = GradientDotProduct(permutationArray[(hashForXYPlusOneAndZ + 1) & PERMUTATION_TABLE_MASK], new float3(xCoordinate, yCoordinate - ONE_UNIT_STEP, zCoordinate - ONE_UNIT_STEP));
            var gradientAt111 = GradientDotProduct(permutationArray[(hashForXPlusOneYPlusOneAndZ + 1) & PERMUTATION_TABLE_MASK], new float3(xCoordinate - ONE_UNIT_STEP, yCoordinate - ONE_UNIT_STEP, zCoordinate - ONE_UNIT_STEP));

            var xInterpolationAtLowerYLowerZ = LinearInterpolate(fadeX, gradientAt000, gradientAt100);
            var xInterpolationAtUpperYLowerZ = LinearInterpolate(fadeX, gradientAt010, gradientAt110);
            var xInterpolationAtLowerYUpperZ = LinearInterpolate(fadeX, gradientAt001, gradientAt101);
            var xInterpolationAtUpperYUpperZ = LinearInterpolate(fadeX, gradientAt011, gradientAt111);
            var yInterpolationAtLowerZ = LinearInterpolate(fadeY, xInterpolationAtLowerYLowerZ, xInterpolationAtUpperYLowerZ);
            var yInterpolationAtUpperZ = LinearInterpolate(fadeY, xInterpolationAtLowerYUpperZ, xInterpolationAtUpperYUpperZ);

            var result = LinearInterpolate(fadeZ, yInterpolationAtLowerZ, yInterpolationAtUpperZ);
            return result;
        }

        public static NativeArray<int> BuildPermutationTable(int seed, Allocator allocator)
        {
            var permutationArray = new NativeArray<int>(PERMUTATION_ARRAY_SIZE, allocator);

            for (var index = 0; index < PERMUTATION_TABLE_SIZE; index++)
                permutationArray[index] = index;

            uint state = (uint)seed;
            for (var index = PERMUTATION_TABLE_SIZE - 1; index > 0; index--)
            {
                state = 1664525u * state + 1013904223u;
                var swapIndex = (int)(state % (uint)(index + 1));
                (permutationArray[index], permutationArray[swapIndex]) = (permutationArray[swapIndex], permutationArray[index]);
            }

            for (var index = PERMUTATION_TABLE_SIZE; index < PERMUTATION_ARRAY_SIZE; index++)
                permutationArray[index] = permutationArray[index - PERMUTATION_TABLE_SIZE];

            return permutationArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastFloor(float value)
            => value >= 0 ? (int)value : (int)value - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float QuinticFade(float value)
            => value * value * value * (value * (value * QUINTIC_FADE_COEFFICIENT_FOR_T5 - QUINTIC_FADE_COEFFICIENT_FOR_T4) + QUINTIC_FADE_COEFFICIENT_FOR_T3);

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
