using System.Collections;
using System.Collections.Generic;
using GameAssembly.BuildSystem.WorldObjects;
using GameAssembly.Core;
using GameAssembly.WorldSystem;
using GameAssembly.WorldSystem.View;
using Mirror;
using Pathfinding;
using UnityEngine;
using VContainer;

namespace GameAssembly.EnemySystem
{
    [DisallowMultipleComponent]
    public class ServerAstarWorldSync : NetworkBehaviour
    {
        [SerializeField] private bool rescanAfterWorldLoaded = true;
        [SerializeField] private bool waitForAstarPath = true;
        [SerializeField, Min(0.1f)] private float maxWaitForAstarPathSeconds = 15f;
        [SerializeField] private bool waitForWorldColliderBuild = true;
        [SerializeField, Min(0.1f)] private float maxWaitForWorldColliderBuildSeconds = 15f;
        [SerializeField, Min(0f)] private float graphUpdateInterval = 0.12f;
        [SerializeField, Min(0f)] private float graphCellPadding = 0.1f;
        [SerializeField] private bool applyWorldWalkabilityOverlay = true;
        [SerializeField] private bool includePlacedObjectsInWalkability = true;
        [SerializeField] private bool flushGraphUpdatesImmediately;
        [SerializeField] private LayerMask obstacleLayersMask = ~((1 << 2) | (1 << 3) | (1 << 5));

        [Inject] private World _world;
        [Inject] private WorldObjectRegistry _registry;

        private readonly List<Chunk> _boundChunks = new();
        private readonly HashSet<Vector2Int> _pendingWalkabilityCells = new();
        private readonly List<Vector2Int> _pendingWalkabilityBuffer = new();
        private readonly List<GridGraph> _gridGraphsBuffer = new();

        private bool _bound;
        private bool _hasPendingBounds;
        private Bounds _pendingBounds;
        private float _nextGraphUpdateTime;
        private Coroutine _setupRoutine;

        public override void OnStartServer()
        {
            base.OnStartServer();

            _world ??= GameInstaller.Resolve<World>();
            _registry ??= GameInstaller.Resolve<WorldObjectRegistry>();

            if (_setupRoutine != null)
                StopCoroutine(_setupRoutine);

            _setupRoutine = StartCoroutine(Server_SetupRoutine());
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Server_Expose();
        }

        private void OnDestroy()
        {
            if (isServer)
                Server_Expose();
        }

        private IEnumerator Server_SetupRoutine()
        {
            if (_world == null)
                yield break;

            yield return new WaitUntil(() => _world.IsLoaded.Value);
            yield return null;

            if (waitForAstarPath)
                yield return Server_WaitForAstarPath();

            if (!isServer || AstarPath.active == null)
                yield break;

            Server_Bind();
            Server_ConfigureGridGraphs();

            if (waitForWorldColliderBuild)
                yield return Server_WaitForWorldColliderBuild();

            if (rescanAfterWorldLoaded)
            {
                Server_ForceRebuildChunkColliders();
                Physics2D.SyncTransforms();
                AstarPath.active.Scan();
            }

            if (applyWorldWalkabilityOverlay)
            {
                // World cell data is the source of truth for runtime block passability.
                Server_QueueFullWorldWalkabilitySync();
                Server_ApplyPendingWalkability(forceFlush: true);
            }
        }

        private void Update()
        {
            if (!isServer || AstarPath.active == null)
                return;

            if (AstarPath.active.isScanning || Time.time < _nextGraphUpdateTime)
                return;

            var didApplyUpdate = false;

            if (_hasPendingBounds)
            {
                Physics2D.SyncTransforms();

                var updateBounds = _pendingBounds;
                _hasPendingBounds = false;

                var graphUpdateObject = new GraphUpdateObject(updateBounds);
                AstarPath.active.UpdateGraphs(graphUpdateObject);
                didApplyUpdate = true;

                if (flushGraphUpdatesImmediately)
                    AstarPath.active.FlushGraphUpdates();
            }

            if (_pendingWalkabilityCells.Count > 0)
            {
                Server_ApplyPendingWalkability(forceFlush: flushGraphUpdatesImmediately);
                didApplyUpdate = true;
            }

            if (!didApplyUpdate)
                return;

            _nextGraphUpdateTime = Time.time + graphUpdateInterval;
        }

        [Server]
        private void Server_Bind()
        {
            if (_bound || _world == null)
                return;

            _boundChunks.Clear();

            foreach (var pair in _world.Chunks)
            {
                var chunk = pair.Value;
                if (chunk == null)
                    continue;

                chunk.OnChunkCellChanged += Server_OnChunkCellChanged;
                _boundChunks.Add(chunk);
            }

            if (_registry != null)
                _registry.CellObjectChanged += Server_OnWorldObjectCellChanged;

            _bound = true;
        }

