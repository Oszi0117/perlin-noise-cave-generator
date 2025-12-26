using CaveCreation.Data;
using UnityEngine;

namespace CaveCreation.Debugging
{
    public class VoxelDataHolder : MonoBehaviour
    {
        public Vector3 Position;
        public float Value;
    
        public void InitializeDataHolder(VoxelData data)
        {
            Position = data.Position;
            Value = data.Value;
        }
    }
}
