using System.Collections.Generic;
using UnityEngine;
using Utils;

public class CaveGenerator : MonoBehaviour
{
    private const float GRID_CENTER_OFFSET = 0.5f;

    [SerializeField] private float _voxelSize = 1f;
    [SerializeField] private Vector3 _volumeSize = new Vector3(50, 30, 50);
    [SerializeField, Range(0f, 1f)] private float _threshold = 0.55f;
    [SerializeField] private Vector3 _noiseScale = new Vector3(0.08f, 0.08f, 0.08f);
    [SerializeField] private int _seed = 1337;
    [SerializeField, Min(1)] private int _octaves = 4;
    [SerializeField, Min(1f)] private float _lacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float _persistence = 0.5f;
    [SerializeField] private Vector3 _offset = Vector3.zero;
    [SerializeField] private bool _invertSelection = false;
    [SerializeField] private string _caveRootName = "CaveVoxels";

    private Transform _caveRoot;


    public void PickRoomPositions()
    {
        //ChunkCenterPickerUtils.GetChunkCenters()
    }
    
    public void GenerateCave()
    {
        ClearCave();

        _caveRoot = new GameObject(_caveRootName).transform;
        _caveRoot.SetParent(transform, false);

        var generationBounds = new Bounds(transform.position, _volumeSize);

        var positions = NoiseUtils.GetVoxelCenters(
            generationBounds,
            _voxelSize,
            _threshold,
            _noiseScale,
            _seed,
            _octaves,
            _lacunarity,
            _persistence,
            _offset
        );

        if (_invertSelection)
        {
            var allCenters = EnumerateGridCenters(generationBounds, _voxelSize);
            var selectedSet = new HashSet<Vector3>(positions);
            var inverted = new List<Vector3>(Mathf.Max(0, allCenters.Count - positions.Length));
            foreach (var center in allCenters)
                if (!selectedSet.Contains(center))
                    inverted.Add(center);
            positions = inverted.ToArray();
        }

        foreach (var position in positions)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_caveRoot, true);
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * _voxelSize;
        }
    }

    public void ClearCave()
    {
        if (_caveRoot == null)
        {
            var foundTransform = transform.Find(_caveRootName);
            if (foundTransform != null) _caveRoot = foundTransform;
        }

        if (_caveRoot != null)
        {
            if (Application.isPlaying) Destroy(_caveRoot.gameObject);
            else DestroyImmediate(_caveRoot.gameObject);
            _caveRoot = null;
        }
    }

    private static List<Vector3> EnumerateGridCenters(Bounds bounds, float voxelSize)
    {
        var gridCenters = new List<Vector3>();
        var boundsMin = bounds.min;
        var boundsMax = bounds.max;

        var cellCountX = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / voxelSize));
        var cellCountY = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / voxelSize));
        var cellCountZ = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / voxelSize));

        for (var yIndex = 0; yIndex < cellCountY; yIndex++)
        {
            var centerY = boundsMin.y + (yIndex + GRID_CENTER_OFFSET) * voxelSize;
            if (centerY > boundsMax.y) continue;

            for (var xIndex = 0; xIndex < cellCountX; xIndex++)
            {
                var centerX = boundsMin.x + (xIndex + GRID_CENTER_OFFSET) * voxelSize;
                if (centerX > boundsMax.x) continue;

                for (var zIndex = 0; zIndex < cellCountZ; zIndex++)
                {
                    var centerZ = boundsMin.z + (zIndex + GRID_CENTER_OFFSET) * voxelSize;
                    if (centerZ > boundsMax.z) continue;

                    gridCenters.Add(new Vector3(centerX, centerY, centerZ));
                }
            }
        }

        return gridCenters;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireCube(transform.position, _volumeSize);
    }
#endif
}