        [Server]
        private void Server_Expose()
        {
            if (!_bound)
                return;

            if (_setupRoutine != null)
            {
                StopCoroutine(_setupRoutine);
                _setupRoutine = null;
            }

            foreach (var chunk in _boundChunks)
            {
                if (chunk != null)
                    chunk.OnChunkCellChanged -= Server_OnChunkCellChanged;
            }

            _boundChunks.Clear();

            if (_registry != null)
                _registry.CellObjectChanged -= Server_OnWorldObjectCellChanged;

            _bound = false;
            _hasPendingBounds = false;
            _pendingWalkabilityCells.Clear();
            _pendingWalkabilityBuffer.Clear();
            _gridGraphsBuffer.Clear();
        }

        [Server]
        private void Server_OnChunkCellChanged(Chunk chunk, Vector2Int localCell)
        {
            if (chunk == null)
                return;

            var worldX = chunk.Coord.X * Chunk.CHUNK_SIZE + localCell.x;
            var worldY = chunk.Coord.Y * Chunk.CHUNK_SIZE + localCell.y;
            Server_MarkCellDirty(new Vector2Int(worldX, worldY));
        }

        [Server]
        private void Server_OnWorldObjectCellChanged(Vector2Int worldCell, PlacedWorldObject _)
        {
            Server_MarkCellDirty(worldCell);
        }

        [Server]
        private void Server_MarkCellDirty(Vector2Int worldCell)
        {
            if (!AstarPath.active)
                return;

            if (applyWorldWalkabilityOverlay)
                _pendingWalkabilityCells.Add(worldCell);

            Server_MarkBoundsDirty(worldCell);
        }

        [Server]
        private void Server_MarkBoundsDirty(Vector2Int worldCell)
        {
            var paddedSize = 1f + graphCellPadding * 2f;
            var cellCenter = new Vector3(worldCell.x + 0.5f, worldCell.y + 0.5f, 0f);
            var cellBounds = new Bounds(cellCenter, new Vector3(paddedSize, paddedSize, 20000f));

            if (!_hasPendingBounds)
            {
                _pendingBounds = cellBounds;
                _hasPendingBounds = true;
                return;
            }

            _pendingBounds.Encapsulate(cellBounds.min);
            _pendingBounds.Encapsulate(cellBounds.max);
        }

        [Server]
        private void Server_QueueFullWorldWalkabilitySync()
        {
            if (_world == null)
                return;

            _pendingWalkabilityCells.Clear();

            var worldSize = World.WORLD_SIZE * Chunk.CHUNK_SIZE;
            for (var x = 0; x < worldSize; x++)
            {
                for (var y = 0; y < worldSize; y++)
                    _pendingWalkabilityCells.Add(new Vector2Int(x, y));
            }
        }

        [Server]
        private void Server_ApplyPendingWalkability(bool forceFlush)
        {
            if (!applyWorldWalkabilityOverlay || _pendingWalkabilityCells.Count == 0 || AstarPath.active == null)
                return;

            if (!Server_CollectGridGraphs(_gridGraphsBuffer))
            {
                _pendingWalkabilityCells.Clear();
                return;
            }

            _pendingWalkabilityBuffer.Clear();
            foreach (var cell in _pendingWalkabilityCells)
                _pendingWalkabilityBuffer.Add(cell);
            _pendingWalkabilityCells.Clear();

            if (_pendingWalkabilityBuffer.Count == 0)
                return;

            var cells = _pendingWalkabilityBuffer.ToArray();
            var gridGraphs = _gridGraphsBuffer.ToArray();
            Dictionary<PlacedWorldObject, bool> blockingObjectCache = null;
            if (includePlacedObjectsInWalkability)
                blockingObjectCache = new Dictionary<PlacedWorldObject, bool>();

            AstarPath.active.AddWorkItem(() =>
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    var worldCell = cells[i];
                    var isWalkable = Server_IsWalkableCell(worldCell, blockingObjectCache);

                    for (var graphIndex = 0; graphIndex < gridGraphs.Length; graphIndex++)
                        Server_ApplyCellWalkability(gridGraphs[graphIndex], worldCell, isWalkable);
                }
            });

