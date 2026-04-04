using UnityEngine;

namespace Extensions
{
    public static class GameObjectExtensions
    {
        public static void UniversalDestroy(this GameObject gameObject)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(gameObject);
#else
            Object.Destroy(gameObject);
#endif
        }
    }
}