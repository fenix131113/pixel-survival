using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameAssembly.BuildSystem.Data;
using GameAssembly.Utils;
using GameAssembly.WorldSystem;
using GameAssembly.WorldSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.BuildSystem.WorldObjects
{
    public class WorldObjectsGenerator
    {
        private const int RowsPerBatch = 4;

        private readonly World _world;
        private readonly WorldObjectRegistry _registry;
        private readonly WorldObjectsGenerationConfigSO _config;

        private bool _isGenerated;
        private Task _generationTask;

        public WorldObjectsGenerator(World world, WorldObjectRegistry registry)
        {
            _world = world;
            _registry = registry;

            _config = Resources.Load<WorldObjectsGenerationConfigSO>(AssetsPaths.WORLD_OBJECTS_GENERATION_CONFIG_PATH);
        }

        [Server]
        public Task Server_GenerateAsync(Action<float> onProgress01 = null)
        {
            if (_isGenerated)
            {
                onProgress01?.Invoke(1f);
                return Task.CompletedTask;
            }

            if (_generationTask is { IsCompleted: false })
                return _generationTask;

            _generationTask = Server_GenerateInternalAsync(onProgress01);
            return _generationTask;
        }

        [Server]
        private async Task Server_GenerateInternalAsync(Action<float> onProgress01)
        {
            if (_isGenerated)
            {
                onProgress01?.Invoke(1f);
                return;
            }

            _isGenerated = true;
            onProgress01?.Invoke(0f);

            if (!_config)
            {
                Debug.LogWarning(
                    $"[{nameof(WorldObjectsGenerator)}] Missing config at Resources/{AssetsPaths.WORLD_OBJECTS_GENERATION_CONFIG_PATH}. Object generation skipped.");
                onProgress01?.Invoke(1f);
                return;
            }

            var rulesByBiome = BuildRulesByBiome();
            if (rulesByBiome.Count == 0)
            {
                Debug.LogWarning($"[{nameof(WorldObjectsGenerator)}] No valid biome rules found. Object generation skipped.");
                onProgress01?.Invoke(1f);
                return;
            }

            var worldCellsSize = World.WORLD_SIZE * Chunk.CHUNK_SIZE;
            var totalCells = worldCellsSize * worldCellsSize;
            var processedCells = 0;
            var seeded = _world.Seed + _config.SeedOffset;
            var densityMultiplier = Mathf.Max(0f, _config.GlobalDensityMultiplier);

            for (var worldX = 0; worldX < worldCellsSize; worldX++)
            {
                for (var worldY = 0; worldY < worldCellsSize; worldY++)
                {
                    var biome = _world.GetBiomeByBlockPosition(worldX, worldY);
                    if (!biome || !rulesByBiome.TryGetValue(biome, out var rule))
                        continue;

                    var spawnChance = Mathf.Clamp01(rule.SpawnChancePerCell * densityMultiplier);
                    if (spawnChance <= 0f)
                        continue;

                    if (Hash01(seeded, worldX, worldY, 0) > spawnChance)
                        continue;

                    var entry = PickEntry(rule, seeded, worldX, worldY);
                    if (!entry.IsValid)
                        continue;

                    var origin = new Vector2Int(worldX, worldY);

                    if (entry.SpawnMode == WorldObjectSpawnMode.Cluster)
                        TrySpawnCluster(entry, origin, seeded);
                    else
                        _registry.TryPlaceObjectForGeneration(entry.Definition, origin, _world);
                }

                processedCells += worldCellsSize;
                onProgress01?.Invoke(Mathf.Clamp01(processedCells / (float)totalCells));

                if ((worldX + 1) % RowsPerBatch == 0)
                    await Task.Yield();
            }

            onProgress01?.Invoke(1f);
        }

        private Dictionary<BiomeDefinition, BiomeRuntimeRule> BuildRulesByBiome()
        {
            var result = new Dictionary<BiomeDefinition, BiomeRuntimeRule>();

            if (_config.BiomeRules == null)
                return result;

            foreach (var biomeRule in _config.BiomeRules)
            {
                if (biomeRule == null || !biomeRule.Biome || biomeRule.Entries == null || biomeRule.Entries.Count == 0)
                    continue;

                var entries = new List<WeightedObjectEntry>();
                var totalWeight = 0f;

                foreach (var entry in biomeRule.Entries)
                {
                    if (entry == null || !entry.Definition || entry.Weight <= 0f)
                        continue;

                    entries.Add(new WeightedObjectEntry(entry));
                    totalWeight += entry.Weight;
                }

                if (entries.Count == 0 || totalWeight <= 0f)
                    continue;

                result[biomeRule.Biome] = new BiomeRuntimeRule(biomeRule.SpawnChancePerCell, entries, totalWeight);
            }

            return result;
        }

        private void TrySpawnCluster(WeightedObjectEntry entry, Vector2Int origin, int seed)
        {
            var minCount = Mathf.Max(1, entry.MinClusterObjects);
            var maxCount = Mathf.Max(minCount, entry.MaxClusterObjects);
            var minRadius = Mathf.Max(1, entry.MinClusterRadius);
            var maxRadius = Mathf.Max(minRadius, entry.MaxClusterRadius);

            var targetCount = HashRangeInt(seed, origin.x, origin.y, 2, minCount, maxCount);
            var radius = HashRangeInt(seed, origin.x, origin.y, 3, minRadius, maxRadius);

            var originPlaced = _registry.TryPlaceObjectForGeneration(entry.Definition, origin, _world);

            var attempts = 0;
            var placed = originPlaced ? 1 : 0;
            var maxAttempts = Mathf.Max(targetCount * 6, 8);
            var usedCells = new HashSet<Vector2Int> { origin };

            while (placed < targetCount && attempts < maxAttempts)
            {
                attempts++;

                var angle = Hash01(seed, origin.x, origin.y, 100 + attempts * 2) * Mathf.PI * 2f;
                var distance = Mathf.Sqrt(Hash01(seed, origin.x, origin.y, 101 + attempts * 2)) * radius;

                var offset = new Vector2Int(
                    Mathf.RoundToInt(Mathf.Cos(angle) * distance),
                    Mathf.RoundToInt(Mathf.Sin(angle) * distance));

                var candidate = origin + offset;
                if (!usedCells.Add(candidate))
                    continue;

                if (_registry.TryPlaceObjectForGeneration(entry.Definition, candidate, _world))
                    placed++;
            }
        }

        private static WeightedObjectEntry PickEntry(BiomeRuntimeRule rule, int seed, int worldX, int worldY)
        {
            var roll = Hash01(seed, worldX, worldY, 1) * rule.TotalWeight;

            foreach (var entry in rule.Entries)
            {
                if (roll <= entry.Weight)
                    return entry;

                roll -= entry.Weight;
            }

            return rule.Entries[^1];
        }

        private static float Hash01(int seed, int x, int y, int salt)
        {
            unchecked
            {
                uint hash = (uint)seed;
                hash ^= (uint)(x * 374761393);
                hash = (hash << 13) | (hash >> 19);
                hash ^= (uint)(y * 668265263);
                hash ^= (uint)(salt * 1442695041);
                hash *= 2246822519u;
                hash ^= hash >> 15;
                hash *= 3266489917u;
                hash ^= hash >> 16;

                return (hash & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static int HashRangeInt(int seed, int x, int y, int salt, int minInclusive, int maxInclusive)
        {
            var min = Mathf.Min(minInclusive, maxInclusive);
            var max = Mathf.Max(minInclusive, maxInclusive);
            var t = Mathf.Min(Hash01(seed, x, y, salt), 0.999999f);
            return min + Mathf.FloorToInt(t * (max - min + 1));
        }

        private readonly struct BiomeRuntimeRule
        {
            public readonly float SpawnChancePerCell;
            public readonly List<WeightedObjectEntry> Entries;
            public readonly float TotalWeight;

            public BiomeRuntimeRule(float spawnChancePerCell, List<WeightedObjectEntry> entries, float totalWeight)
            {
                SpawnChancePerCell = spawnChancePerCell;
                Entries = entries;
                TotalWeight = totalWeight;
            }
        }

        private readonly struct WeightedObjectEntry
        {
            public readonly PlaceableObjectDefinitionSO Definition;
            public readonly float Weight;
            public readonly WorldObjectSpawnMode SpawnMode;
            public readonly int MinClusterObjects;
            public readonly int MaxClusterObjects;
            public readonly int MinClusterRadius;
            public readonly int MaxClusterRadius;
            public bool IsValid => Definition;

            public WeightedObjectEntry(WorldObjectSpawnEntry entry)
            {
                Definition = entry.Definition;
                Weight = entry.Weight;
                SpawnMode = entry.SpawnMode;
                MinClusterObjects = entry.MinClusterObjects;
                MaxClusterObjects = entry.MaxClusterObjects;
                MinClusterRadius = entry.MinClusterRadius;
                MaxClusterRadius = entry.MaxClusterRadius;
            }
        }
    }
}
