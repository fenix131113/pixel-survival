using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Utils
{
    public static class ObjectInjector
    {
        private static IObjectResolver _container;
        
        public static void Initialize(IObjectResolver container) => _container = container;

        public static void Inject(GameObject obj) => _container.InjectGameObject(obj);
        public static void Inject(object obj) => _container.Inject(obj);
    }
}