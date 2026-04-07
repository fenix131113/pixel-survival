using UnityEngine;

namespace GameAssembly.WorldSystem.Data
{
    [CreateAssetMenu(fileName = "new BiomeDefinition", menuName = "SO/BiomeDefinition")]
    public class BiomeDefinition : ScriptableObject
    {
        [field: SerializeField] public string NameKey { get; private set; }
        [field: SerializeField] public BlockDefinitionSO DefaultFloor { get; private set; }
        [field: Header("Path Generation")]
        [field: SerializeField] public BlockDefinitionSO PathFloor { get; private set; }
        [field: SerializeField]
        [field: Min(0.0001f)] public float PathNoiseScale { get; private set; } = 0.035f;
        [field: SerializeField]
        [field: Range(0f, 0.5f)] public float PathWidth { get; private set; } = 0.05f;
        [field: SerializeField] public BlockDefinitionSO DefaultWall { get; private set; }
        [field: SerializeField] public bool NaturalSpawn { get; private set; }
        [field: SerializeField] public float Min { get; private set; }
        [field: SerializeField] public float Max { get; private set; }
        [field: SerializeField]
        [field: Range(0f, 1f)] public float WallDensity { get; private set; }
    }
}
