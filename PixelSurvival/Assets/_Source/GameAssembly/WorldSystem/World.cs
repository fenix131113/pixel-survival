using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameAssembly.Utils;
using GameAssembly.Utils.Extensions;
using GameAssembly.WorldSystem.Data;
using R3;
using UnityEngine;
using Random = System.Random;

namespace GameAssembly.WorldSystem
{
    public class World
    {
        public int Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue); //2147483647;

        public const int WORLD_SIZE = 15;

        private const float BLOCKS_NOISE_STRENGTH = 0.08f;
        private const float BIOMES_NOISE_STRENGTH = 0.02f;

        private const float MAX_BIOME_RADIUS = WORLD_SIZE * Chunk.CHUNK_SIZE * 0.5f;

        public readonly int WorldCenterXY = Mathf.RoundToInt(WORLD_SIZE * Chunk.CHUNK_SIZE * 0.5f);

        private readonly Dictionary<ChunkCoord, Chunk> _chunks = new();

        private readonly BiomeDefinition[] _biomes =
            Resources.LoadAll<BiomeDefinition>(AssetsPaths.BIOMES_CONFIGS_PATH);

        private readonly DifficultyIslandConfig _difficultyConfig =
            Resources.Load<DifficultyIslandConfig>(AssetsPaths.DIFFICULTY_ISLANDS_CONFIGS_PATH);

        private readonly List<DifficultyIsland> _difficultyIslands = new();

        public IReadOnlyDictionary<ChunkCoord, Chunk> Chunks => _chunks;

        public Progress<float> Progress { get; private set; } = new();
        public ReactiveProperty<bool> IsLoaded { get; private set; } = new();

        public World()
        {
            GenerateDifficultyIslands();
        }

        public void SetupSeed(int seed) => Seed = seed;

        public void SetupChunk(Chunk chunk)
        {
            _chunks.Add(chunk.Coord, chunk);

            var progress = (float)_chunks.Count / (WORLD_SIZE * WORLD_SIZE);

            ((IProgress<float>)Progress)?.Report(progress);

            if (!Mathf.Approximately(progress, 1f))
                return;

            GenerateDifficultyIslands();
            IsLoaded.Value = true;
        }

        #region Difficulty islands

        private struct DifficultyIsland
        {
            public Vector2 Center;
            public float YellowRadius;
            public float OrangeRadius;
            public float RedRadius;
        }

