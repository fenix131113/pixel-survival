using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New ItemDefinitionSO", menuName = "SO/New ItemDefinitionSO")]
    public class ItemDefinitionSO : ScriptableObject
    {
        [field: SerializeField] public string NameTranslationKey { get; protected set; }
        [field: SerializeField] public Sprite Icon { get; protected set; }
    }
}