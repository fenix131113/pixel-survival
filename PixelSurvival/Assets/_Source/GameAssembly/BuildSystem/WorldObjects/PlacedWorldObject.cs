using GameAssembly.BuildSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.BuildSystem.WorldObjects
{
    public class PlacedWorldObject : NetworkBehaviour
    {
        [field: SerializeField] public PlaceableObjectDefinitionSO Definition { get; private set; }

        public Vector2Int OriginCell { get; private set; }
        
        private WorldObjectRegistry _registry;

        [Server]
        public void Server_Initialize(Vector2Int originCell, PlaceableObjectDefinitionSO definition, WorldObjectRegistry registry)
        {
            OriginCell = originCell;
            Definition = definition;
            _registry = registry;
        }
        
        public override void OnStopServer()
        {
            base.OnStopServer();
            _registry?.Unregister(this);
        }
    }
}