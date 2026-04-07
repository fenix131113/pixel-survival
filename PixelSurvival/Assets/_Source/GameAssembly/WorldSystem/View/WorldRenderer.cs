using System;
using System.Collections.Generic;
using GameAssembly.WorldSystem.Data;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;

namespace GameAssembly.WorldSystem.View
{
    public class WorldRenderer : MonoBehaviour
    {
        [SerializeField] private ChunkRenderer chunkPrefab;
        [SerializeField] private Tilemap globalUpperVisualTilemap;
        [SerializeField] private Tilemap globalFloorVisualTilemap;
        [SerializeField, Min(1)] private int visualChunksPerFrame = 4;

        private readonly Dictionary<ChunkCoord, ChunkRenderer> _chunksRenderers = new();
        private readonly Dictionary<Vector2Int, float> _activeBlockDamageProgress = new();
        private static readonly Color DamagedTintColor = new(0.45f, 0.45f, 0.45f, 1f);
        private Coroutine _initialVisualBuildCoroutine;
        private bool _isInitialVisualBuildCompleted;

        [Inject] private World _world;
        [Inject] private WorldCreateManager _worldCreateManager;

        public bool IsInitialVisualBuildCompleted => _isInitialVisualBuildCompleted;
        public event Action<float> InitialVisualBuildProgressChanged;
        public event Action<bool> InitialVisualBuildStateChanged;

        private void Awake()
        {
            EnsureVisualTilemaps();
            _world.Progress.ProgressChanged += OnGenerateProgressChanged;

            if (!_worldCreateManager)
                return;

            _worldCreateManager.ClientOnBlockDamageProgress += OnBlockDamageProgress;
            _worldCreateManager.ClientOnBlockDamageCleared += OnBlockDamageCleared;
        }

        private void OnDestroy()
        {
            if (_initialVisualBuildCoroutine != null)
                StopCoroutine(_initialVisualBuildCoroutine);

            if (_world != null)
            {
                _world.Progress.ProgressChanged -= OnGenerateProgressChanged;

                foreach (var pair in _world.Chunks)
                    pair.Value.OnChunkCellChanged -= OnChunkCellChanged;
            }

            if (!_worldCreateManager)
                return;

            _worldCreateManager.ClientOnBlockDamageProgress -= OnBlockDamageProgress;
            _worldCreateManager.ClientOnBlockDamageCleared -= OnBlockDamageCleared;

            InitialVisualBuildProgressChanged = null;
            InitialVisualBuildStateChanged = null;
        }

        private void LateUpdate()
        {
            if (_activeBlockDamageProgress.Count == 0)
                return;

            foreach (var pair in _activeBlockDamageProgress)
                TryApplyBlockDamageTint(pair.Key, pair.Value);
        }

        private void OnGenerateProgressChanged(object sender, float e)
        {
            foreach (var pair in _world.Chunks)
                if (!_chunksRenderers.ContainsKey(pair.Key))
                    SpawnChunk(pair.Value);

            if (!Mathf.Approximately(e, 1f) || _isInitialVisualBuildCompleted || _initialVisualBuildCoroutine != null)
                return;

            _initialVisualBuildCoroutine = StartCoroutine(RebuildAllVisualsAsync());
        }

        public void SpawnChunk(Chunk chunk)
        {
            var pos = new Vector3(chunk.Coord.X * Chunk.CHUNK_SIZE, chunk.Coord.Y * Chunk.CHUNK_SIZE, 0);
            var go = Instantiate(chunkPrefab, pos, Quaternion.identity);
            var rend = go.GetComponent<ChunkRenderer>();
            rend.transform.parent = transform;
            _chunksRenderers.Add(chunk.Coord, rend);
            rend.Construct(chunk);
            chunk.OnChunkCellChanged += OnChunkCellChanged;

            //rend.RebuildVisual();
            rend.RebuildCollider();
            ApplyDamageTintForChunk(chunk.Coord);
        }

