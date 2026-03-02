using GameAssembly.Utils;
using GameAssembly.WorldSystem.Data;
using Mirror;
using UnityEngine;

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

        public static BlockData Air => new() { type = BlockType.AIR, flags = BlockFlags.NONE };

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

    public static class BlockDataWriteReader
    {
        public static void WriteBlockData(this NetworkWriter writer, BlockData blockData)
        {
            writer.WriteByte(!blockData.definition ? (byte)0 : (byte)1);

            if (!blockData.definition)
                return;
            
            writer.WriteString(blockData.definition.name);
            writer.WriteByte(blockData.meta);
        }

        public static BlockData ReadBlockData(this NetworkReader reader)
        {
            BlockData result;

            if (reader.ReadByte() == 0)
                result = BlockData.Air;
            else
                result = new BlockData(
                    Resources.Load<BlockDefinition>(AssetsPaths.BLOCK_CONFIGS_PATH + $"/{reader.ReadString()}"),
                    reader.ReadByte());

            return result;
        }
    }
}