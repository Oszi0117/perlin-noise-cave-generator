using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CaveManagement
{
    public class CaveManager : MonoBehaviour
    {
        [SerializeField] private CaveGenerator _caveGenerator = new();

        public void GenerateNewCave()
        {
            _caveGenerator.GenerateCave().Forget();
        }
        
    }
}