        private void RebuildChunkVisual(Chunk chunk)
        {
            EnsureVisualTilemaps();
            var floorTiles = new TileBase[Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE];
            var upperTiles = new TileBase[Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE];

            for (var x = 0; x < Chunk.CHUNK_SIZE; x++)
            {
                for (var y = 0; y < Chunk.CHUNK_SIZE; y++)
                {
                    var cell = chunk.Cells[x, y];
                    var flatIndex = GetFlatTileIndex(x, y);

                    if (cell.Floor.type != BlockType.AIR)
                    {
                        var floorDef = cell.Floor.definition;
                        floorTiles[flatIndex] = floorDef && floorDef.Tile ? floorDef.Tile : null;
                    }

                    if (cell.Block.type != BlockType.AIR)
                    {
                        var blockDef = cell.Block.definition;
                        upperTiles[flatIndex] = blockDef && blockDef.Tile ? blockDef.Tile : null;
                    }
                }
            }

            var bounds = new BoundsInt(
                chunk.Coord.X * Chunk.CHUNK_SIZE,
                chunk.Coord.Y * Chunk.CHUNK_SIZE,
                0,
                Chunk.CHUNK_SIZE,
                Chunk.CHUNK_SIZE,
                1);

            globalFloorVisualTilemap.SetTilesBlock(bounds, floorTiles);
            globalUpperVisualTilemap.SetTilesBlock(bounds, upperTiles);
        }

        private void RebuildChunkCollider(Chunk chunk)
        {
            if (_chunksRenderers.TryGetValue(chunk.Coord, out var rend))
                rend.RebuildCollider();
        }

        private void OnChunkCellChanged(Chunk chunk, Vector2Int localIndexes)
        {
            var worldPos = new Vector2Int(
                chunk.Coord.X * Chunk.CHUNK_SIZE + localIndexes.x,
                chunk.Coord.Y * Chunk.CHUNK_SIZE + localIndexes.y);

            UpdateVisualArea(worldPos);
        }

        private void OnBlockDamageProgress(Vector2Int blockWorldPos, float progress01, int currentDamage, int maxHealth)
        {
            _activeBlockDamageProgress[blockWorldPos] = progress01;
            TryApplyBlockDamageTint(blockWorldPos, progress01);
        }

        private void OnBlockDamageCleared(Vector2Int blockWorldPos)
        {
            _activeBlockDamageProgress.Remove(blockWorldPos);
            TryClearBlockDamageTint(blockWorldPos);
        }

        private void ApplyDamageTintForChunk(ChunkCoord chunkCoord)
        {
            foreach (var pair in _activeBlockDamageProgress)
            {
                if (!GetChunkCoordByWorldPos(pair.Key).Equals(chunkCoord))
                    continue;

                TryApplyBlockDamageTint(pair.Key, pair.Value);
            }
        }

        private void TryApplyBlockDamageTint(Vector2Int blockWorldPos, float progress01)
        {
            if (!globalUpperVisualTilemap)
                return;

            var tilePos = ToVisualTilePos(blockWorldPos);
            if (!globalUpperVisualTilemap.GetTile(tilePos))
                return;

            globalUpperVisualTilemap.SetTileFlags(tilePos, TileFlags.None);
            globalUpperVisualTilemap.SetColor(tilePos,
                Color.Lerp(Color.white, DamagedTintColor, Mathf.Clamp01(progress01)));
        }

        private void TryClearBlockDamageTint(Vector2Int blockWorldPos)
        {
            if (!globalUpperVisualTilemap)
                return;

            var tilePos = ToVisualTilePos(blockWorldPos);
            globalUpperVisualTilemap.SetTileFlags(tilePos, TileFlags.None);
            globalUpperVisualTilemap.SetColor(tilePos, Color.white);
        }

        private void RebuildAllVisuals()
        {
            EnsureVisualTilemaps();
            globalUpperVisualTilemap.ClearAllTiles();
            globalFloorVisualTilemap.ClearAllTiles();

            foreach (var pair in _world.Chunks)
                RebuildChunkVisual(pair.Value);

            RefreshGlobalVisualTilemaps();
        }

