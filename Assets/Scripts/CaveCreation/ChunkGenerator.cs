using System;
using System.Collections.Generic;
using CaveCreation.Data;
using CaveCreation.GenerationData;
using CaveCreation.Jobs;
using Cysharp.Threading.Tasks;
using RuntimeData;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CaveCreation
{
    [Serializable]
    public class ChunkGenerator
    {
        private CaveCreationDataSO _generateDataSO;

        public void Init(CaveCreationDataSO generateDataSO)
        {
            _generateDataSO = generateDataSO;
        }

        public async UniTask GenerateCave()
        {
            var chunkSize = GetSafeChunkSize();
            var voxelSize = _generateDataSO.VoxelSize <= 0f ? 1f : _generateDataSO.VoxelSize;
            var caveSize = GetSafeCaveSize();
            var caveBoundsMin = new float3(_generateDataSO.CaveOrigin) - caveSize * 0.5f;
            var caveBoundsMax = caveBoundsMin + caveSize;
            var random = new Unity.Mathematics.Random((uint)(_generateDataSO.Seed == 0 ? 1 : _generateDataSO.Seed));

            var rooms = BuildRoomFeatures(caveBoundsMin, caveBoundsMax, voxelSize, ref random);
            var tunnels = BuildTunnelFeatures(rooms, caveBoundsMin, caveBoundsMax, voxelSize, ref random);
            var chunkBounds = BuildActiveChunkBounds(caveBoundsMin, caveBoundsMax, chunkSize, voxelSize, rooms, tunnels);

            if (chunkBounds.Count == 0)
            {
                CaveRuntimeData.Instance.Chunks = new List<ChunkData>();
                return;
            }

            var chunkCount = chunkBounds.Count;
            var boundsMinArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var boundsMaxArray = new NativeArray<float3>(chunkCount, Allocator.Persistent);
            var voxelStartIndexArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var voxelCountArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var roomCenterArray = new NativeArray<float3>(rooms.Count, Allocator.Persistent);
            var roomRadiusArray = new NativeArray<float3>(rooms.Count, Allocator.Persistent);
            var tunnelStartArray = new NativeArray<float3>(tunnels.Count, Allocator.Persistent);
            var tunnelEndArray = new NativeArray<float3>(tunnels.Count, Allocator.Persistent);
            var tunnelRadiusArray = new NativeArray<float>(tunnels.Count, Allocator.Persistent);
            var roomIndexStartArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var roomIndexCountArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var tunnelIndexStartArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var tunnelIndexCountArray = new NativeArray<int>(chunkCount, Allocator.Persistent);
            var roomIndexList = new List<int>(chunkCount);
            var tunnelIndexList = new List<int>(chunkCount);

            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                roomCenterArray[roomIndex] = rooms[roomIndex].Center;
                roomRadiusArray[roomIndex] = rooms[roomIndex].Radius;
            }

            for (var tunnelIndex = 0; tunnelIndex < tunnels.Count; tunnelIndex++)
            {
                tunnelStartArray[tunnelIndex] = tunnels[tunnelIndex].Start;
                tunnelEndArray[tunnelIndex] = tunnels[tunnelIndex].End;
                tunnelRadiusArray[tunnelIndex] = tunnels[tunnelIndex].Radius;
            }

            var totalVoxelCount = 0;
            var featureMargin = GetFeatureSurfaceMargin(voxelSize);
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var chunk = chunkBounds[chunkIndex];
                var generationBounds = ExpandChunkBoundsForMeshing(
                    chunk.Min,
                    chunk.Max,
                    caveBoundsMin,
                    caveBoundsMax,
                    voxelSize);
                boundsMinArray[chunkIndex] = generationBounds.Min;
                boundsMaxArray[chunkIndex] = generationBounds.Max;

                var voxelCount = CalculateVoxelCount(generationBounds.Max - generationBounds.Min, voxelSize);
                voxelStartIndexArray[chunkIndex] = totalVoxelCount;
                voxelCountArray[chunkIndex] = voxelCount;
                totalVoxelCount += voxelCount;

                AddRelevantFeatureIndices(
                    generationBounds,
                    rooms,
                    tunnels,
                    featureMargin,
                    roomIndexList,
                    tunnelIndexList,
                    roomIndexStartArray,
                    roomIndexCountArray,
                    tunnelIndexStartArray,
                    tunnelIndexCountArray,
                    chunkIndex);
            }

            var roomIndexArray = CopyToNativeArray(roomIndexList);
            var tunnelIndexArray = CopyToNativeArray(tunnelIndexList);
            var voxelsNative = new NativeArray<float4>(totalVoxelCount, Allocator.Persistent);
            var data = new MultipleChunkGenerateData(
                boundsMinArray,
                boundsMaxArray,
                voxelSize,
                _generateDataSO.NoiseScale,
                _generateDataSO.Seed,
                _generateDataSO.Octaves,
                _generateDataSO.Lacunarity,
                _generateDataSO.Persistence,
                _generateDataSO.IsoLevel,
                roomCenterArray,
                roomRadiusArray,
                tunnelStartArray,
                tunnelEndArray,
                tunnelRadiusArray,
                roomIndexArray,
                roomIndexStartArray,
                roomIndexCountArray,
                tunnelIndexArray,
                tunnelIndexStartArray,
                tunnelIndexCountArray,
                _generateDataSO.SdfSurfaceThicknessInVoxels,
                _generateDataSO.SdfSmoothUnionInVoxels,
                _generateDataSO.SdfWallNoiseAmplitudeInVoxels,
                _generateDataSO.SdfWallNoiseScaleInVoxels,
                _generateDataSO.SdfWallLobeStrength,
                _generateDataSO.SdfWallLobeScaleMultiplier,
                _generateDataSO.SdfTunnelLobeStrength,
                _generateDataSO.SdfTunnelLobeScaleMultiplier,
                _generateDataSO.SdfInteriorNoiseStrength,
                _generateDataSO.SdfInteriorNoiseCutoff,
                _generateDataSO.SdfInteriorWallClearanceInVoxels,
                _generateDataSO.SdfInteriorTunnelClearanceInVoxels,
                _generateDataSO.SdfInteriorClearanceBlendInVoxels,
                voxelStartIndexArray,
                voxelCountArray);

            var job = new ChunkGenerateJobMultiple(voxelsNative, data);
            var handle = job.Schedule(chunkCount, 1);

            await UniTask.WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            var chunks = new List<ChunkData>(chunkCount);
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var voxelCount = voxelCountArray[chunkIndex];
                var chunkVoxelsManaged = new VoxelData[voxelCount];
                var startIndex = voxelStartIndexArray[chunkIndex];

                for (var voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                {
                    var p = voxelsNative[startIndex + voxelIndex];
                    chunkVoxelsManaged[voxelIndex] = new VoxelData(p.xyz, p.w);
                }

                chunks.Add(new ChunkData(index: chunkIndex, voxels: chunkVoxelsManaged));
            }

            voxelsNative.Dispose();
            boundsMinArray.Dispose();
            boundsMaxArray.Dispose();
            voxelStartIndexArray.Dispose();
            voxelCountArray.Dispose();
            roomCenterArray.Dispose();
            roomRadiusArray.Dispose();
            tunnelStartArray.Dispose();
            tunnelEndArray.Dispose();
            tunnelRadiusArray.Dispose();
            roomIndexArray.Dispose();
            roomIndexStartArray.Dispose();
            roomIndexCountArray.Dispose();
            tunnelIndexArray.Dispose();
            tunnelIndexStartArray.Dispose();
            tunnelIndexCountArray.Dispose();

            CaveRuntimeData.Instance.Chunks = chunks;
        }

        private float3 GetSafeChunkSize()
        {
            return new float3(
                Mathf.Max(1f, _generateDataSO.ChunkSize.x),
                Mathf.Max(1f, _generateDataSO.ChunkSize.y),
                Mathf.Max(1f, _generateDataSO.ChunkSize.z));
        }

        private float3 GetSafeCaveSize()
        {
            return new float3(
                Mathf.Max(1f, _generateDataSO.CaveSize.x),
                Mathf.Max(1f, _generateDataSO.CaveSize.y),
                Mathf.Max(1f, _generateDataSO.CaveSize.z));
        }

        private static int CalculateVoxelCount(float3 chunkSize, float voxelSize)
        {
            var normalizedVoxelSize = voxelSize <= 0f ? 1f : voxelSize;
            var pointsCountX = math.max(1, (int)math.round(chunkSize.x / normalizedVoxelSize)) + 1;
            var pointsCountY = math.max(1, (int)math.round(chunkSize.y / normalizedVoxelSize)) + 1;
            var pointsCountZ = math.max(1, (int)math.round(chunkSize.z / normalizedVoxelSize)) + 1;

            return pointsCountX * pointsCountY * pointsCountZ;
        }

        private static ChunkBounds ExpandChunkBoundsForMeshing(
            float3 chunkMin,
            float3 chunkMax,
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            float voxelSize)
        {
            var padding = new float3(math.max(0.001f, voxelSize));
            return new ChunkBounds(
                math.max(caveBoundsMin, chunkMin - padding),
                math.min(caveBoundsMax, chunkMax + padding));
        }

        private void AddRelevantFeatureIndices(
            ChunkBounds generationBounds,
            IReadOnlyList<RoomFeature> rooms,
            IReadOnlyList<TunnelFeature> tunnels,
            float featureMargin,
            List<int> roomIndexList,
            List<int> tunnelIndexList,
            NativeArray<int> roomIndexStartArray,
            NativeArray<int> roomIndexCountArray,
            NativeArray<int> tunnelIndexStartArray,
            NativeArray<int> tunnelIndexCountArray,
            int chunkIndex)
        {
            roomIndexStartArray[chunkIndex] = roomIndexList.Count;
            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                if (RoomAffectsChunk(generationBounds, rooms[roomIndex], featureMargin))
                    roomIndexList.Add(roomIndex);
            }

            roomIndexCountArray[chunkIndex] = roomIndexList.Count - roomIndexStartArray[chunkIndex];

            tunnelIndexStartArray[chunkIndex] = tunnelIndexList.Count;
            for (var tunnelIndex = 0; tunnelIndex < tunnels.Count; tunnelIndex++)
            {
                if (TunnelAffectsChunk(generationBounds, tunnels[tunnelIndex], featureMargin))
                    tunnelIndexList.Add(tunnelIndex);
            }

            tunnelIndexCountArray[chunkIndex] = tunnelIndexList.Count - tunnelIndexStartArray[chunkIndex];
        }

        private static NativeArray<int> CopyToNativeArray(IReadOnlyList<int> source)
        {
            var array = new NativeArray<int>(Mathf.Max(1, source.Count), Allocator.Persistent);
            for (var i = 0; i < source.Count; i++)
                array[i] = source[i];

            return array;
        }

        private static bool RoomAffectsChunk(ChunkBounds chunk, RoomFeature room, float margin)
        {
            var outerRadius = room.Radius + new float3(math.max(0f, margin));
            return AabbOverlaps(chunk.Min, chunk.Max, room.Center - outerRadius, room.Center + outerRadius);
        }

        private static bool TunnelAffectsChunk(ChunkBounds chunk, TunnelFeature tunnel, float margin)
        {
            var tunnelMargin = tunnel.Radius + math.max(0f, margin);
            return AabbOverlaps(
                chunk.Min,
                chunk.Max,
                math.min(tunnel.Start, tunnel.End) - new float3(tunnelMargin),
                math.max(tunnel.Start, tunnel.End) + new float3(tunnelMargin));
        }

        private List<RoomFeature> BuildRoomFeatures(
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            float voxelSize,
            ref Unity.Mathematics.Random random)
        {
            var totalRoomCount =
                Mathf.Max(0, _generateDataSO.LargeRoomChunks.Amount) +
                Mathf.Max(0, _generateDataSO.MediumRoomChunks.Amount) +
                Mathf.Max(0, _generateDataSO.SmallRoomChunks.Amount);
            var rooms = new List<RoomFeature>(totalRoomCount);

            AddRoomFeatures(_generateDataSO.LargeRoomChunks, caveBoundsMin, caveBoundsMax, voxelSize, ref random, rooms);
            AddRoomFeatures(_generateDataSO.MediumRoomChunks, caveBoundsMin, caveBoundsMax, voxelSize, ref random, rooms);
            AddRoomFeatures(_generateDataSO.SmallRoomChunks, caveBoundsMin, caveBoundsMax, voxelSize, ref random, rooms);

            return rooms;
        }

        private void AddRoomFeatures(
            RoomChunkTypeSettings settings,
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            float voxelSize,
            ref Unity.Mathematics.Random random,
            List<RoomFeature> rooms)
        {
            var amount = Mathf.Max(0, settings.Amount);
            if (amount == 0)
                return;

            var sizeMin = Mathf.Max(voxelSize * 2f, Mathf.Min(settings.SizeRange.x, settings.SizeRange.y));
            var sizeMax = Mathf.Max(sizeMin, Mathf.Max(settings.SizeRange.x, settings.SizeRange.y));
            var heightScaleMin = Mathf.Max(0.1f, Mathf.Min(_generateDataSO.RoomChunkHeightScaleRange.x,
                _generateDataSO.RoomChunkHeightScaleRange.y));
            var heightScaleMax = Mathf.Max(heightScaleMin, Mathf.Max(_generateDataSO.RoomChunkHeightScaleRange.x,
                _generateDataSO.RoomChunkHeightScaleRange.y));
            var attempts = Mathf.Max(1, _generateDataSO.RoomChunkPlacementAttempts);
            var caveSize = caveBoundsMax - caveBoundsMin;
            var placementMargin = GetFeaturePlacementMargin(voxelSize);

            for (var roomIndex = 0; roomIndex < amount; roomIndex++)
            {
                var bestRoom = default(RoomFeature);
                var bestScore = float.NegativeInfinity;

                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    var baseSize = random.NextFloat(sizeMin, sizeMax);
                    var size = new float3(
                        baseSize * random.NextFloat(0.85f, 1.2f),
                        baseSize * random.NextFloat(heightScaleMin, heightScaleMax),
                        baseSize * random.NextFloat(0.85f, 1.2f));
                    size = math.min(size, math.max(new float3(voxelSize * 2f), caveSize - new float3(placementMargin * 2f)));

                    var radius = math.max(size * 0.5f, new float3(voxelSize));
                    var center = RandomPointInsideBounds(
                        caveBoundsMin + radius + new float3(placementMargin),
                        caveBoundsMax - radius - new float3(placementMargin),
                        ref random);
                    var candidate = new RoomFeature(center, radius);
                    var score = ScoreRoomPlacement(candidate, rooms);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRoom = candidate;
                    }
                }

                rooms.Add(bestRoom);
            }
        }

        private List<TunnelFeature> BuildTunnelFeatures(
            IReadOnlyList<RoomFeature> rooms,
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            float voxelSize,
            ref Unity.Mathematics.Random random)
        {
            var tunnels = new List<TunnelFeature>();
            if (!_generateDataSO.ConnectRoomsWithSdfTunnels || rooms.Count < 2)
                return tunnels;

            var connections = BuildMinimumSpanningTree(rooms);
            var radius = math.max(voxelSize * 1.5f, _generateDataSO.TunnelRadius);
            var segmentLength = math.max(voxelSize * 2f, _generateDataSO.TunnelSegmentLength);

            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                AddTunnelFeatures(
                    rooms[connection.From],
                    rooms[connection.To],
                    caveBoundsMin,
                    caveBoundsMax,
                    radius,
                    segmentLength,
                    ref random,
                    tunnels);
            }

            return tunnels;
        }

        private void AddTunnelFeatures(
            RoomFeature fromRoom,
            RoomFeature toRoom,
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            float radius,
            float segmentLength,
            ref Unity.Mathematics.Random random,
            List<TunnelFeature> tunnels)
        {
            var roomDelta = toRoom.Center - fromRoom.Center;
            var roomDistance = math.length(roomDelta);
            if (roomDistance <= 0.001f)
                return;

            var roomDirection = roomDelta / roomDistance;
            var start = GetRoomSurfacePoint(fromRoom, roomDirection);
            var end = GetRoomSurfacePoint(toRoom, -roomDirection);
            var delta = end - start;
            var length = math.length(delta);
            if (length <= 0.001f)
                return;

            var direction = delta / length;
            var curveAmplitude = math.min(math.max(0f, _generateDataSO.TunnelCurveAmplitude), length * 0.45f);
            var curvePointSpacing = math.max(1f, _generateDataSO.TunnelCurvePointSpacing);
            var bendPointCount = curveAmplitude > 0.001f
                ? math.max(1, Mathf.CeilToInt(length / curvePointSpacing) - 1)
                : 0;
            var segmentCount = math.max(
                1,
                math.max(Mathf.CeilToInt(length / segmentLength), bendPointCount * 4 + 1));
            GetPerpendicularBasis(direction, out var sideA, out var sideB);

            var curvePoints = BuildTunnelCurvePoints(
                start,
                end,
                delta,
                sideA,
                sideB,
                curveAmplitude,
                bendPointCount,
                caveBoundsMin,
                caveBoundsMax,
                ref random);

            for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                var t0 = segmentIndex / (float)segmentCount;
                var t1 = (segmentIndex + 1f) / segmentCount;
                var segmentStart = math.clamp(EvaluateTunnelCurve(curvePoints, t0), caveBoundsMin, caveBoundsMax);
                var segmentEnd = math.clamp(EvaluateTunnelCurve(curvePoints, t1), caveBoundsMin, caveBoundsMax);

                if (math.distancesq(segmentStart, segmentEnd) <= 0.0001f)
                    continue;

                tunnels.Add(new TunnelFeature(segmentStart, segmentEnd, radius));
            }
        }

        private static List<float3> BuildTunnelCurvePoints(
            float3 start,
            float3 end,
            float3 delta,
            float3 sideA,
            float3 sideB,
            float curveAmplitude,
            int bendPointCount,
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            ref Unity.Mathematics.Random random)
        {
            var points = new List<float3>(bendPointCount + 2) { start };
            var previousOffset = float3.zero;

            for (var pointIndex = 1; pointIndex <= bendPointCount; pointIndex++)
            {
                var t = pointIndex / (float)(bendPointCount + 1);
                var entranceTaper = math.sin(t * math.PI);
                var randomOffset = RandomCurveOffset(sideA, sideB, curveAmplitude * entranceTaper, ref random);
                var offset = math.lerp(previousOffset, randomOffset, 0.7f);
                var point = math.clamp(start + delta * t + offset, caveBoundsMin, caveBoundsMax);

                points.Add(point);
                previousOffset = offset;
            }

            points.Add(end);
            return points;
        }

        private List<ChunkBounds> BuildActiveChunkBounds(
            float3 caveBoundsMin,
            float3 caveBoundsMax,
            float3 chunkSize,
            float voxelSize,
            IReadOnlyList<RoomFeature> rooms,
            IReadOnlyList<TunnelFeature> tunnels)
        {
            var chunkCounts = new int3(
                math.max(1, (int)math.ceil((caveBoundsMax.x - caveBoundsMin.x) / chunkSize.x)),
                math.max(1, (int)math.ceil((caveBoundsMax.y - caveBoundsMin.y) / chunkSize.y)),
                math.max(1, (int)math.ceil((caveBoundsMax.z - caveBoundsMin.z) / chunkSize.z)));
            var chunks = new List<ChunkBounds>();
            var surfaceMargin = GetFeatureSurfaceMargin(voxelSize);

            for (var x = 0; x < chunkCounts.x; x++)
            for (var y = 0; y < chunkCounts.y; y++)
            for (var z = 0; z < chunkCounts.z; z++)
            {
                var min = caveBoundsMin + new float3(x * chunkSize.x, y * chunkSize.y, z * chunkSize.z);
                var max = math.min(min + chunkSize, caveBoundsMax);

                if (ChunkMayContainSurface(min, max, rooms, tunnels, surfaceMargin) ||
                    ChunkMayContainInteriorStructures(min, max, rooms, voxelSize))
                    chunks.Add(new ChunkBounds(min, max));
            }

            return chunks;
        }

        private float GetFeaturePlacementMargin(float voxelSize)
        {
            var sdfBand = math.max(1f, _generateDataSO.SdfSurfaceThicknessInVoxels);
            var noiseBand = math.max(0f, _generateDataSO.SdfWallNoiseAmplitudeInVoxels);
            return voxelSize * (sdfBand + noiseBand + 2f);
        }

        private float GetFeatureSurfaceMargin(float voxelSize)
        {
            var sdfBand = math.max(1f, _generateDataSO.SdfSurfaceThicknessInVoxels);
            var noiseBand = math.max(0f, _generateDataSO.SdfWallNoiseAmplitudeInVoxels);
            var unionBand = math.max(0f, _generateDataSO.SdfSmoothUnionInVoxels);
            return voxelSize * (sdfBand + noiseBand * 2f + unionBand + 3f);
        }

        private bool ChunkMayContainInteriorStructures(
            float3 chunkMin,
            float3 chunkMax,
            IReadOnlyList<RoomFeature> rooms,
            float voxelSize)
        {
            if (_generateDataSO.SdfInteriorNoiseStrength <= 0f)
                return false;

            var clearance = voxelSize * math.max(0f, _generateDataSO.SdfInteriorWallClearanceInVoxels);
            for (var i = 0; i < rooms.Count; i++)
            {
                var innerRadius = rooms[i].Radius - new float3(clearance);
                if (math.cmin(innerRadius) <= voxelSize)
                    continue;

                if (AabbOverlapsEllipsoid(chunkMin, chunkMax, rooms[i].Center, innerRadius))
                    return true;
            }

            return false;
        }

        private static bool ChunkMayContainSurface(
            float3 chunkMin,
            float3 chunkMax,
            IReadOnlyList<RoomFeature> rooms,
            IReadOnlyList<TunnelFeature> tunnels,
            float margin)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                if (ChunkMayContainRoomSurface(chunkMin, chunkMax, rooms[i], margin))
                    return true;
            }

            for (var i = 0; i < tunnels.Count; i++)
            {
                if (AabbOverlaps(
                        chunkMin,
                        chunkMax,
                        math.min(tunnels[i].Start, tunnels[i].End) - new float3(tunnels[i].Radius + margin),
                        math.max(tunnels[i].Start, tunnels[i].End) + new float3(tunnels[i].Radius + margin)))
                    return true;
            }

            return false;
        }

        private static bool ChunkMayContainRoomSurface(float3 chunkMin, float3 chunkMax, RoomFeature room, float margin)
        {
            var marginVector = new float3(math.max(0f, margin));
            var outerRadius = room.Radius + marginVector;
            if (!AabbOverlaps(chunkMin, chunkMax, room.Center - outerRadius, room.Center + outerRadius))
                return false;

            var innerRadius = math.max(room.Radius - marginVector, new float3(0.001f));
            return !AabbFullyInsideEllipsoid(chunkMin, chunkMax, room.Center, innerRadius);
        }

        private static float ScoreRoomPlacement(RoomFeature candidate, IReadOnlyList<RoomFeature> rooms)
        {
            if (rooms.Count == 0)
                return 0f;

            var candidateRadius = math.length(candidate.Radius);
            var score = float.PositiveInfinity;

            for (var i = 0; i < rooms.Count; i++)
            {
                var radius = math.length(rooms[i].Radius);
                score = math.min(score, math.distance(candidate.Center, rooms[i].Center) - candidateRadius - radius);
            }

            return score;
        }

        private static List<RoomConnection> BuildMinimumSpanningTree(IReadOnlyList<RoomFeature> rooms)
        {
            var connections = new List<RoomConnection>(math.max(0, rooms.Count - 1));
            if (rooms.Count < 2)
                return connections;

            var connected = new bool[rooms.Count];
            connected[0] = true;
            var connectedCount = 1;

            while (connectedCount < rooms.Count)
            {
                var bestFrom = -1;
                var bestTo = -1;
                var bestDistanceSq = float.PositiveInfinity;

                for (var from = 0; from < rooms.Count; from++)
                {
                    if (!connected[from])
                        continue;

                    for (var to = 0; to < rooms.Count; to++)
                    {
                        if (connected[to])
                            continue;

                        var distanceSq = math.distancesq(rooms[from].Center, rooms[to].Center);
                        if (distanceSq >= bestDistanceSq)
                            continue;

                        bestFrom = from;
                        bestTo = to;
                        bestDistanceSq = distanceSq;
                    }
                }

                if (bestTo < 0)
                    break;

                connections.Add(new RoomConnection(bestFrom, bestTo));
                connected[bestTo] = true;
                connectedCount++;
            }

            return connections;
        }

        private static float3 RandomPointInsideBounds(
            float3 min,
            float3 max,
            ref Unity.Mathematics.Random random)
        {
            var center = (min + max) * 0.5f;
            return new float3(
                min.x <= max.x ? random.NextFloat(min.x, max.x) : center.x,
                min.y <= max.y ? random.NextFloat(min.y, max.y) : center.y,
                min.z <= max.z ? random.NextFloat(min.z, max.z) : center.z);
        }

        private static float3 GetRoomSurfacePoint(RoomFeature room, float3 direction)
        {
            var directionLengthSq = math.lengthsq(direction);
            if (directionLengthSq <= 0.0001f)
                return room.Center;

            direction = math.normalize(direction);
            var normalizedDirection = direction / math.max(room.Radius, new float3(0.001f));
            var distanceToSurface = 1f / math.max(0.0001f, math.length(normalizedDirection));
            return room.Center + direction * distanceToSurface;
        }

        private static float3 EvaluateTunnelCurve(IReadOnlyList<float3> points, float t)
        {
            if (points.Count == 0)
                return float3.zero;

            if (points.Count == 1)
                return points[0];

            t = math.saturate(t);
            var lastPointIndex = points.Count - 1;
            var scaledT = t * lastPointIndex;
            var segmentIndex = math.min(lastPointIndex - 1, (int)math.floor(scaledT));
            var localT = scaledT - segmentIndex;

            var p0 = points[math.max(0, segmentIndex - 1)];
            var p1 = points[segmentIndex];
            var p2 = points[segmentIndex + 1];
            var p3 = points[math.min(lastPointIndex, segmentIndex + 2)];

            return EvaluateCatmullRom(p0, p1, p2, p3, localT);
        }

        private static float3 EvaluateCatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float3 RandomCurveOffset(
            float3 sideA,
            float3 sideB,
            float amplitude,
            ref Unity.Mathematics.Random random)
        {
            if (amplitude <= 0f)
                return float3.zero;

            return sideA * random.NextFloat(-amplitude, amplitude) +
                   sideB * random.NextFloat(-amplitude, amplitude);
        }

        private static void GetPerpendicularBasis(float3 direction, out float3 sideA, out float3 sideB)
        {
            var reference = math.abs(direction.y) < 0.85f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            sideA = math.cross(direction, reference);
            sideA = math.lengthsq(sideA) > 0.0001f ? math.normalize(sideA) : new float3(1f, 0f, 0f);
            sideB = math.cross(direction, sideA);
            sideB = math.lengthsq(sideB) > 0.0001f ? math.normalize(sideB) : new float3(0f, 1f, 0f);
        }

        private static bool AabbOverlaps(float3 aMin, float3 aMax, float3 bMin, float3 bMax)
        {
            return aMin.x <= bMax.x && aMax.x >= bMin.x &&
                   aMin.y <= bMax.y && aMax.y >= bMin.y &&
                   aMin.z <= bMax.z && aMax.z >= bMin.z;
        }

        private static bool AabbFullyInsideEllipsoid(float3 aabbMin, float3 aabbMax, float3 center, float3 radius)
        {
            var safeRadius = math.max(radius, new float3(0.001f));
            for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                var corner = new float3(
                    (cornerIndex & 1) == 0 ? aabbMin.x : aabbMax.x,
                    (cornerIndex & 2) == 0 ? aabbMin.y : aabbMax.y,
                    (cornerIndex & 4) == 0 ? aabbMin.z : aabbMax.z);

                if (math.length((corner - center) / safeRadius) > 1f)
                    return false;
            }

            return true;
        }

        private static bool AabbOverlapsEllipsoid(float3 aabbMin, float3 aabbMax, float3 center, float3 radius)
        {
            var safeRadius = math.max(radius, new float3(0.001f));
            var closestPoint = math.clamp(center, aabbMin, aabbMax);
            return math.length((closestPoint - center) / safeRadius) <= 1f;
        }

        private readonly struct ChunkBounds
        {
            public readonly float3 Min;
            public readonly float3 Max;

            public ChunkBounds(float3 min, float3 max)
            {
                Min = min;
                Max = max;
            }
        }

        private readonly struct RoomFeature
        {
            public readonly float3 Center;
            public readonly float3 Radius;

            public RoomFeature(float3 center, float3 radius)
            {
                Center = center;
                Radius = radius;
            }
        }

        private readonly struct TunnelFeature
        {
            public readonly float3 Start;
            public readonly float3 End;
            public readonly float Radius;

            public TunnelFeature(float3 start, float3 end, float radius)
            {
                Start = start;
                End = end;
                Radius = radius;
            }
        }

        private readonly struct RoomConnection
        {
            public readonly int From;
            public readonly int To;

            public RoomConnection(int from, int to)
            {
                From = from;
                To = to;
            }
        }
    }
}
