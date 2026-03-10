using System;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.BuildSystem.Data;
using GameAssembly.WorldSystem;
using Mirror;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameAssembly.BuildSystem.WorldObjects
{
    public class WorldObjectRegistry
    {
        private readonly Dictionary<Vector2Int, PlacedWorldObject> _occupiedCells = new();
        private readonly Dictionary<PlacedWorldObject, PlaceableObjectRecord> _records = new();

        public IReadOnlyDictionary<PlacedWorldObject, PlaceableObjectRecord> Records => _records;

        [Server]
        public bool TryPlaceObject(PlaceableObjectDefinitionSO definition, Vector2Int originCell, LayerMask blockingMask,
            World world)
        {
            if (!definition || !definition.Prefab)
                return false;

            var footprint = GetFootprint(originCell, definition.Size);
            
            if (!CanPlace(definition, footprint, blockingMask, world))
                return false;
            
            var worldPosition = originCell + new Vector2(0.5f, 0.5f);
            var instance = Object.Instantiate(definition.Prefab, worldPosition, Quaternion.identity);

            if (!instance.TryGetComponent(out PlacedWorldObject placedObject))
            {
                Object.Destroy(instance);
                return false;
            }

            placedObject.Server_Initialize(originCell, definition, this);
            NetworkServer.Spawn(instance);

            Register(placedObject, definition, originCell, footprint);
            return true;
        }

        [Server]
        public void Unregister(PlacedWorldObject worldObject)
        {
            if (!_records.Remove(worldObject, out var record))
                return;

            foreach (var cell in record.Footprint)
                _occupiedCells.Remove(cell);
        }

        public bool IsOccupied(Vector2Int cell)
        {
            if (NetworkServer.active)
                return _occupiedCells.ContainsKey(cell);
            
            Debug.LogWarning($"[{nameof(WorldObjectRegistry)}] IsOccupied called on non-server side for cell {cell}. Registry is server-authoritative.");
            return false;

        }

        [Server]
        private void Register(PlacedWorldObject worldObject, PlaceableObjectDefinitionSO definition, Vector2Int originCell,
            IReadOnlyList<Vector2Int> footprint)
        {
            var record = new PlaceableObjectRecord(definition.name, originCell, footprint.ToArray());
            _records[worldObject] = record;

            foreach (var t in footprint)
            {
                _occupiedCells[t] = worldObject;
            }
        }

        private bool CanPlace(PlaceableObjectDefinitionSO definition, IReadOnlyList<Vector2Int> footprint, LayerMask blockingMask,
            World world)
        {
            foreach (var cell in footprint)
            {
                if (IsOccupied(cell))
                    return false;

                if (Physics2D.OverlapBox(cell + new Vector2(0.5f, 0.5f), Vector2.one * 0.95f, 0f, blockingMask))
                    return false;

                var worldCell = world.GetCellByWorldPosition(cell.x, cell.y);

                if (definition.RequireFloor && worldCell.Floor.Equals(BlockData.Air))
                    return false;

                if (definition.RequireEmptyWallLayer && !worldCell.Block.Equals(BlockData.Air))
                    return false;
            }

            return true;
        }

        private static List<Vector2Int> GetFootprint(Vector2Int originCell, Vector2Int size)
        {
            var result = new List<Vector2Int>(Mathf.Max(1, size.x * size.y));

            for (var x = 0; x < size.x; x++)
            for (var y = 0; y < size.y; y++)
                result.Add(originCell + new Vector2Int(x, y));

            return result;
        }
    }

    [Serializable]
    public readonly struct PlaceableObjectRecord
    {
        public readonly string DefinitionName;
        public readonly Vector2Int OriginCell;
        public readonly Vector2Int[] Footprint;

        public PlaceableObjectRecord(string definitionName, Vector2Int originCell, Vector2Int[] footprint)
        {
            DefinitionName = definitionName;
            OriginCell = originCell;
            Footprint = footprint;
        }
    }
}