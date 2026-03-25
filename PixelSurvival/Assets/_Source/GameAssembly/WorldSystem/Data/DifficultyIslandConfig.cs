using UnityEngine;

namespace GameAssembly.WorldSystem.Data
{
    [CreateAssetMenu(fileName = "new DifficultyIsland", menuName = "SO/DifficultyIsland")]
    public class DifficultyIslandConfig : ScriptableObject
    {
        [Header("Island Count")]
        [field: SerializeField]
        public int Min2LayerIslands { get; private set; } = 2;

        [field: SerializeField] public int Max2LayerIslands { get; private set; } = 6;
        
        [field: SerializeField] public int Min3LayerIslands { get; private set; } = 2;

        [field: SerializeField] public int Max3LayerIslands { get; private set; } = 6;

        [Header("Spawn Distance From Center")]
        [Range(0f, 1f)]
        [field: SerializeField]
        public float InnerDeadZone01 { get; private set; } = 0.25f;

        [Range(0f, 1f)]
        [field: SerializeField]
        public float TwoBiomeMax01 { get; private set; } = 0.6f;

        [Range(0f, 1f)]
        [field: SerializeField]
        public float ThreeBiomeMin01 { get; private set; } = 0.65f;

        [Range(0f, 1f)]
        [field: SerializeField]
        public float OuterLimit01 { get; private set; } = 0.95f;

        [Header("Island Spacing")]
        [field: SerializeField]
        public float IslandSpacingMultiplier { get; private set; } = 1.4f;

        [Header("2-Layer Yellow Radius")]
        [field: SerializeField]
        public float TwoLayerYellowMin { get; private set; } = 30f;

        [field: SerializeField] public float TwoLayerYellowMax { get; private set; } = 70f;

        [Header("2-Layer Orange Radius")]
        [field: SerializeField]
        public float TwoLayerOrangeMin { get; private set; } = 18f;

        [field: SerializeField] public float TwoLayerOrangeMax { get; private set; } = 45f;

        [Header("3-Layer Yellow Radius")]
        [field: SerializeField]
        public float ThreeLayerYellowMin { get; private set; } = 30f;

        [field: SerializeField] public float ThreeLayerYellowMax { get; private set; } = 70f;

        [Header("3-Layer Orange Radius")]
        [field: SerializeField]
        public float ThreeLayerOrangeMin { get; private set; } = 18f;

        [field: SerializeField] public float ThreeLayerOrangeMax { get; private set; } = 45f;

        [Header("Red Radius")]
        [field: SerializeField]
        public float RedMin { get; private set; } = 8f;

        [field: SerializeField] public float RedMax { get; private set; } = 25f;

        [Range(0f, 1f)]
        [field: SerializeField]
        public float RedChance { get; private set; } = 0.6f;
    }
}
