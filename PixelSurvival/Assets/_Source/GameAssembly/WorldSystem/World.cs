using System.Collections.Generic;
using System.Linq;
using GameAssembly.WorldSystem.Data;
using UnityEngine;

namespace GameAssembly.WorldSystem
{
    public class World
    {
        public readonly int Seed = 2147483647;
        public const int WORLD_SIZE = 8;
        public const float BLOCKS_NOISE_STRENGTH = 0.08f;
        public const float BIOMES_NOISE_STRENGTH = 0.02f;

        private readonly Dictionary<ChunkCoord, Chunk> _chunks = new();
        private readonly BiomeDefinition[] _biomes;

        public IReadOnlyDictionary<ChunkCoord, Chunk> Chunks => _chunks;

        public World()
        {
            _biomes = Resources.LoadAll<BiomeDefinition>($"Configs/Biomes");

            GenerateWorld(); // TODO: Move to another call point to call this only on host
        }

        public void GenerateWorld()
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

            chunk = GenerateChunk(coord);
            _chunks.Add(coord, chunk);

            return chunk;
        }

        private Chunk GenerateChunk(ChunkCoord coord)
        {
            var chunk = new Chunk(coord);

            for (var x = 0; x < Chunk.CHUNK_SIZE; x++)
            {
                for (var y = 0; y < Chunk.CHUNK_SIZE; y++)
                {
                    var worldX = coord.X * Chunk.CHUNK_SIZE + x;
                    var worldY = coord.Y * Chunk.CHUNK_SIZE + y;

                    var biome = GetBiomeByBlockPosition(worldX, worldY);

                    chunk.Cells[x, y].Floor = BlockData.CreateBlock(biome.DefaultFloor);

                    var nx = worldX * BLOCKS_NOISE_STRENGTH + Seed * 0.00001f;
                    var ny = worldY * BLOCKS_NOISE_STRENGTH + Seed * 0.00001f;

                    var noise = Mathf.PerlinNoise(nx, ny);
                    noise = Mathf.Clamp01(noise);

                    chunk.Cells[x, y].Block = noise < biome.WallDensity
                        ? BlockData.CreateBlock(biome.DefaultWall)
                        : BlockData.Air;
                }
            }

            chunk.DirtyVisual = true;
            chunk.DirtyCollider = true;

            return chunk;
        }

        public BiomeDefinition GetBiomeByBlockPosition(int worldX, int worldY) =>
            PickBiome(GetBiomeWeights(worldX, worldY), worldX, worldY);

        private BiomeDefinition PickBiome(Dictionary<BiomeDefinition, float> weights, int worldX, int worldY)
        {
            var nx = worldX * BIOMES_NOISE_STRENGTH + Seed * 0.00001f;
            var ny = worldY * BIOMES_NOISE_STRENGTH + Seed * 0.00001f;

            var r = Mathf.PerlinNoise(nx, ny);
            r = Mathf.Clamp01(r);

            foreach (var kv in weights)
            {
                if (r <= kv.Value)
                    return kv.Key;

                r -= kv.Value;
            }

            return weights.Keys.First();
        }

        private Dictionary<BiomeDefinition, float> GetBiomeWeights(int worldX, int worldY)
        {
            var nx = worldX * BIOMES_NOISE_STRENGTH + Seed * 0.00001f;
            var ny = worldY * BIOMES_NOISE_STRENGTH + Seed * 0.00001f;

            var noise = Mathf.PerlinNoise(nx, ny);
            noise = Mathf.Clamp01(noise);

            Dictionary<BiomeDefinition, float> result = new();

            foreach (var biome in _biomes)
            {
                if (noise < biome.Min || noise > biome.Max)
                    continue;

                var center = (biome.Min + biome.Max) * 0.5f;
                var half = (biome.Max - biome.Min) * 0.5f;

                var weight = 1f - Mathf.Abs(noise - center) / half;
                result[biome] = weight;
            }

            return result;
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

        /// <returns>Block position in chunk coords space by world position</returns>
        private Vector2Int WorldToLocal(Vector2Int pos)
        {
            var lx = Mathf.FloorToInt(Mathf.Repeat(pos.x, Chunk.CHUNK_SIZE));
            var ly = Mathf.FloorToInt(Mathf.Repeat(pos.y, Chunk.CHUNK_SIZE));
            return new Vector2Int(lx, ly);
        }
    }
}