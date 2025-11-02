using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    public static class ChunkCenterPickerUtils
    {
        private const float CENTER_OFFSET = 0.5f;

        public static Vector3[] GetChunkCenters(Bounds bounds, Vector3 chunkSize, Vector3? alignmentOrigin = null)
        {
            if (chunkSize.x <= 0f || chunkSize.y <= 0f || chunkSize.z <= 0f)
                throw new ArgumentOutOfRangeException(nameof(chunkSize));

            var origin = alignmentOrigin ?? Vector3.zero;
            var boundsMin = bounds.min;
            var boundsMax = bounds.max;

            var startIndexX = Mathf.FloorToInt((boundsMin.x - origin.x) / chunkSize.x);
            var endIndexX = Mathf.FloorToInt((boundsMax.x - origin.x) / chunkSize.x);
            var startIndexY = Mathf.FloorToInt((boundsMin.y - origin.y) / chunkSize.y);
            var endIndexY = Mathf.FloorToInt((boundsMax.y - origin.y) / chunkSize.y);
            var startIndexZ = Mathf.FloorToInt((boundsMin.z - origin.z) / chunkSize.z);
            var endIndexZ = Mathf.FloorToInt((boundsMax.z - origin.z) / chunkSize.z);

            var chunkCountX = Mathf.Max(0, endIndexX - startIndexX + 1);
            var chunkCountY = Mathf.Max(0, endIndexY - startIndexY + 1);
            var chunkCountZ = Mathf.Max(0, endIndexZ - startIndexZ + 1);

            var centers = new List<Vector3>(Mathf.Max(0, chunkCountX * chunkCountY * chunkCountZ));

            for (var yIndex = startIndexY; yIndex <= endIndexY; yIndex++)
            {
                var centerY = origin.y + (yIndex + CENTER_OFFSET) * chunkSize.y;
                if (centerY < boundsMin.y || centerY > boundsMax.y) continue;

                for (var xIndex = startIndexX; xIndex <= endIndexX; xIndex++)
                {
                    var centerX = origin.x + (xIndex + CENTER_OFFSET) * chunkSize.x;
                    if (centerX < boundsMin.x || centerX > boundsMax.x) continue;

                    for (var zIndex = startIndexZ; zIndex <= endIndexZ; zIndex++)
                    {
                        var centerZ = origin.z + (zIndex + CENTER_OFFSET) * chunkSize.z;
                        if (centerZ < boundsMin.z || centerZ > boundsMax.z) continue;

                        centers.Add(new Vector3(centerX, centerY, centerZ));
                    }
                }
            }

            return centers.ToArray();
        }
    }
}