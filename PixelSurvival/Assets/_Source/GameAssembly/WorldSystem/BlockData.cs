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
            type = definition.Type;
            flags = definition.Flags;
            this.definition = definition;
            this.meta = meta;
        }

        public static BlockData Air => new() { type = BlockType.AIR, flags = BlockFlags.NONE};
        
        public static BlockData CreateBlock(BlockDefinition def)
        {
            return new BlockData
            {
                definition = def,
                type = def.Type,
                flags = def.Flags,
                meta = 0
            };
        }
        
        public bool IsSolid => (flags & BlockFlags.SOLID) != 0;
    }
}