using GameAssembly.WorldSystem.Data;
using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New BlockItemDefinitionSO", menuName = "SO/New BlockItemDefinitionSO")]
    public class BlockItemDefinitionSO : ItemDefinitionSO
    {
        [field: SerializeField] public bool IsFloorBlock { get; private set; }
        [field: SerializeField] public BlockDefinition BlockDefinition { get; private set; }
    }
}