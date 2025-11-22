using GameAssembly.Utils;
using Mirror;
using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New ItemDefinitionSO", menuName = "SO/New ItemDefinitionSO")]
    public class ItemDefinitionSO : ScriptableObject
    {
        [field: SerializeField] public string NameTranslationKey { get; protected set; }
        [field: SerializeField] public Sprite Icon { get; protected set; }
        [field: SerializeField] public int MaxCount { get; protected set; }
    }

    public static class ItemDefinitionSOSerializer
    {
        public static void WriteArmor(this NetworkWriter writer, ItemDefinitionSO data) =>
            writer.WriteString(data.name);

        public static ItemDefinitionSO ReadArmor(this NetworkReader reader) =>
            Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/" + reader.ReadString());
    }
}