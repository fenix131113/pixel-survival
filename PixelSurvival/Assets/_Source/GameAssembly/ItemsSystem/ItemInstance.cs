using System.Collections.Generic;
using GameAssembly.ItemsSystem.Data;

namespace GameAssembly.ItemsSystem
{
    public class ItemInstance
    {
        public readonly ItemDefinitionSO Definition;

        public ItemInstance(ItemDefinitionSO itemDefinition)
        {
            Definition = itemDefinition;
        }

        public ItemInstance(ItemDefinitionSO definition, Dictionary<string, string> meta)
        {
            Definition = definition;
            _meta = meta;
        }

        private readonly Dictionary<string, string> _meta = new();

        public IReadOnlyDictionary<string, string> Meta => _meta;
    }
}