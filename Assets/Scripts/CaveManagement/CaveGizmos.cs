using RuntimeData;
using UnityEngine;

namespace CaveManagement
{
    public class CaveGizmos : MonoBehaviour
    {
        [SerializeField] private Color _gizmoColor = Color.green;
        [SerializeField] private float _voxelSize = 1f;

        private void OnDrawGizmos()
        {
            DrawCave();
        }
        
        private void DrawCave()
        {
            if (!Application.isPlaying) return;
            var runtime = CaveRuntimeData.Instance;
            if (runtime == null || runtime.Chunks == null) return;
            Gizmos.color = _gizmoColor;
            var cubeSize = Vector3.one * _voxelSize;

            foreach (var chunk in runtime.Chunks)
            {
                var voxels = chunk.Voxels;
                if (voxels == null) continue;

                foreach (var voxelData in voxels)
                {
                    var pos = new Vector3(voxelData.Position.x, voxelData.Position.y, voxelData.Position.z) * _voxelSize;
                    Gizmos.DrawCube(pos, cubeSize);
                }
            }
        }
    }
}