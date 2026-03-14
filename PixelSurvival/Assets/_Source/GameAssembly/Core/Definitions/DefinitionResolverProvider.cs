using UnityEngine;

namespace GameAssembly.Core.Definitions
{
    public static class DefinitionResolverProvider
    {
        private const string INDEX_RESOURCE_PATH = "Configs/DefinitionIndex";

        private static DefinitionResolver _resolver;

        public static DefinitionResolver Resolver
        {
            get
            {
                EnsureResolver();
                return _resolver;
            }
        }

        public static bool TryResolve<TDefinition>(string id, out TDefinition definition)
            where TDefinition : ScriptableObject
        {
            EnsureResolver();
            if (_resolver == null)
            {
                definition = null;
                return false;
            }

            return _resolver.TryResolve(id, out definition);
        }

        public static void Reload()
        {
            _resolver = null;
        }

        private static void EnsureResolver()
        {
            if (_resolver != null)
            {
                return;
            }

            var index = Resources.Load<DefinitionIndexSO>(INDEX_RESOURCE_PATH);
            
            if (!index)
            {
                Debug.LogError($"Definition index not found in Resources at '{INDEX_RESOURCE_PATH}'. Run Tools/Definitions/Rebuild Index.");
                return;
            }

            _resolver = new DefinitionResolver(index);
        }
    }
}