using System;
using Mirror;

namespace GameAssembly.WorldSystem
{
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X;
        public readonly int Y;

        public ChunkCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    public static class ChunkCoordWriteReader
    {
        public static void WriteChunkCoord(this NetworkWriter writer, ChunkCoord coord)
        {
            writer.WriteInt(coord.X);
            writer.WriteInt(coord.Y);
        }
        
        public static ChunkCoord ReadChunkCoord(this NetworkReader reader)
        {
            return new ChunkCoord(reader.ReadInt(), reader.ReadInt());
        }
    }
}