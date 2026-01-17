namespace GameAssembly.WorldSystem
{
    public struct CellData
    {
        public BlockData Floor;
        public BlockData Block;

        public static CellData Empty => new CellData
        {
            Floor = BlockData.Air,
            Block = BlockData.Air,
        };
    }
}