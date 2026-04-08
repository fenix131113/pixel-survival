using GameAssembly.ItemsSystem.Data;
using UnityEngine;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects.FurnaceSystem.Data
{
    [CreateAssetMenu(fileName = "New SmeltRecipeSO", menuName = "SO/SmeltRecipeSO")]
    public class SmeltRecipeSO : ScriptableObject
    {
        [field: SerializeField] public ItemDefinitionSO InputItem { get; private set; }
        [field: SerializeField] [field: Min(1)] public int InputCount { get; private set; } = 1;

        [field: SerializeField] public ItemDefinitionSO ResultItem { get; private set; }
        [field: SerializeField] [field: Min(1)] public int ResultCount { get; private set; } = 1;

        [field: SerializeField] [field: Min(0.01f)] public float SmeltTimeSeconds { get; private set; } = 1f;
        [field: SerializeField] public FurnaceTier RequiredFurnaceTier { get; private set; }
    }
}
