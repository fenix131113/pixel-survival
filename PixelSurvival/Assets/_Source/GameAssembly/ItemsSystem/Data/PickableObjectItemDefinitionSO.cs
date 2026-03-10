using GameAssembly.BuildSystem.Data;
using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New PlaceableObjectItemDefinitionSO", menuName = "SO/New PlaceableObjectItemDefinitionSO")]
    public class PlaceableObjectItemDefinitionSO : ItemDefinitionSO
    {
        [field: SerializeField] public PlaceableObjectDefinitionSO PlaceableDefinition { get; private set; }
    }
}