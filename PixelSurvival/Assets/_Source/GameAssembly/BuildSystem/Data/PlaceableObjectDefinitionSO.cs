using GameAssembly.Core.Definitions;
using UnityEngine;

namespace GameAssembly.BuildSystem.Data
{
    [CreateAssetMenu(fileName = "New PlaceableObjectDefinitionSO", menuName = "SO/New PlaceableObjectDefinitionSO")]
    public class PlaceableObjectDefinitionSO : ScriptableObject, IDefinitionWithId
    {
        [field: SerializeField] public Vector2Int Size { get; private set; } = Vector2Int.one;
        [field: SerializeField] public bool RequireEmptyWallLayer { get; private set; } = true;
        [field: SerializeField] public bool RequireFloor { get; private set; } = true;
        [field: SerializeField] public GameObject Prefab { get; private set; }
        [SerializeField] private string id;
        
        public string Id => id;
    }
}