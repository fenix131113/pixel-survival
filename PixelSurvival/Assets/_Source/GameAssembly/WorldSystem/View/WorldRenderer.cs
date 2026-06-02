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
        [SerializeField] private Tilemap blockDamageVisualTilemap;
        [SerializeField] private Sprite[] blockDamageSprites;
        [SerializeField] private int blockDamageSortingOrder = 0;
        [SerializeField, Min(1)] private int visualChunksPerFrame = 4;

        private readonly Dictionary<ChunkCoord, ChunkRenderer> _chunksRenderers = new();
        private readonly Dictionary<Vector2Int, float> _activeBlockDamageProgress = new();
        private Tile[] _blockDamageTiles;
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

            if (_worldCreateManager)
            {
                _worldCreateManager.ClientOnBlockDamageProgress -= OnBlockDamageProgress;
                _worldCreateManager.ClientOnBlockDamageCleared -= OnBlockDamageCleared;
            }

            InitialVisualBuildProgressChanged = null;
            InitialVisualBuildStateChanged = null;
            ClearBlockDamageTileCache();
        }

        private void LateUpdate()
        {
            if (_activeBlockDamageProgress.Count == 0)
                return;

            foreach (var pair in _activeBlockDamageProgress)
                TryApplyBlockDamageOverlay(pair.Key, pair.Value);
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
            rend.Construct(chunk, _world);
            chunk.OnChunkCellChanged += OnChunkCellChanged;

            //rend.RebuildVisual();
            rend.RebuildCollider();
            ApplyDamageOverlayForChunk(chunk.Coord);
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
                        var worldX = chunk.Coord.X * Chunk.CHUNK_SIZE + x;
                        var worldY = chunk.Coord.Y * Chunk.CHUNK_SIZE + y;
                        upperTiles[flatIndex] = ResolveBlockTile(worldX, worldY, cell.Block);
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
            TryApplyBlockDamageOverlay(blockWorldPos, progress01);
        }

        private void OnBlockDamageCleared(Vector2Int blockWorldPos)
        {
            _activeBlockDamageProgress.Remove(blockWorldPos);
            TryClearBlockDamageOverlay(blockWorldPos);
        }

        private void ApplyDamageOverlayForChunk(ChunkCoord chunkCoord)
        {
            foreach (var pair in _activeBlockDamageProgress)
            {
                if (!GetChunkCoordByWorldPos(pair.Key).Equals(chunkCoord))
                    continue;

                TryApplyBlockDamageOverlay(pair.Key, pair.Value);
            }
        }

        private void TryApplyBlockDamageOverlay(Vector2Int blockWorldPos, float progress01)
        {
            if (!globalUpperVisualTilemap || !blockDamageVisualTilemap)
                return;

            var blockTilePos = ToVisualTilePos(blockWorldPos);
            var damageTilePos = ToBlockDamageVisualTilePos(blockWorldPos);
            if (!globalUpperVisualTilemap.GetTile(blockTilePos))
            {
                blockDamageVisualTilemap.SetTile(damageTilePos, null);
                return;
            }

            blockDamageVisualTilemap.SetTileFlags(damageTilePos, TileFlags.None);
            blockDamageVisualTilemap.SetColor(damageTilePos, Color.white);
            blockDamageVisualTilemap.SetTile(damageTilePos, ResolveBlockDamageTile(progress01));
        }

        private void TryClearBlockDamageOverlay(Vector2Int blockWorldPos)
        {
            if (!blockDamageVisualTilemap)
                return;

            var tilePos = ToBlockDamageVisualTilePos(blockWorldPos);
            blockDamageVisualTilemap.SetTileFlags(tilePos, TileFlags.None);
            blockDamageVisualTilemap.SetColor(tilePos, Color.white);
            blockDamageVisualTilemap.SetTile(tilePos, null);
        }

        private void RebuildAllVisuals()
        {
            EnsureVisualTilemaps();
            globalUpperVisualTilemap.ClearAllTiles();
            globalFloorVisualTilemap.ClearAllTiles();
            blockDamageVisualTilemap.ClearAllTiles();

            foreach (var pair in _world.Chunks)
                RebuildChunkVisual(pair.Value);

            foreach (var pair in _activeBlockDamageProgress)
                TryApplyBlockDamageOverlay(pair.Key, pair.Value);

            RefreshGlobalVisualTilemaps();
        }

        private System.Collections.IEnumerator RebuildAllVisualsAsync()
        {
            EnsureVisualTilemaps();
            globalUpperVisualTilemap.ClearAllTiles();
            globalFloorVisualTilemap.ClearAllTiles();
            blockDamageVisualTilemap.ClearAllTiles();

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
                ApplyDamageOverlayForChunk(pair.Key);

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
            blockDamageVisualTilemap.RefreshAllTiles();
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
                    blockDamageVisualTilemap.RefreshTile(tilePos);
                }
            }
        }

        private void EnsureVisualTilemaps()
        {
            if (!globalUpperVisualTilemap || !globalFloorVisualTilemap || !blockDamageVisualTilemap)
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

                if (!blockDamageVisualTilemap)
                {
                    var damageTemplateTilemap = globalUpperVisualTilemap;
                    if (!damageTemplateTilemap && chunkPrefab)
                        damageTemplateTilemap = chunkPrefab.UpperTilemap;

                    blockDamageVisualTilemap = CreateVisualTilemap("BlockDamageVisualTilemap", visualGrid.transform,
                        damageTemplateTilemap, blockDamageSortingOrder);
                }
            }

            ConfigureVisualTilemapRenderers();
        }

        private static Tilemap CreateVisualTilemap(string name, Transform parent, Tilemap templateTilemap,
            int sortOrder = -1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();

            if (!templateTilemap)
            {
                if (sortOrder != -1)
                    renderer.sortingOrder = sortOrder;

                return tilemap;
            }

            tilemap.animationFrameRate = templateTilemap.animationFrameRate;
            tilemap.color = templateTilemap.color;
            tilemap.tileAnchor = templateTilemap.tileAnchor;

            if (!templateTilemap.TryGetComponent<TilemapRenderer>(out var templateRenderer))
            {
                if (sortOrder != -1)
                    renderer.sortingOrder = sortOrder;

                return tilemap;
            }

            renderer.sortingLayerID = templateRenderer.sortingLayerID;
            renderer.sortingOrder = sortOrder == -1 ? templateRenderer.sortingOrder : sortOrder;
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
                globalUpperVisualTilemap.SetTile(tilePos, ResolveBlockTile(worldPos.x, worldPos.y, cell.Block));
                globalUpperVisualTilemap.SetTileFlags(tilePos, TileFlags.None);
                globalUpperVisualTilemap.SetColor(tilePos, Color.white);

                if (_activeBlockDamageProgress.TryGetValue(worldPos, out var damageProgress))
                    TryApplyBlockDamageOverlay(worldPos, damageProgress);
                else
                    TryClearBlockDamageOverlay(worldPos);
            }
            else
            {
                globalUpperVisualTilemap.SetTile(tilePos, null);
                globalUpperVisualTilemap.SetTileFlags(tilePos, TileFlags.None);
                globalUpperVisualTilemap.SetColor(tilePos, Color.white);
                TryClearBlockDamageOverlay(worldPos);
            }
        }

        private TileBase ResolveBlockTile(int worldX, int worldY, BlockData block)
        {
            if (_world != null && _world.TryGetBorderWallTileOverride(worldX, worldY, out var borderTile))
                return borderTile;

            var blockDef = block.definition;
            return blockDef && blockDef.Tile ? blockDef.Tile : null;
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
                    blockDamageVisualTilemap.RefreshTile(tilePos);
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

            if (blockDamageVisualTilemap &&
                blockDamageVisualTilemap.TryGetComponent<TilemapRenderer>(out var damageRenderer))
            {
                if (globalUpperVisualTilemap &&
                    globalUpperVisualTilemap.TryGetComponent<TilemapRenderer>(out var upperTilemapRenderer))
                {
                    damageRenderer.sortingLayerID = upperTilemapRenderer.sortingLayerID;
                    damageRenderer.sortingOrder = blockDamageSortingOrder == -1
                        ? upperTilemapRenderer.sortingOrder + 1
                        : blockDamageSortingOrder;
                    damageRenderer.sharedMaterial = upperTilemapRenderer.sharedMaterial;
                }
                else if (blockDamageSortingOrder != -1)
                {
                    damageRenderer.sortingOrder = blockDamageSortingOrder;
                }

                damageRenderer.mode = TilemapRenderer.Mode.SRPBatch;
            }
        }

        private TileBase ResolveBlockDamageTile(float progress01)
        {
            if (blockDamageSprites == null || blockDamageSprites.Length == 0)
                return null;

            var index = GetBlockDamageSpriteIndex(progress01, blockDamageSprites.Length);
            var sprite = blockDamageSprites[index];
            if (!sprite)
                return null;

            EnsureBlockDamageTileCache();

            var tile = _blockDamageTiles[index];
            if (tile && tile.sprite == sprite)
                return tile;

            if (tile)
                Destroy(tile);

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.DontSave;
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            _blockDamageTiles[index] = tile;

            return tile;
        }

        private void EnsureBlockDamageTileCache()
        {
            if (_blockDamageTiles != null && _blockDamageTiles.Length == blockDamageSprites.Length)
                return;

            ClearBlockDamageTileCache();
            _blockDamageTiles = new Tile[blockDamageSprites.Length];
        }

        private void ClearBlockDamageTileCache()
        {
            if (_blockDamageTiles == null)
                return;

            foreach (var tile in _blockDamageTiles)
            {
                if (tile)
                    Destroy(tile);
            }

            _blockDamageTiles = null;
        }

        private static int GetBlockDamageSpriteIndex(float progress01, int spritesCount)
        {
            if (spritesCount <= 0)
                return -1;

            // The first sprite covers 0..step; for 5 sprites that is 0..20%.
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(progress01) * spritesCount) - 1, 0,
                spritesCount - 1);
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

        private static Vector3Int ToBlockDamageVisualTilePos(Vector2Int blockWorldPos)
        {
            return new Vector3Int(blockWorldPos.x, blockWorldPos.y + 1, 0);
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
