using System;
using GameAssembly.Core;
using Mirror;
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

        public Chunk(ChunkCoord coord, CellData[,] cells)
        {
            Coord = coord;
            Cells = cells;
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
            
            if(NetworkServer.active)
            {
                Debug.Log("PUPUPU");
                GameInstaller.Resolve<WorldCreateManager>().Rpc_SyncCell(Coord, x, y, cell);
            }
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
            
            if(NetworkServer.active)
            {
                Debug.Log("PUPUPU");
                GameInstaller.Resolve<WorldCreateManager>().Rpc_SyncCell(Coord, x, y, Cells[x, y]);
            }
        }
    }

    public static class ChunkWriteReader
    {
        public static void WriteChunk(this NetworkWriter writer, Chunk chunk)
        {
            writer.Write(chunk.Coord);

            for (var x = 0; x < Chunk.CHUNK_SIZE; x++)
            for (var y = 0; y < Chunk.CHUNK_SIZE; y++)
                writer.Write(chunk.Cells[x, y]);
        }

        public static Chunk ReadChunk(this NetworkReader reader)
        {
            var coord = reader.ReadChunkCoord();
            var cells = new CellData[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];

            for (var x = 0; x < Chunk.CHUNK_SIZE; x++)
            for (var y = 0; y < Chunk.CHUNK_SIZE; y++)
                cells[x, y] = reader.ReadCellData();

            return new Chunk(coord, cells);
        }
    }
}