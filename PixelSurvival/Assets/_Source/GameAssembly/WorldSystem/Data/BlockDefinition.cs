using GameAssembly.ItemsSystem.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAssembly.WorldSystem.Data
{
    [CreateAssetMenu(fileName = "new BlockDefinition", menuName = "SO/BlockDefinition")]
    public class BlockDefinition : ScriptableObject
    {
        [field: SerializeField] public string BlockNameKey { get; private set; }
        [field: SerializeField] public BlockType Type { get; private set; }
        [field: SerializeField] public BlockFlags Flags { get; private set; }
        [field: SerializeField] public TileBase Tile { get; private set; }
        [field: SerializeField] public ItemDefinitionSO DropItem { get; private set; }
        [field: SerializeField] public int DropItemCount { get; private set; }
        [field: SerializeField] public int Health { get; private set; }
    }
}