        private System.Collections.IEnumerator RebuildAllVisualsAsync()
        {
            EnsureVisualTilemaps();
            globalUpperVisualTilemap.ClearAllTiles();
            globalFloorVisualTilemap.ClearAllTiles();

            var chunks = new List<KeyValuePair<ChunkCoord, Chunk>>(_world.Chunks);
            var totalChunks = chunks.Count;
            var processedChunks = 0;

            _isInitialVisualBuildCompleted = false;
            InitialVisualBuildStateChanged?.Invoke(true);
            InitialVisualBuildProgressChanged?.Invoke(0f);

            if (totalChunks == 0)
            {
                _isInitialVisualBuildCompleted = true;
                _initialVisualBuildCoroutine = null;
                InitialVisualBuildProgressChanged?.Invoke(1f);
                InitialVisualBuildStateChanged?.Invoke(false);
                yield break;
            }

            foreach (var pair in chunks)
            {
                if (_chunksRenderers.TryGetValue(pair.Key, out var renderer))
                {
                    renderer.RebuildVisual();
                    renderer.RebuildCollider();
                }

                RebuildChunkVisual(pair.Value);
                RefreshChunkArea(pair.Key);
                ApplyDamageTintForChunk(pair.Key);

                processedChunks++;
                InitialVisualBuildProgressChanged?.Invoke(processedChunks / (float)totalChunks);

                if (processedChunks % Mathf.Max(1, visualChunksPerFrame) == 0)
                    yield return null;
            }

            _isInitialVisualBuildCompleted = true;
            _initialVisualBuildCoroutine = null;
            InitialVisualBuildStateChanged?.Invoke(false);
        }

        private void UpdateVisualArea(Vector2Int centerWorldPos)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var worldPos = new Vector2Int(centerWorldPos.x + dx, centerWorldPos.y + dy);
                    if (!World.IsWorldPositionInsideBounds(worldPos.x, worldPos.y))
                        continue;

