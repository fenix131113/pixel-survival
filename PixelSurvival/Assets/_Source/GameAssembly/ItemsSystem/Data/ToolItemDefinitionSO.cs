using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New ToolItemDefinitionSO", menuName = "SO/New ToolItemDefinitionSO")]
    public class ToolItemDefinitionSO : ItemDefinitionSO
    {
        [field: SerializeField] public ToolType ToolType { get; private set; }
        [field: SerializeField, Min(1)] public int MiningDamage { get; private set; } = 2;
    }
}
