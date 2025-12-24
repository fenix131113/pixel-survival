using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New ToolItemDefinitionSO", menuName = "SO/New ToolItemDefinitionSO")]
    public class ToolItemDefinitionSO : ItemDefinitionSO
    {
        [field: SerializeField] public ToolType ToolType { get; private set; }
    }
}