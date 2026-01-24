using UnityEngine;

namespace GameAssembly.WorldSystem.Data
{
    [CreateAssetMenu(fileName = "new BiomeDefinition", menuName = "SO/BiomeDefinition")]
    public class BiomeDefinition : ScriptableObject
    {
        [field: SerializeField] public string NameKey { get; private set; }
        [field: SerializeField] public BlockDefinition DefaultFloor { get; private set; }
        [field: SerializeField] public BlockDefinition DefaultWall { get; private set; }
        [field: SerializeField] public bool NaturalSpawn { get; private set; }
        [field: SerializeField] public float Min { get; private set; }
        [field: SerializeField] public float Max { get; private set; }
        [field: SerializeField]
        [field: Range(0f, 1f)] public float WallDensity { get; private set; }
        
        public float MinDistance; // 0..1
        public float MaxDistance; // 0..1
        
    }
}