            if (forceFlush || flushGraphUpdatesImmediately)
                AstarPath.active.FlushWorkItems();
        }

        [Server]
        private bool Server_IsWalkableCell(Vector2Int worldCell, Dictionary<PlacedWorldObject, bool> blockingObjectCache)
        {
            if (_world == null || !World.IsWorldPositionInsideBounds(worldCell.x, worldCell.y))
                return false;

            var cell = _world.GetCellByWorldPosition(worldCell.x, worldCell.y);
            if (cell.Block.IsSolid)
                return false;

            if (!includePlacedObjectsInWalkability || _registry == null)
                return true;

            if (!_registry.TryGetObjectAtCell(worldCell, out var placedObject) || !placedObject)
                return true;

            if (blockingObjectCache != null && blockingObjectCache.TryGetValue(placedObject, out var cachedBlocking))
                return !cachedBlocking;

            var isBlocking = Server_DoesPlacedObjectBlockPath(placedObject);
            if (blockingObjectCache != null)
                blockingObjectCache[placedObject] = isBlocking;
            return !isBlocking;
        }

        private bool Server_DoesPlacedObjectBlockPath(PlacedWorldObject placedObject)
        {
            if (!placedObject)
                return false;

            var layerMaskBits = obstacleLayersMask.value;
            var colliders = placedObject.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (!collider || !collider.enabled || collider.isTrigger)
                    continue;

                var colliderLayerBit = 1 << collider.gameObject.layer;
                if ((layerMaskBits & colliderLayerBit) == 0)
                    continue;

                return true;
            }

            return false;
        }

        private static void Server_ApplyCellWalkability(GridGraph gridGraph, Vector2Int worldCell, bool isWalkable)
        {
            if (gridGraph == null)
                return;

            if (!Server_TryWorldCellToGraphCell(gridGraph, worldCell, out var graphX, out var graphZ))
                return;

            if (gridGraph.GetNode(graphX, graphZ) is not GridNodeBase node)
                return;

            if (node.Walkable == isWalkable && node.WalkableErosion == isWalkable)
                return;

            node.Walkable = isWalkable;
            node.WalkableErosion = isWalkable;
            gridGraph.CalculateConnectionsForCellAndNeighbours(graphX, graphZ);
        }

        private static bool Server_TryWorldCellToGraphCell(GridGraph gridGraph, Vector2Int worldCell, out int graphX,
            out int graphZ)
        {
            var worldCellCenter = new Vector3(worldCell.x + 0.5f, worldCell.y + 0.5f, 0f);
            var graphPosition = gridGraph.transform.InverseTransform(worldCellCenter);

            graphX = Mathf.FloorToInt(graphPosition.x);
            graphZ = Mathf.FloorToInt(graphPosition.z);

            return graphX >= 0 && graphZ >= 0 && graphX < gridGraph.width && graphZ < gridGraph.depth;
        }

        private static bool Server_CollectGridGraphs(List<GridGraph> result)
        {
            result.Clear();

            if (AstarPath.active == null || AstarPath.active.data == null)
                return false;

            var graphs = AstarPath.active.data.graphs;
            if (graphs == null)
                return false;

            foreach (var graph in graphs)
            {
                if (graph is GridGraph gridGraph)
                    result.Add(gridGraph);
            }

            return result.Count > 0;
        }

        [Server]
        private void Server_ConfigureGridGraphs()
        {
            if (AstarPath.active == null || AstarPath.active.data == null)
                return;

            var graphs = AstarPath.active.data.graphs;
            if (graphs == null)
                return;

            foreach (var graph in graphs)
            {
                if (graph is not GridGraph gridGraph || gridGraph.collision == null)
                    continue;

                gridGraph.collision.use2D = true;
                gridGraph.collision.collisionCheck = true;
                gridGraph.collision.heightCheck = false;
                gridGraph.collision.mask = obstacleLayersMask;
            }
        }

        [Server]
        private IEnumerator Server_WaitForAstarPath()
        {
            var startTime = Time.time;

            while (AstarPath.active == null && Time.time - startTime < maxWaitForAstarPathSeconds)
            {
                if (!isServer)
                    yield break;

                yield return null;
            }
        }

        [Server]
        private IEnumerator Server_WaitForWorldColliderBuild()
        {
            var worldRenderer = FindFirstObjectByType<WorldRenderer>();
            var startTime = Time.time;

            while (Time.time - startTime < maxWaitForWorldColliderBuildSeconds)
            {
                if (!isServer)
                    yield break;

                if (worldRenderer && worldRenderer.IsInitialVisualBuildCompleted)
                    break;

                yield return null;
            }
        }

        [Server]
        private void Server_ForceRebuildChunkColliders()
        {
            var chunkRenderers = FindObjectsByType<ChunkRenderer>(FindObjectsSortMode.None);
            foreach (var chunkRenderer in chunkRenderers)
            {
                if (!chunkRenderer || chunkRenderer.Chunk == null)
                    continue;

                chunkRenderer.RebuildVisual();
                chunkRenderer.Chunk.DirtyCollider = true;
                chunkRenderer.RebuildCollider();
            }
        }
    }
}
