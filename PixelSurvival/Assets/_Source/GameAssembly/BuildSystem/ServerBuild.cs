using GameAssembly.BuildSystem.WorldObjects;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.PlayerSystem;
using GameAssembly.WorldSystem;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.BuildSystem
{
    public class ServerBuild : NetworkBehaviour
    {
        public const float MAX_PLACE_DISTANCE = 2.5f;

        [SerializeField] private LayerMask blockBuildingLayers;

        [Inject] private World _world;
        [Inject] private WorldObjectRegistry _objectRegistry;

        [Server]
        public void Server_PlaceBlock(Vector2Int blockWorldPos, NetworkConnectionToClient sender)
        {
            if (sender == null || !sender.identity ||
                Vector2.Distance(blockWorldPos, sender.identity.transform.position) > MAX_PLACE_DISTANCE)
                return;

            var selector = sender.identity.GetComponent<PlayerSelector>();

            if (!selector || !selector.IsSelectedItem)
                return;

            if (selector.GetSelectedItem().Definition is PlaceableObjectItemDefinitionSO objectItem)
            {
                TryPlaceObject(blockWorldPos, selector, objectItem);
                return;
            }

            if (selector.GetSelectedItem().Definition is not BlockItemDefinitionSO blockItem)
                return;

            if (Physics2D.OverlapBox(blockWorldPos + new Vector2(0.5f, 0.5f), Vector2.one * 0.95f, 0,
                    blockBuildingLayers))
                return;

            var chunk = _world.GetChunkByWorldPosition(blockWorldPos.x, blockWorldPos.y);
            var coords = World.ConvertWorldToChunkSpace(blockWorldPos.x, blockWorldPos.y);

            var isBlockPlaced = blockItem.IsFloorBlock
                ? !chunk.GetCell(coords.x, coords.y).Floor
                    .Equals(BlockData
                        .Air) // TODO: Make check for base ground block (instead of Air) to give an ability to place floor over the base biome block
                : !chunk.GetCell(coords.x, coords.y).Block.Equals(BlockData.Air);

            if (isBlockPlaced || !selector.GetSelectedItem().TryRemoveCount(1))
                return;

            chunk.SetBlock(coords.x, coords.y, blockItem.IsFloorBlock,
                BlockData.CreateBlock(blockItem.BlockDefinition));
        }

        [Server]
        private void TryPlaceObject(Vector2Int blockWorldPos, PlayerSelector selector,
            PlaceableObjectItemDefinitionSO objectItem)
        {
            if (!objectItem.PlaceableDefinition)
                return;

            if (selector.GetSelectedItem() == null || selector.GetSelectedItem().Count < 1 ||
                !_objectRegistry.TryPlaceObject(objectItem.PlaceableDefinition, blockWorldPos, blockBuildingLayers,
                    _world))
                return;

            selector.GetSelectedItem().TryRemoveCount(1);
        }
    }
}