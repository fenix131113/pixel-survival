using GameAssembly.ItemsSystem;
using UnityEngine;

namespace GameAssembly.Utils
{
    public class StaticBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            ItemRegistry.Initialize();
            BehaviourRegister.Initialize();
        }
    }
}