                    PaintVisualCell(worldPos);
                }
            }

            RefreshVisualArea(centerWorldPos);
        }

        private void RefreshGlobalVisualTilemaps()
        {
            globalUpperVisualTilemap.RefreshAllTiles();
            globalFloorVisualTilemap.RefreshAllTiles();
        }

        private void RefreshChunkArea(ChunkCoord chunkCoord)
        {
            var startX = Mathf.Max(0, chunkCoord.X * Chunk.CHUNK_SIZE - 1);
            var startY = Mathf.Max(0, chunkCoord.Y * Chunk.CHUNK_SIZE - 1);
            var endX = Mathf.Min(World.WORLD_SIZE * Chunk.CHUNK_SIZE - 1, (chunkCoord.X + 1) * Chunk.CHUNK_SIZE);
            var endY = Mathf.Min(World.WORLD_SIZE * Chunk.CHUNK_SIZE - 1, (chunkCoord.Y + 1) * Chunk.CHUNK_SIZE);

            for (var x = startX; x <= endX; x++)
            {
                for (var y = startY; y <= endY; y++)
                {
                    var tilePos = new Vector3Int(x, y, 0);
                    globalFloorVisualTilemap.RefreshTile(tilePos);
                    globalUpperVisualTilemap.RefreshTile(tilePos);
                }
            }
        }

        private void EnsureVisualTilemaps()
        {
            if (!globalUpperVisualTilemap || !globalFloorVisualTilemap)
            {
                var visualGrid = GetOrCreateVisualGrid();

                if (!globalFloorVisualTilemap)
                {
                    globalFloorVisualTilemap = CreateVisualTilemap("FloorVisualTilemap", visualGrid.transform,
                        chunkPrefab ? chunkPrefab.FloorTilemap : null);
                }

                if (!globalUpperVisualTilemap)
                {
                    globalUpperVisualTilemap = CreateVisualTilemap("UpperVisualTilemap", visualGrid.transform,
                        chunkPrefab ? chunkPrefab.UpperTilemap : null);
                }
            }

            ConfigureVisualTilemapRenderers();
        }

        private static Tilemap CreateVisualTilemap(string name, Transform parent, Tilemap templateTilemap)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();

            if (!templateTilemap)
                return tilemap;

            tilemap.animationFrameRate = templateTilemap.animationFrameRate;
            tilemap.color = templateTilemap.color;
            tilemap.tileAnchor = templateTilemap.tileAnchor;

            if (!templateTilemap.TryGetComponent<TilemapRenderer>(out var templateRenderer))
                return tilemap;

            renderer.sortingLayerID = templateRenderer.sortingLayerID;
            renderer.sortingOrder = templateRenderer.sortingOrder;
            renderer.sharedMaterial = templateRenderer.sharedMaterial;
            renderer.mode = templateRenderer.mode;

            return tilemap;
        }

        private void PaintVisualCell(Vector2Int worldPos)
        {
            var cell = _world.GetCellByWorldPosition(worldPos.x, worldPos.y);
            var tilePos = ToVisualTilePos(worldPos);

            if (cell.Floor.type != BlockType.AIR)
            {
                var floorDef = cell.Floor.definition;
                globalFloorVisualTilemap.SetTile(tilePos, floorDef && floorDef.Tile ? floorDef.Tile : null);
            }
            else
            {
                globalFloorVisualTilemap.SetTile(tilePos, null);
            }

            if (cell.Block.type != BlockType.AIR)
            {
                var blockDef = cell.Block.definition;
                globalUpperVisualTilemap.SetTile(tilePos, blockDef && blockDef.Tile ? blockDef.Tile : null);
                globalUpperVisualTilemap.SetTileFlags(tilePos, TileFlags.None);
                globalUpperVisualTilemap.SetColor(tilePos, Color.white);

                if (_activeBlockDamageProgress.TryGetValue(worldPos, out var damageProgress))
                    TryApplyBlockDamageTint(worldPos, damageProgress);
            }
            else
            {
                globalUpperVisualTilemap.SetTile(tilePos, null);
                globalUpperVisualTilemap.SetTileFlags(tilePos, TileFlags.None);
                globalUpperVisualTilemap.SetColor(tilePos, Color.white);
            }
        }

        private void RefreshVisualArea(Vector2Int centerWorldPos)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var worldPos = new Vector2Int(centerWorldPos.x + dx, centerWorldPos.y + dy);
                    if (!World.IsWorldPositionInsideBounds(worldPos.x, worldPos.y))
                        continue;

                    var tilePos = ToVisualTilePos(worldPos);
                    globalFloorVisualTilemap.RefreshTile(tilePos);
                    globalUpperVisualTilemap.RefreshTile(tilePos);
                }
            }
        }

        private void ConfigureVisualTilemapRenderers()
        {
            if (globalUpperVisualTilemap &&
                globalUpperVisualTilemap.TryGetComponent<TilemapRenderer>(out var upperRenderer))
            {
                upperRenderer.mode = TilemapRenderer.Mode.Individual;
            }
        }

        private GameObject GetOrCreateVisualGrid()
        {
            var existingGrid = transform.Find("WorldVisualGrid");
            if (existingGrid)
                return existingGrid.gameObject;

            var visualGrid = new GameObject("WorldVisualGrid");
            visualGrid.transform.SetParent(transform, false);
            visualGrid.AddComponent<Grid>();
            return visualGrid;
        }

        private static Vector3Int ToVisualTilePos(Vector2Int blockWorldPos)
        {
            return new Vector3Int(blockWorldPos.x, blockWorldPos.y, 0);
        }

        private static ChunkCoord GetChunkCoordByWorldPos(Vector2Int blockWorldPos)
        {
            return new ChunkCoord(
                Mathf.FloorToInt(blockWorldPos.x / (float)Chunk.CHUNK_SIZE),
                Mathf.FloorToInt(blockWorldPos.y / (float)Chunk.CHUNK_SIZE));
        }

        private static int GetFlatTileIndex(int x, int y)
        {
            return x + y * Chunk.CHUNK_SIZE;
        }
    }
}
