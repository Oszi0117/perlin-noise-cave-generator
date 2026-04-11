using CaveCreation;
using Cysharp.Threading.Tasks;
using Presets;
using RuntimeData;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PresetsFactoryPanel : MonoBehaviour
    {
        [SerializeField] private Button _initializeButton;
        [SerializeField] private GameObject _buttonsHolder;
        [SerializeField] private Button _generatePresetButton;
        [SerializeField] private Button _generateRandomPresetButton;
        [SerializeField] private Button _saveRandomPresetButton;
        private CaveManager _caveManager; 

        private void Start()
        {
            _initializeButton.onClick.AddListener(Initialize);
        }

        private void Initialize()
        {
            _caveManager = FindFirstObjectByType<CaveManager>();
            InitButtonCallbacks();
            _buttonsHolder.SetActive(true);
            _initializeButton.gameObject.SetActive(false);
            
            return;
            
            void InitButtonCallbacks()
            {
                _generatePresetButton.onClick.AddListener(()=> _caveManager.CreateCaveAsync(PresetsFactoryRuntimeData.Instance.DefaultPreset).Forget());
                _generateRandomPresetButton.onClick.AddListener(()=> _caveManager.CreateCaveAsync(PresetsFactory.GenerateRandom()).Forget());
                _saveRandomPresetButton.onClick.AddListener(PresetsFactory.SaveRandom);
            }
        }
    }
}