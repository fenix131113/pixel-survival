using GameAssembly.Core.Definitions;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New ItemDefinitionSO", menuName = "SO/New ItemDefinitionSO")]
    public class ItemDefinitionSO : ScriptableObject, IDefinitionWithId
    {
        [field: SerializeField] public string NameTranslationKey { get; protected set; }
        [field: SerializeField] public Sprite Icon { get; protected set; }
        [field: SerializeField] public int MaxCount { get; protected set; }
        
        [SerializeField] private string id;
        
        public string Id => id;
    }

    public static class ItemDefinitionSOSerializer
    {
        public static void WriteItemDefinition(this NetworkWriter writer, ItemDefinitionSO data) =>
            writer.WriteString(data.Id);

        public static ItemDefinitionSO ReadItemDefinition(this NetworkReader reader)
        {
            DefinitionResolverProvider.TryResolve<ItemDefinitionSO>(reader.ReadString(), out var res);
            return res;
        }
    }
}