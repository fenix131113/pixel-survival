using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameAssembly.WorldSystem
{
    public class World
    {
        public const int WORLD_SIZE = 3;
        
        private readonly Dictionary<ChunkCoord, Chunk> _chunks = new();
        
        public IReadOnlyDictionary<ChunkCoord, Chunk> Chunks => _chunks;

        public World()
        {
            GenerateEmptyWorld(); // TODO: Move to another call point to call this only on host
        }

        public void GenerateEmptyWorld()
        {
            for (var i = 0; i < WORLD_SIZE; i++)
            {
                for (var j = 0; j < WORLD_SIZE; j++)
                {
                    var coords = new ChunkCoord(i, j);
                    GetOrCreateChunk(coords);
                }
            } 
        }
        
        public IEnumerable<Chunk> GetDirtyChunks()
        {
            return _chunks.Values.Where(c => c.DirtyVisual || c.DirtyCollider);
        }
        
        public Chunk GetOrCreateChunk(ChunkCoord coord)
        {
            if (_chunks.TryGetValue(coord, out var chunk))
                return chunk;

            chunk = new Chunk(coord);
            _chunks.Add(coord, chunk);

            return chunk;
        }

        public CellData GetCell(Vector2Int worldPos)
        {
            var chunkCoord = WorldToChunk(worldPos);
            var localPos = WorldToLocal(worldPos);

            var chunk = GetOrCreateChunk(chunkCoord);
            return chunk.GetCell(localPos.x, localPos.y);
        }

        public void SetBlock(Vector2Int worldPos, BlockData block, bool isFloor = false)
        {
            var chunkCoord = WorldToChunk(worldPos);
            var localPos = WorldToLocal(worldPos);

            var chunk = GetOrCreateChunk(chunkCoord);

            var cell = chunk.GetCell(localPos.x, localPos.y);
            
            if (isFloor)
                cell.Floor = block;
            else
                cell.Block = block;

            chunk.SetCell(localPos.x, localPos.y, cell);
        }
        
        /// <returns>Chunk coords by world position</returns>
        private ChunkCoord WorldToChunk(Vector2Int pos)
        {
            var cx = Mathf.FloorToInt((float)pos.x / Chunk.CHUNK_SIZE);
            var cy = Mathf.FloorToInt((float)pos.y / Chunk.CHUNK_SIZE);
            return new ChunkCoord(cx, cy);
        }

        /// <returns>Block position in chunk coords by world position</returns>
        private Vector2Int WorldToLocal(Vector2Int pos)
        {
            var lx = Mathf.FloorToInt(Mathf.Repeat(pos.x, Chunk.CHUNK_SIZE));
            var ly = Mathf.FloorToInt(Mathf.Repeat(pos.y, Chunk.CHUNK_SIZE));
            return new Vector2Int(lx, ly);
        }
    }
}