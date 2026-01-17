using GameAssembly.ItemsSystem.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAssembly.WorldSystem.Data
{
    [CreateAssetMenu(fileName = "new BlockDefinition", menuName = "SO/BlockDefinition")]
    public class BlockDefinition : ScriptableObject
    {
        public string blockNameKey;
        public BlockType type;
        public BlockFlags flags;
        public TileBase tile;
        public ItemDefinitionSO dropItem;
        public int dropItemCount;
        public int health;
    }
}