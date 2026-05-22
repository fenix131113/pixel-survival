using Mirror;

namespace GameAssembly.WorldSystem
{
    public struct CellData
    {
        public BlockData BaseFloor;
        public BlockData Floor;
        public BlockData Block;

        public static CellData Empty => new()
        {
            BaseFloor = BlockData.Air,
            Floor = BlockData.Air,
            Block = BlockData.Air,
        };
    }

    public static class CellDataWriteReader
    {
        public static void WriteCellData(this NetworkWriter writer, CellData cellData)
        {
            writer.Write(cellData.BaseFloor);
            writer.Write(cellData.Floor);
            writer.Write(cellData.Block);
        }

        public static CellData ReadCellData(this NetworkReader reader)
        {
            return new CellData
            {
                BaseFloor = reader.Read<BlockData>(),
                Floor = reader.Read<BlockData>(),
                Block = reader.Read<BlockData>()
            };
        }
    }
}
