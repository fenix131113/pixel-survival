using System;
using System.Collections.Generic;
using GameAssembly.WorldSystem.Data;
using UnityEngine;

namespace GameAssembly.BuildSystem.Data
{
    [CreateAssetMenu(fileName = "WorldObjectsGenerationConfig", menuName = "SO/World Objects Generation Config")]
    public class WorldObjectsGenerationConfigSO : ScriptableObject
    {
        [field: SerializeField, Min(0)] public int SeedOffset { get; private set; } = 100_003;
        [field: SerializeField, Min(0f)] public float GlobalDensityMultiplier { get; private set; } = 1f;
        [field: SerializeField] public List<BiomeWorldObjectSpawnRule> BiomeRules { get; private set; } = new();
    }

    [Serializable]
    public class BiomeWorldObjectSpawnRule
    {
        [field: SerializeField] public BiomeDefinition Biome { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float SpawnChancePerCell { get; private set; } = 0.03f;
        [field: SerializeField] public List<WorldObjectSpawnEntry> Entries { get; private set; } = new();
    }

    [Serializable]
    public class WorldObjectSpawnEntry
    {
        [field: SerializeField] public PlaceableObjectDefinitionSO Definition { get; private set; }
        [field: SerializeField, Min(0.0001f)] public float Weight { get; private set; } = 1f;
        [field: SerializeField] public WorldObjectSpawnMode SpawnMode { get; private set; } = WorldObjectSpawnMode.Single;
        [field: SerializeField, Min(1)] public int MinClusterObjects { get; private set; } = 3;
        [field: SerializeField, Min(1)] public int MaxClusterObjects { get; private set; } = 8;
        [field: SerializeField, Min(1)] public int MinClusterRadius { get; private set; } = 2;
        [field: SerializeField, Min(1)] public int MaxClusterRadius { get; private set; } = 4;
    }

    public enum WorldObjectSpawnMode
    {
        Single = 0,
        Cluster = 1
    }
}
