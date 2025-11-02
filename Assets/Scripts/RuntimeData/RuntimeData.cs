using UnityEngine;

namespace RuntimeData
{
    public abstract class RuntimeData<T> : MonoBehaviour where T : RuntimeData<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
            
                var found = FindFirstObjectByType<T>();
                if (found != null)
                {
                    _instance = found;
                    return _instance;
                }

                var newInstance = new GameObject($"[RuntimeData] {typeof(T).Name}");
                _instance = newInstance.AddComponent<T>();
                DontDestroyOnLoad(newInstance);
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
    }
}