using GameAssembly.ItemsSystem;
using UnityEngine;

namespace GameAssembly.Utils
{
    public class StaticBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            ItemRegistry.Initialize();
            BehaviourRegister.Initialize();
        }
    }
}