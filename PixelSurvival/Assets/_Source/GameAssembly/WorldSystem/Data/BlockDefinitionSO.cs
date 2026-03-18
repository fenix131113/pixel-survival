using GameAssembly.Core.Definitions;
using GameAssembly.ItemsSystem.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAssembly.WorldSystem.Data
{
    [CreateAssetMenu(fileName = "new BlockDefinition", menuName = "SO/BlockDefinition")]
    public class BlockDefinitionSO : ScriptableObject, IDefinitionWithId
    {
        [field: SerializeField] public string BlockNameKey { get; private set; }
        [field: SerializeField] public BlockType Type { get; private set; }
        [field: SerializeField] public BlockFlags Flags { get; private set; }
        [field: SerializeField] public TileBase Tile { get; private set; }
        [field: SerializeField] public ItemDefinitionSO DropItem { get; private set; }
        [field: SerializeField] public int MinDropAmount { get; private set; }
        [field: SerializeField] public int MaxDropAmount { get; private set; }
        [field: SerializeField] public int Health { get; private set; }
        [field: SerializeField] public ToolType EffectiveToolType { get; private set; } = ToolType.UNKNOWN;
        [field: SerializeField] public bool RequireEffectiveToolToBreak { get; private set; }

        [SerializeField] private string id;
        
        public string Id => id;
        
        public int RandomizeDropAmount() => Random.Range(MinDropAmount, MaxDropAmount + 1);

        public bool IsEffectiveTool(ToolItemDefinitionSO toolDefinition)
        {
            return toolDefinition &&
                   EffectiveToolType != ToolType.UNKNOWN &&
                   toolDefinition.ToolType == EffectiveToolType;
        }

        public bool CanTakeDamageFrom(ToolItemDefinitionSO toolDefinition)
        {
            return !RequireEffectiveToolToBreak || IsEffectiveTool(toolDefinition);
        }
    }
}
