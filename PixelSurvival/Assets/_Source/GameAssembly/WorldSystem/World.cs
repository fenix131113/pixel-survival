using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameAssembly.Utils;
using GameAssembly.WorldSystem.Data;
using UnityEngine;
using Random = System.Random;

namespace GameAssembly.WorldSystem
{
    public class World
    {
        public readonly int Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);//2147483647;

        public const int WORLD_SIZE = 20;
        public const float WORLD_CENTER_XY = WORLD_SIZE * Chunk.CHUNK_SIZE * 0.5f;

        private const float BLOCKS_NOISE_STRENGTH = 0.08f;
        private const float BIOMES_NOISE_STRENGTH = 0.02f;

        private const float MAX_BIOME_RADIUS = WORLD_SIZE * Chunk.CHUNK_SIZE * 0.5f;


        private readonly Dictionary<ChunkCoord, Chunk> _chunks = new();

        private readonly BiomeDefinition[] _biomes =
            Resources.LoadAll<BiomeDefinition>(AssetsPaths.BIOMES_CONFIGS_PATH);

        private readonly DifficultyIslandConfig _difficultyConfig =
            Resources.Load<DifficultyIslandConfig>($"Configs/DifficultyIsland");

        private readonly List<DifficultyIsland> _difficultyIslands = new();

        public IReadOnlyDictionary<ChunkCoord, Chunk> Chunks => _chunks;

        public Progress<float> Progress { get; private set; } = new();

        public World()
        {
            GenerateDifficultyIslands();
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

            var center = new Vector2(WORLD_CENTER_XY, WORLD_CENTER_XY);
            var rng = new Random(Seed);

            var totalCount =
                rng.Next(_difficultyConfig.MinIslands, _difficultyConfig.MaxIslands + 1);

            var redCount = Mathf.CeilToInt(totalCount * _difficultyConfig.RedChance);
            var yellowCount = totalCount - redCount;

            var guard = 0;

            for (var i = 0; i < redCount; i++)
            {
                if (guard++ > 5000) break;

                var island = CreateIsland(
                    i,
                    minDist01: _difficultyConfig.ThreeBiomeMin01,
                    maxDist01: _difficultyConfig.OuterLimit01,
                    allowRed: true
                );

                if (_difficultyIslands.Any(x => IsIntersects(x, island)))
                {
                    i--;
                    continue;
                }

                _difficultyIslands.Add(island);
            }

            guard = 0;
            
            for (var i = 0; i < yellowCount; i++)
            {
                if (guard++ > 5000) break;

                var island = CreateIsland(
                    i + 1000,
                    minDist01: _difficultyConfig.InnerDeadZone01,
                    maxDist01: _difficultyConfig.TwoBiomeMax01,
                    allowRed: false
                );

                if (_difficultyIslands.Any(x => IsIntersects(x, island)))
                {
                    i--;
                    continue;
                }

                _difficultyIslands.Add(island);
            }

            return;

            DifficultyIsland CreateIsland(
                int index,
                float minDist01,
                float maxDist01,
                bool allowRed)// TODO: Change that circles can spawn less then minimum value
            {
                var si = index * 0.2f + Seed * 0.00001f;

                var angle = Mathf.PerlinNoise(si, 0.2f) * Mathf.PI * 2f;

                var dist01 = Mathf.Lerp(
                    minDist01,
                    maxDist01,
                    Mathf.PerlinNoise(si, 3.3f)
                );

                var dist = dist01 * MAX_BIOME_RADIUS * _difficultyConfig.IslandSpacingMultiplier;

                var pos = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) * dist;

                var yellow = Mathf.Lerp(
                    _difficultyConfig.YellowMin,
                    _difficultyConfig.YellowMax,
                    Mathf.PerlinNoise(si, 10.1f)
                );

                var orange = Mathf.Lerp(
                    _difficultyConfig.OrangeMin,
                    _difficultyConfig.OrangeMax,
                    Mathf.PerlinNoise(si, 20.2f)
                );

                var red = allowRed
                    ? Mathf.Lerp(
                        _difficultyConfig.RedMin,
                        _difficultyConfig.RedMax,
                        Mathf.PerlinNoise(si, 30.3f)
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

            return Vector2.Distance(a.Center, b.Center) < (ra + rb);
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

                    ((IProgress<float>)Progress)?.Report(done / (float)total);
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

                    var noise = Mathf.PerlinNoise(nx, ny);

                    chunk.Cells[x, y].Block =
                        noise < biome.WallDensity
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

        public BiomeDefinition GetBiomeByBlockPosition(int worldX, int worldY) =>
            PickBiome(GetBiomeWeights(worldX, worldY), worldX, worldY);// TODO: Also return difficulty biomes

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
    }
}