using GameAssembly.WorldSystem.Data;

namespace GameAssembly.WorldSystem
{
    [System.Serializable]
    public struct BlockData
    {
        public BlockType type;
        public BlockFlags flags;
        public BlockDefinition definition;
        public byte meta;

        public BlockData(BlockDefinition definition, byte meta = 0)
        {
            type = definition.type;
            flags = definition.flags;
            this.definition = definition;
            this.meta = meta;
        }

        public static BlockData Air => new() { type = BlockType.AIR, flags = BlockFlags.NONE};
        
        public bool IsSolid => (flags & BlockFlags.SOLID) != 0;
    }
}