using CaveCreation.GenerationData;
using Presets;
using UnityEditor;

namespace RuntimeData
{
    public class PresetsFactoryRuntimeData : RuntimeData<PresetsFactoryRuntimeData>
    {
        private CaveCreationDataSO _defaultCache;
        public CaveCreationDataSO DefaultPreset
        {
            get
            {
                if (_defaultCache == null)
                    _defaultCache = AssetDatabase.LoadAssetAtPath<CaveCreationDataSO>(PresetsFactory.DEFAULT_PRESET_PATH);

                return _defaultCache;
            }
        }

        public CaveCreationDataSO RandomPresetCache;
    }
}