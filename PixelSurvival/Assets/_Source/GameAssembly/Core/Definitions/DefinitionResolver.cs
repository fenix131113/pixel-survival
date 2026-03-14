using UnityEngine;

namespace GameAssembly.Core.Definitions
{
    public sealed class DefinitionResolver
    {
        private readonly DefinitionIndexSO _index;

        public DefinitionResolver(DefinitionIndexSO index) => _index = index;

        public bool TryResolve<TDefinition>(string id, out TDefinition definition)
            where TDefinition : ScriptableObject
        {
            definition = null;
            if (!_index || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (!_index.TryGet(id, out var rawDefinition))
            {
                return false;
            }

            definition = rawDefinition as TDefinition;
            return definition;
        }
    }
}