        private void GenerateDifficultyIslands()
        {
            _difficultyIslands.Clear();

            var center = new Vector2(WorldCenterXY, WorldCenterXY);
            var rng = new Random(Seed);

            var threeLayerCount = rng.Next(_difficultyConfig.Min3LayerIslands, _difficultyConfig.Max3LayerIslands + 1);
            var twoLayerCount = rng.Next(_difficultyConfig.Min2LayerIslands, _difficultyConfig.Max2LayerIslands + 1);

            GenerateIslandsWithGuaranteedCount(
                threeLayerCount,
                minDist01: _difficultyConfig.ThreeBiomeMin01,
                maxDist01: _difficultyConfig.OuterLimit01,
                allowRed: true);

            GenerateIslandsWithGuaranteedCount(
                twoLayerCount,
                minDist01: _difficultyConfig.InnerDeadZone01,
                maxDist01: _difficultyConfig.TwoBiomeMax01,
                allowRed: false);

            return;

            void GenerateIslandsWithGuaranteedCount(int targetCount, float minDist01, float maxDist01, bool allowRed)
            {
                const int maxPlacementAttemptsPerIsland = 200;
                for (var i = 0; i < targetCount; i++)
                {
                    var island = default(DifficultyIsland);
                    var placed = false;

                    for (var attempt = 0; attempt < maxPlacementAttemptsPerIsland; attempt++)
                    {
                        island = CreateIsland(minDist01, maxDist01, allowRed);

                        if (_difficultyIslands.Any(x => IsIntersects(x, island)))
                            continue;

                        _difficultyIslands.Add(island);
                        placed = true;
                        break;
                    }

                    if (!placed)
                    {
                        // Fallback: if the map is too dense, we still add the island to guarantee count.
                        _difficultyIslands.Add(island);
                    }
                }
            }

            DifficultyIsland CreateIsland(
                float minDist01,
                float maxDist01,
                bool allowRed)
            {
                var angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                var dist01 = Mathf.Lerp(minDist01, maxDist01, (float)rng.NextDouble());

                var dist = dist01 * MAX_BIOME_RADIUS * _difficultyConfig.IslandSpacingMultiplier;

                var pos = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) * dist;

                var yellow = Mathf.Lerp(_difficultyConfig.YellowMin, _difficultyConfig.YellowMax,
                    (float)rng.NextDouble());
                var orange = Mathf.Lerp(_difficultyConfig.OrangeMin, _difficultyConfig.OrangeMax,
                    (float)rng.NextDouble());

                var red = allowRed
                    ? Mathf.Lerp(
                        _difficultyConfig.RedMin,
                        _difficultyConfig.RedMax,
                        (float)rng.NextDouble()
                    )
                    : 0f;

                return new DifficultyIsland
                {
                    Center = pos,
                    YellowRadius = yellow,
                    OrangeRadius = orange,
                    RedRadius = red
                };
            }
        }


        private BiomeDefinition GetDifficultyBiome(int worldX, int worldY)
        {
            var pos = new Vector2(worldX, worldY);

            foreach (var island in _difficultyIslands)
            {
                var dist = Vector2.Distance(pos, island.Center);

                if (island.RedRadius > 0f && dist <= island.RedRadius)
                    return GetBiome("Red");

                if (dist <= island.OrangeRadius)
                    return GetBiome("Orange");

                if (dist <= island.YellowRadius)
                    return GetBiome("Yellow");
            }

            return null;
        }

        private BiomeDefinition GetBiome(string name)
        {
            return _biomes.First(b => b.name == name);
        }

        private bool IsIntersects(DifficultyIsland a, DifficultyIsland b)
        {
            var ra = Mathf.Max(a.YellowRadius, a.OrangeRadius, a.RedRadius);
            var rb = Mathf.Max(b.YellowRadius, b.OrangeRadius, b.RedRadius);

            return Vector2.Distance(a.Center, b.Center) < ra + rb;
        }

        #endregion

        #region World generation

        public async Task GenerateWorldAsync()
        {
            const int total = WORLD_SIZE * WORLD_SIZE;
            var done = 0;

            for (var i = 0; i < WORLD_SIZE; i++)
            {
                for (var j = 0; j < WORLD_SIZE; j++)
                {
                    GetOrCreateChunk(new ChunkCoord(i, j));
                    done++;

                    var currentProgress = done / (float)total;
                    ((IProgress<float>)Progress)?.Report(currentProgress);

                    if (Mathf.Approximately(currentProgress, 1f))
                        IsLoaded.Value = true;

                    await Task.Yield();
                }
            }
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

                    var biome =
                        GetDifficultyBiome(worldX, worldY)
                        ?? GetBiomeByBlockPosition(worldX, worldY);

                    chunk.Cells[x, y].Floor =
                        BlockData.CreateBlock(biome.DefaultFloor);

                    var nx = worldX * BLOCKS_NOISE_STRENGTH + Seed * 0.00001f;
                    var ny = worldY * BLOCKS_NOISE_STRENGTH + Seed * 0.00001f;

                    var noise = Mathf.Clamp01(Mathf.PerlinNoise(nx, ny));

                    chunk.Cells[x, y].Block =
                        noise <= biome.WallDensity
                            ? BlockData.CreateBlock(biome.DefaultWall)
                            : BlockData.Air;
                }
            }

            chunk.DirtyVisual = true;
            chunk.DirtyCollider = true;
            return chunk;
        }

        #endregion

        #region Base biome logic

        public BiomeDefinition GetBiomeByBlockPosition(int worldX, int worldY)
        {
            var difficultyBiome = GetDifficultyBiome(worldX, worldY);

            return !difficultyBiome ? PickBiome(GetBiomeWeights(worldX, worldY), worldX, worldY) : difficultyBiome;
        }

        private BiomeDefinition PickBiome(
            Dictionary<BiomeDefinition, float> weights,
            int worldX,
            int worldY)
        {
            if (weights.Count == 0)
                return _biomes[0];

            var sum = weights.Values.Sum();

            var nx = worldX * BIOMES_NOISE_STRENGTH + Seed * 0.00001f;
            var ny = worldY * BIOMES_NOISE_STRENGTH + Seed * 0.00001f;

            var r = Mathf.PerlinNoise(nx + 1000f, ny + 1000f) * sum;

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

            Dictionary<BiomeDefinition, float> result = new();
            var total = 0f;

            foreach (var biome in _biomes)
            {
                if (!biome.NaturalSpawn)
                    continue;

                if (noise < biome.Min || noise > biome.Max)
                    continue;

                var w = 1f - Mathf.Abs(noise - (biome.Min + biome.Max) * 0.5f)
                    / ((biome.Max - biome.Min) * 0.5f);

                w = Mathf.Clamp01(w);
                if (w <= 0f) continue;

                result[biome] = w;
                total += w;
            }

            if (total <= 0f)
                return result;

            foreach (var key in result.Keys.ToArray())
                result[key] /= total;

            return result;
        }

        #endregion

        #region Utils

        #region Base Utils

        public Chunk GetChunk(ChunkCoord coord)
        {
            var chunk = _chunks.GetValueOrDefault(coord);
            return chunk;
        }

        /// <summary>
        /// Converts world position to local chunk cell position
        /// </summary>
        public static Vector2Int ConvertWorldToChunkSpace(int worldX, int worldY)
        {
            var localX = Mathf.RoundToInt(worldX) % Chunk.CHUNK_SIZE;
            var localY = Mathf.RoundToInt(worldY) % Chunk.CHUNK_SIZE;

            return new Vector2Int(localX, localY);
        }

        #endregion

        #region Complex Utils

        public Chunk GetChunkByWorldPosition(int worldX, int worldY)
        {
            var chunkX = worldX / Chunk.CHUNK_SIZE;
            var chunkY = worldY / Chunk.CHUNK_SIZE;

            return GetChunk(new ChunkCoord(chunkX, chunkY));
        }

        public CellData GetCellByWorldPosition(int worldX, int worldY)
        {
            var chunk = GetChunkByWorldPosition(worldX, worldY);

            return chunk?.GetCell(ConvertWorldToChunkSpace(worldX, worldY)) ?? CellData.Empty;
        }

        public Vector2Int FindRandomNearestBlockByType(int x, int y, BlockType findType, bool isFloor)
        {
            var currentLayerIndex = 0;
            List<(Vector2Int, CellData)> needBlocks = null;

            while (needBlocks == null || needBlocks.Count == 0)
            {
                var nearestBlocks = GetLayer(x, y, currentLayerIndex);

                if (x > WORLD_SIZE * Chunk.CHUNK_SIZE / 2 || y > WORLD_SIZE * Chunk.CHUNK_SIZE / 2)
                    return Vector2Int.one * WorldCenterXY;

                needBlocks = nearestBlocks.Where(tuple =>
                    isFloor ? tuple.Item2.Floor.type == findType : tuple.Item2.Block.type == findType).ToList();

                currentLayerIndex++;
            }

            return needBlocks.GetRandomElement().Item1;

            List<(Vector2Int, CellData)> GetLayer(int centerX, int centerY, int layerIndex)
            {
                var min = -layerIndex;

                var result = new List<(Vector2Int, CellData)>();

                for (var cx = min; cx <= layerIndex; cx++)
                {
                    var pos = new Vector2Int(centerX + cx, centerY + layerIndex);
                    result.Add((pos, GetCellByWorldPosition(pos.x, pos.y)));
                }

                for (var cx = min; cx <= layerIndex; cx++)
                {
                    var pos = new Vector2Int(centerX + cx, centerY + min);
                    result.Add((pos, GetCellByWorldPosition(pos.x, pos.y)));
                }

                for (var cy = min + 1; cy < layerIndex; cy++)
                {
                    var pos = new Vector2Int(centerX + layerIndex, centerY + cy);
                    result.Add((pos, GetCellByWorldPosition(pos.x, pos.y)));
                }

                for (var cy = min + 1; cy < layerIndex; cy++)
                {
                    var pos = new Vector2Int(centerX + min, centerY + cy);
                    result.Add((pos, GetCellByWorldPosition(pos.x, pos.y)));
                }

                return result;
            }
        }

        #endregion

        #endregion
    }

    /*public static class WorldReaderWriter
    {
        public static void WriteWorld(this NetworkWriter writer, World world)
        {
            writer.WriteInt(world.Seed);
            writer.WriteInt(world.Chunks.Count);

            for (var i = 0; i < world.Chunks.Count; i++)
            {
                writer.WriteChunkCoord(world.Chunks.ElementAt(i).Key);
                writer.WriteChunk(world.Chunks.ElementAt(i).Value);
            }
        }

        public static World ReadWorld(this NetworkReader reader)
        {
            var seed = reader.ReadInt();
            var chunkCount = reader.ReadInt();
            var chunks = new Dictionary<ChunkCoord, Chunk>();

            for (var i = 0; i < chunkCount; i++)
            {
                var coord = reader.ReadChunkCoord();
                var chunk = reader.ReadChunk();
                chunks.Add(coord, chunk);
            }

            return new World(seed, chunks);
        }*/
}