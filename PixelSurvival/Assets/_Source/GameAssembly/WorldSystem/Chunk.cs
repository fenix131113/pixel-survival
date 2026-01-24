using System;
using UnityEngine;

namespace GameAssembly.WorldSystem
{
    public class Chunk
    {
        public const int CHUNK_SIZE = 32;

        public ChunkCoord Coord { get; }
        public CellData[,] Cells { get; }

        public bool DirtyVisual { get; set; }
        public bool DirtyCollider { get; set; }

        public event Action<Chunk> OnChunkChanged;

        public Chunk(ChunkCoord coord)
        {
            Coord = coord;
            Cells = new CellData[CHUNK_SIZE, CHUNK_SIZE];

            FillEmpty();
        }

        private void FillEmpty()
        {
            for (var x = 0; x < CHUNK_SIZE; x++)
            for (var y = 0; y < CHUNK_SIZE; y++)
                Cells[x, y] = CellData.Empty;
        }

        public CellData GetCell(int x, int y)
        {
            return Cells[x, y];
        }
        
        public CellData GetCell(Vector2Int indexes)
        {
            return Cells[indexes.x, indexes.y];
        }

        public void SetCell(int x, int y, CellData cell)
        {
            Cells[x, y] = cell;
            DirtyVisual = true;
            DirtyCollider = true;
            OnChunkChanged?.Invoke(this);
        }

        public void SetBlock(int x, int y, bool isFloor, BlockData block)
        {
            if (isFloor)
                Cells[x, y].Floor = block;
            else
                Cells[x, y].Block = block;

            DirtyVisual = true;
            DirtyCollider = true;
            OnChunkChanged?.Invoke(this);
        }
    }
}