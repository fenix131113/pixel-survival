using Mirror;

namespace GameAssembly.WorldSystem
{
    public struct CellData
    {
        public BlockData Floor;
        public BlockData Block;

        public static CellData Empty => new()
        {
            Floor = BlockData.Air,
            Block = BlockData.Air,
        };
    }

    public static class CellDataWriteReader
    {
        public static void WriteCellData(this NetworkWriter writer, CellData cellData)
        {
            writer.Write(cellData.Floor);
            writer.Write(cellData.Block);
        }

        public static CellData ReadCellData(this NetworkReader reader)
        {
            return new CellData { Floor = reader.Read<BlockData>(), Block = reader.Read<BlockData>() };
        }
    }
}