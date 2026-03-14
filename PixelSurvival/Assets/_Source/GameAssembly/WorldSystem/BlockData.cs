using System;
using GameAssembly.Core.Definitions;
using GameAssembly.Utils;
using GameAssembly.WorldSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.WorldSystem
{
    [Serializable]
    public struct BlockData : IEquatable<BlockData>
    {
        public BlockType type;
        public BlockFlags flags;
        public BlockDefinitionSO definition;
        public byte meta;

        public BlockData(BlockDefinitionSO definition, byte meta = 0)
        {
            type = definition.Type;
            flags = definition.Flags;
            this.definition = definition;
            this.meta = meta;
        }

        public static BlockData Air => new() { type = BlockType.AIR, flags = BlockFlags.NONE };

        public static BlockData CreateBlock(BlockDefinitionSO def)
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
        public bool IsBreakable => (flags & BlockFlags.BREAKABLE) != 0;

        public bool Equals(BlockData other) =>
            type == other.type && Equals(definition, other.definition);

        public override bool Equals(object obj) =>
            obj is BlockData other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)type, definition);
    }

    public static class BlockDataWriteReader
    {
        public static void WriteBlockData(this NetworkWriter writer, BlockData blockData)
        {
            writer.WriteByte(!blockData.definition ? (byte)0 : (byte)1);

            if (!blockData.definition)
                return;

            writer.WriteString(blockData.definition.Id);
            writer.WriteByte(blockData.meta);
        }

        public static BlockData ReadBlockData(this NetworkReader reader)
        {
            BlockData result;

            if (reader.ReadByte() == 0)
                result = BlockData.Air;
            else
            {
                DefinitionResolverProvider.TryResolve<BlockDefinitionSO>(reader.ReadString(), out var block);
                result = new BlockData(block, reader.ReadByte());
            }

            return result;
        }
    }
}