using CaveCreation.GenerationData;
using RuntimeData;
using UnityEditor;
using UnityEngine;

namespace Presets
{
    public static class PresetsFactory
    {
        public const string DEFAULT_PRESET_PATH = "Assets/Scripts/Presets/Default.asset";
        private const string SAVES_PATH = "Assets/Scripts/Presets/Saves/";

        public static CaveCreationDataSO GenerateRandom()
        {
            PresetsFactoryRuntimeData.Instance.RandomPresetCache = CaveCreationDataSO.CreateRandomizedInstance();
            PresetsFactoryRuntimeData.Instance.RandomPresetCache.name = "Random";
            return PresetsFactoryRuntimeData.Instance.RandomPresetCache;
        }

        public static void SaveRandom()
        {
            var rndCache = PresetsFactoryRuntimeData.Instance.RandomPresetCache;
            if (rndCache == null)
            {
                Debug.LogError($"Random preset cache is null, cannot save");
                return;
            }

            var uniquePath = GenerateUniqueAssetPath(rndCache);
            AssetDatabase.CreateAsset(rndCache, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Random preset saved at path: {uniquePath}", rndCache);
        }

        private static string GenerateUniqueAssetPath(CaveCreationDataSO asset)
        {
            var assetName = $"Random_{Mathf.RoundToInt(asset.CaveSize.x)}x{Mathf.RoundToInt(asset.CaveSize.y)}x{Mathf.RoundToInt(asset.CaveSize.z)}_{asset.Seed}.asset";
            var assetPath = SAVES_PATH + assetName;
            return AssetDatabase.GenerateUniqueAssetPath(assetPath);
        }
    }
}
