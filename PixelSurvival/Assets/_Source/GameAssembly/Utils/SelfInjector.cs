using GameAssembly.Utils.Data;
using UnityEngine;
using Utils;

namespace GameAssembly.Utils
{
    public class SelfInjector : MonoBehaviour
    {
        [SerializeField] private SelfInjectMethod injectMethod;

        private void Awake()
        {
            if (injectMethod == SelfInjectMethod.AWAKE)
                ObjectInjector.Inject(gameObject);
        }

        private void Start()
        {
            if (injectMethod == SelfInjectMethod.START)
                ObjectInjector.Inject(gameObject);
        }
    }
}