using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

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

        public static void GetSdfVoxelValues(
            NativeList<float4> result,
            float3 boundsMin,
            float3 boundsMax,
            float voxelSize,
            float3 noiseScale,
            int seed,
            int octaves,
            float lacunarity,
            float persistence,
            float isoLevel,
            NativeArray<float3> roomCenterArray,
            NativeArray<float3> roomRadiusArray,
            NativeArray<float3> tunnelStartArray,
            NativeArray<float3> tunnelEndArray,
            NativeArray<float> tunnelRadiusArray,
            NativeArray<int> roomIndexArray,
            int roomIndexStart,
            int roomIndexCount,
            NativeArray<int> tunnelIndexArray,
            int tunnelIndexStart,
            int tunnelIndexCount,
            float surfaceThicknessInVoxels,
            float smoothUnionInVoxels,
            float wallNoiseAmplitudeInVoxels,
            float wallNoiseScaleInVoxels,
            float wallLobeStrength,
            float wallLobeScaleMultiplier,
            float tunnelLobeStrength,
            float tunnelLobeScaleMultiplier,
            float interiorNoiseStrength,
            float interiorNoiseCutoff,
            float interiorWallClearanceInVoxels,
            float interiorTunnelClearanceInVoxels,
            float interiorClearanceBlendInVoxels)
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
            var surfaceThickness = voxelSize * math.max(0.1f, surfaceThicknessInVoxels);
            var smoothUnionDistance = voxelSize * math.max(0f, smoothUnionInVoxels);
            var wallNoiseAmplitude = voxelSize * math.max(0f, wallNoiseAmplitudeInVoxels);
            var wallNoiseFrequency = 1f / (voxelSize * math.max(0.1f, wallNoiseScaleInVoxels));
            var wallLobeFrequency = wallNoiseFrequency / math.max(0.1f, wallLobeScaleMultiplier);
            var tunnelLobeFrequency = wallNoiseFrequency / math.max(0.1f, tunnelLobeScaleMultiplier);
            var interiorWallClearance = voxelSize * math.max(0f, interiorWallClearanceInVoxels);
            var interiorTunnelClearance = voxelSize * math.max(0f, interiorTunnelClearanceInVoxels);
            var interiorClearanceBlend = voxelSize * math.max(0.1f, interiorClearanceBlendInVoxels);
            var previousNoiseScale = math.select(
                new float3(wallNoiseFrequency),
                math.abs(noiseScale),
                math.abs(noiseScale) > new float3(0.0001f));
            var noiseInfluenceDistance = wallNoiseAmplitude + smoothUnionDistance + surfaceThickness * 2f;

            for (var yIndex = 0; yIndex < pointsCountY; yIndex++)
            {
                var yCoordinate = boundsMin.y + yIndex * voxelSize;

                for (var xIndex = 0; xIndex < pointsCountX; xIndex++)
                {
                    var xCoordinate = boundsMin.x + xIndex * voxelSize;

                    for (var zIndex = 0; zIndex < pointsCountZ; zIndex++)
                    {
                        var zCoordinate = boundsMin.z + zIndex * voxelSize;
                        var position = new float3(xCoordinate, yCoordinate, zCoordinate);
                        var sdf = EvaluateCaveSdf(
                            position,
                            roomCenterArray,
                            roomRadiusArray,
                            tunnelStartArray,
                            tunnelEndArray,
                            tunnelRadiusArray,
                            roomIndexArray,
                            roomIndexStart,
                            roomIndexCount,
                            tunnelIndexArray,
                            tunnelIndexStart,
                            tunnelIndexCount,
                            smoothUnionDistance,
                            permutationArray,
                            octaves,
                            lacunarity,
                            persistence,
                            previousNoiseScale,
                            wallNoiseFrequency,
                            wallLobeFrequency,
                            tunnelLobeFrequency,
                            wallNoiseAmplitude,
                            wallLobeStrength,
                            tunnelLobeStrength,
                            noiseInfluenceDistance);

                        var scalarValue = SdfToScalarValue(sdf, isoLevel, surfaceThickness);
                        scalarValue = ApplyInteriorNoiseStructures(
                            position,
                            scalarValue,
                            sdf,
                            isoLevel,
                            permutationArray,
                            octaves,
                            lacunarity,
                            persistence,
                            previousNoiseScale,
                            wallNoiseFrequency,
                            tunnelStartArray,
                            tunnelEndArray,
                            tunnelRadiusArray,
                            tunnelIndexArray,
                            tunnelIndexStart,
                            tunnelIndexCount,
                            math.saturate(interiorNoiseStrength),
                            math.saturate(interiorNoiseCutoff),
                            interiorWallClearance,
                            interiorTunnelClearance,
                            interiorClearanceBlend);
                        result.Add(new float4(position, scalarValue));
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

        private static float EvaluateCaveSdf(
            float3 position,
            NativeArray<float3> roomCenterArray,
            NativeArray<float3> roomRadiusArray,
            NativeArray<float3> tunnelStartArray,
            NativeArray<float3> tunnelEndArray,
            NativeArray<float> tunnelRadiusArray,
            NativeArray<int> roomIndexArray,
            int roomIndexStart,
            int roomIndexCount,
            NativeArray<int> tunnelIndexArray,
            int tunnelIndexStart,
            int tunnelIndexCount,
            float smoothUnionDistance,
            NativeArray<int> permutationArray,
            int octaves,
            float lacunarity,
            float persistence,
            float3 previousNoiseScale,
            float wallNoiseFrequency,
            float wallLobeFrequency,
            float tunnelLobeFrequency,
            float wallNoiseAmplitude,
            float wallLobeStrength,
            float tunnelLobeStrength,
            float noiseInfluenceDistance)
        {
            var sdf = float.PositiveInfinity;

            for (var roomOffset = 0; roomOffset < roomIndexCount; roomOffset++)
            {
                var i = roomIndexArray[roomIndexStart + roomOffset];
                var baseRoomSdf = EllipsoidSdf(position, roomCenterArray[i], roomRadiusArray[i]);
                var roomSdf = math.abs(baseRoomSdf) <= noiseInfluenceDistance
                    ? NoisyEllipsoidSdf(
                        position,
                        roomCenterArray[i],
                        roomRadiusArray[i],
                        permutationArray,
                        octaves,
                        lacunarity,
                        persistence,
                        previousNoiseScale,
                        wallNoiseFrequency,
                        wallLobeFrequency,
                        wallNoiseAmplitude,
                        wallLobeStrength)
                    : baseRoomSdf;
                sdf = UnionSdf(sdf, roomSdf, smoothUnionDistance);
            }

            for (var tunnelOffset = 0; tunnelOffset < tunnelIndexCount; tunnelOffset++)
            {
                var i = tunnelIndexArray[tunnelIndexStart + tunnelOffset];
                var baseTunnelSdf = CapsuleSdf(position, tunnelStartArray[i], tunnelEndArray[i], tunnelRadiusArray[i]);
                var tunnelSdf = math.abs(baseTunnelSdf) <= noiseInfluenceDistance
                    ? NoisyCapsuleSdf(
                        position,
                        tunnelStartArray[i],
                        tunnelEndArray[i],
                        tunnelRadiusArray[i],
                        permutationArray,
                        octaves,
                        lacunarity,
                        persistence,
                        previousNoiseScale,
                        wallNoiseFrequency,
                        tunnelLobeFrequency,
                        wallNoiseAmplitude,
                        tunnelLobeStrength)
                    : baseTunnelSdf;
                sdf = UnionSdf(sdf, tunnelSdf, smoothUnionDistance);
            }

            return math.isfinite(sdf) ? sdf : 100000f;
        }

        private static float ApplyInteriorNoiseStructures(
            float3 position,
            float scalarValue,
            float sdf,
            float isoLevel,
            NativeArray<int> permutationArray,
            int octaves,
            float lacunarity,
            float persistence,
            float3 previousNoiseScale,
            float wallNoiseFrequency,
            NativeArray<float3> tunnelStartArray,
            NativeArray<float3> tunnelEndArray,
            NativeArray<float> tunnelRadiusArray,
            NativeArray<int> tunnelIndexArray,
            int tunnelIndexStart,
            int tunnelIndexCount,
            float interiorNoiseStrength,
            float interiorNoiseCutoff,
            float interiorWallClearance,
            float interiorTunnelClearance,
            float interiorClearanceBlend)
        {
            if (interiorNoiseStrength <= 0f || sdf >= -interiorWallClearance)
                return scalarValue;

            var wallMask = math.saturate((-sdf - interiorWallClearance) / interiorClearanceBlend);
            var baseNoise = FractalBrownianMotion3D(
                position.x * previousNoiseScale.x,
                position.y * previousNoiseScale.y,
                position.z * previousNoiseScale.z,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
            var detailNoise = FractalBrownianMotion3D(
                position.x * wallNoiseFrequency * 1.65f + 83.11f,
                position.y * wallNoiseFrequency * 1.65f - 12.47f,
                position.z * wallNoiseFrequency * 1.65f + 29.73f,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
            var structureNoise = baseNoise * 0.75f + detailNoise * 0.25f;
            var structureScalar = structureNoise + isoLevel - interiorNoiseCutoff;
            if (structureScalar >= isoLevel)
                return scalarValue;

            var tunnelMask = ComputeTunnelInteriorMask(
                position,
                tunnelStartArray,
                tunnelEndArray,
                tunnelRadiusArray,
                tunnelIndexArray,
                tunnelIndexStart,
                tunnelIndexCount,
                interiorTunnelClearance,
                interiorClearanceBlend);
            var influence = interiorNoiseStrength * wallMask * tunnelMask;
            if (influence <= 0f)
                return scalarValue;

            return math.lerp(scalarValue, math.min(scalarValue, structureScalar), influence);
        }

        private static float ComputeTunnelInteriorMask(
            float3 position,
            NativeArray<float3> tunnelStartArray,
            NativeArray<float3> tunnelEndArray,
            NativeArray<float> tunnelRadiusArray,
            NativeArray<int> tunnelIndexArray,
            int tunnelIndexStart,
            int tunnelIndexCount,
            float extraClearance,
            float blendDistance)
        {
            var mask = 1f;
            for (var tunnelOffset = 0; tunnelOffset < tunnelIndexCount; tunnelOffset++)
            {
                var i = tunnelIndexArray[tunnelIndexStart + tunnelOffset];
                var clearRadius = tunnelRadiusArray[i] + extraClearance;
                var axisDistanceSq = DistanceSqToSegment(position, tunnelStartArray[i], tunnelEndArray[i]);
                var clearRadiusSq = clearRadius * clearRadius;
                if (axisDistanceSq <= clearRadiusSq)
                    return 0f;

                var blendEnd = clearRadius + blendDistance;
                if (axisDistanceSq >= blendEnd * blendEnd)
                    continue;

                var axisDistance = math.sqrt(axisDistanceSq);
                mask = math.min(mask, math.saturate((axisDistance - clearRadius) / blendDistance));
            }

            return mask;
        }

        private static float SdfToScalarValue(float sdf, float isoLevel, float surfaceThickness)
        {
            var normalizedDistance = sdf / math.max(0.001f, surfaceThickness);
            return math.clamp(isoLevel - normalizedDistance * 0.5f, 0f, 1f);
        }

        private static float UnionSdf(float a, float b, float smoothDistance)
        {
            if (!math.isfinite(a))
                return b;

            if (smoothDistance <= 0f)
                return math.min(a, b);

            var h = math.saturate(0.5f + 0.5f * (b - a) / smoothDistance);
            return math.lerp(b, a, h) - smoothDistance * h * (1f - h);
        }

        private static float EllipsoidSdf(float3 position, float3 center, float3 radius)
        {
            var safeRadius = math.max(radius, new float3(0.001f));
            return (math.length((position - center) / safeRadius) - 1f) * math.cmin(safeRadius);
        }

        private static float CapsuleSdf(float3 position, float3 start, float3 end, float radius)
        {
            var segment = end - start;
            var segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.0001f)
                return math.length(position - start) - radius;

            var t = math.saturate(math.dot(position - start, segment) / segmentLengthSq);
            var closestPoint = start + segment * t;
            return math.length(position - closestPoint) - radius;
        }

        private static float DistanceSqToSegment(float3 position, float3 start, float3 end)
        {
            var segment = end - start;
            var segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.0001f)
                return math.distancesq(position, start);

            var t = math.saturate(math.dot(position - start, segment) / segmentLengthSq);
            return math.distancesq(position, start + segment * t);
        }

        private static float NoisyEllipsoidSdf(
            float3 position,
            float3 center,
            float3 radius,
            NativeArray<int> permutationArray,
            int octaves,
            float lacunarity,
            float persistence,
            float3 previousNoiseScale,
            float wallNoiseFrequency,
            float wallLobeFrequency,
            float wallNoiseAmplitude,
            float wallLobeStrength)
        {
            var safeRadius = math.max(radius, new float3(0.001f));
            var offset = position - center;
            var normalizedDistance = math.length(offset / safeRadius);
            var baseSdf = (normalizedDistance - 1f) * math.cmin(safeRadius);
            var safeAmplitude = math.min(wallNoiseAmplitude, math.cmin(safeRadius) * 0.28f);
            if (safeAmplitude <= 0f)
                return baseSdf;

            var direction = math.lengthsq(offset) > 0.0001f ? math.normalize(offset) : new float3(1f, 0f, 0f);
            var surfaceDistance = 1f / math.max(0.0001f, math.length(direction / safeRadius));
            var surfacePoint = center + direction * surfaceDistance;

            return baseSdf - ComputeClosedWallDisplacement(
                surfacePoint,
                previousNoiseScale,
                wallNoiseFrequency,
                wallLobeFrequency,
                safeAmplitude,
                wallLobeStrength,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
        }

        private static float NoisyCapsuleSdf(
            float3 position,
            float3 start,
            float3 end,
            float radius,
            NativeArray<int> permutationArray,
            int octaves,
            float lacunarity,
            float persistence,
            float3 previousNoiseScale,
            float wallNoiseFrequency,
            float wallLobeFrequency,
            float wallNoiseAmplitude,
            float wallLobeStrength)
        {
            var segment = end - start;
            var segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.0001f)
                return math.length(position - start) - radius;

            var t = math.saturate(math.dot(position - start, segment) / segmentLengthSq);
            var closestPoint = start + segment * t;
            var radialOffset = position - closestPoint;
            var baseSdf = math.length(radialOffset) - radius;
            var safeAmplitude = math.min(wallNoiseAmplitude * 0.6f, radius * 0.28f);
            if (safeAmplitude <= 0f)
                return baseSdf;

            var radialDirection = math.lengthsq(radialOffset) > 0.0001f
                ? math.normalize(radialOffset)
                : GetAnyPerpendicularDirection(segment);
            var surfacePoint = closestPoint + radialDirection * radius;

            return baseSdf - ComputeClosedWallDisplacement(
                surfacePoint,
                previousNoiseScale,
                wallNoiseFrequency,
                wallLobeFrequency,
                safeAmplitude,
                wallLobeStrength,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
        }

        private static float ComputeClosedWallDisplacement(
            float3 surfacePoint,
            float3 previousNoiseScale,
            float wallNoiseFrequency,
            float wallLobeFrequency,
            float safeAmplitude,
            float wallLobeStrength,
            NativeArray<int> permutationArray,
            int octaves,
            float lacunarity,
            float persistence)
        {
            var previousNoise = FractalBrownianMotion3D(
                surfacePoint.x * previousNoiseScale.x,
                surfacePoint.y * previousNoiseScale.y,
                surfacePoint.z * previousNoiseScale.z,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
            var lobeNoise = FractalBrownianMotion3D(
                surfacePoint.x * wallLobeFrequency - 71.43f,
                surfacePoint.y * wallLobeFrequency + 14.19f,
                surfacePoint.z * wallLobeFrequency - 37.65f,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
            var wallNoise = FractalBrownianMotion3D(
                surfacePoint.x * wallNoiseFrequency + 31.17f,
                surfacePoint.y * wallNoiseFrequency - 47.93f,
                surfacePoint.z * wallNoiseFrequency + 11.61f,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
            var detailNoise = FractalBrownianMotion3D(
                surfacePoint.x * wallNoiseFrequency * 2.35f - 19.73f,
                surfacePoint.y * wallNoiseFrequency * 2.35f + 7.41f,
                surfacePoint.z * wallNoiseFrequency * 2.35f + 53.29f,
                permutationArray,
                octaves,
                lacunarity,
                persistence);
            var lobe = ShapeWallNoise(lobeNoise * 2f - 1f) * math.max(0f, wallLobeStrength);
            var broad = (previousNoise * 2f - 1f) * 0.55f;
            var medium = (wallNoise * 2f - 1f) * 0.35f;
            var detail = (detailNoise * 2f - 1f) * 0.1f;
            return math.clamp((lobe + broad + medium + detail) * safeAmplitude, -safeAmplitude, safeAmplitude);
        }

        private static float ShapeWallNoise(float value)
        {
            var sign = math.select(-1f, 1f, value >= 0f);
            var magnitude = math.pow(math.saturate(math.abs(value)), 0.72f);
            return sign * magnitude;
        }

        private static float3 GetAnyPerpendicularDirection(float3 direction)
        {
            var reference = math.abs(direction.y) < 0.85f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            var perpendicular = math.cross(direction, reference);
            return math.lengthsq(perpendicular) > 0.0001f ? math.normalize(perpendicular) : new float3(1f, 0f, 0f);
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
